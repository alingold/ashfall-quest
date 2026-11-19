#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        readonly int[] latestLocalSlots=new int[8];
        readonly long[] latestLocalGenerations=new long[8];
        LineRenderer LatestLocalTrace(int pellet)
        {
            var s=streaks[latestLocalSlots[pellet]];
            return s.Visual==latestLocalGenerations[pellet]&&s.Line!=null&&s.Line.enabled?s.Line:null;
        }
    }
}
#endif
