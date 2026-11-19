#if UNITY_6000_0_OR_NEWER
using System;using System.IO;using System.Globalization;using Ashfall.Integration.Playable.Networking;using Ashfall.Modules.HealthStates;using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R25NetBot
    {
        internal void HoldTeamVisRound(){requestedMap=true;}
        internal void StartTeamVisRound(){root.LanSession.RouteRoundRequest(HitregRoundRules());}
    }
    // Explicit diagnostic flag only: real host Health mutations + joiner renderer
    // and map draw observations. Never active in ordinary headset/NetBot play.
    [DefaultExecutionOrder(1100)]
    public sealed class R26TeamVisRuntimeEvidence:MonoBehaviour
    {
        string log,folder,victim;float next,started=-1;int phase,captures,expectedPeers=4;float rosterReady=-1;bool launched;R23PlayableLoop root;R25NetBot bot;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
        {
            var args=Environment.GetCommandLineArgs();bool enabled=false;string log=null;int expected=4;
            for(int i=0;i+1<args.Length;i++){if(args[i]=="-ashfallTeamVisEvidence"&&args[i+1]=="true")enabled=true;if(args[i]=="-ashfallNetBotLog")log=args[i+1];if(args[i]=="-ashfallTeamVisPeers")Int32.TryParse(args[i+1],out expected);}
            if(!enabled||String.IsNullOrEmpty(log))return;var go=new GameObject("TEAMVIS opt-in runtime evidence");DontDestroyOnLoad(go);var p=go.AddComponent<R26TeamVisRuntimeEvidence>();p.expectedPeers=Math.Max(2,expected);p.log=log+".teamvis.log";p.folder=Path.GetDirectoryName(log);p.Write("BEGIN "+Path.GetFileName(log));
        }
        void Write(string s){File.AppendAllText(log,Time.realtimeSinceStartup.ToString("F2",CultureInfo.InvariantCulture)+" "+s+"\n");}
        void LateUpdate()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;if(root==null)root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();
            if(root==null||!root.Ready||root.LanSession==null)return;var lobby=root.LanSession.Lobby;if(lobby==null)return;
            if(bot==null)bot=UnityEngine.Object.FindAnyObjectByType<R25NetBot>();if(bot!=null)bot.HoldTeamVisRound();
            var members=lobby.Members;
            if(!launched&&lobby.State==R24LanLobbyState.Hosting&&members.Length>=expectedPeers){if(rosterReady<0)rosterReady=Time.unscaledTime;if(Time.unscaledTime-rosterReady>5&&bot!=null){launched=true;Write("HOST_START peers="+members.Length);bot.StartTeamVisRound();}}
            int ownTeam=-1;foreach(var m in members)if(m.Actor==lobby.OwnActor)ownTeam=m.Team;
            bool match=R26TeamVisibility.Match(lobby);int teams=match?lobby.SelectedMap.Rules.Teams:0;
            if(root.MatchActive&&lobby.RoundCountdown<=0&&started<0)started=Time.unscaledTime;
            if(lobby.State==R24LanLobbyState.Hosting&&started>=0&&teams>0&&members.Length>=4)
            {
                if(victim==null)foreach(var m in members)if(!m.Host&&m.Team!=ownTeam){victim=m.Actor;break;}
                R24LanHealthState h;if(victim!=null&&lobby.TryReadHealth(victim,out h))
                {
                    float age=Time.unscaledTime-started;
                    if(phase==0&&age>12){var r=lobby.ApplyHostHealthLoss(victim,h.Revision,Math.Min(27,h.Health),Math.Min(63,h.Shield),true);Write("HOST_DAMAGE actor="+victim+" accepted="+r.Accepted+" reason="+r.Reason);if(r.Accepted)phase++;}
                    else if(phase==1&&age>24){var r=lobby.ApplyHostHealthLoss(victim,h.Revision,h.Health,h.Shield,true);Write("HOST_DOWN actor="+victim+" accepted="+r.Accepted+" reason="+r.Reason);if(r.Accepted)phase++;}
                    else if(phase==2&&age>36){R24LanHealthState host;if(lobby.TryReadHealth(lobby.OwnActor,out host)){var r=lobby.ApplyHostHealthFall(lobby.OwnActor,host.Revision);Write("HOST_DEAD actor="+lobby.OwnActor+" accepted="+r.Accepted+" reason="+r.Reason);if(r.Accepted)phase++;}}
                    else if(phase==3&&age>50){Write("HOST_WARMUP");root.LanSession.RouteMapEntry(root.Map.MapId);phase++;}
                }
            }
            if(root.TacticalMap!=null&&!root.Lobby.Visible&&!root.LanSession.MapEntryBlocked){root.TacticalMap.Sample(true,true);root.TacticalMap.Refresh();}
            Write("VIEW actor="+lobby.OwnActor+" host="+(lobby.State==R24LanLobbyState.Hosting)+" match="+match+" teams="+teams+" ownTeam="+ownTeam+" mapOpen="+(root.TacticalMap!=null&&root.TacticalMap.Opened));
            foreach(var m in members)
            {
                R24LanHealthState h;lobby.TryReadHealth(m.Actor,out h);R24LanRemoteAvatar avatar;root.LanSession.TryReadRemoteAvatar(m.Actor,out avatar);
                bool inactive=false;bool marker=root.TacticalMap!=null&&root.TacticalMap.Opened&&root.TacticalMap.TryReadTeamMarker(m.Actor,out inactive);
                bool self=m.Actor==lobby.OwnActor,friend=R26TeamVisibility.Teammate(teams,ownTeam,m.Team);
                bool eligible=R26TeamVisibility.Show(match,teams,ownTeam,m.Team,self);
                Write("MEMBER actor="+m.Actor+" self="+self+" team="+m.Team+" friend="+friend+" life="+(h==null?"pending":h.Life.ToString())+" health="+(h==null?-1:h.Health)+" shield="+(h==null?-1:h.Shield)+" ready="+(avatar!=null&&avatar.Visible)+" name="+(avatar!=null&&avatar.TeamNameVisible)+" bars="+(avatar!=null&&avatar.TeamBarsVisible)+" barHealth="+(avatar==null?-1:avatar.TeamHealth)+" barShield="+(avatar==null?-1:avatar.TeamShield)+" map="+marker+" inactive="+inactive+" eligible="+eligible);
                if(!self&&avatar!=null&&avatar.Visible&&(avatar.TeamNameVisible!=eligible||match&&friend&&h!=null&&h.Connected&&!avatar.TeamBarsVisible||avatar.TeamBarsVisible&&(!match||!friend)||avatar.TeamBarsVisible&&h!=null&&(avatar.TeamHealth!=h.Health||avatar.TeamShield!=h.Shield)))Write("FAIL avatar "+m.Actor);
                if(marker&&!eligible||marker&&h!=null&&inactive!=R26TeamVisibility.Inactive(h.Life))Write("FAIL map "+m.Actor);
            }
            if(lobby.State==R24LanLobbyState.Connected&&started>=0&&captures<3&&Time.unscaledTime-started>10+captures*15)
            {
                Capture("map-"+captures,root.Rig.Head.transform.position,root.Rig.Head.transform.rotation);
                foreach(var m in members)if(m.Actor!=lobby.OwnActor&&m.Team==ownTeam){R24LanRemoteAvatar avatar;if(root.LanSession.TryReadRemoteAvatar(m.Actor,out avatar)&&avatar.Visible){var at=avatar.Head.position+Vector3.up*.25f;Capture("tag-"+captures,root.Rig.Head.transform.position,Quaternion.LookRotation(at-root.Rig.Head.transform.position));break;}}
                captures++;
            }
        }
        void Capture(string label,Vector3 position,Quaternion rotation)
        {
            var go=new GameObject("TEAMVIS evidence camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(root.Rig.Head);camera.enabled=false;if(label.StartsWith("tag",StringComparison.Ordinal)){camera.fieldOfView=25;camera.cullingMask&=~(1<<5);}camera.transform.SetPositionAndRotation(position,rotation);
            var target=RenderTexture.GetTemporary(1280,720,24);var active=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(folder,Path.GetFileNameWithoutExtension(log)+"-"+label+".png"),pixels.EncodeToPNG());}
            catch(Exception e){Write("CAPTURE_ERROR "+e.Message);}
            finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)Destroy(pixels);Destroy(go);}
        }
    }
}
#endif
