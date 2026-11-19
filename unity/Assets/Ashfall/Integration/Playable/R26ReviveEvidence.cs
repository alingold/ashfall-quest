#if UNITY_6000_0_OR_NEWER
using System;using System.IO;using UnityEngine;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable
{
    // Opt-in rig setup: only placements and damage are scripted. Rescue, station contact,
    // authority mutations and health/cooldown replication run through production owners.
    [DefaultExecutionOrder(-120)]
    public sealed class R26ReviveEvidence:MonoBehaviour
    {
        R23PlayableLoop root;int phase;double at;string ally;Vector3 station;StreamWriter log;float nextLog;bool clientDown,clientRevived,requested,opened;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(R25NetBot.Requested&&R25NetBot.Arg("-ashfallReviveEvidence")=="true"){var go=new GameObject("R26 revive evidence");if(R25NetBot.Arg("-ashfallRevivePair")=="true")go.AddComponent<R26RevivePairEvidence>();else go.AddComponent<R26ReviveEvidence>();}}
        void Note(string message){if(log==null){string path=R25NetBot.Arg("-ashfallNetBotLog")+".revive.log";log=new StreamWriter(path){AutoFlush=true};}log.WriteLine(Time.realtimeSinceStartup.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" "+message);}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        void Update()
        {
            if(root==null)root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(root==null||!root.Ready||root.LanSession==null||root.Rig==null)return;
            var lobby=root.LanSession.Lobby;
            if(lobby!=null&&lobby.State==R24LanLobbyState.Hosting&&lobby.Members.Length>=3&&!opened){opened=true;root.LanSession.RouteMapEntry(root.Map.MapId);}
            if(opened&&!requested&&lobby!=null&&lobby.WarmupPlayersPlaced&&!root.LanSession.LoadingSharedMap&&!root.LanSession.MapEntryBlocked){requested=true;root.LanSession.RouteRoundRequest(Ashfall.Integration.Playable.Match.R24PlayableMatchRules.ForPreset("team_revive",root.Map.MapId).With("zone_enabled",false).With("protection",0f).With("damage",0f));Note("REQUEST TEAM REVIVE");}
            if(lobby==null||lobby.SelectedMap==null||lobby.SelectedMap.Rules==null||lobby.RoundCountdown>0||root.LanSession.MapEntryBlocked)return;
            // Real desktop tracking continues even for a downed peer.
            var head=root.Rig.transform.position+Vector3.up*1.7f;root.Rig.NetBotSample(Vector2.zero,1.7f,head+new Vector3(-.3f,-.35f,.3f),head+new Vector3(.3f,-.35f,.3f),Quaternion.identity,0,0,Mathf.Min(.1f,Time.unscaledDeltaTime));
            R24LanHealthState own;if(!lobby.TryReadHealth(lobby.OwnActor,out own))return;
            if(lobby.State!=R24LanLobbyState.Hosting){if(own.Life==LifeState.Downed&&!clientDown){clientDown=true;Note("CLIENT DOWNED observed");}if(clientDown&&own.Life==LifeState.Alive&&!clientRevived){clientRevived=true;Note("PASS CLIENT REVIVED health="+own.Health+" shield="+own.Shield);}return;}
            double now=lobby.EstimatedHostTime;
            if(Time.realtimeSinceStartup>=nextLog){nextLog=Time.realtimeSinceStartup+2;string target;float progress;lobby.TryNearbyReviveProgress(out target,out progress);Note("phase="+phase+" members="+lobby.Members.Length+" hold="+target+" progress="+progress+" rejected="+lobby.LastRawTrackingRejection);}
            if(phase==0){
                if(root.HealthStations==null||!root.HealthStations.Ready||lobby.Members.Length<3||now<own.ProtectionUntil+2)return;
                foreach(var member in lobby.Members){R24LanHealthState h;if(member.Actor!=lobby.OwnActor&&lobby.TryReadHealth(member.Actor,out h)&&h.Team==own.Team){ally=member.Actor;break;}}
                if(ally==null)return;var rows=root.HealthStations.AuthoredStations;if(rows.Length==0)throw new Exception("Evidence map needs authored stations");station=new Vector3(rows[0].Center.X,rows[0].Center.Y,rows[0].Center.Z);
                // Select nearby clear standing floor from native geometry for this scripted rendezvous.
                Vector3 rendezvous=station+Vector3.right*4;RaycastHit floor;if(Physics.Raycast(rendezvous+Vector3.up*3,Vector3.down,out floor,8,1<<0))rendezvous.y=floor.point.y+.02f;
                lobby.ReviveEvidencePlace(lobby.OwnActor,F(rendezvous));lobby.ReviveEvidencePlace(ally,F(rendezvous+Vector3.right*.65f));phase=1;at=now;Note("SETUP station="+station+" rendezvous="+rendezvous);return;
            }
            R24LanHealthState friend;if(!lobby.TryReadHealth(ally,out friend))return;
            if(phase==1&&now-at>3){var loss=lobby.ApplyHostHealthLoss(ally,friend.Revision,friend.Health,friend.Shield,true);if(!loss.Accepted){Note("damage refused "+loss.Reason);return;}phase=2;at=now;Note("DOWNED ally="+ally);return;}
            if(phase==2&&friend.Life==LifeState.Alive){Note("PASS HOST REVIVE health="+friend.Health+" shield="+friend.Shield+" seconds="+(now-at));phase=3;at=now;Capture("revived");}
            if(phase==3&&now-at>1){own=null;lobby.TryReadHealth(lobby.OwnActor,out own);var loss=lobby.ApplyHostHealthLoss(lobby.OwnActor,own.Revision,40,30,true);if(!loss.Accepted)return;Note("PARTIAL host health="+(own.Health-40)+" shield="+(own.Shield-30));lobby.ReviveEvidencePlace(lobby.OwnActor,F(station));phase=4;at=now;return;}
            if(phase==4&&now-at>.1&&lobby.StationRestores>0&&own.Health==100&&own.Shield==100){Note("PASS NETWORK STATION health=100 shield=100 restores="+lobby.StationRestores+" cooldown="+lobby.StationCooldownRemaining(0));phase=5;at=now;Capture("station");}
            if(phase==5&&now-at>2){Note("PASS SCENARIO COMPLETE");phase=6;Capture("complete");CaptureRingOrder();}
        }
        void Capture(string label){string folder=Path.GetDirectoryName(R25NetBot.Arg("-ashfallNetBotLog"));var camera=root.Rig.Head;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;camera.transform.position=station+new Vector3(0,1.5f,-3);camera.transform.LookAt(station+Vector3.up*.86f);var old=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);Texture2D pixels=null;try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(960,540,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(folder,"revive-"+label+".png"),pixels.EncodeToPNG());}finally{camera.transform.position=oldPosition;camera.transform.rotation=oldRotation;camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Destroy(pixels);}}
        void CaptureRingOrder()
        {
            var go=new GameObject("R26 ring sorting evidence camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,50000,0);camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=1.2f;
            var board=GameObject.CreatePrimitive(PrimitiveType.Quad);board.layer=31;board.transform.position=camera.transform.position+Vector3.forward*3;board.transform.localScale=Vector3.one*3;
            var ring=GameObject.CreatePrimitive(PrimitiveType.Quad);ring.layer=31;ring.transform.position=camera.transform.position+Vector3.forward*2;ring.transform.localScale=Vector3.one*1.35f;
            var boardInk=new Material(Shader.Find("Ashfall/R24 World Score"));var texture=new Texture2D(1,1);texture.SetPixel(0,0,new Color(.05f,.1f,.5f,1));texture.Apply();boardInk.mainTexture=texture;board.GetComponent<Renderer>().sharedMaterial=boardInk;
            var ringInk=new Material(Resources.Load<Shader>("R23StationRing"));ring.GetComponent<Renderer>().sharedMaterial=ringInk;var rt=RenderTexture.GetTemporary(512,512,24);var active=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=rt;int before=0,after=0;for(int pass=0;pass<2;pass++){ringInk.renderQueue=pass==0?3000:3200;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(512,512,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,512,512),0,0);pixels.Apply();int green=0;foreach(var c in pixels.GetPixels32())if(c.g>150&&c.g>c.r*1.2f)green++;if(pass==0)before=green;else after=green;File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(R25NetBot.Arg("-ashfallNetBotLog")),pass==0?"ring-before.png":"ring-after.png"),pixels.EncodeToPNG());Destroy(pixels);pixels=null;}Note((after>100&&before==0?"PASS":"FAIL")+" RING ORDER legacyGreenPixels="+before+" correctedGreenPixels="+after);}
            finally{camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Destroy(pixels);Destroy(go);Destroy(board);Destroy(ring);Destroy(boardInk);Destroy(ringInk);Destroy(texture);}
        }
        void OnDestroy(){if(log!=null)log.Dispose();}
    }
}
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        internal void ReviveEvidencePlace(string actor,Float3 feet)
        {
            if(R25NetBot.Arg("-ashfallReviveEvidence")!="true"||State!=R24LanLobbyState.Hosting)return;
            var member=sessions.Get(actor);if(member==null)return;var result=sessions.RelocateRoot(LifecycleContext(),actor,member.SpawnRevision,feet);if(!result.Accepted)throw new InvalidOperationException("Evidence placement: "+result.Reason);PublishSpawn(actor,Epoch,sessions.Get(actor));SyncHostHealth();
        }
    }
}
#endif
