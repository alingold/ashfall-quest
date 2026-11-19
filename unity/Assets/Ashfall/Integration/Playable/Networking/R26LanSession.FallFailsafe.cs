#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;

namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        sealed partial class RootSpawnWorld:IR26LanFallFrame
        {
            public bool IsBelowFallFailsafe(string map,Float3 worldFeet)
            {return Ready(map)&&worldFeet.IsFinite&&owner.root.Map.IsBelowFallFailsafe(worldFeet.Y);}
        }
    }
}
#endif
