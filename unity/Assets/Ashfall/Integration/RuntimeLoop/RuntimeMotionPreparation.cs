using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Climbing;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.ClimbingMotor;
namespace Ashfall.Integration.RuntimeLoop
{
    public enum RuntimeMotionMode { PlayerOnly, PlayerFlightClimb, Capsule }
    public enum RuntimeMotionOwner { PlayerFlight, Climb }
    // Local source-lifetime observation only. Root must bind real Session/driver
    // incarnations; this interface never certifies physics or gameplay admission.
    public interface IRuntimeMotionLifetime
    {
        object Identity {get;}
        event Action Invalidated;
    }
    // A quarantined mechanics draft. No root writer, no input reader, no commit
    // callback and no public owner tokens: it cannot enable uncertified gameplay.
    public sealed class RuntimeMotionDraft : IDisposable
    {
        internal readonly RuntimeMotionPreparation Owner;
        internal readonly PreparedClimbMotion Climb;
        internal readonly PreparedPlayerFlightStep Flight;
        internal bool Finished;
        public readonly RuntimeMotionOwner MotionOwner;
        public readonly MotionStep Motion;
        public readonly ClimbStep Climbing;
        internal RuntimeMotionDraft(RuntimeMotionPreparation owner,PreparedClimbMotion climb,
            PreparedPlayerFlightStep flight,RuntimeMotionOwner selected,MotionStep motion)
        {Owner=owner;Climb=climb;Flight=flight;MotionOwner=selected;Motion=motion;Climbing=climb.Step;}
        public RuleResult Validate(){return Owner.Validate(this);}
        public RuleResult Publish(){return RuleResult.Reject(RuntimeMotionPreparation.PublicationBlocker);}
        public void Dispose(){Owner.Abort(this);}
    }
    // Exclusive, synchronous host lane only, with stable query/authority ports.
    // Source support completion, release feedback and capsule lifecycle are still
    // required before a draft can become a complete physical publication plan.
    public sealed class RuntimeMotionPreparation : IDisposable
    {
        public const string PublicationBlocker="UNKNOWN certified physical root publication and prepared lifecycle adoption unavailable";
        readonly string session,actor;readonly long epoch,generation;
        readonly IAuthorityGate authority;readonly IRuntimeMotionLifetime lifetime;readonly object identity;
        readonly PlayerFlightMotor motion;readonly ClimbController climb;
        RuntimeMotionDraft pending;bool retired,disposed;
        public RuntimeMotionPreparation(string session,string actor,long epoch,long generation,
            IAuthorityGate authority,IRuntimeMotionLifetime lifetime,PlayerFlightMotor motion,ClimbController climb)
        {
            if(String.IsNullOrEmpty(session)||String.IsNullOrEmpty(actor)||epoch<0||generation<1||authority==null||
                lifetime==null||lifetime.Identity==null||motion==null||climb==null)throw new ArgumentException("motion composition binding");
            this.session=session;this.actor=actor;this.epoch=epoch;this.generation=generation;
            this.authority=authority;this.lifetime=lifetime;identity=lifetime.Identity;this.motion=motion;this.climb=climb;
            lifetime.Invalidated+=Invalidate;
        }
        bool Live {get{return !retired&&!disposed&&Object.ReferenceEquals(identity,lifetime.Identity);}}
        public RuleResult Prepare(MotorFrame frame,out RuntimeMotionDraft draft)
        {
            draft=null;var c=frame.Context;float dt=frame.Flight.DeltaSeconds;
            if(!Live){Invalidate();return RuleResult.Reject("motion source retired");}
            if(pending!=null||frame.LifecycleGeneration!=generation||!c.IsAuthority||c.SessionId!=session||c.ActorId!=actor||
                c.AuthorityEpoch!=epoch||String.IsNullOrEmpty(c.CommandId)||c.Tick<0||c.Tick!=frame.Input.Tick||
                !frame.Root.IsFinite||!frame.Input.HeadPosition.IsFinite||Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<=0||dt>.1f||
                frame.Environment.ExternalMotionOwner)return RuleResult.Reject("motion scope/owner/frame");
            var allowed=authority.Validate(c);if(!allowed.Accepted)return allowed;
            var input=frame.Input;var options=frame.Climb;
            if(!frame.Ready||input.Paused||!input.Tracked){options.Enabled=false;input.GripLeft=input.GripRight=false;}
            options.LeftReserved|=frame.Reservations.Left;options.RightReserved|=frame.Reservations.Right;
            if(options.LeftReserved)options.LeftTracked=false;if(options.RightReserved)options.RightTracked=false;
            bool had=climb.LeftAnchored||climb.RightAnchored||climb.Mantling;
            PreparedClimbMotion cp;var result=climb.PrepareStep(input,frame.Root,dt,options,!frame.Environment.IsXr,
                frame.Ready&&!input.Paused&&!options.LeftReserved&&frame.DesktopE,frame.DesktopForward,false,out cp);
            if(!result.Accepted)return result;
            PreparedPlayerFlightStep fp=null;
            try {
                bool owns=cp.Step.OwnsMotion||had||!frame.Ready||input.Paused||!input.Tracked;
                var env=frame.Environment;env.ExternalMotionOwner=owns;
                var observation=frame.Flight;observation.Climbing=owns;observation.Mantling=cp.Step.Mantling;
                observation.InputEnabled=frame.Ready&&!input.Paused&&input.Tracked;
                result=motion.PrepareStep(c,input,env,observation,out fp);if(!result.Accepted)return result;
                var step=fp.Motion;
                if(owns){step.Displacement=cp.Step.RootDisplacement;step.Velocity=cp.Step.Velocity;step.YawRadians=0;step.OwnsYaw=false;step.OwnsMotion=true;}
                if(!Live||!cp.Validate().Accepted||!fp.Validate().Accepted)return RuleResult.Reject("motion changed during preparation");
                draft=new RuntimeMotionDraft(this,cp,fp,owns?RuntimeMotionOwner.Climb:RuntimeMotionOwner.PlayerFlight,step);
                pending=draft;return RuleResult.Accept();
            }finally{if(draft==null){cp.Abort();if(fp!=null)fp.Abort();}}
        }
        internal RuleResult Validate(RuntimeMotionDraft draft)
        {
            if(!Live){Invalidate();return RuleResult.Reject("motion source retired");}
            if(draft==null||draft.Owner!=this||draft!=pending||draft.Finished)return RuleResult.Reject("motion draft retired");
            var r=draft.Climb.Validate();return r.Accepted?draft.Flight.Validate():r;
        }
        internal void Abort(RuntimeMotionDraft draft)
        {if(draft==null||draft.Owner!=this||draft.Finished)return;draft.Finished=true;draft.Climb.Abort();draft.Flight.Abort();if(pending==draft)pending=null;}
        void Invalidate(){retired=true;Abort(pending);}
        public void Dispose(){if(disposed)return;disposed=true;lifetime.Invalidated-=Invalidate;Invalidate();}
    }
}
