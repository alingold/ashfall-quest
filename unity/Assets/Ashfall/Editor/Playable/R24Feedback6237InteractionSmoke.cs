#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using System.IO;
using Ashfall.Core;using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.UI;using Ashfall.Integration.Playable.Input;using Ashfall.Integration.Playable.TeamLaunch;
using Ashfall.Modules.Player;
using UnityEditor;using UnityEngine;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static Vector3 V(Float3 value){return new Vector3(value.X,value.Y,value.Z);}
  static IEnumerator CompactPod(R23PlayableLoop loop,Action<string> note)
  {
   loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range deployment card");float end=Time.realtimeSinceStartup+45;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range map loaded");
   loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset("team_revive");Need(loop.UI.SetMatchRule("zone_enabled",false)&&loop.UI.RequestMatchStart(),"Actual team-revive start");end=Time.realtimeSinceStartup+40;while(!loop.OnLaunchDeck&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.OnLaunchDeck&&loop.TeamLaunch.Ready,"Actual source deck and pods");
   var layout=loop.TeamLaunch.Runtime.Layout;var frame=layout.Pod(loop.LocalMatchTeam,0);var docks=(IDictionary)Read(loop.TeamLaunch,"docks");var pod=docks[layout.PodId(loop.LocalMatchTeam,0)];var root=(Transform)Read(pod,"Root");var controls=(Transform)Read(pod,"Controls");
   foreach(var value in docks.Values)Need(Vector3.Distance(((Transform)Read(value,"Root")).lossyScale,Vector3.one*.8f)<.00001f,"Every actual dock cabin compact");
   var mesh=(Mesh)Read(loop.TeamLaunch,"cabin");var renderer=root.GetComponent<MeshRenderer>();Need(Vector3.Distance(renderer.bounds.size,mesh.bounds.size*.8f)<.0001f,"Actual rendered exterior is20percent smaller on all axes");
   Need(Vector3.Distance(controls.lossyScale,Vector3.one)<.00001f,"Console text and control world size retained");
   foreach(Transform child in controls)if(child.localScale==new Vector3(.4f,.16f,.12f))Need(Vector3.Distance(child.position,V(frame.Point(R24TeamLaunchRuntime.ControlLocalPoint)))<.0001f,"Raised physical button and logical target remain aligned");
   var label=(TextMesh)Read(pod,"ControlLabel");Need(Vector3.Distance(label.transform.position,V(frame.Point(new Float3(0,.83f,.875f))))<.0001f,"Sign text stays at its previous world point");
   Physics.SyncTransforms();Vector3 centreFloor=Vector3.zero;
   foreach(var local in new[]{new Vector3(0,0,0),new Vector3(.35f,0,.35f),new Vector3(-.35f,0,-.35f)})
   {
    var from=V(frame.Point(new Float3(local.x,.6f,local.z)));bool found=false;RaycastHit floor=new RaycastHit();float nearest=Single.PositiveInfinity;
    foreach(var hit in Physics.RaycastAll(from,Vector3.down,1,~0,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(root)&&hit.normal.y>.99f&&hit.distance<nearest){floor=hit;nearest=hit.distance;found=true;}
    Need(found,"Actual compact cabin floor under standing footprint "+local);var head=floor.point+Vector3.up*1.7088f;Need(loop.Rig.HeadCollision.Clear(head,Vector3.zero),"Actual calibrated goggle hull has clearance in compact cabin "+local);if(local==Vector3.zero)centreFloor=floor.point;
    note("Actual compact-floor/head fit "+local+": floor="+floor.point.ToString("F6")+" head="+head.ToString("F6"));
   }
   R24LaunchFrame ignored;int slot;Need(!layout.TryCabin(new R24LaunchPlayer{Id="outside",Team=loop.LocalMatchTeam,Alive=true,OnDeck=true,Position=frame.Point(new Float3(.9f,.12f,0))},out slot,out ignored),"Old wide boarding footprint no longer extends past compact cabin");
   var camera=new GameObject("Owned compact pod comparison camera").AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.03f;camera.farClipPlane=15;camera.fieldOfView=58;camera.cullingMask=1;camera.allowHDR=false;
   try{var eye=V(frame.Point(new Float3(3,1.3f,-3)));var target=V(frame.Point(new Float3(0,1.1f,0)));camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,Vector3.up));string dir=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";Directory.CreateDirectory(dir);string path=Path.Combine(dir,"pod-compact-current.png");Capture(camera,path);note("Actual compact pod PNG "+path+" eye="+eye.ToString("F6")+" target="+target.ToString("F6")+" FOV58; same camera as prior092758 Unity pod, not a Godot pair.");}finally{UnityEngine.Object.Destroy(camera.gameObject);}
   loop.Rig.SetSpawn(centreFloor+Vector3.up*.02f,V(frame.Point(new Float3(0,0,2))));loop.Calibration.NotifyRootPoseReset();Need(loop.Rig.HeadCollision.Clear(loop.Rig.Head.transform.position,Vector3.zero),"Actual standing rig head fits compact cabin");loop.Rig.Right.position=V(frame.Point(R24TeamLaunchRuntime.ControlLocalPoint))-V(frame.Forward)*.12f;loop.Rig.Right.rotation=Quaternion.LookRotation(V(frame.Forward));loop.Rig.Left.position=loop.Rig.Head.transform.position-Vector3.up*.25f;
   loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();Need(loop.TeamLaunch.Runtime.ReadRide("player")==null,"Neutral compact cabin does not launch");loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true,Trigger=.9f});loop.SmokeUiUpdate();Need(loop.TeamLaunch.Runtime.ReadRide("player")!=null,"Actual compact-cabin trigger launches through Root: "+loop.LaunchFailure);
   var flights=(IDictionary)Read(loop.TeamLaunch,"flights");Need(flights.Contains("player")&&Vector3.Distance(((Transform)Read(flights["player"],"Root")).lossyScale,Vector3.one*.8f)<.00001f,"Actual transit cabin also compact");
   foreach(var material in renderer.sharedMaterials)foreach(var message in ShaderUtil.GetShaderMessages(material.shader))Need(message.severity.ToString()!="Error",message.message);
   note("Actual dock/transit cabins20percent smaller; three real standing-floor/goggle checks and Root launch pass; raised control/sign retained. HMD fit and full flight replay not claimed.");
  }
  static IEnumerator PodReach(R23PlayableLoop loop,Action<string> note)
  {
   loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range deployment card");float load=Time.realtimeSinceStartup+45;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<load)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range deployment map loaded");
   loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset("team_revive");Need(loop.UI.SetMatchRule("zone_enabled",false),"Actual zone rule");Need(loop.UI.RequestMatchStart(),"Actual team-revive Start");
   float end=Time.realtimeSinceStartup+40;while((loop.ActualMatchRules==null||!loop.OnLaunchDeck)&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
   Need(loop.OnLaunchDeck&&loop.TeamLaunch.Ready&&loop.TeamLaunch.Eligible,"Actual source deployment deck");
   foreach(string name in new[]{"deck","cabin"})
   {
    var mesh=(Mesh)Read(loop.TeamLaunch,name);var vertices=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;
    for(int i=0;i<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];Need(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).normalized,normals[a])>.999f,"Actual native outward "+name+" triangle "+i/3);}
    note("Actual native "+name+" winding agrees with outward normals: "+triangles.Length/3+" triangles.");
   }
   Physics.SyncTransforms();var layout=loop.TeamLaunch.Runtime.Layout;
   for(int i=0;i<3;i++)
   {
    var at=V(layout.Station(loop.LocalMatchTeam).Point(new Float3(-15+i*15,1,-3)));bool found=false;
    foreach(var hit in Physics.RaycastAll(at,Vector3.down,3,~0,QueryTriggerInteraction.Ignore))if(loop.TeamLaunch.OwnsCollider(hit.collider)&&hit.normal.y>.99f)found=true;
    Need(found,"Actual authored deck floor after facing correction "+i);
   }
   var pod=layout.Pod(loop.LocalMatchTeam,0);var docks=(IDictionary)Read(loop.TeamLaunch,"docks");var native=docks[layout.PodId(loop.LocalMatchTeam,0)];var controls=(Transform)Read(native,"Controls");bool button=false,sign=false;
   foreach(Transform t in controls){if(t.localScale==new Vector3(.4f,.16f,.12f)){Need(Vector3.Distance(t.localPosition,V(R24TeamLaunchRuntime.ControlLocalPoint))<.00001f,"Actual button matches aim target");button=true;}if(t.localScale==new Vector3(1.5f,.56f,.075f)){Need(t.localPosition==new Vector3(0,.78f,.92f),"Sign retained");sign=true;}}
   Need(button&&sign,"Actual console art parts");var label=(TextMesh)Read(native,"ControlLabel");Need(label.transform.localPosition==new Vector3(0,.83f,.875f),"Sign label retained");
   var camera=new GameObject("Owned pod surface camera").AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.03f;camera.farClipPlane=15;camera.fieldOfView=58;camera.cullingMask=1;camera.allowHDR=false;
   try{var eye=V(pod.Point(new Float3(3,1.3f,-3)));var target=V(pod.Point(new Float3(0,1.1f,0)));camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,Vector3.up));string dir=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";Directory.CreateDirectory(dir);string path=Path.Combine(dir,"pod-current-unmatched.png");Capture(camera,path);note("UNMATCHED actual pod exterior capture "+path+" eye="+eye.ToString("F6")+" target="+target.ToString("F6"));}finally{UnityEngine.Object.Destroy(camera.gameObject);}
   loop.Rig.SetSpawn(V(pod.Origin)+Vector3.up*.12f,V(pod.Point(new Float3(0,0,2))));loop.Calibration.NotifyRootPoseReset();loop.Rig.Right.position=V(pod.Point(R24TeamLaunchRuntime.ControlLocalPoint))-V(pod.Forward)*.12f;loop.Rig.Right.rotation=Quaternion.LookRotation(V(pod.Forward));loop.Rig.Left.position=loop.Rig.Head.transform.position-Vector3.up*.25f;
   loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();Need(loop.TeamLaunch.Runtime.ReadRide("player")==null,"Neutral raised console does not launch");
   loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true,Trigger=.9f});loop.SmokeUiUpdate();
   Need(loop.TeamLaunch.Runtime.ReadRide("player")!=null,"Actual raised-button tracked trigger launches through Root: "+loop.LaunchFailure);
   foreach(var renderer in controls.GetComponentsInChildren<Renderer>(true))foreach(var message in ShaderUtil.GetShaderMessages(renderer.sharedMaterial.shader))Need(message.severity.ToString()!="Error",message.message);
   note("Standing-reach button and interaction target1.12m, sign unchanged; actual Root neutral/fresh-trigger launch accepted. Deck floors exercised; no HMD fit or flight replay claim.");
  }
  static IEnumerator MovingPointers(R23PlayableLoop loop,Action<string> note)
  {
   loop.UI.SetCreativeCatalog(true);loop.UI.OpenLoadout();loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();
   int requests=loop.CreativeRequestCount,capacity=loop.InventoryCapacity;string owned=String.Join(",",loop.Gameplay.OwnedWeaponActionIds);var original=loop.Rig.transform.position;
   var beams=(LineRenderer[])Read(loop.UI,"beams");
   for(int pose=0;pose<3;pose++)
   {
    // Supplied finite root pose tests render transport, not locomotion physics.
    loop.Rig.SetSpawn(original+new Vector3(.2f*(pose+1),0,-.15f*pose),original+new Vector3(2,0,3));
    loop.Rig.Right.position+=new Vector3(.03f,.01f,-.02f);loop.Rig.Right.rotation=Quaternion.Euler(12+pose*5,32+pose*13,-8);
    var position=loop.Rig.Right.position;var rotation=loop.Rig.Right.rotation;Call(loop,"RefreshMenuPointerPresentation");
    Need(beams[1].enabled&&Vector3.Distance(beams[1].GetPosition(0),loop.Rig.RightAimPosition)<.00001f,"Visible beam follows current moved aim origin");
    Need(Vector3.Distance(loop.Rig.RightAimPosition,position)<.00001f,"Once sampled zero-offset aim follows current raw grip");
    Need(loop.Rig.Right.position==position&&Quaternion.Angle(loop.Rig.Right.rotation,rotation)<.001f,"Render callback does not move controller");
    Need(loop.CreativeRequestCount==requests&&loop.InventoryCapacity==capacity&&String.Join(",",loop.Gameplay.OwnedWeaponActionIds)==owned&&loop.UI.Page==R23UiPage.Loadout,"Late rendering never dispatches items or changes page");
    note("Actual beam origin current for translated/rotated pose "+pose+"; no action dispatch/tracked-transform write.");yield return null;
   }
   loop.UI.SetPlaying();Call(loop,"RefreshMenuPointerPresentation");Need(!beams[0].enabled&&!beams[1].enabled,"Late callback retains gameplay beam hide");
  }
  static int Bags(R23PlayableLoop loop){int count=0;foreach(var b in ((IDictionary)Read(loop.Supplies,"boxes")).Values)if((string)Read(b,"Family")=="backpack")count++;return count;}
  static void CatalogBag(R23PlayableLoop loop,bool drop)
  {
   loop.UI.SetCreativeCatalog(true);loop.UI.OpenLoadout();for(int i=0;i<30;i++)loop.UI.ScrollBackpack(-1,.1f,true);Pointer(loop.UI,Button(loop.UI,null,"catalog/item/backpack"),!drop);Call(loop,"ApplyLoadoutRequests");
  }
  static IEnumerator BackpackCapacity(R23PlayableLoop loop,Action<string> note)
  {
   loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card");float end=Time.realtimeSinceStartup+45;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");loop.RestartRound();loop.Gameplay.HolsterCurrent();
   loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.Gameplay.UpdateInputAvailability(new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true});
   Need(loop.InventoryCapacity==3,"Initial real capacity3");CatalogBag(loop,false);Need(loop.InventoryCapacity==4,"Actual catalog backpack owner grants one extra slot: "+loop.LastCreativeResult);CatalogBag(loop,false);Need(loop.InventoryCapacity==5,"Second separate backpack grants one more slot");
   int before=Bags(loop);CatalogBag(loop,true);Need(Bags(loop)==before+1&&loop.InventoryCapacity==5,"Catalog trigger creates one actual world backpack without capacity grant");
   loop.UI.OpenLoadout();var button=Button(loop.UI,null,"backpack/drop-owned");var surface=(Transform)Read(button,"Surface");var origin=surface.TransformPoint(new Vector3(0,0,-.4f));var direction=surface.forward;
   loop.UI.UpdateLoadoutPointer(1,origin,direction,false,false,true);loop.UI.UpdateLoadoutPointer(1,origin,direction,false,true,true);Call(loop,"ApplyLoadoutRequests");
   Need(loop.InventoryCapacity==4,"Actual worn-backpack button withdraws one free capacity unit");int after=Bags(loop);
   for(int i=0;i<20;i++){loop.UI.UpdateLoadoutPointer(1,origin,direction,false,true,true);Call(loop,"ApplyLoadoutRequests");}
   Need(loop.InventoryCapacity==4&&Bags(loop)==after,"Held grip cannot cascade across additional backpacks");
   yield return FillAndRefuseBackpack(loop,note);
   note("Actual catalog2 bag grants3->5; catalog trigger places1 world bag; worn button5->4 and20held frames no cascade; full actual fourth weapon blocks capacity withdrawal. Source checkpoint bag-disappearance remains separate.");yield return null;
  }
  static IEnumerator BackpackFullRefusal(R23PlayableLoop loop,Action<string> note)
  {
   loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card");float end=Time.realtimeSinceStartup+45;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");loop.RestartRound();loop.Gameplay.HolsterCurrent();
   note("Before real neutral Root frame: paused="+Read(loop.Gameplay,"gameplayPaused")+" focus="+Read(loop.Gameplay,"hadFocus")+" owned="+String.Join(",",loop.Gameplay.OwnedWeaponActionIds)+" capacity="+loop.InventoryCapacity);
   loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();
   note("After real neutral Root frame: paused="+Read(loop.Gameplay,"gameplayPaused")+" focus="+Read(loop.Gameplay,"hadFocus")+" owned="+String.Join(",",loop.Gameplay.OwnedWeaponActionIds)+" capacity="+loop.InventoryCapacity);
   CatalogBag(loop,false);note("Actual bag setup: "+loop.LastCreativeResult+" capacity="+loop.InventoryCapacity);Need(loop.InventoryCapacity==4,"One finite actual catalog backpack prepares capacity4");
   yield return FillAndRefuseBackpack(loop,note);
  }
  static IEnumerator FillAndRefuseBackpack(R23PlayableLoop loop,Action<string> note)
  {
   // Map/menu entry pauses firearms until the actual Root receives its next
   // neutral frame. Do not force the consumer's private pause/focus fields.
   loop.UI.SetPlaying();loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();
   note("Before full-inventory setup: paused="+Read(loop.Gameplay,"gameplayPaused")+" focus="+Read(loop.Gameplay,"hadFocus")+" owned="+String.Join(",",loop.Gameplay.OwnedWeaponActionIds)+" capacity="+loop.InventoryCapacity);
   for(int i=0;i<6&&loop.Gameplay.OwnedWeaponActionIds.Length<loop.InventoryCapacity;i++)
   {
    int before=loop.Gameplay.OwnedWeaponActionIds.Length;loop.UI.SetCreativeCatalog(false);loop.UI.SetCreativeCatalog(true);loop.UI.OpenLoadout();Pointer(loop.UI,Button(loop.UI,null,"catalog/weapon/cx4"),true);Call(loop,"ApplyLoadoutRequests");
    note("Actual CX4 dispatch="+loop.LastCreativeResult+" paused="+Read(loop.Gameplay,"gameplayPaused")+" focus="+Read(loop.Gameplay,"hadFocus")+" owned="+String.Join(",",loop.Gameplay.OwnedWeaponActionIds)+" capacity="+loop.InventoryCapacity);
    Need(loop.Gameplay.OwnedWeaponActionIds.Length==before+1,"Actual catalog accepted one retained instance; inspect preceding dispatch/roster");
   }
   Need(loop.Gameplay.OwnedWeaponActionIds.Length==loop.InventoryCapacity,"Actual carried weapons fill current real capacity");loop.UI.OpenLoadout();Need(!(bool)Read(Button(loop.UI,null,"backpack/drop-owned"),"Enabled"),"Occupied last slot disables worn-backpack drop");
   int bags=Bags(loop),capacity=loop.InventoryCapacity;string ids=String.Join(",",loop.Gameplay.OwnedWeaponActionIds);Pointer(loop.UI,Button(loop.UI,null,"backpack/drop-owned"),true);Call(loop,"ApplyLoadoutRequests");
   // Exercise the Root guard too: a request queued before UI invalidation must
   // not discard an occupied slot even if it reaches the ordinary callback.
   Call(loop,"QueueBackpackDrop");Call(loop,"ApplyLoadoutRequests");
   Need(loop.InventoryCapacity==capacity&&Bags(loop)==bags&&String.Join(",",loop.Gameplay.OwnedWeaponActionIds)==ids,"Full-capacity UI/stale-request refusal preserves exact roster and creates no bag");
   note("Actual full-capacity UI and stale queued Root request refused; exact carried IDs/capacity/world-bag count retained.");yield return null;
  }
 }
}
#endif
