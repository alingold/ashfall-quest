#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Diagnostics
{
    // Runs immediately before R24LanSession (order 1300); rig probe only.
    [DefaultExecutionOrder(1299)]
    public sealed class R26PerfLanOpen:MonoBehaviour
    {
        void Update(){R26PerfMarks.Begin(R26PerfMarks.Lan);}
    }
}
#endif
