#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.IO;using UnityEngine;
using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.World;using Ashfall.Integration.Playable.UI;
namespace Ashfall.Integration.Playable
{
    // Explicit Linux NetBot probe. Drives the real UI/session paths and observes each
    // rendered frame; never grants readiness, membership, health or spawn authority.
    [DefaultExecutionOrder(10000)]
    public sealed class R26RoomFlowEvidence:MonoBehaviour
    {
        R23PlayableLoop root;string folder,session,actor,lastMap;long lastEpoch=-1,capturedLoadingEpoch=-1;
        int changes,stableFrames;bool entered,finished,startupSeen,logoSeen;float stableSince,finishedAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(!R25NetBot.Requested||R25NetBot.Arg("-ashfallRoomFlowEvidence")!="true")return;
            var loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null)loop.gameObject.AddComponent<R26RoomFlowEvidence>();
        }
        void Start(){root=GetComponent<R23PlayableLoop>();folder=Path.GetDirectoryName(Path.GetFullPath(R25NetBot.Arg("-ashfallNetBotLog")));Directory.CreateDirectory(folder);}
        void Note(string text){Debug.Log("R26 ROOMFLOW EVIDENCE "+text);}
        void Check(bool pass,string text){if(!pass){Note("FAIL "+text);Application.Quit(3);enabled=false;}}
        void Capture(string name)
        {
            var camera=root.Rig.Head;var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(960,540,24);Texture2D pixels=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(960,540,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(folder,R25NetBot.Arg("-ashfallNetBot")+"-"+name+".png"),pixels.EncodeToPNG());Note("capture="+name);}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)Destroy(pixels);}
        }
        void LateUpdate()
        {
            if(root==null||root.UI==null||root.Lobby==null)return;
            if(!root.Ready&&root.UI.Page==R23UiPage.Loading&&!startupSeen)
            {
                var panel=root.UI.transform.Find("R23 eye-level menu");if(panel==null)return;
                var viewport=root.Rig.Head.WorldToViewportPoint(panel.position);
                Check(viewport.z>0&&Mathf.Abs(viewport.x-.5f)<.1f&&Mathf.Abs(viewport.y-.5f)<.1f,"startup logo in view "+viewport);
                Check(panel.parent!=root.Rig.Head.transform,"startup logo world anchored");
                startupSeen=true;Note("startup-logo first-frame viewport="+viewport+" anchor="+panel.position+" head="+root.Rig.Head.transform.position);Capture("startup-first");
            }
            if(startupSeen&&!logoSeen&&!root.Ready)
            {
                var panel=root.UI.transform.Find("R23 eye-level menu");var logo=panel==null?null:panel.Find("ASHFALL loading wordmark");
                if(logo!=null&&logo.GetComponent<Renderer>().enabled){logoSeen=true;Note("startup-logo texture visible while Ready=false");Capture("startup-logo");}
            }
            if(root.Failure!=null){Check(false,"root failure "+root.Failure);return;}
            if(root.LanSession==null||root.LanSession.Lobby==null)return;var lobby=root.LanSession.Lobby;var selected=lobby.SelectedMap;
            if(session==null&&lobby.Members.Length==2){session=lobby.SessionId;actor=lobby.OwnActor;Note("room-bound session="+session+" actor="+actor);}
            if(finished){if(Time.realtimeSinceStartup-finishedAt>(lobby.State==R24LanLobbyState.Hosting?12:9))Application.Quit(0);return;}
            if(entered){Check(lobby.SessionId==session&&lobby.OwnActor==actor&&lobby.Members.Length==2,"room membership retained");Check(!root.Lobby.Visible,"no intermediate menu map");stableFrames++;}
            if(selected!=null&&selected.Epoch!=lastEpoch){lastEpoch=selected.Epoch;Note("epoch="+lastEpoch+" map="+selected.MapId+" home="+selected.Home+" active="+selected.Active+" members="+lobby.Members.Length);}
            if(root.Map!=null&&root.Map.MapId!=lastMap){lastMap=root.Map.MapId;Note("world-map="+lastMap+" menuVisible="+root.Lobby.Visible);}
            bool ready=root.Ready&&selected!=null&&selected.Active&&!selected.Cancelled&&!root.LanSession.MapEntryBlocked&&!root.LanSession.LoadingSharedMap&&!root.Lobby.Visible&&lobby.Members.Length==2;
            if(!ready){stableSince=0;if(entered&&selected!=null&&!root.Ready&&capturedLoadingEpoch!=selected.Epoch){capturedLoadingEpoch=selected.Epoch;Check(root.UI.Page==R23UiPage.Loading,"direct transition keeps loading logo");Capture("transition-"+selected.Epoch);}return;}if(stableSince==0)stableSince=Time.realtimeSinceStartup;
            if(!entered){entered=true;Check(startupSeen&&logoSeen,"startup captures completed");Note("initial-match ready; begin no-menu guard");}
            if(lobby.State==R24LanLobbyState.Hosting&&Time.realtimeSinceStartup-stableSince<5)return;
            string expected=changes==0?R23MapCatalog.PaintballId:changes==1?R23MapCatalog.RangeId:R23MapCatalog.PaintballId;
            if(root.Map.MapId!=expected)return;
            Check(changes==1?selected.Rules==null&&!root.LanRoundBound:selected.Rules!=null&&root.LanRoundBound,"warmup versus match state preserved");
            foreach(var member in lobby.Members)Check(member.WorldLoaded,"member passed loading barrier "+member.Actor);
            Note("ready map="+root.Map.MapId+" mode="+(selected.Rules==null?"warmup":"match")+" ownLoaded="+lobby.OwnWorldLoaded+" members="+lobby.Members.Length);
            if(changes==2){finished=true;finishedAt=Time.realtimeSinceStartup;Note("PASS changes=2 menuFrames=0 guardedFrames="+stableFrames+" session="+session+" actor="+actor+" members="+lobby.Members.Length+" epoch="+lobby.Epoch+" warmupThenMatch=true");Capture("final-match");return;}
            if(lobby.State==R24LanLobbyState.Hosting)
            {
                if(changes==0){root.UI.OpenMaps();Check(root.UI.ChooseMap(R23MapCatalog.RangeId),"UI host map choice");Note("request-1 MapSelect range warmup");}
                else{root.LanSession.RouteRoundRequest(R24PlayableMatchRules.ForPreset("free_for_all",R23MapCatalog.PaintballId).With("zone_enabled",false).With("damage",0f));Note("request-2 START MATCH paintball");}
            }
            changes++;stableSince=0;
        }
    }
}
#endif
