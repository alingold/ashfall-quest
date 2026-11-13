#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    public struct R23RigCarrierMotion
    {
        public Vector3 Target;
        public bool Flying;
    }
    public sealed partial class R23VRRig
    {
        // Source team_launch_r12 owns the real actor pose while flying; player.gd
        // pod_carried_r12 returns before gravity/climb/glide/support processing.
        // The rig remains the sole writer. Tracking-origin and raw local poses
        // survive; the cabin must not collide against its own carried occupant.
        public Func<R23RigCarrierMotion?> ReadCarrierMotion;
        public Action<Vector3> CarrierMotionAccepted;
        public bool CarrierActive {get;private set;}
        bool TryAdvanceCarrier()
        {
            R23RigCarrierMotion? candidate=ReadCarrierMotion==null?null:ReadCarrierMotion();
            if(!candidate.HasValue){CarrierActive=false;carrierWroteRoot=false;return false;}
            CancelTraversal();var motion=candidate.Value;
            if(!F(motion.Target).IsFinite)throw new InvalidOperationException("Finite source carrier target required");
            if(!CarrierActive){if(Climbing!=null)Climbing.ResetMotion();if(Activities!=null)Activities.ResetCarrierMotion();}
            CarrierActive=motion.Flying;
            Vector3 before=transform.position;
            body.enabled=false;
            try{transform.position=motion.Target;}
            finally{body.enabled=true;}
            motor.SetVelocity(new Ashfall.Core.Float3());grounded=false;ceiling=false;
            DistanceMoved+=Vector3.Distance(before,transform.position);
            previousSourceHead=Head.transform.position;haveSourceHead=true;
            carrierWroteRoot=true;if(CarrierMotionAccepted!=null)CarrierMotionAccepted(transform.position);
            return true;
        }
        public void ClearCarrierMotion()
        {
            CarrierActive=false;
            if(motor!=null)motor.SetVelocity(new Ashfall.Core.Float3());
            grounded=false;ceiling=false;haveSourceHead=false;carrierWroteRoot=false;RequestHeadRecovery(Ashfall.Integration.Playable.Exclusion.R25RecoveryReason.CarrierExit,false);
        }
    }
}
#endif
