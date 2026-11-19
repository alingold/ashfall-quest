using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.Flight
{
    internal static class V
    {
        internal static Float3 Add(Float3 a, Float3 b) { return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        internal static Float3 Sub(Float3 a, Float3 b) { return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        internal static Float3 Mul(Float3 a, float s) { return new Float3(a.X*s,a.Y*s,a.Z*s); }
        internal static float Dot(Float3 a, Float3 b) { return a.X*b.X+a.Y*b.Y+a.Z*b.Z; }
        internal static float Len(Float3 a) { return (float)Math.Sqrt((double)a.X*a.X+(double)a.Y*a.Y+(double)a.Z*a.Z); }
        internal static Float3 Unit(Float3 a) { float l=Len(a); return l>0.00001f?Mul(a,1/l):new Float3(); }
        internal static float Clamp(float a,float lo,float hi) { return Math.Max(lo,Math.Min(hi,a)); }
        internal static bool Finite(double a) { return !Double.IsNaN(a)&&!Double.IsInfinity(a); }
        internal static bool Bounded(Float3 a,float limit) { return a.IsFinite&&Math.Abs(a.X)<=limit&&Math.Abs(a.Y)<=limit&&Math.Abs(a.Z)<=limit; }
        internal static Float3 Toward(Float3 a,Float3 b,float d)
        {
            double x=(double)b.X-a.X,y=(double)b.Y-a.Y,z=(double)b.Z-a.Z;
            double length=Math.Sqrt(x*x+y*y+z*z);
            if(length<=d)return b;
            double scale=d/length;
            return new Float3((float)(a.X+x*scale),(float)(a.Y+y*scale),(float)(a.Z+z*scale));
        }
    }
    // Session-scoped bounded replay fence. External gate additionally verifies host actor/tick ownership.
    internal sealed class CommandFence
    {
        readonly string session,actor; readonly long epoch; readonly IAuthorityGate gate;
        readonly Dictionary<string,long> seen=new Dictionary<string,long>(4096,StringComparer.Ordinal);
        readonly string[] expired=new string[4096];
        long newest=-1;
        internal bool AllowsTick(long tick) { return tick >= newest; }
        internal bool HasCommand(string id) { return id!=null&&seen.ContainsKey(id); }
        internal void AdvanceFloor(long tick) { newest=Math.Max(newest,tick); }
        internal CommandFence(string session,string actor,long epoch,IAuthorityGate gate)
        { if(String.IsNullOrEmpty(session)||String.IsNullOrEmpty(actor)||gate==null)throw new ArgumentException("authority scope"); this.session=session;this.actor=actor;this.epoch=epoch;this.gate=gate; }
        // Validation never mutates this fence. Host gate must be an authorization/replay
        // decision, not a gameplay commit; its own external bookkeeping is host-owned.
        internal RuleResult Validate(CommandContext c)
        {
            if(!c.IsAuthority||c.SessionId!=session||c.ActorId!=actor||c.AuthorityEpoch!=epoch||c.Tick<0||c.Tick<newest||String.IsNullOrEmpty(c.CommandId))return RuleResult.Reject("authority scope/tick");
            if(seen.ContainsKey(c.CommandId))return RuleResult.Reject("replay");
            long edge=Math.Max(newest,c.Tick)-256;int retained=0;
            foreach(var p in seen)if(p.Value>=edge)retained++;
            if(retained>=4096)return RuleResult.Reject("command window full");
            return gate.Validate(c);
        }
        internal void Commit(CommandContext c)
        {
            long edge=Math.Max(newest,c.Tick)-256;int count=0;
            foreach(var p in seen)if(p.Value<edge)expired[count++]=p.Key;
            for(int i=0;i<count;i++){seen.Remove(expired[i]);expired[i]=null;}
            newest=Math.Max(newest,c.Tick);seen.Add(c.CommandId,c.Tick);
        }
        internal CommandFence Stage(CommandContext c)
        {
            var next=new CommandFence(session,actor,epoch,gate);
            next.newest=newest;foreach(var pair in seen)next.seen.Add(pair.Key,pair.Value);
            next.Commit(c);return next;
        }
        internal RuleResult Take(CommandContext c)
        {RuleResult r=Validate(c);if(r.Accepted)Commit(c);return r;}
    }

    public enum FlightMode { Ground, Air, Glide, Dive, Launch, Suspended }
    public struct FlightFrame
    {
        public float DeltaSeconds;
        public bool CeilingBlocked, Supported, InputEnabled, Tracked, Paused, InventoryOpen, Climbing, Mantling, DesktopGlide, XR, HandsReady, DiveForward;
        public Float3 Velocity, Forward, MoveDirection, WalkTarget, LeftHand, RightHand;
        // Analog forward stick (-1..1) for dive pitch; 0 means DiveForward alone requests a full dive.
        public float DiveAxis;
        // NearGround is the legacy desktop step-down hint. DownhillRun comes from a directional support query.
        public bool NearGround, DownhillRun;
        // The heading and stick direction already include this frame's player turn.
        public bool PlayerTurned;
    }
    public sealed partial class FlightMachine
    {
        FlightSettingsSnapshot settings=FlightSettingsSnapshot.Default;
        long settingsRevision;
        public FlightSettingsSnapshot Settings {get{return settings;}}
        public long SettingsRevision {get{return settingsRevision;}}
        // Privileged host publication: the actual actor/epoch gate and replay fence
        // authorize replacement. No callback occurs after the final revision check.
        public RuleResult PublishSettings(CommandContext c,long expectedGeneration,long revision,FlightSettingsSnapshot snapshot)
        {
            if(snapshot==null||inactive||expectedGeneration!=generation||revision<=settingsRevision||revision==long.MaxValue||
                c.Tick<=lifecycleTick||lifecycleCommands.HasCommand(c.CommandId))return RuleResult.Reject("settings scope/revision");
            long before=stateRevision;
            var result=commands.Validate(c);if(!result.Accepted)return result;
            if(before!=stateRevision)return RuleResult.Reject("Flight changed during settings publication");
            commands.Commit(c);settings=snapshot;settingsRevision=revision;stateRevision++;return result;
        }
        // Flight has no complete owner checkpoint codec. New private state MUST NOT
        // be silently omitted from a historical capture or global quiescence claim.
        public RuleResult SettingsCheckpointReady()
        {return RuleResult.Reject("Flight settings v1 snapshot/revision and owner reservations require a versioned owner codec");}
        CommandFence commands;
        readonly CommandFence lifecycleCommands;
        long generation, lifecycleTick=-1, motionRevision;
        public FlightMotionReceipt PendingMotion { get; private set; }
        bool inactive;
        float fallTime, clearance, divePitch; bool ascent;
        float armsHold;
        public FlightMode Mode { get; private set; }
        public Float3 Velocity { get; private set; }
        public float GlideSpeed { get; private set; }
        public FlightMachine(string session,string actor,long epoch,IAuthorityGate gate,float effectiveWalkSpeed,float networkFlightMultiplier)
        {
            if(!V.Finite(effectiveWalkSpeed)||effectiveWalkSpeed<=0||!V.Finite(networkFlightMultiplier)||networkFlightMultiplier<=0)throw new ArgumentException("flight speed");
            GlideSpeed=effectiveWalkSpeed*(12f/6.5f)*networkFlightMultiplier;
            if(!V.Finite(GlideSpeed)||GlideSpeed>1000)throw new ArgumentException("flight speed overflow");
            commands=new CommandFence(session,actor,epoch,gate);
            lifecycleCommands=new CommandFence(session,actor,epoch,gate);
        }
        public RuleResult ResetActor(CommandContext c,long nextGeneration,FlightLifecycle reason,IFlightLifecycleAuthorization authorization)
        {
            if(authorization==null||nextGeneration<=generation||!Enum.IsDefined(typeof(FlightLifecycle),reason)||!commands.AllowsTick(c.Tick)||commands.HasCommand(c.CommandId))return RuleResult.Reject("lifecycle generation/tick");
            RuleResult r=lifecycleCommands.Validate(c);if(!r.Accepted)return r;
            r=authorization.ValidateTransition(c,nextGeneration,reason);if(!r.Accepted)return r;
            lifecycleCommands.Commit(c);commands.AdvanceFloor(c.Tick);generation=nextGeneration;lifecycleTick=c.Tick;
            stateRevision++;PendingMotion=null;
            inactive=reason!=FlightLifecycle.Respawn;
            Velocity=new Float3();Mode=FlightMode.Ground;fallTime=clearance=divePitch=0;ascent=false;armsHold=0;
            return r;
        }
        // r2 public numeric envelope: SI per-component limits, inclusive.
        // Velocity 10000m/s, walk target 1000m/s, direction 2, root-relative hands 10m.
        public RuleResult Step(CommandContext c, FlightFrame f)
        {
            if(inactive||c.Tick<=lifecycleTick||lifecycleCommands.HasCommand(c.CommandId))return RuleResult.Reject("inactive lifecycle/replay");
            if(!V.Finite(f.DeltaSeconds)||f.DeltaSeconds<=0||f.DeltaSeconds>1||!V.Finite(f.DiveAxis)||Math.Abs(f.DiveAxis)>2||!V.Bounded(f.Velocity,10000)||!V.Bounded(f.Forward,2)||!V.Bounded(f.MoveDirection,2)||!V.Bounded(f.WalkTarget,1000)||!V.Bounded(f.LeftHand,10)||!V.Bounded(f.RightHand,10))return RuleResult.Reject("invalid frame");
            long before=stateRevision;
            Float3 velocity;FlightMode mode=Mode;float fall=fallTime,clear=clearance,pitch=divePitch;bool rising=ascent;
            Calculate(f,out velocity,ref mode,ref fall,ref clear,ref rising,ref pitch);
            if(f.CeilingBlocked&&velocity.Y>0){velocity=new Float3(velocity.X,0,velocity.Z);clear=0;rising=false;if(mode!=FlightMode.Suspended)mode=f.Supported?FlightMode.Ground:FlightMode.Air;}
            if(!V.Bounded(velocity,10000)||!V.Finite(fall)||!V.Finite(clear)||!V.Finite(pitch))return RuleResult.Reject("invalid derived frame");
            RuleResult r=commands.Validate(c);if(!r.Accepted)return r;
            if(before!=stateRevision)return RuleResult.Reject("Flight changed during step");
            commands.Commit(c);
            Velocity=velocity;Mode=mode;fallTime=fall;clearance=clear;ascent=rising;divePitch=pitch;armsHold=NextArmsHold(f);
            stateRevision++;PendingMotion=new FlightMotionReceipt(c,generation,++motionRevision,velocity);return r;
        }
        // Arm span is horizontal and root-relative, so seated height, calibration and yaw drop out.
        // Prime on the ground as well. Only committed frames consume the grace interval.
        float NextArmsHold(FlightFrame f)
        {
            if(!f.XR||!f.InputEnabled||!f.Tracked||f.Paused||f.InventoryOpen||f.Climbing||f.Mantling)return 0;
            if(f.HandsReady)return HorizontalDistance(f.LeftHand,f.RightHand)>=R26FreefallTuning.GlideSpread?R26FreefallTuning.GlidePoseGrace:0;
            // Bridge missing tracking only. A measured fold releases on this frame.
            return Math.Max(0,armsHold-f.DeltaSeconds);
        }
        // Bound course changes from tracked poses and movement input between player turns.
        static Float3 SteerGlide(Float3 heading,Float3 velocity,float dt)
        {
            Float3 prior=V.Unit(new Float3(velocity.X,0,velocity.Z));
            if(V.Len(prior)<.1f)return heading;
            double angle=Math.Atan2(prior.Z*heading.X-prior.X*heading.Z,V.Dot(prior,heading));
            double limit=Math.PI*dt, step=Math.Max(-limit,Math.Min(limit,angle));
            float c=(float)Math.Cos(step),s=(float)Math.Sin(step);
            return new Float3(prior.X*c+prior.Z*s,0,prior.Z*c-prior.X*s);
        }
        void Calculate(FlightFrame f,out Float3 velocity,ref FlightMode mode,ref float fall,ref float clear,ref bool rising,ref float pitch)
        {
            velocity=f.Velocity;

            if(!f.InputEnabled||!f.Tracked||f.Paused||f.Climbing||f.Mantling){mode=FlightMode.Suspended;fall=0;clear=0;rising=false;pitch=0;return;}
            bool wants=f.XR?NextArmsHold(f)>0:!f.InventoryOpen&&f.DesktopGlide;
            if(!f.XR&&R26GlideEntry.StepDownRule&&f.NearGround&&mode!=FlightMode.Glide&&velocity.Y<=0)wants=false;
            bool launched=(rising&&velocity.Y>0)||clear>0;
            clear=Math.Max(0,clear-f.DeltaSeconds);
            if(launched&&wants&&clear<=0){rising=false;launched=false;}
            if(launched)wants=false;else rising=false;
            bool slopeTakeoff=f.XR&&f.Supported&&f.DownhillRun&&wants;
            bool ground=f.Supported&&!launched&&!slopeTakeoff;
            float requested=!f.InventoryOpen?R26Freefall.DiveCommand(f.DiveAxis,f.DiveForward):0;
            // Fold the wings to freefall/dive in XR. Desktop keeps its existing dive command.
            if(!f.XR&&!ground&&!launched&&velocity.Y<=0&&requested>0)wants=false;
            bool controlledGlide=wants&&!ground;
            fall=!ground&&!launched&&!controlledGlide&&velocity.Y<-.5f?Math.Min(R26FreefallTuning.DiveGateSeconds,fall+f.DeltaSeconds):0;
            // FLIGHT-DIVE: forward stick commands a body pitch; drag (R26Freefall), not thrust, sets fall speed.
            float command=!controlledGlide&&fall>=R26FreefallTuning.DiveGateSeconds?requested:0;
            bool dive=command>0;
            if(dive)wants=false;
            if(ground){mode=FlightMode.Ground;fall=0;pitch=0;return;}
            if(wants)
            {
                Float3 heading=V.Unit(new Float3(f.Forward.X,0,f.Forward.Z));
                if(V.Len(heading)<.1f)heading=new Float3(0,0,1);
                if(f.XR&&f.HandsReady)
                {
                    float diff=f.LeftHand.Y-f.RightHand.Y;
                    float bank=Math.Sign(diff)*V.Clamp((Math.Abs(diff)-.08f)/.32f,0,1);
                    // Reflection of Godot forward.cross(UP) gives Unity right=(z,0,-x).
                    heading=V.Unit(V.Add(heading,V.Mul(new Float3(heading.Z,0,-heading.X),bank*.715f*settings.GlideSteeringGain)));
                }
                Float3 move=new Float3(f.MoveDirection.X,0,f.MoveDirection.Z);
                heading=slopeTakeoff?V.Unit(move):V.Unit(V.Add(heading,V.Mul(move,.35f)));
                if(f.XR&&mode==FlightMode.Glide&&!f.PlayerTurned)heading=SteerGlide(heading,velocity,f.DeltaSeconds);
                Float3 target=V.Mul(heading,GlideSpeed);
                velocity=new Float3(target.X,-GlideSpeed/7,target.Z);mode=FlightMode.Glide;pitch=0;return;
            }
            if(clear>0){mode=FlightMode.Launch;pitch=0;return;}
            float average;pitch=R26Freefall.StepPitch(pitch,command,f.DeltaSeconds,out average);
            float y=R26Freefall.Fall(velocity.Y,average,f.DeltaSeconds);
            Float3 horizontal=V.Toward(new Float3(velocity.X,0,velocity.Z),new Float3(f.WalkTarget.X,0,f.WalkTarget.Z),(V.Len(f.MoveDirection)<.00001f?.8f:6f)*f.DeltaSeconds);
            velocity=new Float3(horizontal.X,y,horizontal.Z);mode=dive?FlightMode.Dive:(launched?FlightMode.Launch:FlightMode.Air);return;
        }
        public RuleResult Launch(CommandContext c,Float3 normal,float speed,bool inputReady,bool climbing,bool mantling)
        {
            if(inactive||c.Tick<=lifecycleTick||lifecycleCommands.HasCommand(c.CommandId))return RuleResult.Reject("inactive lifecycle/replay");
            if(!inputReady||climbing||mantling||!V.Bounded(normal,10000)||(!V.Finite(V.Dot(normal,normal))||V.Dot(normal,normal)<.1f)||!V.Finite(speed)||speed<=0||speed>1000)return RuleResult.Reject("launch eligibility");
            Float3 n=V.Unit(normal),velocity=V.Mul(n,speed);
            if(!velocity.IsFinite)return RuleResult.Reject("invalid launch velocity");
            RuleResult r=commands.Take(c);if(!r.Accepted)return r;
            stateRevision++;PendingMotion=null;Velocity=velocity;clearance=.3f;ascent=n.Y>.05f;fallTime=divePitch=0;Mode=FlightMode.Launch;return r;
        }
        public RuleResult ApplyCollisionResult(CommandContext c,FlightMotionReceipt receipt,FlightCollisionResult result,IFlightCollisionAuthorization authorization)
        {
            if(inactive||lifecycleCommands.HasCommand(c.CommandId)||receipt==null||!Object.ReferenceEquals(receipt,PendingMotion)||receipt.Generation!=generation||
                receipt.Tick!=c.Tick||receipt.SessionId!=c.SessionId||receipt.ActorId!=c.ActorId||receipt.AuthorityEpoch!=c.AuthorityEpoch||
                authorization==null||!V.Bounded(result.Velocity,10000)||((result.Grounded||result.CeilingBlocked)&&result.Velocity.Y>0))
                return RuleResult.Reject("collision receipt/result");
            RuleResult r=commands.Validate(c);if(!r.Accepted)return r;
            r=authorization.ValidateCollision(c,receipt,result);if(!r.Accepted)return r;
            // Pure host authorization may not reenter; fail closed if it nevertheless invalidated the proposal.
            if(!Object.ReferenceEquals(receipt,PendingMotion))return RuleResult.Reject("collision proposal changed");
            // Ordinary fence publication; no mover/gravity/lifecycle reset.
            commands.Commit(c);stateRevision++;PendingMotion=null;Velocity=result.Velocity;
            if(result.Grounded||result.CeilingBlocked||result.ExternalMotionOwner){clearance=0;ascent=false;}
            else if(Velocity.Y<=0)ascent=false; // Horizontal/tilted pads retain their authored .3s clearance.
            if(result.Grounded){Mode=FlightMode.Ground;fallTime=divePitch=0;}
            else if(result.ExternalMotionOwner){Mode=FlightMode.Suspended;fallTime=divePitch=0;}
            else if(result.CeilingBlocked&&Mode!=FlightMode.Glide){Mode=FlightMode.Air;fallTime=divePitch=0;}
            return r;
        }
        static float HorizontalDistance(Float3 a,Float3 b){return V.Len(new Float3(a.X-b.X,0,a.Z-b.Z));}
    }
    public sealed class LaunchPad
    {
        public readonly string Id; public readonly Float3 Center,Normal;public readonly float Radius,Speed;
        public LaunchPad(string id,Float3 center,Float3 normal,float radius,float speed)
        {
            if(String.IsNullOrEmpty(id)||!V.Bounded(center,1000000)||!V.Bounded(normal,10000)||(!V.Finite(V.Len(normal))||V.Len(normal)<.31623f)||!V.Finite(radius)||radius<=0||radius>10000||!V.Finite(speed)||speed<=0||speed>1000)throw new ArgumentException("pad");
            Id=id;Center=center;Normal=V.Unit(normal);Radius=radius;Speed=speed;
        }
        public bool Contains(Float3 p)
        { if(!V.Bounded(p,1000000))return false;Float3 d=V.Sub(p,Center);float s=V.Dot(d,Normal);return s>=-.28f&&s<=.75f&&V.Dot(V.Sub(d,V.Mul(Normal,s)),V.Sub(d,V.Mul(Normal,s)))<=Radius*Radius; }
    }
    // One pad-contact tracker per actor. Stable pad IDs prevent duplicate colliders from double launching.
    public sealed partial class PadContacts
    {
        Dictionary<string,double> cooldown=new Dictionary<string,double>(StringComparer.Ordinal);
        double clock=-1;
        static RuleResult FindContact(LaunchPad pad,Float3 feet,Float3 previous,bool hasPrevious,out Float3 contact)
        {
            contact=feet;
            if(!pad.Contains(contact))
            {
                Float3 segment=V.Sub(feet,previous);float length2=V.Dot(segment,segment);
                if(!hasPrevious||length2>225||length2<.000001f)return RuleResult.Reject("no pad contact");
                contact=V.Add(previous,V.Mul(segment,V.Clamp(V.Dot(V.Sub(pad.Center,previous),segment)/length2,0,1)));
                if(!pad.Contains(contact))return RuleResult.Reject("no swept pad contact");
            }
            return RuleResult.Accept();
        }
        public RuleResult TryLaunch(CommandContext c,FlightMachine flight,LaunchPad pad,IFlightWorld world,Float3 feet,Float3 previous,bool hasPrevious,double now,bool ready,bool climbing,bool mantling,bool ghost)
        {
            if(flight==null||pad==null||world==null||!V.Bounded(feet,1000000)||!V.Bounded(previous,1000000)||!V.Finite(now)||now<0||now>1e12||now<clock||ghost)return RuleResult.Reject("pad observation");
            double until;if(cooldown.TryGetValue(pad.Id,out until)&&now<until)return RuleResult.Reject("pad cooldown");
            Float3 contact;RuleResult contactResult=FindContact(pad,feet,previous,hasPrevious,out contact);
            if(!contactResult.Accepted)return contactResult;
            if(!world.ClearPadContact(pad.Id,V.Add(pad.Center,V.Mul(pad.Normal,.2f)),V.Add(contact,V.Mul(pad.Normal,.12f))))return RuleResult.Reject("pad occluded");
            RuleResult r=flight.Launch(c,pad.Normal,pad.Speed,ready,climbing,mantling);
            if(r.Accepted){cooldown[pad.Id]=now+1.2;clock=now;padRevision++;}return r;
        }
    }
    // FLIGHT-DIVE (playtest 2026-11-15): the single home of every freefall/dive tuning number.
    // Pop1-style skydive: gravity is opposed by quadratic air drag, so speed settles at a
    // tunable terminal velocity. Forward stick sets a target body pitch (0 belly-to-earth,
    // 1 head-down); pitch ramps at a fixed rate and only changes the drag coefficient, so
    // speed approaches the new terminal through drag, never through direct acceleration.
    // Kept in this file because Tools/ CPU source lists name Flight files explicitly.
    public static class R26FreefallTuning
    {
        // Slightly stronger descent (r13 9.81, r14 12, r15 13). Source ascent retains authored jump/pad height and timing.
        public const float Gravity=13f, AscentGravity=16.2f;
        // Raise the two sustained descent limits together while keeping gravity and the pitch ramp.
        public const float BellyTerminalSpeed=70f, DiveTerminalSpeed=100f;
        // Forward stick below the dead zone keeps belly pitch; at Full it commands head-down.
        public const float DiveStickDeadZone=.35f, DiveStickFull=.9f;
        // Root-relative horizontal hand separation in metres, for both entry and release.
        public const float GlideSpread=.9f+.1016f;
        public const float GlidePoseGrace=.15f;
        // Pitch ramp times for the full 0..1 range: tuck into the dive, and arch back out.
        public const float DiveTuckSeconds=.8f, DiveRecoverSeconds=1f;
        // Source rule: a dive is available only after this much uncontrolled freefall.
        public const float DiveGateSeconds=1f;
    }
    // FLY4 evidence controls only (scripted before/after runs); always true in play.
    public static class R26GlideEntry
    {
        // The archived FLY4 test harness sets CarriedStickRule. XR pose ownership is unconditional.
        public static bool CarriedStickRule=true, StepDownRule=true, InferredPoseRule=true;
    }
    // Pure frame-rate-independent freefall mechanics. Ascent keeps the exact source gravity
    // step so authored jump heights and pad apexes are unchanged; drag acts on descent.
    public static class R26Freefall
    {
        static float Clamp01(float a){return a<0?0:a>1?1:a;}
        // Body drag area blends linearly with pitch; k=g/vt^2 per pitch.
        public static float TerminalSpeed(float pitch)
        {
            double p=Clamp01(pitch),b=R26FreefallTuning.BellyTerminalSpeed,d=R26FreefallTuning.DiveTerminalSpeed;
            return (float)(1/Math.Sqrt((1-p)/(b*b)+p/(d*d)));
        }
        // Analog forward stick wins; callers that only know the boolean dive request get full pitch.
        public static float DiveCommand(float forwardAxis,bool diveForward)
        {
            if(forwardAxis>0)return Clamp01((forwardAxis-R26FreefallTuning.DiveStickDeadZone)/(R26FreefallTuning.DiveStickFull-R26FreefallTuning.DiveStickDeadZone));
            return diveForward?1:0;
        }
        // Constant-rate pitch ramp toward the command; average is the exact time-average over the step.
        public static float StepPitch(float pitch,float command,float dt,out float average)
        {
            pitch=Clamp01(pitch);command=Clamp01(command);
            float rate=command>pitch?1/R26FreefallTuning.DiveTuckSeconds:1/R26FreefallTuning.DiveRecoverSeconds,travel=Math.Abs(command-pitch),reach=rate*dt;
            if(travel<=reach){float settle=travel/rate;average=dt>0?((pitch+command)*.5f*settle+command*(dt-settle))/dt:command;return command;}
            float next=pitch+(command>pitch?reach:-reach);average=(pitch+next)*.5f;return next;
        }
        // Exact solution of dv/dt=-g+k v^2 (descent) over dt at the step's average pitch.
        public static float Fall(float vy,float pitch,float dt)
        {
            float g=R26FreefallTuning.Gravity,rise=vy-R26FreefallTuning.AscentGravity*dt;
            if(vy>0&&rise>=0)return rise;
            double t=dt,s=-(double)vy,vt=TerminalSpeed(pitch);
            if(vy>0){t-=vy/(double)R26FreefallTuning.AscentGravity;s=0;}
            double x=t*g/vt;
            if(s<vt){double u=s/vt;s=vt*Math.Tanh(.5*Math.Log((1+u)/(1-u))+x);}
            else if(s>vt){double u=vt/s;s=vt/Math.Tanh(.5*Math.Log((1+u)/(1-u))+x);}
            return (float)-s;
        }
    }
}


