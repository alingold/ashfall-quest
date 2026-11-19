#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Appearance
{
    public sealed partial class R23PlayerAppearance
    {
        Ashfall.Modules.Flight.FlightMode trailMode;
        public void SetFlightTrailMode(Ashfall.Modules.Flight.FlightMode mode){trailMode=mode;}
        // Separate component runs after the existing avatar IK, for both self and replicas.
        void Awake(){gameObject.AddComponent<R26FlightTrails>().Initialize(this);}
        internal bool ReadFlightTrailPose(out Vector3 a,out Vector3 b,out Vector3 velocity,out bool emitting)
        {
            a=b=Vector3.zero;velocity=acceptedMotionVelocity;emitting=false;
            if(!isActiveAndEnabled||PreviewMode||!tracked||paused||current==null||!current.Root.activeInHierarchy||joints==null||!motionStateBound||!motionEnabled||VehicleSeated)return false;
            a=jointForearm[0].TransformPoint(joints.Forearm[0].From);b=jointForearm[1].TransformPoint(joints.Forearm[1].From);
            emitting=!motionSupported&&(trailMode==Ashfall.Modules.Flight.FlightMode.Glide||trailMode==Ashfall.Modules.Flight.FlightMode.Dive);return true;
        }
    }
}
#endif
