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
        float fallTime, clearance; bool ascent;
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
            Velocity=new Float3();Mode=FlightMode.Ground;fallTime=clearance=0;ascent=false;
            return r;
        }
        // r2 public numeric envelope: SI per-component limits, inclusive.
        // Velocity 10000m/s, walk target 1000m/s, direction 2, root-relative hands 10m.
        public RuleResult Step(CommandContext c, FlightFrame f)
        {
            if(inactive||c.Tick<=lifecycleTick||lifecycleCommands.HasCommand(c.CommandId))return RuleResult.Reject("inactive lifecycle/replay");
            if(!V.Finite(f.DeltaSeconds)||f.DeltaSeconds<=0||f.DeltaSeconds>1||!V.Bounded(f.Velocity,10000)||!V.Bounded(f.Forward,2)||!V.Bounded(f.MoveDirection,2)||!V.Bounded(f.WalkTarget,1000)||!V.Bounded(f.LeftHand,10)||!V.Bounded(f.RightHand,10))return RuleResult.Reject("invalid frame");
            long before=stateRevision;
            Float3 velocity;FlightMode mode=Mode;float fall=fallTime,clear=clearance;bool rising=ascent;
            Calculate(f,out velocity,ref mode,ref fall,ref clear,ref rising);
            if(f.CeilingBlocked&&velocity.Y>0){velocity=new Float3(velocity.X,0,velocity.Z);clear=0;rising=false;if(mode!=FlightMode.Suspended)mode=f.Supported?FlightMode.Ground:FlightMode.Air;}
            if(!V.Bounded(velocity,10000)||!V.Finite(fall)||!V.Finite(clear))return RuleResult.Reject("invalid derived frame");
            RuleResult r=commands.Validate(c);if(!r.Accepted)return r;
            if(before!=stateRevision)return RuleResult.Reject("Flight changed during step");
            commands.Commit(c);
            Velocity=velocity;Mode=mode;fallTime=fall;clearance=clear;ascent=rising;
            stateRevision++;PendingMotion=new FlightMotionReceipt(c,generation,++motionRevision,velocity);return r;
        }
        void Calculate(FlightFrame f,out Float3 velocity,ref FlightMode mode,ref float fall,ref float clear,ref bool rising)
        {
            velocity=f.Velocity;

            if(!f.InputEnabled||!f.Tracked||f.Paused||f.Climbing||f.Mantling){mode=FlightMode.Suspended;fall=0;clear=0;rising=false;return;}
            bool wants=!f.InventoryOpen&&(f.XR?(f.HandsReady&&HorizontalDistance(f.LeftHand,f.RightHand)>.95890625f):f.DesktopGlide);
            bool launched=(rising&&velocity.Y>0)||clear>0;
            clear=Math.Max(0,clear-f.DeltaSeconds);
            if(launched&&wants&&clear<=0){rising=false;launched=false;}
            if(launched)wants=false;else rising=false;
            bool ground=f.Supported&&!launched;
            // A controlled glide descends, but is not accumulating free fall.
            // The source bug let a held forward stick force a dive after one
            // second of spread-hand flight and poisoned the next glide release.
            bool controlledGlide=wants&&!ground;
            fall=!ground&&!launched&&!controlledGlide&&velocity.Y<-.5f?Math.Min(1,fall+f.DeltaSeconds):0;
            bool dive=!controlledGlide&&!f.InventoryOpen&&fall>=1&&f.DiveForward;
            if(dive)wants=false;
            if(ground){mode=FlightMode.Ground;fall=0;return;}
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
                Float3 target=V.Mul(V.Unit(V.Add(heading,V.Mul(new Float3(f.MoveDirection.X,0,f.MoveDirection.Z),.35f))),GlideSpeed);
                velocity=new Float3(target.X,-GlideSpeed/7,target.Z);mode=FlightMode.Glide;return;
            }
            if(clear>0){mode=FlightMode.Launch;return;}
            float y=velocity.Y-16.2f*f.DeltaSeconds;
            if(dive)y=Math.Max(Math.Min(-150,y),y-96*f.DeltaSeconds);
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
            stateRevision++;PendingMotion=null;Velocity=velocity;clearance=.3f;ascent=n.Y>.05f;fallTime=0;Mode=FlightMode.Launch;return r;
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
            if(result.Grounded){Mode=FlightMode.Ground;fallTime=0;}
            else if(result.ExternalMotionOwner){Mode=FlightMode.Suspended;fallTime=0;}
            else if(result.CeilingBlocked){Mode=FlightMode.Air;fallTime=0;}
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
}


