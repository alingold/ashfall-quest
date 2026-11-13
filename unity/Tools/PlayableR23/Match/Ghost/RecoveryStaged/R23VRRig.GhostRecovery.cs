#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        // Source _relocate_r12 / spawn_revision: pose and motion only. No Health
        // spawn, inventory reset, map home change, heading or raw tracked writes.
        public bool RelocateSourceGhost(Vector3 actualSourcePosition)
        {
            if(!SourceGhostMotion||body==null||!F(actualSourcePosition).IsFinite)return false;
            bool enabled=body.enabled;body.enabled=false;
            try{transform.position=actualSourcePosition;}finally{body.enabled=enabled;}
            DisarmMotion();if(Climbing!=null)Climbing.ResetMotion();if(Activities!=null)Activities.ResetCarrierMotion();ClearCarrierMotion();
            haveSourceHead=false;artificialOriginOffset=Vector3.zero;return true;
        }
    }
}
#endif
