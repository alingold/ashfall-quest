#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using Ashfall.Integration.Playable.World;using Ashfall.Integration.Playable.Match;using UnityEngine;using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        Coroutine mapWorker;long handledMapEpoch=-1;UnityWebRequest manifestRequest;bool preparingMap,awaitingSpawn;
        public bool LoadingSharedMap{get{return mapWorker!=null;}}
        public bool MapEntryBlocked{get{return peer!=null&&(awaitingSpawn||preparingMap||peer.Lobby.SelectedMap!=null&&!peer.Lobby.SelectedMap.Cancelled&&(!peer.Lobby.SelectedMap.Active||!OwnMapReady(peer.Lobby)||peer.Lobby.SelectedMap.Rules!=null&&(!root.LanRoundBound||peer.Lobby.RoundCountdown>0||!peer.Lobby.ClockSynchronized)));}}
        public bool RouteMapEntry(string id)
        {
            if(peer==null||peer.Lobby.State==R24LanLobbyState.Offline)return false;
            if(peer.Lobby.State!=R24LanLobbyState.Hosting){Error("The LAN host selects and opens the shared map");return true;}
            if(mapWorker!=null||peer.Lobby.MapStage==Ashfall.Modules.Sessions.SessionStage.Loading){Error("Wait for the shared map to finish loading");return true;}
            var choice=R23MapCatalog.Find(id);if(choice==null){Error("Selected map is not available in this build");return true;}
            preparingMap=true;mapWorker=StartCoroutine(PrepareHostMap(choice,peer));return true;
        }
        void OpenSelectedMap(){if(root!=null)RouteMapEntry(root.UI.SelectedMapId??root.Map.MapId);}
        IEnumerator PrepareHostMap(R23MapChoice choice,R24LanPeer owner,R24PlayableMatchRules rules=null)
        {
            var warmup=rules==null?(root.UI.SelectedMatchRules??R24PlayableMatchRules.ForPreset("team_revive",choice.Id)).WithMap(choice.Id):null;
            string url=Application.streamingAssetsPath.TrimEnd('/','\\')+"/PlayableR23/"+choice.PayloadFolder+"/map.json";if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;string hash=null,error=null;
            try{using(var request=UnityWebRequest.Get(url)){manifestRequest=request;request.timeout=30;yield return request.SendWebRequest();manifestRequest=null;if(request.result!=UnityWebRequest.Result.Success)error=request.error;else using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(request.downloadHandler.data)).Replace("-","").ToLowerInvariant();}
                if(peer!=owner)yield break;if(error!=null||hash==null){Error("Cannot read selected map: "+error);yield break;}bool accepted=rules==null?owner.Lobby.BeginMapSelection(choice.Id,hash,warmup):owner.Lobby.BeginRoundSelection(rules,hash,UnityEngine.Random.Range(1,Int32.MaxValue));if(!accepted)Error("Shared map or match selection was refused");
            }finally{manifestRequest=null;preparingMap=false;mapWorker=null;}
        }
        void SyncMaps()
        {
            if(peer==null||mapWorker!=null||peer.Lobby.SelectedMap==null)return;var selected=peer.Lobby.SelectedMap;if(selected.Epoch==handledMapEpoch)return;handledMapEpoch=selected.Epoch;mapWorker=StartCoroutine(ApplySharedMap(selected,peer));
        }
        bool OwnMapReady(R24LanLobby lobby){foreach(var member in lobby.Members)if(member.Actor==lobby.OwnActor)return member.WorldLoaded;return false;}
        IEnumerator ApplySharedMap(R24LanMapSelection selection,R24LanPeer owner)
        {
            try{
                yield return null;
                if(selection.Cancelled){if(root.Ready)root.ReturnToLobbyForLanTransition();yield break;}
                if(selection.Results){if(root.Ready)root.ReturnToLobbyForLanTransition();root.UI.SetLocalMatchStatus("LAN MATCH RESULTS | WAIT FOR HOST");yield break;}
                float until=Time.realtimeSinceStartup+60;while(peer==owner&&!root.Ready&&root.Failure==null&&Time.realtimeSinceStartup<until)yield return null;if(peer!=owner)yield break;
                if(!root.Ready||root.Failure!=null||R23MapCatalog.Find(selection.MapId)==null){MapFailure("Shared map is unavailable or the local world failed");yield break;}
                root.ReturnToLobbyForLanTransition();if(root.Map.MapId!=selection.MapId)root.SelectMap(selection.MapId,false);
                while(peer==owner&&!root.Ready&&root.Failure==null&&Time.realtimeSinceStartup<until)yield return null;if(peer!=owner)yield break;
                if(!root.Ready||!root.Map.IsLoaded||root.Map.MapId!=selection.MapId||root.Map.ManifestSha256!=selection.ManifestHash){MapFailure("Shared map bytes differ or failed to load");yield break;}
                if(selection.Rules!=null){string refusal=root.ValidateLanRound(selection.Rules);if(refusal!=null){MapFailure(refusal);yield break;}root.UI.ApplyLanMatchRules(selection.Rules);while(peer==owner&&!root.LanRoundAssetsReady(selection.Rules)&&root.LanRoundAssetFailure(selection.Rules)==null&&Time.realtimeSinceStartup<until)yield return null;if(peer!=owner)yield break;if(!root.LanRoundAssetsReady(selection.Rules)){MapFailure(root.LanRoundAssetFailure(selection.Rules)??"Actual shared round assets timed out");yield break;}}
                while(peer==owner&&(root.LaunchMapCatalog==null||!root.LaunchMapCatalog.Ready)&&(root.LaunchMapCatalog==null||root.LaunchMapCatalog.Failure==null)&&Time.realtimeSinceStartup<until)yield return null;
                if(peer!=owner)yield break;if(root.LaunchMapCatalog==null||!root.LaunchMapCatalog.Ready){MapFailure("Map spawn data could not load");yield break;}
                if(owner.Lobby.SelectedMap==null||owner.Lobby.SelectedMap.Epoch!=selection.Epoch||owner.Lobby.SelectedMap.Cancelled)yield break;
                if(!owner.Lobby.ReportMapLoaded(root.Map.ManifestSha256)){
                    // A late host acknowledgement can itself trigger the source60s
                    // recovery; the lobby already published that cancellation and
                    // SyncMaps applies it. Do not tear down every member's LAN.
                    var recovered=owner.Lobby.SelectedMap;if(recovered==null||recovered.Epoch!=selection.Epoch||recovered.Cancelled)yield break;
                    MapFailure("Actual shared map readiness was refused");yield break;
                }
                while(peer==owner&&owner.Lobby.SelectedMap!=null&&owner.Lobby.SelectedMap.Epoch==selection.Epoch&&!owner.Lobby.SelectedMap.Cancelled&&(!owner.Lobby.SelectedMap.Active||!OwnMapReady(owner.Lobby))&&Time.realtimeSinceStartup<until)yield return null;
                if(peer!=owner)yield break;var current=owner.Lobby.SelectedMap;
                if(current==null||current.Epoch!=selection.Epoch||current.Cancelled){if(root.Ready)root.ReturnToLobbyForLanTransition();yield break;}
                if(!current.Active||!OwnMapReady(owner.Lobby)){MapFailure("Shared map readiness timed out");yield break;}
                if(selection.Rules==null){if(!root.EnterLanWarmup(selection.MapId,selection.ManifestHash)||!root.BindLanWarmupHealth()){MapFailure("Actual shared map entry was refused");yield break;}}
                else{
                    while(peer==owner&&owner.Lobby.SelectedMap!=null&&owner.Lobby.SelectedMap.Epoch==selection.Epoch&&!owner.Lobby.SelectedMap.Results&&(owner.Lobby.RoundCountdown>0||!owner.Lobby.ClockSynchronized)&&Time.realtimeSinceStartup<until)yield return null;if(peer!=owner)yield break;current=owner.Lobby.SelectedMap;if(current==null||current.Epoch!=selection.Epoch||current.Results)yield break;
                    if(!owner.Lobby.ClockSynchronized||owner.Lobby.RoundCountdown>0){MapFailure("Host clock or round countdown timed out");yield break;}
                    if(!root.BeginLanRound(selection.Rules,selection.Seed,selection.Round,OwnTeam(owner.Lobby),delegate{return owner.Lobby.RoundElapsed;})){MapFailure("Actual shared round start was refused");yield break;}
                }
                awaitingSpawn=true;R24LanSpawn placement;R24LanHealthState health;
                while(peer==owner&&owner.Lobby.State==R24LanLobbyState.Hosting&&owner.Lobby.SelectedMap!=null&&owner.Lobby.SelectedMap.Epoch==selection.Epoch&&!owner.Lobby.SelectedMap.Cancelled&&!owner.Lobby.HostSpawnWorldReady&&Time.realtimeSinceStartup<until)yield return null;
                if(peer!=owner)yield break;current=owner.Lobby.SelectedMap;if(current==null||current.Epoch!=selection.Epoch||current.Cancelled)yield break;
                if(owner.Lobby.State==R24LanLobbyState.Hosting&&!owner.Lobby.HostSpawnWorldReady){MapFailure("Actual source spawn world did not become ready");yield break;}
                if(!owner.Lobby.RequestSpawn()){MapFailure("The host could not approve a safe spawn on this map");yield break;}
                while(peer==owner&&owner.Lobby.SelectedMap!=null&&owner.Lobby.SelectedMap.Epoch==selection.Epoch&&!owner.Lobby.SelectedMap.Cancelled&&owner.Lobby.SpawnFailure==null&&(!owner.Lobby.TryReadSpawn(owner.Lobby.OwnActor,out placement)||!owner.Lobby.TryReadHealth(owner.Lobby.OwnActor,out health))&&Time.realtimeSinceStartup<until)yield return null;
                if(peer!=owner)yield break;
                if(owner.Lobby.SpawnWaitingForNextRound&&owner.Lobby.SelectedMap!=null&&owner.Lobby.SelectedMap.Epoch==selection.Epoch&&selection.Rules!=null){
                    // Revive-mode late join: Session keeps this member waiting until
                    // the host's next round epoch. Stay connected at the menu; the
                    // next epoch's SyncMaps loads and spawns normally.
                    if(root.Ready)root.ReturnToLobbyForLanTransition();root.UI.SetLocalMatchStatus("LAN ROUND IN PROGRESS | YOU JOIN NEXT ROUND");Publish();yield break;
                }
                if(!owner.Lobby.TryReadSpawn(owner.Lobby.OwnActor,out placement)||!owner.Lobby.TryReadHealth(owner.Lobby.OwnActor,out health)||!ApplyActualLanSpawn(placement)){MapFailure("The host could not approve a safe spawn on this map");yield break;}
            }finally{awaitingSpawn=false;mapWorker=null;}
        }
        void MapFailure(string message){mapWorker=null;Close();Error(message);}
        void StopMapWork(){if(manifestRequest!=null){manifestRequest.Abort();manifestRequest=null;}if(mapWorker!=null){var job=mapWorker;mapWorker=null;StopCoroutine(job);}preparingMap=false;awaitingSpawn=false;handledMapEpoch=-1;}
    }
}
#endif
