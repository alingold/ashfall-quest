#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Diagnostics
{
    // Last script of Update and LateUpdate; added only beside an R25NetBot perf probe.
    [DefaultExecutionOrder(32000)]
    public sealed class R26PerfFrameEnd:MonoBehaviour
    {
        void Update(){R26PerfMarks.End(R26PerfMarks.Updates);}
        void LateUpdate(){R26PerfMarks.End(R26PerfMarks.Frame);}
    }
}
#endif
