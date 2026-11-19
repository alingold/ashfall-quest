#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.World;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        public bool RouteRoundRequest(R24PlayableMatchRules rules)
        {
            if(peer==null)return false;
            if(peer.Lobby.State!=R24LanLobbyState.Hosting){Error("The LAN host starts the shared match");return true;}
            if(rules==null||root==null||!root.Ready||mapWorker!=null||MapEntryBlocked){Error("Wait for the shared map before starting a match");return true;}
            string failure=root.ValidateLanRound(rules);if(failure!=null){Error(failure);return true;}
            var choice=R23MapCatalog.Find(rules.MapId);if(choice==null){Error("Match map is unavailable in this build");return true;}
            var selected=peer.Lobby.SelectedMap;
            if(selected==null||selected.Cancelled||selected.MapId!=choice.Id)return RouteMapEntry(choice.Id);
            if(selected.Rules==null&&!peer.Lobby.WarmupPlayersPlaced){Error("Wait for everyone to enter warm-up");return true;}
            preparingMap=true;mapWorker=StartCoroutine(PrepareHostMap(choice,peer,rules));return true;
        }
        void SyncRoundCompletion(){if(peer!=null&&peer.Lobby.SelectedMap!=null&&peer.Lobby.SelectedMap.Results&&root!=null)root.FinishLanRound();}
        int OwnTeam(R24LanLobby lobby){foreach(var member in lobby.Members)if(member.Actor==lobby.OwnActor)return member.Team;return -1;}
    }
}
#endif
