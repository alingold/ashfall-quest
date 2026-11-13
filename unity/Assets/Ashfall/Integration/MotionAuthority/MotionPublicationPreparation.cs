using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Climbing;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.ClimbingMotor;
namespace Ashfall.Integration.MotionAuthority
{
    // An actual owner preparation participant, deliberately NOT a geometry certificate.
    // No physical root reference is retained and no publication entrypoint is exposed.
    // One trusted serialized host lane; all query/authorization ports must be stable.
    public sealed class MotionPublicationPreparation
    {
        readonly HostMotionLease leases;
        readonly ClimbController climb;
        readonly PlayerFlightMotor motor;
        readonly IClimbFrameAuthorization authorization;
        readonly IMotionCollisionReceipts receipts;
        readonly IClimbSupport support;
        readonly IRootCollisionPreflight preflight;
        PreparedMotionPublication pending;
        public MotionPublicationPreparation(HostMotionLease leases,ClimbController climb,PlayerFlightMotor motor,
            IClimbFrameAuthorization authorization,IMotionCollisionReceipts receipts,IClimbSupport support,IRootCollisionPreflight preflight)
        {
            if(leases==null||climb==null||motor==null||authorization==null||receipts==null||support==null||preflight==null)throw new ArgumentException("preparation ports");
            this.leases=leases;this.climb=climb;this.motor=motor;this.authorization=authorization;this.receipts=receipts;this.support=support;this.preflight=preflight;
        }
        public RuleResult Prepare(PreparedMotionLease lease,MotorFrame frame,out PreparedMotionPublication plan)
        {
            plan=null;if(pending!=null)return RuleResult.Reject("motion preparation outstanding");
            var r=Admission(lease,frame);if(!r.Accepted)return r;
            var input=frame.Input;var options=frame.Climb;
            options.Enabled &= frame.Ready;
            options.LeftReserved|=frame.Reservations.Left;options.RightReserved|=frame.Reservations.Right;
            if(options.LeftReserved)options.LeftTracked=false;if(options.RightReserved)options.RightTracked=false;
            if(!frame.Ready)input.GripLeft=input.GripRight=false;
            bool had=climb.LeftAnchored||climb.RightAnchored||climb.Mantling;
            PreparedClimbMotion cp;
            r=climb.PrepareStep(input,frame.Root,frame.Flight.DeltaSeconds,options,!frame.Environment.IsXr,
                frame.Ready&&!options.LeftReserved&&frame.DesktopE,frame.DesktopForward,!frame.Ready,out cp);
            if(!r.Accepted)return r;
            PreparedPlayerFlightStep mp=null;
            try
            {
                var step=cp.Step;bool owns=step.OwnsMotion||had||!frame.Ready;
                var env=frame.Environment;env.ExternalMotionOwner=owns;
                var observation=frame.Flight;observation.Climbing=owns;observation.Mantling=step.Mantling;observation.InputEnabled=frame.Ready;
                r=motor.PrepareStep(frame.Context,input,env,observation,out mp);
                if(!r.Accepted){cp.Abort();return r;}
                Float3 delta=owns?step.RootDisplacement:mp.Motion.Displacement;
                if(owns&&!step.Climbing&&!step.Mantling&&!step.Launch&&had)
                {
                    Float3 completion;
                    if(!support.TryComplete(frame.Root,input.HeadPosition,delta,step.Velocity,frame.Flight.DeltaSeconds,out completion)||
                        !completion.IsFinite||Length(completion)>.27f)
                    {cp.Abort();mp.Abort();return RuleResult.Reject("support completion unavailable");}
                    delta=new Float3(delta.X+completion.X,delta.Y+completion.Y,delta.Z+completion.Z);
                }
                var proposal=RootMotionSafety.Plan(frame.Root,input.HeadPosition,delta,owns?0:mp.Motion.YawRadians,true);
                r=RootMotionSafety.Approve(proposal,true,preflight);
                if(!r.Accepted){cp.Abort();mp.Abort();return r;}
                var prepared=new PreparedMotionPublication(this,lease,frame,cp,mp,proposal,step,owns);
                r=Validate(prepared);
                if(!r.Accepted){cp.Abort();mp.Abort();return r;}
                pending=prepared;plan=prepared;return r;
            }
            catch {cp.Abort();if(mp!=null)mp.Abort();throw;}
        }
        RuleResult Admission(PreparedMotionLease lease,MotorFrame f)
        {
            if(lease==null||lease.Host!=leases||lease.Owner!=MotionOwner.Locomotion||
                f.LifecycleGeneration!=lease.Binding.Generation||f.Context.SessionId!=lease.Context.SessionId||
                f.Context.ActorId!=lease.Context.ActorId||f.Context.AuthorityEpoch!=lease.Context.AuthorityEpoch||
                f.Context.CommandId!=lease.Context.CommandId||f.Context.IsAuthority!=lease.Context.IsAuthority||
                f.Context.Tick!=lease.Context.Tick||f.Input.Tick!=f.Context.Tick||f.Input.Sequence!=lease.Sequence||
                f.Environment.ExternalMotionOwner||!f.Root.IsFinite||!f.Input.HeadPosition.IsFinite||
                Single.IsNaN(f.Flight.DeltaSeconds)||Single.IsInfinity(f.Flight.DeltaSeconds)||f.Flight.DeltaSeconds<=0||f.Flight.DeltaSeconds>.1f)
                return RuleResult.Reject("motion preparation scope/frame/owner");
            var r=lease.Validate();if(!r.Accepted)return r;
            r=authorization.Validate(f.Context,f.Input.Sequence,f.LifecycleGeneration);if(!r.Accepted)return r;
            return receipts.ValidateBinding(lease.Binding,f.Context.Tick,f.Input.Sequence);
        }
        internal RuleResult Validate(PreparedMotionPublication p)
        {
            if(p.Owner!=this||p.Finished||(pending!=null&&pending!=p))return RuleResult.Reject("motion preparation stale");
            var r=Admission(p.Lease,p.Frame);if(!r.Accepted)return r;
            r=p.Climb.Validate();if(!r.Accepted)return r;
            r=p.Motor.Validate();if(!r.Accepted)return r;
            // A query approval is still not a reserved, no-fail physical result.
            return RootMotionSafety.Approve(p.Proposal,true,preflight);
        }
        internal void Abort(PreparedMotionPublication p)
        {
            if(p.Finished||p.Applied)return;p.Finished=true;p.Climb.Abort();p.Motor.Abort();if(pending==p)pending=null;
        }
        internal RuleResult ValidateAccounting(PreparedMotionPublication p)
        {return p.Owner==this&&pending==p&&!p.Finished?Admission(p.Lease,p.Frame):RuleResult.Reject("accounting association stale");}
        static double Length(Float3 v){return Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y+(double)v.Z*v.Z);}
    }
    public sealed class PreparedMotionPublication:IDisposable
    {
        internal readonly MotionPublicationPreparation Owner;
        internal readonly PreparedMotionLease Lease;
        internal readonly MotorFrame Frame;
        internal readonly PreparedClimbMotion Climb;
        internal readonly PreparedPlayerFlightStep Motor;
        internal bool Finished;
        internal bool Applied=false;
        internal readonly bool ClimbOwnsMotion;
        public RootMotionProposal Proposal {get;private set;}
        public ClimbStep Climbing {get;private set;}
        public MotionStep Motion {get{return Motor.Motion;}}
        internal PreparedMotionPublication(MotionPublicationPreparation owner,PreparedMotionLease lease,MotorFrame frame,
            PreparedClimbMotion climb,PreparedPlayerFlightStep motor,RootMotionProposal proposal,ClimbStep climbing,bool owns)
        {Owner=owner;Lease=lease;Frame=frame;Climb=climb;Motor=motor;Proposal=proposal;Climbing=climbing;ClimbOwnsMotion=owns;}
        public RuleResult Validate(){return Owner.Validate(this);}
        public RuleResult Publish(){return RuleResult.Reject(HostMotionArbiter.PublicationBlocker);}
        public void Abort(){Owner.Abort(this);}
        public void Dispose(){Abort();}
    }
}
