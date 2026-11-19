#if UNITY_6000_0_OR_NEWER
using System.Collections;
using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        IEnumerator AwaitEntrySpawn(R24LanPeer owner,R24LanMapSelection selection)
        {
            var lobby=owner.Lobby;
            double until=Time.realtimeSinceStartupAsDouble+Ashfall.Modules.Sessions.Session.LoadingTimeoutSeconds;
            bool requested=lobby.SpawnRequestOutstanding;
            while(peer==owner){
                var map=lobby.SelectedMap;
                if(map==null||map.Epoch!=selection.Epoch||map.Cancelled||map.Results)yield break;
                if(lobby.SpawnWaitingForNextRound&&map.Rules!=null){
                    if(root.Ready)root.ReturnToLobbyForLanTransition();
                    root.UI.SetLocalMatchStatus("LAN ROUND IN PROGRESS | YOU JOIN NEXT ROUND");Publish();yield break;
                }
                if(lobby.SpawnFailure!=null){
                    MapFailure(lobby.SpawnFailure=="Spawn refused: no-safe-spawn"?"Host found no safe spawn position on this map":lobby.SpawnFailure);yield break;
                }
                R24LanSpawn placement;R24LanHealthState health;
                bool placed=lobby.TryReadSpawn(lobby.OwnActor,out placement);
                if(placed&&lobby.TryReadHealth(lobby.OwnActor,out health)){
                    if(!ApplyActualLanSpawn(placement))MapFailure("Could not apply the host's placement in the local world");
                    yield break;
                }
                bool worldReady=lobby.State!=R24LanLobbyState.Hosting||lobby.HostSpawnWorldReady;
                if(Time.realtimeSinceStartup>=until){
                    MapFailure(!worldReady?"Host spawn world did not become ready":placed?"Timed out waiting for host spawn health":requested?"Timed out waiting for host placement":"Local spawn request did not become ready");
                    yield break;
                }
                if(!placed&&!requested&&worldReady)requested=lobby.RequestSpawn()||lobby.SpawnRequestOutstanding;
                yield return null;
            }
        }
    }
}
#endif
