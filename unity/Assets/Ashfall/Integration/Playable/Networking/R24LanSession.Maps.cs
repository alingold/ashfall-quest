#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using Ashfall.Integration.Playable.World;using Ashfall.Integration.Playable.Match;using UnityEngine;using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        Coroutine mapWorker;long handledMapEpoch=-1;UnityWebRequest manifestRequest;bool preparingMap,awaitingSpawn;R24PlayableMatchRules preparingRules;
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
            preparingRules=rules;
            var warmup=rules==null?(root.UI.SelectedMatchRules??R24PlayableMatchRules.ForPreset("team_revive",choice.Id)).WithMap(choice.Id):null;
            string url=Application.streamingAssetsPath.TrimEnd('/','\\')+"/PlayableR23/"+choice.PayloadFolder+"/map.json";if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;string hash=null,error=null;
            try{using(var request=UnityWebRequest.Get(url)){manifestRequest=request;request.timeout=30;yield return request.SendWebRequest();manifestRequest=null;if(request.result!=UnityWebRequest.Result.Success)error=request.error;else using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(request.downloadHandler.data)).Replace("-","").ToLowerInvariant();}
                if(peer!=owner)yield break;if(error!=null||hash==null){Error("Cannot read selected map: "+error);yield break;}bool accepted=rules==null?owner.Lobby.BeginMapSelection(choice.Id,hash,warmup):owner.Lobby.BeginRoundSelection(rules,hash,UnityEngine.Random.Range(1,Int32.MaxValue));if(!accepted)Error("Shared map or match selection was refused");
            }finally{manifestRequest=null;preparingMap=false;preparingRules=null;mapWorker=null;}
        }
        void SyncMaps()
        {
            if(peer==null||mapWorker!=null||peer.Lobby.SelectedMap==null)return;var selected=peer.Lobby.SelectedMap;if(selected.Epoch==handledMapEpoch)return;handledMapEpoch=selected.Epoch;mapWorker=StartCoroutine(ApplySharedMap(selected,peer));
        }
        bool OwnMapReady(R24LanLobby lobby){return lobby.OwnWorldLoaded;}
        bool CurrentWorld(R24LanPeer owner,long epoch)
        {if(peer!=owner)return false;var map=owner.Lobby.SelectedMap;return map!=null&&map.Epoch==epoch&&!map.Cancelled;}
        IEnumerator ApplySharedMap(R24LanMapSelection selection,R24LanPeer owner)
        {
            try{
                yield return null;
                if(peer!=owner||owner.Lobby.SelectedMap==null||owner.Lobby.SelectedMap.Epoch!=selection.Epoch)yield break;
                if(selection.Home){
                    // Host HOME: every member returns to the room lobby (menu city) and stays in the room.
                    float home=Time.realtimeSinceStartup+60;while(peer==owner&&!root.Ready&&root.Failure==null&&Time.realtimeSinceStartup<home)yield return null;
                    if(peer==owner&&root.Ready&&owner.Lobby.SelectedMap!=null&&owner.Lobby.SelectedMap.Epoch==selection.Epoch)root.ReturnToLobbyForLanTransition();yield break;
                }
                if(selection.Cancelled){if(root.Ready)root.ReturnToLobbyForLanTransition();yield break;}
                if(selection.Results){if(root.Ready)root.ReturnToLobbyForLanTransition();root.UI.SetLocalMatchStatus("LAN MATCH RESULTS | WAIT FOR HOST");yield break;}
                float startedAt=Time.realtimeSinceStartup,until=startedAt+R24LanLobby.LocalMapLoadSeconds;
                while(CurrentWorld(owner,selection.Epoch)&&!root.Ready&&root.Failure==null&&Time.realtimeSinceStartup<until)yield return null;
                if(!CurrentWorld(owner,selection.Epoch))yield break;
                var choice=R23MapCatalog.Find(selection.MapId);
                if(!root.Ready||root.Failure!=null||choice==null){MapFailure("Shared map is unavailable or the local world failed");yield break;}
                root.ReturnToLobbyForLanTransition();if(root.Map.MapId!=choice.Id)root.SelectMap(choice.Id,false);
                while(CurrentWorld(owner,selection.Epoch)&&!root.Ready&&root.Failure==null&&Time.realtimeSinceStartup<until)yield return null;
                if(!CurrentWorld(owner,selection.Epoch))yield break;
                if(!root.Ready||!root.Map.IsLoaded||root.Map.MapId!=choice.Id){
                    string reason=root.Map.LoadError??root.Failure??(Time.realtimeSinceStartup>=until?"local load timed out":"local world did not load");
                    MapFailure(choice.Label+": "+reason);yield break;
                }
                if(root.Map.ManifestSha256!=selection.ManifestHash){
                    Debug.LogWarning("Shared map "+choice.Id+" local SHA-256="+root.Map.ManifestSha256+" host SHA-256="+selection.ManifestHash);
                    MapFailure(choice.Label+": map version differs from host");yield break;
                }
                if(selection.Rules!=null){string refusal=root.ValidateLanRound(selection.Rules);if(refusal!=null){MapFailure(refusal);yield break;}root.UI.ApplyLanMatchRules(selection.Rules);while(CurrentWorld(owner,selection.Epoch)&&!root.LanRoundAssetsReady(selection.Rules)&&root.LanRoundAssetFailure(selection.Rules)==null&&Time.realtimeSinceStartup<until)yield return null;if(!CurrentWorld(owner,selection.Epoch))yield break;if(!root.LanRoundAssetsReady(selection.Rules)){MapFailure(root.LanRoundAssetFailure(selection.Rules)??"Actual shared round assets timed out");yield break;}}
                while(CurrentWorld(owner,selection.Epoch)&&(root.LaunchMapCatalog==null||!root.LaunchMapCatalog.Ready)&&(root.LaunchMapCatalog==null||root.LaunchMapCatalog.Failure==null)&&Time.realtimeSinceStartup<until)yield return null;
                if(!CurrentWorld(owner,selection.Epoch))yield break;
                if(root.LaunchMapCatalog==null||!root.LaunchMapCatalog.Ready){MapFailure("Map spawn data could not load");yield break;}
                if(!owner.Lobby.ReportMapLoaded(root.Map.ManifestSha256)){
                    // A late host acknowledgement can itself trigger the source60s
                    // recovery; the lobby already published that cancellation and
                    // SyncMaps applies it. Do not tear down every member's LAN.
                    var recovered=owner.Lobby.SelectedMap;if(recovered==null||recovered.Epoch!=selection.Epoch||recovered.Cancelled)yield break;
                    MapFailure("Actual shared map readiness was refused");yield break;
                }
                until=startedAt+R24LanLobby.SharedMapReadySeconds;
                while(CurrentWorld(owner,selection.Epoch)&&(!owner.Lobby.SelectedMap.Active||!OwnMapReady(owner.Lobby))&&Time.realtimeSinceStartup<until)yield return null;
                if(peer!=owner)yield break;var current=owner.Lobby.SelectedMap;
                if(current==null||current.Epoch!=selection.Epoch||current.Cancelled||current.Results)yield break;
                if(!current.Active||!OwnMapReady(owner.Lobby)){MapFailure("Shared map readiness timed out");yield break;}
                if(selection.Rules==null){if(!root.EnterLanWarmup(selection.MapId,selection.ManifestHash)||!root.BindLanWarmupHealth()){MapFailure("Actual shared map entry was refused");yield break;}}
                else{
                    until=Time.realtimeSinceStartup+60;
                    while(CurrentWorld(owner,selection.Epoch)&&!owner.Lobby.SelectedMap.Results&&(owner.Lobby.RoundCountdown>0||!owner.Lobby.ClockSynchronized)&&Time.realtimeSinceStartup<until)yield return null;if(peer!=owner)yield break;current=owner.Lobby.SelectedMap;if(current==null||current.Epoch!=selection.Epoch||current.Cancelled||current.Results)yield break;
                    if(!owner.Lobby.ClockSynchronized||owner.Lobby.RoundCountdown>0){MapFailure("Host clock or round countdown timed out");yield break;}
                    if(!root.BeginLanRound(selection.Rules,selection.Seed,selection.Round,OwnTeam(owner.Lobby),delegate{return owner.Lobby.RoundElapsed;})){MapFailure("Actual shared round start was refused");yield break;}
                }
                awaitingSpawn=true;yield return AwaitEntrySpawn(owner,selection);
            }finally{awaitingSpawn=false;mapWorker=null;}
        }
        // A member whose world fails leaves alone, so it cannot hold the room behind the barrier.
        // A host keeps its room: the failed world is abandoned for everyone and the room returns to its lobby.
        void MapFailure(string message)
        {
            mapWorker=null;Debug.LogWarning("Shared map failure ("+(Hosting?"host":"guest")+"): "+message);
            if(Hosting){bool returned=peer.Lobby.BeginRoomLobby();Error(returned?"Room returned to its lobby: "+message:message);return;}
            Close();Error(message);
        }
        void StopMapWork(){if(manifestRequest!=null){manifestRequest.Abort();manifestRequest=null;}if(mapWorker!=null){var job=mapWorker;mapWorker=null;StopCoroutine(job);}preparingMap=false;preparingRules=null;awaitingSpawn=false;handledMapEpoch=-1;}
    }
}
#endif
