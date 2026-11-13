#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;
using Ashfall.Core;using Ashfall.Modules.Player;using Ashfall.Modules.Construction;
using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.Activities;using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;using UnityEditor;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static Float3 BuildF(Vector3 v){return new Float3(v.x,v.y,v.z);}
  static void BuildRootFrame(R23PlayableLoop loop,int primary,bool trigger,Vector2 movement)
  {
   var left=new HandSample{Tracked=true,Position=BuildF(loop.Rig.transform.InverseTransformPoint(loop.Rig.Left.position)),StickX=movement.x,StickY=movement.y,Trigger=primary==0&&trigger?1:0};
   var right=new HandSample{Tracked=true,Position=BuildF(loop.Rig.transform.InverseTransformPoint(loop.Rig.Right.position)),Trigger=primary==1&&trigger?1:0};
   loop.Rig.SmokeTrackedHands(left,right);
   Call(loop.Rig,"Advance",R23InputProjection.Sample(true,true,BuildF(loop.Rig.transform.InverseTransformPoint(loop.Rig.Head.transform.position)),left,right,false,primary),.02f);
  }
  static GameObject BuildControl(R23PlayableLoop loop)
  {
   var root=new GameObject("Declared finite building source-overlap controls");root.transform.SetParent((Transform)Read(loop.Activities,"map"),false);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Declared source support floor";floor.transform.SetParent(root.transform,false);floor.transform.position=new Vector3(100,-.1f,-600);floor.transform.localScale=new Vector3(40,.2f,40);floor.GetComponent<MeshRenderer>().enabled=false;Physics.SyncTransforms();return root;
  }
  static void BeginBuildControl(R23PlayableLoop loop,int primary,Action<string> note)
  {
   loop.RestartRound();loop.Gameplay.HolsterCurrent();loop.UI.SetSettings(loop.UI.Settings.With(dominantHand:primary,walk:1));Call(loop,"ApplySettings",loop.UI.Settings);
   var at=new Vector3(100,.04f,-600);loop.Rig.SetSpawn(at,at+Vector3.forward);loop.Calibration.NotifyRootPoseReset();
   loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.35f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.35f);
   var pose=Quaternion.LookRotation(Vector3.forward)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);loop.Rig.Left.rotation=loop.Rig.Right.rotation=pose;
   loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();
   int handCommitFrames=0;while((loop.Gameplay.PrimaryHand!=primary||loop.Rig.PrimaryHand!=primary)&&handCommitFrames<4)
   {loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();handCommitFrames++;}
   note("Actual handedness setup requested="+primary+" rig="+loop.Rig.PrimaryHand+" gameplay="+loop.Gameplay.PrimaryHand+" pending="+Read(loop,"requestedDominantHand")+" neutralRootFrames="+handCommitFrames);
   Need(loop.Rig.PrimaryHand==primary&&loop.Gameplay.PrimaryHand==primary,"Actual Root handedness committed before trigger sample");
   for(int i=0;i<12;i++)BuildRootFrame(loop,primary,false,Vector2.zero);
  }
  static IEnumerator BuildingPlacement98254(R23PlayableLoop loop,Action<string> note)
  {
   note("BUILD98254 actual Root placement/source-overlap fixture entered");var settings=loop.UI.Settings;GameObject controls=null;
   try
   {
    loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range");float end=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");
    controls=BuildControl(loop);BeginBuildControl(loop,1,note);var a=loop.Activities;var hand=loop.Rig.Right;
    note("Initial actual gate ready="+loop.Rig.RightInputReady+"/"+loop.Rig.LeftInputReady+" allowed="+loop.Rig.ConstructionInputAllowed+" holstered="+loop.Gameplay.IsHolstered+" supported="+loop.Rig.Supported+" status="+a.BuildStatus);
    BuildRootFrame(loop,1,true,Vector2.zero);note("Before obstruction active="+a.BuildActive+" panels="+a.PreviewPanelCount+" status="+a.BuildStatus);
    Need(a.BuildActive&&a.PreviewPanelCount>0,"Actual fresh Root trigger has valid supported preview");
    var preview=(R23ConstructionPreview)Read(a,"preview");var shapes=(List<GameObject>)Read(preview,"panels");Vector3 center=shapes[0].transform.position;
    var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.name="Declared center-ray obstruction (source permits world overlap)";obstruction.transform.SetParent(controls.transform,false);obstruction.transform.position=Vector3.Lerp(hand.position,center,.5f);obstruction.transform.localScale=new Vector3(.5f,.7f,.5f);obstruction.GetComponent<MeshRenderer>().enabled=false;Physics.SyncTransforms();
    bool legacyBlocked=false;var ray=center-hand.position;foreach(var hit in Physics.RaycastAll(hand.position,ray.normalized,ray.magnitude,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(hit.collider==obstruction.GetComponent<Collider>())legacyBlocked=true;
    Need(legacyBlocked,"Actual new control intersects the old center-LOS predicate");Need(a.HasLineOfSight("player",BuildF(hand.position),BuildF(center)),"Source overlap policy removes extra center-LOS rejection");
    int count=a.PanelCount,balance=a.Balance;BuildRootFrame(loop,1,true,Vector2.zero);note("Obstructed-center actual preview="+a.PreviewPanelCount+" status="+a.BuildStatus+" legacyCenterBlocked="+legacyBlocked);
    Need(a.PreviewPanelCount>0,"Source-valid preview survives center-ray obstruction");BuildRootFrame(loop,1,false,Vector2.zero);Need(a.PanelCount>count&&balance-a.Balance==a.PanelCount-count,"Actual release places and debits only committed panels");
    BuildRootFrame(loop,1,false,Vector2.zero);BuildRootFrame(loop,1,true,Vector2.zero);Vector3 before=loop.Rig.transform.position;int valid=0;
    for(int i=0;i<12;i++){BuildRootFrame(loop,1,true,new Vector2(.5f,0));if(a.PreviewPanelCount>0)valid++;}
    note("Around previous build: actual distance="+Vector3.Distance(before,loop.Rig.transform.position)+" validFrames="+valid+"/12 final="+a.BuildStatus);
    Need(Vector3.Distance(before,loop.Rig.transform.position)>.1f&&valid>0,"Actual movement retains at least one supported source-valid adjacent preview");
    BuildRootFrame(loop,1,false,Vector2.zero);
    var current=a.PanelSnapshot;Need(current.Length>0,"Actual committed geometry retained");Need(a.Clearance(new Panel("wall",BuildF(loop.Rig.Head.transform.position),0,false,1),"player").Length>0,"Player clearance remains enforced");
    note("Actual Root overlap/placed-cover path retains support/charge and player-clearance checks. Synthetic obstruction witness; not all human map/build failures reproduced.");
   }
   finally{if(controls!=null){controls.SetActive(false);UnityEngine.Object.Destroy(controls);}loop.UI.SetSettings(settings);Call(loop,"ApplySettings",settings);loop.RestartRound();}
  }
  static IEnumerator BuildingBeam98254(R23PlayableLoop loop,Action<string> note)
  {
   note("BUILD98254 actual moving beam fixture entered");var settings=loop.UI.Settings;GameObject controls=null;
   try
   {
    if(loop.Map.MapId!="range"){loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range");float end=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Range loaded");}
    controls=BuildControl(loop);
    foreach(int primary in new[]{0,1})
    {
     BeginBuildControl(loop,primary,note);var a=loop.Activities;var hand=primary==0?loop.Rig.Left:loop.Rig.Right;BuildRootFrame(loop,primary,true,Vector2.zero);
     note("Beam hand="+primary+" active="+a.BuildActive+" panels="+a.PreviewPanelCount+" lightning="+a.PreviewLightningVisible+" gate="+loop.Rig.ConstructionInputAllowed+" status="+a.BuildStatus);
     Need(a.BuildActive&&a.PreviewLightningVisible,"Actual fresh dominant-hand build preview");
     var preview=(R23ConstructionPreview)Read(a,"preview");var mesh=(Mesh)Read(preview,"bolt");var bolt=(GameObject)Read(preview,"boltObject");var origin=loop.Rig.transform.position;float largestOldResidual=0;
     for(int frame=0;frame<12;frame++)
     {
      BuildRootFrame(loop,primary,true,new Vector2(.5f,0));Need(a.PreviewLightningVisible,"Moving actual preview remains valid: "+a.BuildStatus);
      var vertices=mesh.vertices;Need(vertices.Length>12,"Actual lightning mesh exists");var stale=bolt.transform.TransformPoint((vertices[0]+vertices[1])*.5f);largestOldResidual=Mathf.Max(largestOldResidual,Vector3.Distance(stale,hand.position));
      int count=a.PanelCount,balance=a.Balance,previewCount=a.PreviewPanelCount;Vector3 left=loop.Rig.Left.localPosition,right=loop.Rig.Right.localPosition;Quaternion lq=loop.Rig.Left.localRotation,rq=loop.Rig.Right.localRotation;
      a.RefreshConstructionPresentation();var start=bolt.transform.TransformPoint((vertices[0]+vertices[1])*.5f);Need(Vector3.Distance(start,hand.position)<.0001f,"Actual beam starts at post-movement hand");
      Need(a.PanelCount==count&&a.Balance==balance&&a.PreviewPanelCount==previewCount,"Presentation does not place/debit/recompute candidates");Need(loop.Rig.Left.localPosition==left&&loop.Rig.Right.localPosition==right&&loop.Rig.Left.localRotation==lq&&loop.Rig.Right.localRotation==rq,"Presentation retains raw tracked transforms");
      var after=mesh.vertices;Need(after.Length==vertices.Length,"No late mesh regeneration");for(int i=0;i<after.Length;i++)Need(after[i]==vertices[i],"Late pose only transports generated vertices");
     }
     Need(Vector3.Distance(origin,loop.Rig.transform.position)>.1f&&largestOldResidual>.001f,"Actual motor motion exposes old pre-movement beam lag");
     var originalLocal=hand.localPosition;var originalRotation=hand.localRotation;hand.localPosition+=new Vector3(.06f,.025f,-.02f);hand.localRotation=Quaternion.Euler(10,25,0)*originalRotation;var beforeRefresh=mesh.vertices;a.RefreshConstructionPresentation();
     Need(Vector3.Distance(bolt.transform.TransformPoint((beforeRefresh[0]+beforeRefresh[1])*.5f),hand.position)<.0001f,"Late translated/rotated supplied tracking still anchors beam");hand.localPosition=originalLocal;hand.localRotation=originalRotation;
     foreach(var message in ShaderUtil.GetShaderMessages(preview.GhostMaterial.shader))Need(message.severity.ToString()!="Error",message.message);
     note("Dominant="+primary+" 12actualmotor-strafe frames, largest old origin residual="+largestOldResidual+"m; rendered beam within.1mm after late refresh, vertices/plan/charges/tracking unchanged.");
     BuildRootFrame(loop,primary,false,Vector2.zero);Need(!a.PreviewLightningVisible,"Release clears beam; late render cannot resurrect it");
    }
   }
   finally{if(controls!=null){controls.SetActive(false);UnityEngine.Object.Destroy(controls);}loop.UI.SetSettings(settings);Call(loop,"ApplySettings",settings);loop.RestartRound();}
   yield break;
  }
 }
}
#endif
