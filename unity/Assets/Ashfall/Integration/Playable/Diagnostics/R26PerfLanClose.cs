#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Diagnostics
{
    // Runs immediately after R24LanSession (order 1300); rig probe only.
    [DefaultExecutionOrder(1301)]
    public sealed class R26PerfLanClose:MonoBehaviour
    {
        void Update(){R26PerfMarks.End(R26PerfMarks.Lan);}
    }
}
#endif
