using System;
using Ashfall.Core;
namespace Ashfall.Modules.Flight
{
    // Opaque one-use host capability. No public constructor or caller-selected revision.
    public sealed class PreparedFlightStep
    {
        internal readonly FlightMachine Owner;
        internal readonly CommandContext Context;
        internal readonly FlightFrame Frame;
        internal readonly long Revision, Generation;
        internal readonly FlightMotionReceipt Receipt;
        internal bool Finished, Resolved, HasCollision;
        internal Float3 Output;
        internal FlightMode OutputMode;
        internal float Fall, Clear;
        internal bool Rising;
        public FlightMode Mode { get; private set; }
        internal PreparedFlightStep(FlightMachine owner,CommandContext c,FlightFrame frame,long revision,long generation,long motionRevision,FlightMode mode)
        {Owner=owner;Context=c;Frame=frame;Revision=revision;Generation=generation;Mode=mode;Receipt=new FlightMotionReceipt(c,generation,motionRevision+1,new Float3());}
        public void Abort() { Finished=true; }
    }
    public sealed partial class FlightMachine
    {
        long stateRevision;
        public RuleResult PrepareStep(CommandContext c,FlightFrame f,out PreparedFlightStep plan)
        {
            plan=null;
            if(inactive||c.Tick<=lifecycleTick||lifecycleCommands.HasCommand(c.CommandId))return RuleResult.Reject("inactive lifecycle/replay");
            if(!V.Finite(f.DeltaSeconds)||f.DeltaSeconds<=0||f.DeltaSeconds>.1f||!V.Bounded(f.Velocity,10000)||!V.Bounded(f.Forward,2)||!V.Bounded(f.MoveDirection,2)||!V.Bounded(f.WalkTarget,1000)||!V.Bounded(f.LeftHand,10)||!V.Bounded(f.RightHand,10))return RuleResult.Reject("invalid prepared frame");
            long sourceRevision=stateRevision;
            Float3 velocity;FlightMode mode=Mode;float fall=fallTime,clear=clearance;bool rising=ascent;
            Calculate(f,out velocity,ref mode,ref fall,ref clear,ref rising);
            if(f.CeilingBlocked&&velocity.Y>0&&mode!=FlightMode.Suspended)mode=f.Supported?FlightMode.Ground:FlightMode.Air;
            RuleResult r=commands.Validate(c);if(!r.Accepted)return r;
            if(sourceRevision!=stateRevision)return RuleResult.Reject("Flight changed during preparation");
            plan=new PreparedFlightStep(this,c,f,stateRevision,generation,motionRevision,mode);return r;
        }
        // Calculate the complete proposed state without publishing Flight or replay.
        // Detached Player output supplies yaw; the actual Calculate algorithm remains
        // the single flight/gravity implementation.
        public RuleResult ResolvePreparedStep(PreparedFlightStep plan,float yaw,Float3 playerVelocity,out Float3 produced)
        {
            produced=new Float3();
            RuleResult r=ValidatePreparedStep(plan);if(!r.Accepted)return r;
            if(plan.HasCollision||!V.Finite(yaw)||Math.Abs(yaw)>16||!V.Bounded(playerVelocity,10000))return RuleResult.Reject("prepared output");
            FlightFrame f=plan.Frame;
            f.Forward=Rotate(f.Forward,yaw);f.MoveDirection=Rotate(f.MoveDirection,yaw);f.WalkTarget=Rotate(f.WalkTarget,yaw);
            FlightMode mode=Mode;float fall=fallTime,clear=clearance;bool rising=ascent;
            Float3 velocity;Calculate(f,out velocity,ref mode,ref fall,ref clear,ref rising);
            if(f.CeilingBlocked&&velocity.Y>0){velocity=new Float3(velocity.X,0,velocity.Z);clear=0;rising=false;if(mode!=FlightMode.Suspended)mode=f.Supported?FlightMode.Ground:FlightMode.Air;}
            if(mode==FlightMode.Ground||mode==FlightMode.Suspended)velocity=playerVelocity;
            if(!V.Bounded(velocity,10000))return RuleResult.Reject("prepared derived output");
            plan.Output=velocity;plan.OutputMode=mode;plan.Fall=fall;plan.Clear=clear;plan.Rising=rising;plan.Resolved=true;
            produced=velocity;return RuleResult.Accept();
        }
        public RuleResult ValidatePreparedStep(PreparedFlightStep plan)
        {
            RuleResult local=ValidatePreparedStepLocal(plan);if(!local.Accepted)return local;
            RuleResult authorized=commands.Validate(plan.Context);
            return authorized.Accepted?ValidatePreparedStepLocal(plan):authorized;
        }
        public RuleResult ValidatePreparedStepLocal(PreparedFlightStep plan)
        {
            return plan==null||plan.Finished||!Object.ReferenceEquals(plan.Owner,this)||plan.Revision!=stateRevision||plan.Generation!=generation||inactive||
                plan.Context.Tick<=lifecycleTick||lifecycleCommands.HasCommand(plan.Context.CommandId)
                ?RuleResult.Reject("prepared scope/revision"):RuleResult.Accept();
        }
        // The certified solver result must already exist, and its host receipt must
        // already be authorized. This API checks numbers, never certifies geometry.
        // Fold feedback into the pending step: no post-root authorization callback.
        public RuleResult PrepareCollision(PreparedFlightStep plan,FlightCollisionResult result)
        {
            RuleResult r=ValidatePreparedStep(plan);if(!r.Accepted)return r;
            if(!plan.Resolved||plan.HasCollision||!V.Bounded(result.Velocity,10000)||((result.Grounded||result.CeilingBlocked)&&result.Velocity.Y>0))return RuleResult.Reject("prepared collision");
            plan.Output=result.Velocity;plan.HasCollision=true;
            if(result.Grounded||result.CeilingBlocked||result.ExternalMotionOwner||result.Velocity.Y<=0){plan.Clear=0;plan.Rising=false;}
            if(result.Grounded){plan.OutputMode=FlightMode.Ground;plan.Fall=0;}
            else if(result.ExternalMotionOwner){plan.OutputMode=FlightMode.Suspended;plan.Fall=0;}
            else if(result.CeilingBlocked){plan.OutputMode=FlightMode.Air;plan.Fall=0;}
            return r;
        }
        // Trusted host lane only: caller just validated ALL participants, then performs
        // no callbacks between commits. No authorization/geometry/IO in publication.
        public void CommitPreparedStepValidated(PreparedFlightStep plan)
        {
            if(plan==null||plan.Finished||plan.Owner!=this||plan.Revision!=stateRevision||plan.Generation!=generation||!plan.Resolved)
                throw new InvalidOperationException("stale Flight publication");
            commands.Commit(plan.Context);plan.Finished=true;stateRevision++;motionRevision++;
            Velocity=plan.Output;Mode=plan.OutputMode;fallTime=plan.Fall;clearance=plan.Clear;ascent=plan.Rising;
            plan.Receipt.ProposedVelocity=plan.Output;PendingMotion=plan.HasCollision?null:plan.Receipt;
        }
        // Compatibility module-only call. A root transaction uses the split methods.
        public RuleResult PublishPreparedStep(CommandContext c,PreparedFlightStep plan,float yaw,Float3 playerVelocity,out Float3 produced)
        {
            produced=new Float3();
            if(plan==null||c.SessionId!=plan.Context.SessionId||c.ActorId!=plan.Context.ActorId||c.AuthorityEpoch!=plan.Context.AuthorityEpoch||
                c.CommandId!=plan.Context.CommandId||c.Tick!=plan.Context.Tick||c.IsAuthority!=plan.Context.IsAuthority)return RuleResult.Reject("prepared context");
            RuleResult r=ResolvePreparedStep(plan,yaw,playerVelocity,out produced);if(!r.Accepted)return r;
            CommitPreparedStepValidated(plan);return r;
        }
        static Float3 Rotate(Float3 v,float yaw)
        {float s=(float)Math.Sin(yaw),c=(float)Math.Cos(yaw);return new Float3(c*v.X+s*v.Z,v.Y,-s*v.X+c*v.Z);}
    }
}
