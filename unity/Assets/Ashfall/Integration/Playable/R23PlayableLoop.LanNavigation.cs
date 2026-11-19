#if UNITY_6000_0_OR_NEWER
using System;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        readonly R24LanHomeBoundary lanHome=new R24LanHomeBoundary();
        // Only the actual LAN map owner invokes this guarded programmatic path.
        public void ReturnToLobbyForLanTransition()
        {
            // Loading a shared world is not HOME. Keep the loading presentation up until
            // ApplySharedMap completes the existing acknowledgements and spawn barrier.
            var lobby=LanSession==null?null:LanSession.Lobby;var selected=lobby==null?null:lobby.SelectedMap;
            if(selected!=null&&!selected.Home&&!selected.Cancelled&&!selected.Results&&(!selected.Active||!lobby.OwnWorldLoaded))
            {if(Ready&&!switchingMap)StartCoroutine(ChangeRoomMap(selected.MapId));return;}
            lanHome.WorldHome(ReturnToLobby);
        }
        public bool CloseLanMenuBoundary(Action release){return lanHome.Close(ReturnToLobby,release);}
    }
}
#endif
