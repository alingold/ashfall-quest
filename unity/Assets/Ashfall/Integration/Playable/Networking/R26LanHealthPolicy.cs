#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    // MP-PERF: host Health stays authoritative in memory. On-disk Health images cost the host
    // main thread two fsyncs per Health operation and have no player value: a native restart
    // always creates a fresh room. R25NetBot -ashfallNetBotDurableHealth true re-enables them
    // for rig comparisons.
    static class R26LanHealthPolicy
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Apply(){R24LanLobby.DurableHealthImages=false;}
    }
}
#endif
