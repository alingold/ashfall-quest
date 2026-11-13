using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.Flight
{
    // Mechanics only. Host supplies monotonic time; world/gate ports must remain pure
    // and stable on the exclusive lane from final validation through publication.
    public sealed class PreparedPadLaunch
    {
        internal readonly PadContacts Contacts;
        internal readonly FlightMachine Flight;
        internal readonly LaunchPad Pad;
        internal readonly IFlightWorld World;
        internal readonly Float3 Face, Contact;
        internal readonly CommandContext Context;
        internal readonly long Revision, FlightRevision;
        internal readonly double Now;
        internal readonly Dictionary<string,double> Cooldown;
        internal CommandFence Commands;
        internal PreparedFlightStep Step;
        internal bool Finished, Validated;
        public Float3 Velocity {get; private set;}
        public Float3 PreparedVelocity {get{return Step==null?Velocity:Step.Output;}}
        public RuleResult PrepareCollision(FlightCollisionResult result)
        {Validated=false;return Step==null||!ValidateLocal().Accepted?RuleResult.Reject("pad step missing/stale"):Flight.PrepareCollision(Step,result);}
        internal PreparedPadLaunch(PadContacts contacts,FlightMachine flight,LaunchPad pad,IFlightWorld world,Float3 face,Float3 contact,
            CommandContext c,long revision,long flightRevision,double now,Dictionary<string,double> cooldown,CommandFence commands)
        {Contacts=contacts;Flight=flight;Pad=pad;World=world;Face=face;Contact=contact;Context=c;Revision=revision;FlightRevision=flightRevision;
         Now=now;Cooldown=cooldown;Commands=commands;Velocity=V.Mul(pad.Normal,pad.Speed);}
        public RuleResult Validate(double now){Validated=false;var r=Contacts.Validate(this,now);Validated=r.Accepted;return r;}
        public void CommitValidated(){Contacts.Commit(this);}
        public void Abort(){if(Finished)return;Finished=true;Contacts.Release(this);Flight.ReleasePad(this);}
        public RuleResult ValidateLocal(){return Contacts.Local(this);}
    }
    public sealed partial class PadContacts
    {
        long padRevision;
        PreparedPadLaunch pendingPad;
        public RuleResult PrepareLaunch(CommandContext c,FlightMachine flight,LaunchPad pad,IFlightWorld world,Float3 feet,Float3 previous,
            bool hasPrevious,double now,bool ready,bool climbing,bool mantling,bool ghost,out PreparedPadLaunch plan)
        {
            plan=null;
            if(pendingPad!=null)return RuleResult.Reject("pad reservation outstanding");
            if(flight==null||pad==null||world==null||!V.Bounded(feet,1000000)||!V.Bounded(previous,1000000)||!V.Finite(now)||now<0||now>1e12||now<clock||ghost||!ready||climbing||mantling)
                return RuleResult.Reject("pad observation/eligibility");
            double until;if(cooldown.TryGetValue(pad.Id,out until)&&now<until)return RuleResult.Reject("pad cooldown");
            Float3 contact;RuleResult contactResult=FindContact(pad,feet,previous,hasPrevious,out contact);
            if(!contactResult.Accepted)return contactResult;
            long revision=padRevision;Float3 face=V.Add(pad.Center,V.Mul(pad.Normal,.2f));contact=V.Add(contact,V.Mul(pad.Normal,.12f));
            if(!world.ClearPadContact(pad.Id,face,contact)||revision!=padRevision)return RuleResult.Reject("pad occluded/changed");
            if(cooldown.Count>=4096&&!cooldown.ContainsKey(pad.Id))return RuleResult.Reject("pad history full");
            var next=new Dictionary<string,double>(cooldown,StringComparer.Ordinal);next[pad.Id]=now+1.2;
            var r=flight.ReservePad(this,pad,world,face,contact,c,revision,now,next,out plan);
            if(r.Accepted)pendingPad=plan;return r;
        }
        internal RuleResult Local(PreparedPadLaunch p)
        {return p==null||p.Finished||p.Contacts!=this||pendingPad!=p||p.Revision!=padRevision?RuleResult.Reject("pad revision/reservation"):p.Flight.PadLocal(p);}
        internal RuleResult Validate(PreparedPadLaunch p,double now)
        {
            var r=Local(p);if(!r.Accepted)return r;
            if(!V.Finite(now)||now<p.Now||now-p.Now>.1)return RuleResult.Reject("pad expiry");
            if(!p.World.ClearPadContact(p.Pad.Id,p.Face,p.Contact))return RuleResult.Reject("pad source changed");
            r=p.Flight.ValidatePadAuthority(p);return r.Accepted?Local(p):r;
        }
        internal void Commit(PreparedPadLaunch p)
        {
            if(!Local(p).Accepted||!p.Validated)throw new InvalidOperationException("unvalidated/stale pad publication");
            p.Flight.CommitPad(p);cooldown=p.Cooldown;clock=p.Now;padRevision++;pendingPad=null;p.Finished=true;
        }
        internal void Release(PreparedPadLaunch p){if(pendingPad==p)pendingPad=null;}
    }
    public sealed partial class FlightMachine
    {
        PreparedPadLaunch reservedPad;
        internal RuleResult ReservePad(PadContacts contacts,LaunchPad pad,IFlightWorld world,Float3 face,Float3 contact,CommandContext c,long revision,
            double now,Dictionary<string,double> cooldown,out PreparedPadLaunch plan)
        {
            plan=null;if(reservedPad!=null||inactive||c.Tick<=lifecycleTick||lifecycleCommands.HasCommand(c.CommandId))return RuleResult.Reject("Flight reservation/lifecycle");
            long before=stateRevision;var r=commands.Validate(c);if(!r.Accepted)return r;
            if(before!=stateRevision||reservedPad!=null)return RuleResult.Reject("Flight changed");
            plan=new PreparedPadLaunch(contacts,this,pad,world,face,contact,c,revision,before,now,cooldown,commands.Stage(c));reservedPad=plan;return r;
        }
        internal RuleResult PadLocal(PreparedPadLaunch p)
        {return p.Flight!=this||reservedPad!=p||p.FlightRevision!=stateRevision||inactive||p.Context.Tick<=lifecycleTick||
            (p.Step!=null&&(p.Step.Finished||!p.Step.Resolved))?RuleResult.Reject("Flight pad revision"):RuleResult.Accept();}
        internal RuleResult ValidatePadAuthority(PreparedPadLaunch p)
        {
            var r=commands.Validate(p.Context);if(!r.Accepted)return r;
            return p.Step==null?r:commands.Validate(p.Step.Context);
        }
        internal void ReleasePad(PreparedPadLaunch p){if(reservedPad==p)reservedPad=null;}
        public RuleResult PrepareStepAfterPad(PreparedPadLaunch pad,CommandContext c,FlightFrame f,float yaw,Float3 playerVelocity,out FlightMode stepMode)
        {
            stepMode=FlightMode.Ground;if(pad==null||!pad.ValidateLocal().Accepted||pad.Flight!=this||pad.Step!=null||c.Tick!=pad.Context.Tick||
                !V.Finite(yaw)||Math.Abs(yaw)>16||!V.Bounded(playerVelocity,10000))return RuleResult.Reject("pad step scope");
            f.Velocity=pad.Velocity;
            if(!V.Finite(f.DeltaSeconds)||f.DeltaSeconds<=0||f.DeltaSeconds>.1f||!V.Bounded(f.Forward,2)||!V.Bounded(f.MoveDirection,2)||!V.Bounded(f.WalkTarget,1000)||!V.Bounded(f.LeftHand,10)||!V.Bounded(f.RightHand,10))return RuleResult.Reject("pad step frame");
            var r=pad.Commands.Validate(c);if(!r.Accepted)return r;
            if(!pad.ValidateLocal().Accepted)return RuleResult.Reject("pad changed");
            f.Forward=Rotate(f.Forward,yaw);f.MoveDirection=Rotate(f.MoveDirection,yaw);f.WalkTarget=Rotate(f.WalkTarget,yaw);
            FlightMode mode=FlightMode.Launch;float fall=0,clear=.3f;bool rising=pad.Pad.Normal.Y>.05f;Float3 output;
            Calculate(f,out output,ref mode,ref fall,ref clear,ref rising);
            if(f.CeilingBlocked&&output.Y>0){output=new Float3(output.X,0,output.Z);clear=0;rising=false;if(mode!=FlightMode.Suspended)mode=f.Supported?FlightMode.Ground:FlightMode.Air;}
            if(mode==FlightMode.Ground||mode==FlightMode.Suspended)output=playerVelocity;
            if(!V.Bounded(output,10000))return RuleResult.Reject("pad step output");
            var plan=new PreparedFlightStep(this,c,f,stateRevision,generation,motionRevision,mode);stepMode=mode;
            plan.Output=output;plan.OutputMode=mode;plan.Fall=fall;plan.Clear=clear;plan.Rising=rising;plan.Resolved=true;
            pad.Step=plan;pad.Validated=false;pad.Commands=pad.Commands.Stage(c);return r;
        }
        internal void CommitPad(PreparedPadLaunch p)
        {
            commands=p.Commands;stateRevision++;reservedPad=null;
            if(p.Step==null){Velocity=p.Velocity;Mode=FlightMode.Launch;fallTime=0;clearance=.3f;ascent=p.Pad.Normal.Y>.05f;PendingMotion=null;return;}
            var s=p.Step;Velocity=s.Output;Mode=s.OutputMode;fallTime=s.Fall;clearance=s.Clear;ascent=s.Rising;motionRevision++;
            s.Receipt.ProposedVelocity=s.Output;PendingMotion=s.HasCollision?null:s.Receipt;s.Finished=true;
        }
    }
}
