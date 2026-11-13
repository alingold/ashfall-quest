using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
namespace Ashfall.Integration.ClimbingMotor
{
    // One instance per authenticated actor/lifecycle, called under the shared host lane.
    // No driver Update, camera ownership or independently running gravity loop.
    public sealed class ClimbingRootMotor
    {
        readonly string session,actor;readonly long epoch;
        readonly ClimbController climb;readonly PlayerFlightMotor motion;readonly IExclusivePlayerMotor root;
        readonly IClimbFrameAuthorization authorization;readonly IClimbSupport support;
        long tick=-1,sequence=-1,generation=-1;bool ready,xr;
        public ClimbingRootMotor(string session,string actor,long epoch,ClimbController climb,PlayerFlightMotor motion,
            IExclusivePlayerMotor root,IClimbFrameAuthorization authorization,IClimbSupport support)
        {
            if(String.IsNullOrEmpty(session)||String.IsNullOrEmpty(actor)||climb==null||motion==null||root==null||authorization==null||support==null)throw new ArgumentException("bound motor ports");
            this.session=session;this.actor=actor;this.epoch=epoch;this.climb=climb;this.motion=motion;this.root=root;this.authorization=authorization;this.support=support;
        }
        public RuleResult Step(MotorFrame f,out ClimbStep climbing,out PlayerMotorResult applied)
        {
            climbing=new ClimbStep();applied=new PlayerMotorResult();var c=f.Context;
            float dt=f.Flight.DeltaSeconds;
            if(!c.IsAuthority||c.SessionId!=session||c.ActorId!=actor||c.AuthorityEpoch!=epoch||String.IsNullOrEmpty(c.CommandId)||
                c.Tick!=f.Input.Tick||c.Tick<=tick||f.Input.Sequence<=sequence||f.LifecycleGeneration<generation||
                !f.Root.IsFinite||!f.Input.HeadPosition.IsFinite||Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<=0||dt>.1f)
                return RuleResult.Reject("motor scope/frame/replay");
            var valid=authorization.Validate(c,f.Input.Sequence,f.LifecycleGeneration);if(!valid.Accepted)return valid;
            // Reject BEFORE anchor/mantle, replay/readiness, Flight or physical root
            // mutation. Arbitration/recovery belongs to the host lane; this wrapper
            // cannot clear another owner's flag or fabricate a handoff receipt.
            if(f.Environment.ExternalMotionOwner)return RuleResult.Reject("exclusive motion owner unavailable");
            // A generation/readiness change cancels local anchors before any reacquisition.
            bool changed=generation!=f.LifecycleGeneration||ready!=f.Ready||xr!=f.Environment.IsXr;
            if(changed)climb.Reset();generation=f.LifecycleGeneration;ready=f.Ready;xr=f.Environment.IsXr;
            tick=c.Tick;sequence=f.Input.Sequence;
            var input=f.Input;var options=f.Climb;
            if(!ready||changed){options.Enabled=false;input.GripLeft=input.GripRight=false;}
            options.LeftReserved|=f.Reservations.Left;options.RightReserved|=f.Reservations.Right;
            // Reservations also release existing claims, rather than only prevent new grabs.
            if(options.LeftReserved)options.LeftTracked=false;if(options.RightReserved)options.RightTracked=false;
            bool had=climb.LeftAnchored||climb.RightAnchored||climb.Mantling;
            climbing=f.Environment.IsXr?climb.Step(input,f.Root,dt,options):
                climb.StepDesktop(input,f.Root,dt,options,ready&&!changed&&!options.LeftReserved&&f.DesktopE,f.DesktopForward);
            bool owns=climbing.OwnsMotion||had||!ready||changed;
            var env=f.Environment;env.ExternalMotionOwner|=owns;
            var observation=f.Flight;observation.Climbing=owns;observation.Mantling=climbing.Mantling;
            observation.InputEnabled=ready&&!changed;
            MotionStep walking;valid=motion.Step(c,input,env,observation,out walking);
            if(!valid.Accepted){climb.Reset();return valid;}
            Float3 delta=owns?climbing.RootDisplacement:walking.Displacement;
            if(owns&&!climbing.Climbing&&!climbing.Mantling&&!climbing.Launch&&had)
            {
                Float3 completion;
                if(!support.TryComplete(f.Root,input.HeadPosition,delta,climbing.Velocity,dt,out completion)||!completion.IsFinite||Length(completion)>.27f)
                {climb.Reset();return RuleResult.Reject("support completion unavailable");}
                delta=Add(delta,completion);
            }
            var plan=RootMotionSafety.Plan(f.Root,input.HeadPosition,delta,owns?0:walking.YawRadians,true);
            applied=root.Apply(plan,dt); // Exactly one root application. Root implementation revalidates geometry.
            if(!applied.Complete)climb.Reset();
            // Actual Flight feedback propagates the composed result into its actual Player momentum.
            // Release fling is velocity for the NEXT frame; no second translation/gravity this frame.
            var feedback=ReleaseFeedback.Resolve(owns,climbing,applied,dt);
            var feedbackContext=new CommandContext(c.SessionId,c.ActorId,c.CommandId+"/climb-collision",c.Tick,c.AuthorityEpoch,c.IsAuthority);
            return motion.ApplyCollisionResult(feedbackContext,motion.PendingMotion,feedback,new CollisionAuthorization(c));
        }
        sealed class CollisionAuthorization:IFlightCollisionAuthorization
        {
            readonly CommandContext step;public CollisionAuthorization(CommandContext step){this.step=step;}
            public RuleResult ValidateCollision(CommandContext c,FlightMotionReceipt receipt,FlightCollisionResult result)
            {return c.SessionId==step.SessionId&&c.ActorId==step.ActorId&&c.AuthorityEpoch==step.AuthorityEpoch&&c.Tick==step.Tick?RuleResult.Accept():RuleResult.Reject("step collision scope");}
        }
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        static float Length(Float3 p){return (float)Math.Sqrt((double)p.X*p.X+(double)p.Y*p.Y+(double)p.Z*p.Z);}
    }
}
