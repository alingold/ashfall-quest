using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.ClimbingMotor;
namespace Ashfall.Integration.MotionAuthority
{
    // Candidate composition only. No Update, Transform, tracked pose, collider or
    // native solver implementation. Host must retain one instance per canonical actor.
    public sealed class HostMotionArbiter:IDisposable
    {
        readonly HostMotionLease leases;readonly ClimbController climb;
        readonly PlayerFlightMotor flight;readonly IMotionInvalidation invalidation;
        bool disposed;
        public HostMotionLease Leases {get{return leases;}}
        public Float3 Velocity {get{return flight.Velocity;}}
        public const string PublicationBlocker="prepared capsule/climb lifecycle and certified physical root plan unavailable; legacy motion publication refused";
        public HostMotionArbiter(MotionBinding binding,IMotionLeaseAuthorization authorization,IMotionCollisionReceipts receipts,
            IMotionInvalidation invalidation,ClimbController climb,PlayerFlightMotor flight,CapsuleFleet capsules,
            IExclusivePlayerMotor physicalRoot,IClimbFrameAuthorization climbAuthorization,IClimbSupport support)
        {
            if(receipts==null||invalidation==null||climb==null||flight==null||capsules==null||physicalRoot==null||climbAuthorization==null||support==null)throw new ArgumentException("motion ports");
            leases=new HostMotionLease(binding,authorization);this.climb=climb;this.flight=flight;this.invalidation=invalidation;
            invalidation.Invalidated+=Invalidate;
        }
        public void Invalidate(){leases.Invalidate();climb.Reset();}
        public void Dispose(){if(disposed)return;disposed=true;invalidation.Invalidated-=Invalidate;Invalidate();}
        // Fail before lease ownership, anchors, private input, Flight/pad/fleet replay,
        // or root writes. A boolean promise or legacy Apply port cannot manufacture
        // a certificate for CharacterController.Move's still-unknown physical result.
        public RuleResult StepLocomotion(PreparedMotionLease lease,MotorFrame frame,out ClimbStep climbing,out PlayerMotorResult applied)
        {climbing=new ClimbStep();applied=new PlayerMotorResult();return RuleResult.Reject(PublicationBlocker);}
        public RuleResult StepPad(PreparedMotionLease lease,MotorFrame frame,LaunchPad pad,IFlightWorld world,
            Float3 previous,bool hasPrevious,double now,out ClimbStep climbing,out PlayerMotorResult applied)
        {climbing=new ClimbStep();applied=new PlayerMotorResult();return RuleResult.Reject(PublicationBlocker);}
        public RuleResult BeginCapsule(PreparedMotionLease lease,MotorFrame frame,CapsuleEligibility eligibility,CapsulePlan plan,
            double now,ICapsuleRouteValidation routes,out CapsuleUpdate update,out PlayerMotorResult applied)
        {update=new CapsuleUpdate();applied=new PlayerMotorResult();return RuleResult.Reject(PublicationBlocker);}
        public RuleResult AdvanceCapsule(PreparedMotionLease lease,MotorFrame frame,double now,bool match,bool alive,bool eject,
            IFlightWorld world,Float3 releaseVelocity,out CapsuleUpdate update,out PlayerMotorResult applied)
        {update=new CapsuleUpdate();applied=new PlayerMotorResult();return RuleResult.Reject(PublicationBlocker);}
        // ResetActor on Flight and Fleet are separately fallible and cannot publish
        // together. Explicit Invalidate remains the documented safety cancellation.
        public RuleResult ResetLifecycle(CommandContext c,long generation,FlightLifecycle reason,IFlightLifecycleAuthorization authorization)
        {return RuleResult.Reject(PublicationBlocker);}
    }
}
