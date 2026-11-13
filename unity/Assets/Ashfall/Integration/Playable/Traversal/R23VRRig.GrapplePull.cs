#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;
using Ashfall.Integration.Playable.Traversal;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        readonly R25GrapplePull grapplePull=new R25GrapplePull();
        long grapplePullRevision;
        public bool BeingGrappled {get{return grapplePull.Active;}}
        public void AdoptGrapplePull(long revision,Float3 target,float elapsed)
        {
            if(revision<=grapplePullRevision)return;grapplePullRevision=revision;
            if(CarrierActive||Riding||SourceGhostMotion||GameplayBlocked||!Tracking)return;
            CancelTraversal();if(Climbing!=null)Climbing.ResetMotion();if(Activities!=null)Activities.ResetCarrierMotion();
            grapplePull.Begin(target,elapsed);
            if(grapplePull.Active){TraversalHaptic(0,.35f);TraversalHaptic(1,.35f);}
        }
        public void ClearGrapplePull(bool reset=false)
        {
            if(grapplePull.Active){grapplePull.End();if(motor!=null)motor.SetVelocity(grapplePull.Velocity);}
            if(reset)grapplePullRevision=0;
        }
        bool EvaluateGrapplePull(InputFrame frame,float dt,out Vector3 delta,out Vector3 velocity)
        {
            delta=velocity=Vector3.zero;
            if(!BeingGrappled)return false;
            if(CarrierActive||Riding||SourceGhostMotion||GameplayBlocked||MenuLocomotion||!frame.Tracked||frame.Paused||HeadCollision==null||!HeadCollision.Ready||!UseSourceHeadHull){ClearGrapplePull();return false;}
            CancelTraversal();if(Climbing!=null)Climbing.ResetMotion();
            delta=U(grapplePull.Propose(F(Head.transform.position-Vector3.up*.45f),dt));
            velocity=delta/dt;return true;
        }
        Vector3 SweptPullMotion(Vector3 requested)
        {
            Vector3 feet=transform.position,eye=Head.transform.position;
            if(!HeadCollision.HeadClear(eye))return Vector3.zero;
            Vector3 bodySafe=HeadCollision.PullBodyMotion(feet,eye,requested);
            bool hit;Vector3 safe=VerifiedHeadMotion(eye,bodySafe,false,false,out hit);
            return HeadCollision.HeadClear(eye+safe)&&HeadCollision.PullBodyClear(feet+safe,eye+safe)?safe:Vector3.zero;
        }
        void AcceptGrapplePull(Vector3 wanted,Vector3 physical,float dt)
        {
            grapplePull.Accept(F(wanted),F(physical),dt);motor.SetVelocity(grapplePull.Velocity);grounded=false;
        }
    }
}
#endif
