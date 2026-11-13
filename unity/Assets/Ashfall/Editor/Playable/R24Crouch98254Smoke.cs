#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;
using Ashfall.Core;using Ashfall.Modules.Player;using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.Climbing;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static void CrouchRootFrame(R23PlayableLoop loop,int physical,bool held)
  {
   var l=new HandSample{Tracked=true,Grip=physical==0&&held?1:0,Position=BuildF(loop.Rig.transform.InverseTransformPoint(loop.Rig.Left.position))};
   var r=new HandSample{Tracked=true,Grip=physical==1&&held?1:0,Position=BuildF(loop.Rig.transform.InverseTransformPoint(loop.Rig.Right.position))};
   loop.Rig.SmokeTrackedHands(l,r);Call(loop.Rig,"Advance",R23InputProjection.Sample(true,true,BuildF(loop.Rig.transform.InverseTransformPoint(loop.Rig.Head.transform.position)),l,r,false,loop.Gameplay.PrimaryHand),.02f);
  }
  static GameObject CrouchBox(Transform parent,string name,Vector3 center,Vector3 size)
  {
   var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name=name;item.transform.SetParent(parent,false);item.transform.position=center;item.transform.localScale=size;item.GetComponent<MeshRenderer>().enabled=false;return item;
  }
  static IEnumerator ContinuousCrouch98254(R23PlayableLoop loop,Action<string> note)
  {
   note("CROUCH98254 actual Root anchored pull fixture entered");GameObject controls=null;var settings=loop.UI.Settings;float originalFling=loop.Climbing.FlingPower;
   try
   {
    loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range");float end=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Range loaded");
    controls=new GameObject("Declared incremental grab-crouch controls");controls.transform.SetParent(loop.Map.transform,false);CrouchBox(controls.transform,"Declared support floor",new Vector3(110,-.1f,-600),new Vector3(20,.2f,20));
    CrouchBox(controls.transform,"Declared side grab wall",new Vector3(110.65f,1.5f,-600),new Vector3(.2f,3,8));Physics.SyncTransforms();
    foreach(int physical in new[]{0,1})
    {
     loop.RestartRound();loop.Gameplay.HolsterCurrent();loop.Rig.SetSpawn(new Vector3(110,.04f,-600),new Vector3(110,.04f,-590));loop.Calibration.NotifyRootPoseReset();
     loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.35f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.35f);
     var hand=physical==0?loop.Rig.Left:loop.Rig.Right;hand.position=new Vector3(110.5f,1.2f,-599.7f);loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();
     for(int frame=0;frame<12;frame++)CrouchRootFrame(loop,physical,false);Need(loop.Rig.Supported,"Actual supported standing start");
     CrouchRootFrame(loop,physical,true);note("Anchor physical="+physical+" left="+loop.Climbing.LeftAnchored+" right="+loop.Climbing.RightAnchored+" feet="+loop.Rig.transform.position.ToString("F6")+" head="+loop.Rig.Head.transform.position.ToString("F6")+" status="+loop.Climbing.Status);
     Need(physical==0?loop.Climbing.LeftAnchored:loop.Climbing.RightAnchored,"Real wall grip anchors selected free hand");
     float firstHead=loop.Rig.Head.transform.position.y,firstFeet=loop.Rig.transform.position.y,lastHead=firstHead;var origin=loop.Rig.TrackingOrigin.localPosition;var rawHead=loop.Rig.Head.transform.localPosition;
     for(int step=0;step<12;step++)
     {
      hand.localPosition+=Vector3.up*.005f;float before=loop.Rig.Head.transform.position.y;CrouchRootFrame(loop,physical,true);float delta=loop.Rig.Head.transform.position.y-before;
      note("Physical="+physical+" incrementalLift="+((step+1)*.005f)+" actualHeadDelta="+delta+" totalHeadDrop="+(firstHead-loop.Rig.Head.transform.position.y)+" feet="+loop.Rig.transform.position.y);
      Need(delta<-.004f&&delta>-.006f,"Every5mm hand lift lowers actual head smoothly from first increment");Need(loop.Rig.Head.transform.position.y<lastHead,"No crouch deadzone/toggle");lastHead=loop.Rig.Head.transform.position.y;
      Need(loop.Rig.Head.transform.localPosition==rawHead&&loop.Rig.TrackingOrigin.localPosition==origin,"Crouch moves sole root without rewriting tracked head/origin");Need(loop.Rig.HeadCollision.Clear(loop.Rig.Head.transform.position,Vector3.zero),"Actual head remains clear during lowering");
     }
     Need(firstHead-lastHead>.055f&&firstFeet-loop.Rig.transform.position.y>.055f,"Actual continuous6cm crouch while logical feet may cross floor");
     note("Physical="+physical+" accepted12x5mm head lowering; source head-only collision retained. Release/fling/mantle policy is unchanged and not newly qualified here.");
    }
   }
   finally{if(controls!=null){controls.SetActive(false);UnityEngine.Object.Destroy(controls);}loop.UI.SetSettings(settings);Call(loop,"ApplySettings",settings);loop.Climbing.FlingPower=originalFling;loop.RestartRound();}
  }
  static IEnumerator ThinMeshObservation98254(R23PlayableLoop loop,Action<string> note)
  {
   note("OVERHANG98254 actual thin mesh front/back sweep observation entered");GameObject controls=null;Mesh mesh=null;
   try
   {
    controls=new GameObject("Declared thin mesh collision controls");controls.transform.SetParent(loop.Map.transform,false);controls.transform.position=new Vector3(150,0,-600);
    mesh=new Mesh();mesh.name="Declared single-sided vertical sheet";mesh.vertices=new[]{new Vector3(-2,0,0),new Vector3(2,0,0),new Vector3(2,3,0),new Vector3(-2,3,0)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();var collider=controls.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=false;Physics.SyncTransforms();
    bool ceiling,backfaces=Physics.queriesHitBackfaces;var front=new Vector3(150,1.3f,-602);var back=new Vector3(150,1.3f,-598);var a=loop.Rig.HeadCollision.Sweep(front,Vector3.forward*4,false,out ceiling);Need(Physics.queriesHitBackfaces==backfaces,"Caller backface setting restored after front sweep");var b=loop.Rig.HeadCollision.Sweep(back,Vector3.back*4,false,out ceiling);Need(Physics.queriesHitBackfaces==backfaces,"Caller backface setting restored after back sweep");
    note("Actual source168-point hull single sheet: callerPhysicsBackfaces="+backfaces+" restored="+(Physics.queriesHitBackfaces==backfaces)+" frontMotion="+a.ToString("F6")+" backMotion="+b.ToString("F6")+" frontStartClear="+loop.Rig.HeadCollision.Clear(front,Vector3.zero)+" backStartClear="+loop.Rig.HeadCollision.Clear(back,Vector3.zero)+" collider="+collider.GetType().Name+" sourceTriangles=2; no persistent global policy change.");
    Need(a.magnitude<2&&b.magnitude<2,"Declared solid sheet blocks actual head traversal from BOTH sides");
    note("This control passes both sides only; no exact human overhang/transition geometry or all-map collision acceptance inferred.");
   }
   finally{if(controls!=null){controls.SetActive(false);UnityEngine.Object.Destroy(controls);}if(mesh!=null)UnityEngine.Object.Destroy(mesh);}
   yield break;
  }
 }
}
#endif
