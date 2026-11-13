#if UNITY_6000_0_OR_NEWER
using System;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        readonly R24LanHomeBoundary lanHome=new R24LanHomeBoundary();
        // Only the actual LAN map owner invokes this guarded programmatic path.
        public void ReturnToLobbyForLanTransition(){lanHome.WorldHome(ReturnToLobby);}
        public bool CloseLanMenuBoundary(Action release){return lanHome.Close(ReturnToLobby,release);}
    }
}
#endif
