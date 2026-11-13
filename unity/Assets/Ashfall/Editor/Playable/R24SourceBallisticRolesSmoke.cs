#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using System.IO;
using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.HandsWeapons;using UnityEngine;using UnityEditor;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  [Serializable]sealed class BallisticRolePlan{public string candidateMapSha256="";public BallisticRoleRow[] roles=null;}
  [Serializable]sealed class BallisticRoleRow{public string kind="",boxName="";public int flags=0,chunk=0,vertexCount=0,indexCount=0;}
  static BallisticRolePlan ReadBallisticRolePlan(){return JsonUtility.FromJson<BallisticRolePlan>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../campaign-staging/20261104-three-chats/unity/source-ballistic-roles-v001/SOURCE-ROLE-BINDINGS.json"))));}
  static bool ProductionNearest(R23HandsWeapons h,Vector3 origin,Vector3 direction,float distance,bool ballistic,out RaycastHit hit){object[] args={origin,direction,distance,new RaycastHit(),ballistic};bool found=(bool)typeof(R23HandsWeapons).GetMethod("Nearest",Fields).Invoke(h,args);hit=(RaycastHit)args[3];return found;}
  static IEnumerator SourceBallisticRoles(R23PlayableLoop loop,Action<string> note)
  {
   note("SOURCE_BALLISTIC_ROLES actual Valley role binding and production ray entry");bool backfaces=Physics.queriesHitBackfaces;
   try{
    loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("valley"),"Actual source Valley");float end=Time.realtimeSinceStartup+90;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="valley","Actual Valley loaded");loop.StartPlaying();var plan=ReadBallisticRolePlan();Need(plan!=null&&plan.roles!=null&&plan.roles.Length==14&&loop.Map.ManifestSha256==plan.candidateMapSha256,"Exact source 14-role manifest binding");
    var surfaces=loop.Map.GetComponentsInChildren<R23ImpactMarkSurface>(true);Need(surfaces.Length==14,"Exactly14 metadata components, no broad neighbor assignment");int meshCount=0,boxes=0,rocks=0,witnesses=0,visualWitnesses=0,rayPairs=0;
    foreach(var row in plan.roles){R23ImpactMarkSurface found=null;foreach(var s in surfaces){bool same=row.kind=="BoxShape3D"?s.name==row.boxName:s.name=="R23 chunk "+row.chunk;if(!same)continue;Need(found==null,"Unique exact source role");found=s;}Need(found!=null,"Source role exists");var collider=found.GetComponent<Collider>();Need(collider!=null&&collider.enabled&&collider.gameObject.activeInHierarchy&&collider.gameObject.layer==0&&!collider.isTrigger,"Source collision remains active world0");Need(found.BallisticProxyIgnore==((row.flags&1)!=0)&&found.StairSupport==((row.flags&2)!=0)&&R23ImpactMarkSurface.ExcludesBallistic(collider),"Exact source group flags");
     var mesh=collider as MeshCollider;if(mesh!=null){meshCount++;Need(mesh.convex&&mesh.sharedMesh.vertexCount==row.vertexCount&&mesh.sharedMesh.triangles.Length==row.indexCount,"Source convex vertices/indices retained");}else{boxes++;Need(collider is BoxCollider,"Source landing remains box");}
     if(row.flags!=1)continue;rocks++;var bounds=collider.bounds;float radius=bounds.extents.magnitude+2;bool witness=false,visual=false;
     for(int height=0;height<3;height++)for(int step=0;step<16;step++){float angle=step*Mathf.PI/8;var center=bounds.center+Vector3.up*(height-1)*bounds.extents.y*.45f;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var origin=center+direction*radius;RaycastHit raw,accepted;bool rawHit=ProductionNearest(loop.Gameplay,origin,-direction,radius*2,false,out raw);bool actualHit=ProductionNearest(loop.Gameplay,origin,-direction,radius*2,true,out accepted);rayPairs++;if(!rawHit||raw.collider!=collider)continue;Need(!actualHit||!R23ImpactMarkSurface.ExcludesBallistic(accepted.collider),"Production ballistic ray skips tagged helpers");witness=true;if(actualHit&&accepted.collider.gameObject.layer==19)visual=true;note("BALLISTIC_RAY chunk="+row.chunk+" raw="+raw.collider.name+" rawDistance="+raw.distance+" corrected="+(actualHit?accepted.collider.name:"none")+" correctedLayer="+(actualHit?accepted.collider.gameObject.layer:-1)+" correctedDistance="+(actualHit?accepted.distance:0));}
     Need(witness,"Actual raw physical proxy is first-hit witness chunk="+row.chunk);witnesses++;if(visual)visualWitnesses++;
    }
    note("SOURCE_BALLISTIC_ROLES meshes="+meshCount+" boxes="+boxes+" rocks="+rocks+" rawPhysicalProxyWitnesses="+witnesses+" underlyingBallisticArtWitnesses="+visualWitnesses+" finiteRayPairs="+rayPairs);Need(meshCount==9&&boxes==5&&rocks==4&&witnesses==4&&visualWitnesses>0,"Actual source role and underlying visible-surface witnesses");Need(Physics.queriesHitBackfaces==backfaces,"Caller physics flags unchanged");
   }finally{loop.ReturnToLobby();Need(Physics.queriesHitBackfaces==backfaces,"Cleanup retains caller backface flag");}
  }
  static IEnumerator SourceBuildImpactRoles(R23PlayableLoop loop,Action<string> note)
  {
   note("SOURCE_BUILD_IMPACTS declared isolated layer8 committed-build collision-role control, no committed-build authority claim");GameObject control=null;Vector3 oldHead=loop.Rig.Head.transform.localPosition;Quaternion oldQ=loop.Rig.Head.transform.localRotation;var marks=(R23ImpactMarks)Read(loop.Gameplay,"impactMarks");Need(marks!=null,"Actual Root world-owned impact adapter");
   try{
    loop.ReturnToLobby();loop.StartPlaying();control=new GameObject("Declared source committed-build layer8 impact control");control.layer=8;control.transform.position=new Vector3(900,250,900);var box=control.AddComponent<BoxCollider>();box.size=new Vector3(4,4,.2f);Physics.SyncTransforms();RaycastHit hit;Need(box.Raycast(new Ray(control.transform.position+Vector3.back*2,Vector3.forward),out hit,4),"Actual native build-role collision hit");long accepted=marks.Accepted;Need(marks.AddHit(hit.point,hit.normal,box)&&marks.Accepted==accepted+1&&marks.LastProjectionRays==9,"Actual layer8 impact eligible with exact9 native same-collider projection rays");int slot=marks.LastSlot;var points=new Vector3[9];for(int i=0;i<9;i++){Need(marks.TryReadWorldVertex(slot,i,out points[i]),"Actual emitted world patch vertex");Need(Mathf.Abs(Vector3.Dot(points[i]-hit.point,hit.normal)-.003f)<.0001f,"Source3mm normal offset retained");}
    var shader=Resources.Load<Shader>("R23ImpactMarks");Need(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Actual impact shader oracle");
    for(int frame=0;frame<6;frame++){loop.Rig.Head.transform.localPosition=oldHead+new Vector3(frame*.09f,.02f*frame,0);loop.Rig.Head.transform.localRotation=Quaternion.Euler(0,frame*9,0)*oldQ;marks.Advance(.02f);for(int i=0;i<9;i++){Vector3 current;Need(marks.TryReadWorldVertex(slot,i,out current)&&current==points[i],"World mark remains exact under managed head translation/rotation");}}
    var ignored=control.AddComponent<R23ImpactMarkSurface>();ignored.StairSupport=true;long rejected=marks.Rejected;Need(!marks.AddHit(hit.point,hit.normal,box)&&marks.Rejected==rejected+1,"Source excluded support refuses impact despite layer eligibility");note("Actual world adapter layer8 accepted9-rays/source.003m/world anchored6headposes; support rejection and shader passed. No GPU pixel/stereo/scope-parallax/HMD cause claim.");
   }finally{if(marks!=null)marks.Clear();loop.Rig.Head.transform.localPosition=oldHead;loop.Rig.Head.transform.localRotation=oldQ;if(control!=null)UnityEngine.Object.Destroy(control);loop.ReturnToLobby();}
   yield break;
  }
 }
}
#endif
