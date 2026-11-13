#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.Climbing;using Ashfall.Modules.Player;using UnityEngine;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static IEnumerator MeshSidesRepair98254(R23PlayableLoop loop,Action<string> note)
  {
   note("OVERHANG98254 actual failed thin-sheet repair check entered");
   loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual baseline Range");float end=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Same Range as actual failed sheet observation");
   yield return ThinMeshObservation98254(loop,note);
  }
  static IEnumerator MeshOverhangWalk98254(R23PlayableLoop loop,Action<string> note)
  {
   note("OVERHANG98254 actual Root thin sloping-ceiling walk entered");GameObject controls=null;Mesh mesh=null,floorMesh=null;var original=loop.UI.Settings;bool backfaces=Physics.queriesHitBackfaces;bool trace=loop.Rig.HeadCollision.CaptureSweepDiagnostics;
   try
   {
    loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range");float end=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");
    loop.RestartRound();loop.Gameplay.HolsterCurrent();Call(loop,"ApplySettings",original.With(walk:1));
    controls=new GameObject("Declared mesh underside walk controls");controls.transform.SetParent(loop.Map.transform,false);controls.transform.position=new Vector3(210,0,-600);
    CrouchBox(controls.transform,"Declared walk support floor",new Vector3(210,-.1f,-600),new Vector3(8,.2f,40));
    var roof=new GameObject("Declared upward-facing thin ceiling (approached from underside)");roof.transform.SetParent(controls.transform,false);
    mesh=new Mesh();mesh.name="Declared2triangle thin descending ceiling";mesh.vertices=new[]{new Vector3(-2,2.4f,0),new Vector3(2,2.4f,0),new Vector3(2,.8f,4),new Vector3(-2,.8f,4)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();roof.AddComponent<MeshCollider>().sharedMesh=mesh;Physics.SyncTransforms();
    var at=new Vector3(210,.04f,-603);loop.Rig.SetSpawn(at,at+Vector3.forward);loop.Calibration.NotifyRootPoseReset();loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.35f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.35f);
    loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();for(int i=0;i<12;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
    note("Root starting state leftReady="+loop.Rig.LeftInputReady+" rightReady="+loop.Rig.RightInputReady+" supported="+loop.Rig.Supported+" feet="+loop.Rig.transform.position.ToString("F6")+" collisionBackfaces="+backfaces);
    Need(loop.Rig.LeftInputReady&&loop.Rig.RightInputReady&&loop.Rig.Supported,"Real neutral tracked movement armed and supported");
    Vector3 start=loop.Rig.transform.position;float minimumFeet=start.y,maximumMargin=float.NegativeInfinity,maxProgress=0;int unclears=0;var normal=mesh.normals[0];var plane=roof.transform.TransformPoint(mesh.vertices[0]);
    var source=R23HeadCollision.CreateSourceMesh();float extent=float.NegativeInfinity;try{foreach(var p in source.vertices)extent=Mathf.Max(extent,Vector3.Dot(normal,loop.Rig.transform.rotation*p));}finally{UnityEngine.Object.Destroy(source);}
    loop.Rig.HeadCollision.CaptureSweepDiagnostics=true;
    for(int step=0;step<120;step++)
    {
     loop.Rig.SmokeXrMove(Vector2.up,.02f);var feet=loop.Rig.transform.position;var eye=loop.Rig.Head.transform.position;minimumFeet=Mathf.Min(minimumFeet,feet.y);maxProgress=Mathf.Max(maxProgress,feet.z-start.z);if(!loop.Rig.HeadCollision.Clear(eye,Vector3.zero))unclears++;
     float margin=Vector3.Dot(normal,eye-plane)+extent;maximumMargin=Mathf.Max(maximumMargin,margin);Need(Physics.queriesHitBackfaces==backfaces,"Head query restores caller backface policy every walking step");
     if(step%12==0||margin>-.1f)note("WalkStep="+step+" feet="+feet.ToString("F6")+" eye="+eye.ToString("F6")+" planeMargin="+margin+" clear="+loop.Rig.HeadCollision.Clear(eye,Vector3.zero)+" velocity="+loop.Rig.MotionVelocity.ToString("F6")+" sweep="+loop.Rig.HeadCollision.LastSweepDiagnostics);
    }
    Vector3 stopped=loop.Rig.transform.position;note("Actual120forward frames: progress="+maxProgress+" feetMinimum="+minimumFeet+" maxHeadPlaneMargin="+maximumMargin+" unclears="+unclears+" stopped="+stopped.ToString("F6")+" requestedMoveY="+loop.Rig.LastFrame.MoveY+" rawHead="+loop.Rig.Head.transform.localPosition.ToString("F6"));
    Need(maxProgress>1&&stopped.z<-598.2f,"Real walking approaches then blocks beneath thin descending mesh");Need(minimumFeet>=-.005f&&maximumMargin<=.005f&&unclears==0,"Supported walk stays above floor and outside actual ceiling plane");
    for(int step=0;step<60;step++){loop.Rig.SmokeXrMove(Vector2.down,.02f);Need(loop.Rig.HeadCollision.Clear(loop.Rig.Head.transform.position,Vector3.zero),"Reverse exit keeps actual head clear");Need(Physics.queriesHitBackfaces==backfaces,"Reverse queries restore caller policy");}
    note("Actual60reverse frames: endpoint="+loop.Rig.transform.position.ToString("F6")+" clear="+loop.Rig.HeadCollision.Clear(loop.Rig.Head.transform.position,Vector3.zero)+" supported="+loop.Rig.Supported);
    Need(loop.Rig.transform.position.z<stopped.z-1&&loop.Rig.transform.position.y>=-.005f,"Actual rig reverses out rather than sticking/sinking");
    var floor=new GameObject("Declared mesh-floor separating-contact control");floor.transform.SetParent(controls.transform,false);floorMesh=new Mesh();floorMesh.vertices=new[]{new Vector3(-2,0,-2),new Vector3(-2,0,2),new Vector3(2,0,2),new Vector3(2,0,-2)};floorMesh.triangles=new[]{0,1,2,0,2,3};floorMesh.RecalculateNormals();floorMesh.RecalculateBounds();floor.AddComponent<MeshCollider>().sharedMesh=floorMesh;Physics.SyncTransforms();
    bool ceiling;var separating=loop.Rig.HeadCollision.Sweep(new Vector3(210,.001f,-600),Vector3.up*.05f,false,out ceiling);note("Actual front-side mesh-floor separation="+separating.ToString("F6")+" ceiling="+ceiling+" callerPolicy="+Physics.queriesHitBackfaces);
    Need(Mathf.Abs(separating.y-.05f)<.0001f&&!ceiling&&Physics.queriesHitBackfaces==backfaces,"Near-contact backside blocking preserves genuine upward floor separation");
    note("Declared thin-mesh approach/reversal only. Exact human building geometry, starting-overlap recovery, allmaps and headset remain unverified.");
   }
   finally{loop.Rig.HeadCollision.CaptureSweepDiagnostics=trace;if(controls!=null){controls.SetActive(false);UnityEngine.Object.Destroy(controls);}if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(floorMesh!=null)UnityEngine.Object.Destroy(floorMesh);loop.UI.SetSettings(original);Call(loop,"ApplySettings",original);loop.RestartRound();}
  }
 }
}
#endif
