#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.IO;
using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.Input;using Ashfall.Modules.Player;
using UnityEngine;using UnityEditor;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static IEnumerator PaddlesWheel98254(R23PlayableLoop loop,Action<string> note)
  {
   note("PADDLES98254 actual Root wheel/electrode-art fixture entered");var settings=loop.UI.Settings;
   try{
    loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range");float end=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Range loaded");
    foreach(int hand in new[]{0,1}){
     loop.StowPaddles();loop.UI.SetSettings(settings.With(dominantHand:hand));Call(loop,"ApplySettings",loop.UI.Settings);loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();loop.SmokeUiUpdate();PaddleFrame98254(loop,.02f);
     Need(loop.Gameplay.PrimaryHand==hand,"Actual configured hand committed");string before=String.Join("|",loop.Gameplay.OwnedWeaponActionIds);int capacity=loop.InventoryCapacity;var wheel=loop.WeaponWheel;wheel.Cancel();var ray=new Ray(loop.Rig.Head.transform.position,loop.Rig.Head.transform.forward);
     wheel.Step(.02f,false,true,ray,loop.Rig.LocomotionAnchorPosition,hand);wheel.Step(.11f,true,true,ray,loop.Rig.LocomotionAnchorPosition,hand);Need(wheel.Active,"Actual held-A wheel opens");ray=new Ray(loop.Rig.Head.transform.position,(wheel.FixtureSectorPoint(3)-loop.Rig.Head.transform.position).normalized);
     wheel.Step(.02f,true,true,ray,loop.Rig.LocomotionAnchorPosition,hand);Need(wheel.HighlightedSector==3&&wheel.StatusText=="REVIVE PADDLES","Actual cross selection");Need(wheel.Step(.02f,false,true,ray,loop.Rig.LocomotionAnchorPosition,hand),"Actual release invokes accepted Root paddle equip");Need(loop.Paddles.State.Active&&!wheel.Active,"Reusable paddle tool active");
     SetPaddlePose98254(loop,0,new Vector3(-.2f,1.15f,.6f),Vector3.right);SetPaddlePose98254(loop,1,new Vector3(.2f,1.15f,.6f),Vector3.left);var l=loop.Rig.Left.localPosition;var r=loop.Rig.Right.localPosition;var lq=loop.Rig.Left.localRotation;var rq=loop.Rig.Right.localRotation;PaddleFrame98254(loop,.02f);
     Need(loop.Paddles.State.Visible&&loop.Paddles.State.Facing,"Logical electrodes still face one another");foreach(string field in new[]{"left","right"}){var pose=(Transform)Read(loop.Paddles,field);var art=pose.Find("Quarter-turn paddle artwork");Need(art!=null&&Quaternion.Angle(art.localRotation,Quaternion.AngleAxis(90,Vector3.forward))<.001f,"Requested 90degree artwork roll");Need(Vector3.Dot(art.forward,pose.forward)>.99999f,"Electrode normal unchanged by artwork roll");}
     Need(l==loop.Rig.Left.localPosition&&r==loop.Rig.Right.localPosition&&lq==loop.Rig.Left.localRotation&&rq==loop.Rig.Right.localRotation,"Raw tracked transforms retained");Need(before==String.Join("|",loop.Gameplay.OwnedWeaponActionIds)&&capacity==loop.InventoryCapacity,"Reusable utility adds no inventory item or slot");note("Actual wheel sector3 release hand="+hand+" accepted; logical facing/art-only roll/raw tracking/inventory retained.");
    }
   }finally{loop.StowPaddles();loop.UI.SetSettings(settings);Call(loop,"ApplySettings",settings);}
   yield break;
  }
  static void SetPaddlePose98254(R23PlayableLoop loop,int hand,Vector3 local,Vector3 localNormal)
  {var tracked=hand==0?loop.Rig.Left:loop.Rig.Right;var normal=loop.Rig.transform.TransformDirection(localNormal);tracked.SetPositionAndRotation(loop.Rig.transform.TransformPoint(local)-normal*.10f,Quaternion.LookRotation(normal,Vector3.up)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation));}
  static void PaddleFrame98254(R23PlayableLoop loop,float dt)
  {var left=new HandSample{Tracked=true};var right=new HandSample{Tracked=true};loop.Rig.SmokeTrackedHands(left,right);loop.StepItemInput(dt,left,right,true,false);Physics.SyncTransforms();}
  static IEnumerator PaddleMeter98254(R23PlayableLoop loop,Action<string> note)
  {
   note("PADDLES98254 actual continuous charge/render-sort fixture entered");loop.StowPaddles();loop.Gameplay.HolsterCurrent();loop.Rig.SetSpawn(loop.Map.GameSpawn,loop.Map.GameSpawn+Vector3.forward);loop.Calibration.NotifyRootPoseReset();
   SetPaddlePose98254(loop,0,new Vector3(-.2f,1.15f,.6f),Vector3.right);SetPaddlePose98254(loop,1,new Vector3(.2f,1.15f,.6f),Vector3.left);PaddleFrame98254(loop,.02f);Need(loop.EquipPaddles(),"Actual Root equip for independent charge group");var p=loop.Paddles;var meter=(Transform)Read(p,"meter");var fill=(Transform)Read(p,"fill");var backing=meter.Find("meter backing").GetComponent<MeshRenderer>();var fillRenderer=fill.GetComponent<MeshRenderer>();var text=(TextMesh)Read(p,"label");string output=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";Directory.CreateDirectory(output);
   try{
    Need(backing.sortingOrder==114&&fillRenderer.sortingOrder==115&&text.GetComponent<MeshRenderer>().sortingOrder==116,"Actual source explicit meter priorities");float previous=0,maxIncrement=0;int steps=0;bool captured=false;
    while(!p.State.Ready&&steps<210){PaddleFrame98254(loop,.05f);steps++;float change=p.State.Charge-previous;Need(change>=-.000001f&&change<=.0091f,"Actual charge has no discontinuity/accelerated jump");maxIncrement=Mathf.Max(maxIncrement,change);previous=p.State.Charge;
     Need(p.MeterVisible&&Mathf.Abs(fill.localScale.x/.286f-Mathf.Max(.0001f,p.State.Charge))<.0001f,"Rendered fill follows actual state on every step");
     if(!captured&&p.State.Charge>=.25f){captured=true;yield return null;int control,current;backing.sortingOrder=fillRenderer.sortingOrder=0;try{control=CapturePaddleMeter98254(loop,meter,Path.Combine(output,"charge25-old-sort-unmatched.png"));}finally{backing.sortingOrder=114;fillRenderer.sortingOrder=115;}current=CapturePaddleMeter98254(loop,meter,Path.Combine(output,"charge25-current-sort-unmatched.png"));note("Actual charge="+p.State.Charge+" old equal-sort cyanPixels="+control+" current source-order cyanPixels="+current);Need(current>50&&current>control+30,"Actual early-charge fill is visible; old ordering hides it");}
    }
    Need(captured&&p.State.Ready&&p.State.Completions==1&&Mathf.Abs(p.State.Remaining-3.5f)<.001f,"Full original charge creates one original ready window");note("Actual Root charge steps="+steps+" maxIncrement="+maxIncrement+" duration="+p.State.ChargeDuration+" readyWindow="+p.State.Remaining+". State rates/contact/ready window unchanged; only meter render order and paddle art roll changed. No HMD or network revive claim.");
    foreach(var renderer in meter.GetComponentsInChildren<MeshRenderer>())foreach(var msg in ShaderUtil.GetShaderMessages(renderer.sharedMaterial.shader))Need(msg.severity.ToString()!="Error",msg.message);
   }finally{backing.sortingOrder=114;fillRenderer.sortingOrder=115;loop.StowPaddles();}
  }
  static int CapturePaddleMeter98254(R23PlayableLoop loop,Transform meter,string path)
  {
   Need(!File.Exists(path),"Fresh meter capture");var camera=loop.Rig.Head;var target=RenderTexture.GetTemporary(1200,800,24);var old=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;
   try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());var a=camera.WorldToScreenPoint(meter.TransformPoint(new Vector3(-.145f,-.011f,-.003f)));var b=camera.WorldToScreenPoint(meter.TransformPoint(new Vector3(.145f,.011f,-.003f)));int count=0;for(int y=Mathf.Max(0,Mathf.FloorToInt(a.y));y<Mathf.Min(800,Mathf.CeilToInt(b.y));y++)for(int x=Mathf.Max(0,Mathf.FloorToInt(a.x));x<Mathf.Min(1200,Mathf.CeilToInt(b.x));x++){var c=(Color32)pixels.GetPixel(x,y);if(c.r<60&&c.g>100&&c.b>140)count++;}return count;}
   finally{camera.targetTexture=old;RenderTexture.active=active;if(pixels!=null)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(target);}
  }
 }
}
#endif
