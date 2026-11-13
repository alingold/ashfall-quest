#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void TraceLocomotion(string owner)
        {
#if UNITY_EDITOR
            if (LocomotionWrite != null) LocomotionWrite(owner, transform.position, TrackingOrigin.localPosition);
#endif
        }
    }
}
#endif
