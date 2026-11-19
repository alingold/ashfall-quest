#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using System.IO;using UnityEngine;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable {
 public sealed partial class R25NetBot {
  internal void ShieldsBegin(){StartCoroutine(Arg("-ashfallNetBotShieldsGrip")=="true"?ShieldsGripRun():ShieldsRun());}
  [Serializable] sealed class ShieldMesh {public string name,mesh;public Vector3 position,size;public bool shown;}
  [Serializable] sealed class ShieldObject {public string name,parent;public string instance;public Vector3 position;public bool active;public ShieldMesh[] meshes;}
  [Serializable] sealed class ShieldScene {public string phase,role,map;public long epoch;public Vector3 eye;public ShieldObject[] objects;public Vector3[] sites;}
  void ShieldsDump(string phase){if(probe!=null)probe.Audit(phase);var rows=new List<ShieldObject>();
   foreach(var t in FindObjectsByType<Transform>(FindObjectsInactive.Include)){
    if(t.parent==null)continue;string parent=t.parent.name;
    if(!(parent=="R23 source consumable world props"||parent=="R23 hands-weapons content"||parent.Contains("world copies")||parent=="R24 original finite world supplies"||parent=="R23 grenade items and projectiles"))continue;
    var meshes=new List<ShieldMesh>();foreach(var r in t.GetComponentsInChildren<Renderer>(true)){var f=r.GetComponent<MeshFilter>();meshes.Add(new ShieldMesh{name=r.name,mesh=f!=null&&f.sharedMesh!=null?f.sharedMesh.name:"",position=r.bounds.center,size=r.bounds.size,shown=r.enabled&&r.gameObject.activeInHierarchy&&!r.forceRenderingOff});}
    rows.Add(new ShieldObject{name=t.name,parent=parent,instance=t.GetEntityId().ToString(),position=t.position,active=t.gameObject.activeInHierarchy,meshes=meshes.ToArray()});
   }
   File.WriteAllText(Path.Combine(captureFolder,mode+"-"+phase+"-scene.json"),JsonUtility.ToJson(new ShieldScene{phase=phase,role=mode,map=root.Map.MapId,epoch=root.LanSession.Lobby.Epoch,eye=root.Rig.Head.transform.position,objects=rows.ToArray(),sites=root.WorldLoot.CacheSitePositions},true));Write("shields-census",phase+" objects="+rows.Count+" sites="+root.WorldLoot.ActivatedSites);
  }
  void ShieldsFar(){var sites=root.WorldLoot.CacheSitePositions;if(sites.Length==0)return;int selected=0;float best=-1;for(int i=0;i<sites.Length;i++){float d=Vector3.ProjectOnPlane(sites[i]-root.Map.WarmupSpawn,Vector3.up).sqrMagnitude;if(d>best){best=d;selected=i;}}
   var at=sites[selected];root.Rig.SetSpawn(at+new Vector3(0,.15f,-2),at);Write("shields-far","site="+selected+" at="+at.ToString("F3")+" distanceFromWarmup="+Mathf.Sqrt(best));
  }
  void ShieldsPhoto(int index){var cam=root.Rig.Head;Vector3 old=cam.transform.position;Quaternion rot=cam.transform.rotation;Vector3 best=old;float distance=Single.MaxValue;
   foreach(string id in root.Consumables.WorldItemIds){var info=root.Consumables.InspectItem(id);Vector3 p;Quaternion q;bool owned;if(info==null||info.Owner!=null||info.Kind!="soda"||!root.Consumables.TryReadWorldItem(id,out p,out q,out owned))continue;float d=(p-old).sqrMagnitude;if(d<distance){distance=d;best=p;}}
   cam.transform.position=best+new Vector3(.65f,.55f,-.65f);cam.transform.rotation=Quaternion.LookRotation(best+Vector3.up*.08f-cam.transform.position);Capture(index);cam.transform.SetPositionAndRotation(old,rot);
  }
  IEnumerator ShieldsRun(){while(root==null||log==null||root.LanSession.Lobby==null){if(closed)yield break;yield return null;}enabled=false;requestedMap=true;
   float begin=Time.realtimeSinceStartup,entered=-1,phaseAt=-1;int phase=0,visited=0;bool opened=false,far=false,dumped=false;string preset=Arg("-ashfallNetBotShieldsPreset");if(preset.Length==0)preset="free_for_all";
   while(!closed&&Time.realtimeSinceStartup-begin<220){if(probe!=null)probe.Frame();float now=Time.realtimeSinceStartup;var lobby=root.LanSession.Lobby;if(lobby==null)break;bool host=lobby.State==R24LanLobbyState.Hosting;
    if(host&&!opened&&lobby.MemberCount>=2&&now-begin>3)opened=root.LanSession.ShieldsOpenWarmup(Arg("-ashfallNetBotMap"));
    bool active=root.Ready&&!root.Lobby.Visible&&!root.LanSession.MapEntryBlocked&&lobby.SelectedMap!=null&&lobby.SelectedMap.Active&&!lobby.SelectedMap.Home;
    if(!active){yield return null;continue;}if(entered<0){entered=now;phaseAt=now;playBegan=now;if(probe!=null)probe.PlayBegan=now;Write("shields-entry",preset);}
    bool live=lobby.SelectedMap.Rules!=null;if(phase==0&&live){phase=1;phaseAt=now;far=dumped=false;visited=0;}else if(phase==1&&!live&&now-phaseAt>10){phase=2;phaseAt=now;far=dumped=false;visited=0;}
    float age=now-phaseAt;
    var sites=root.WorldLoot.CacheSitePositions;if(age>2&&visited<sites.Length){var at=sites[visited++];root.WorldLoot.ShieldsActivate(at);phaseAt=now-3;yield return null;continue;}
    if(!far&&age>4&&root.WorldLoot.CacheSitePositions.Length>0){if(phase!=1)ShieldsFar();far=true;}
    
    var head=root.Rig.transform.position+Vector3.up*1.7f;root.Rig.NetBotSample(Vector2.zero,1.7f,head+new Vector3(-.3f,-.4f,.3f),head+new Vector3(.3f,-.4f,.3f),root.Rig.transform.rotation,0,0,Mathf.Clamp(Time.unscaledDeltaTime,0,.1f));
    if(!dumped&&far&&age>15){string label=phase==0?"warmup":phase==1?"match":"return";ShieldsDump(label);ShieldsPhoto(700+phase);dumped=true;}
    if(host&&phase==0&&age>24){Write("shields-start",root.LanSession.RouteRoundRequest(R24PlayableMatchRules.ForPreset(preset,root.Map.MapId).With("zone_enabled",false).With("damage",0)).ToString());}
    if(host&&phase==1&&age>26){Write("shields-return",root.LanSession.HostReturnAllPlayersToWarmupSpawns().ToString());}
    if(phase==2&&age>(host?23:19))break;yield return null;
   }
   bool ok=phase==2&&dumped&&File.Exists(Path.Combine(captureFolder,mode+"-match-scene.json"));Write("shields-summary","passed="+ok);closed=true;Passed=ok;Finished=true;root.LanSession.Leave();if(probe!=null){probe.Dispose();probe=null;}log.Dispose();log=null;if(!Application.isEditor)Application.Quit(ok?0:2);
  }
 }
}
#endif
