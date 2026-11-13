#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;
using Ashfall.Core;using Ashfall.Modules.Player;using Ashfall.Modules.Pickups;
using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.UI;using Ashfall.Integration.Playable.Input;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.Explosives;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static R23GripObjective WorldGripFrame(R23PlayableLoop loop,int hand,float grip)
  {
   var left=new HandSample{Tracked=true,Grip=hand==0?grip:0};var right=new HandSample{Tracked=true,Grip=hand==1?grip:0};loop.Rig.SmokeTrackedHands(left,right);loop.PrepareObjectiveGrips(left,right,true,true,true);var selected=(R23GripObjective)Read(loop,"selectedObjective");loop.StepItemInput(.02f,left,right,true,false);return selected;
  }
  static bool WorldTaken(R23PlayableLoop loop,string kind,string id)
  {
   if(kind=="weapon")return loop.Gameplay.OwnsWeapon(id);
   if(kind=="soda"){var item=loop.Consumables.InspectItem(id);return item!=null&&item.Owner!=null;}
   if(kind=="frag"){R23GrenadeItemState item;return loop.Explosives.TryReadItem(id,out item)&&item.Owned;}
   Ashfall.Integration.Playable.Consumables.R24SupplyItem box;return loop.Supplies.TryReadItem(id,out box)&&box.Collected;
  }
  static IEnumerator DominantWorldPickups(R23PlayableLoop loop,Action<string> note)
  {
   var original=loop.UI.Settings;int cell=0;
   try
   {
    loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card");float end=Time.realtimeSinceStartup+45;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");
    foreach(int primary in new[]{0,1})foreach(string kind in new[]{"weapon","soda","frag","backpack"})
    {
     loop.RestartRound();loop.Gameplay.HolsterCurrent();Call(loop,"ApplySettings",original.With(dominantHand:primary,gripToggle:false));
     var at=new Vector3(14+cell++*3.5f,.2f,-620);loop.Rig.SetSpawn(at,at+Vector3.forward);loop.Calibration.NotifyRootPoseReset();loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.35f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.35f);loop.Rig.Left.localRotation=loop.Rig.Right.localRotation=Quaternion.identity;
     loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();Need(loop.Gameplay.PrimaryHand==primary&&loop.Consumables.PreferredHand==primary+1,"Committed Root settings mirror all item consumers");
     var owned=loop.Gameplay.OwnedWeaponActionIds;Need(owned.Length>0&&loop.Gameplay.DropOwned(owned[owned.Length-1],at+Vector3.forward*4+Vector3.up,Quaternion.identity),"Actual carried item dropped for one finite free slot");Call(loop,"RefreshSlotBudget");
     int support=1-primary;var other=support==0?loop.Rig.Left:loop.Rig.Right;var main=primary==0?loop.Rig.Left:loop.Rig.Right;string id="hand-world/"+primary+"/"+kind;bool spawned;
     if(kind=="weapon")spawned=loop.Gameplay.SpawnWorldWeapon("px4",1,other.position,Quaternion.identity,out id);
     else if(kind=="soda")spawned=loop.Consumables.Spawn(id,"soda",other.position);
     else if(kind=="frag")spawned=loop.Explosives.SpawnWorldItem(id,"frag",1,other.position,Quaternion.identity);
     else spawned=loop.Supplies.Spawn(id,"backpack",other.position,1);
     Need(spawned,"Real finite source prop "+kind);WorldGripFrame(loop,support,0);var objective=WorldGripFrame(loop,support,1);note("Offhand candidate primary="+primary+" kind="+kind+" target="+id+" selected="+objective.Kind+"/"+objective.Id+" hand="+objective.Hand+" taken="+WorldTaken(loop,kind,id));
     Need(!WorldTaken(loop,kind,id)&&(!objective.Valid||objective.Kind==R23GripObjectiveKind.Open),"Support grip cannot claim a world item");
     WorldGripFrame(loop,support,0);main.position=other.position;Physics.SyncTransforms();WorldGripFrame(loop,primary,0);objective=WorldGripFrame(loop,primary,1);
     note("Dominant candidate primary="+primary+" kind="+kind+" target="+id+" selected="+objective.Kind+"/"+objective.Id+" hand="+objective.Hand+" distance="+objective.Distance+" taken="+WorldTaken(loop,kind,id));
     Need(objective.Id==id&&objective.Hand==primary&&WorldTaken(loop,kind,id),"Selected dominant hand claims the actual finite target");
     if(kind=="weapon")Need(loop.Gameplay.HasWeapon&&loop.Gameplay.PrimaryHand==primary,"Actual weapon stays in selected dominant hand");
     if(kind=="frag")Need(loop.Explosives.HeldId==id&&loop.Explosives.HeldHand==primary+1,"Actual grenade in selected dominant hand");
     if(kind=="soda")
     {
      Need(loop.Consumables.HeldId==id&&loop.Consumables.HeldHand==primary+1,"Actual soda in selected dominant hand");
      WorldGripFrame(loop,primary,0);var items=(System.Collections.IDictionary)Read(loop.Consumables,"items");var view=(GameObject)Read(items[id],"View");other.position=view.transform.TransformPoint(Vector3.up*.10f);Physics.SyncTransforms();WorldGripFrame(loop,support,0);objective=WorldGripFrame(loop,support,1);
      note("Actual support cap primary="+primary+" selected="+objective.Kind+"/"+objective.Id+" phase="+loop.Consumables.Phase);Need(objective.Kind==R23GripObjectiveKind.Open,"Support hand retains genuine held-cap objective");
     }
     WorldGripFrame(loop,primary,0);yield return null;
    }
    note("Actual finite pistol/soda/grenade/backpack: both handedness modes reject support world pickups and accept dominant pickups. Held soda cap remains a support-hand action; no input/physics/HMD universality claim.");
   }
   finally{loop.RestartRound();Call(loop,"ApplySettings",original);loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();}
  }
  static IEnumerator ReverseInclineProbe(R23PlayableLoop loop,Action<string> note)
  {
   GameObject fixture=null;var original=loop.UI.Settings;var headLocal=loop.Rig.Head.transform.localPosition;var originLocal=loop.Rig.TrackingOrigin.localPosition;
   try
   {
    loop.RestartRound();loop.Gameplay.HolsterCurrent();Call(loop,"ApplySettings",original.With(dominantHand:1,forward:R23Forward.Head,walk:1));loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();
    Need(loop.Rig.UseSourceHeadHull&&loop.Rig.HeadCollision!=null,"Actual source goggle-hull movement mode required");
    fixture=new GameObject("Owned reverse-incline physics controls");fixture.transform.SetParent(loop.Map.transform,false);fixture.transform.position=new Vector3(2000,0,2000);
    Action<string,Vector3,Vector3,Quaternion> box=delegate(string name,Vector3 at,Vector3 size,Quaternion rotation){var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name=name;item.layer=0;item.transform.SetParent(fixture.transform,false);item.transform.localPosition=at;item.transform.localScale=size;item.transform.localRotation=rotation;item.GetComponent<MeshRenderer>().enabled=false;};
    box("Declared control floor",new Vector3(0,-.1f,1),new Vector3(12,.6f,80),Quaternion.identity);
    box("Declared descending ceiling",new Vector3(0,2,1.5f),new Vector3(3,.3f,4.24264f),Quaternion.Euler(45,0,0));
    box("Declared low ceiling after slope",new Vector3(0,.5f,4.5f),new Vector3(3,.3f,3),Quaternion.identity);Physics.SyncTransforms();
    var start=fixture.transform.TransformPoint(new Vector3(0,.22f,-3));loop.Rig.SetSpawn(start,start+Vector3.forward);loop.Calibration.NotifyRootPoseReset();loop.Climbing.ResetMotion();loop.Activities.ResetRound();loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.35f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.35f);
    Need(loop.Rig.HeadCollision.Clear(loop.Rig.Head.transform.position,Vector3.zero),"Controlled start has genuine clear head space");
    for(int neutral=0;neutral<8;neutral++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
    note("Actual neutral rearm: leftReady="+loop.Rig.LeftInputReady+" rightReady="+loop.Rig.RightInputReady+" supported="+loop.Rig.Supported+" blocked="+loop.Rig.GameplayBlocked+" frameTracked="+loop.Rig.LastFrame.Tracked+" framePaused="+loop.Rig.LastFrame.Paused+" start="+loop.Rig.transform.position.ToString("F6"));
    Need(loop.Rig.LeftInputReady&&loop.Rig.RightInputReady&&loop.Rig.LastFrame.Tracked&&!loop.Rig.LastFrame.Paused,"Real neutral motor frames rearmed actual tracked input");
    // Same real hull and slope: retain the unmodified airborne/climb default,
    // while the explicit standing-walk policy must not manufacture downward travel.
    bool controlCeiling;var controlHead=loop.Rig.Head.transform.position;var controlMotion=Vector3.forward*5;
    var ordinary=loop.Rig.HeadCollision.Sweep(controlHead,controlMotion,true,out controlCeiling);
    var standing=loop.Rig.HeadCollision.Sweep(controlHead,controlMotion,true,out controlCeiling,true);
    note("Native slope response default="+ordinary.ToString("F6")+" supportedWalk="+standing.ToString("F6"));
    Need(ordinary.y<-.1f&&Mathf.Abs(standing.y)<.0001f&&standing.z<ordinary.z,"Standing underside policy is bounded; default physical slide remains available");
    float minimumFeet=Single.PositiveInfinity,largestDown=0,maxProgress=0;int unclears=0;var previous=loop.Rig.transform.position;bool reported=false;
    for(int i=0;i<150;i++)
    {
     loop.Rig.SmokeXrMove(Vector2.up,.02f);var feet=loop.Rig.transform.position;var head=loop.Rig.Head.transform.position;minimumFeet=Mathf.Min(minimumFeet,feet.y);largestDown=Mathf.Min(largestDown,(feet.y-previous.y)/.02f);maxProgress=Mathf.Max(maxProgress,feet.z-start.z);bool clear=loop.Rig.HeadCollision.Clear(head,Vector3.zero);if(!clear)unclears++;
     if(i==3){note("Actual requested movement: MoveY="+loop.Rig.LastFrame.MoveY+" tracked="+loop.Rig.LastFrame.Tracked+" paused="+loop.Rig.LastFrame.Paused+" position="+feet.ToString("F6"));Need(loop.Rig.LastFrame.MoveY>.9f,"Actual motor admitted forward axis");}
     if(!reported&&feet.y<.15f){reported=true;note("CONTROLLED reverse-ceiling witness step="+i+" feet="+feet.ToString("F6")+" head="+head.ToString("F6")+" previous="+previous.ToString("F6")+" velocity="+loop.Rig.MotionVelocity.ToString("F6")+" supported="+loop.Rig.Supported+" headClear="+clear+" rawHead="+loop.Rig.Head.transform.localPosition.ToString("F6"));}
     previous=feet;if(i%15==0)yield return null;
    }
    note("CONTROLLED physics observation only: requested forward150x.02/walk1, floorTop=.2/floorZ[-39,41], maxForwardProgress="+maxProgress.ToString("F6")+" minFeet="+minimumFeet.ToString("F6")+" largestDownSpeed="+largestDown.ToString("F6")+" headUnclearSamples="+unclears+" endpoint="+loop.Rig.transform.position.ToString("F6")+". Synthetic BoxCollider slope/ceiling; not user's actual building or all-map acceptance.");
    Need(maxProgress>1,"Actual movement must reach controlled approach; stationary completion is not collision proof");
    Need(minimumFeet>=.19f&&largestDown>-.1f&&unclears==0&&loop.Rig.Supported,"Grounded approach must retain floor/head clearance without injected descent");
    Need(maxProgress<6,"Standing player stops at descending ceiling rather than passing through it");
    for(int i=0;i<75;i++){loop.Rig.SmokeXrMove(Vector2.down,.02f);if(i%15==0)yield return null;}
    note("CONTROLLED reverse exit endpoint="+loop.Rig.transform.position.ToString("F6")+" headClear="+loop.Rig.HeadCollision.Clear(loop.Rig.Head.transform.position,Vector3.zero)+" supported="+loop.Rig.Supported);
   }
   finally{if(fixture!=null){fixture.SetActive(false);UnityEngine.Object.Destroy(fixture);}loop.Rig.Head.transform.localPosition=headLocal;loop.Rig.TrackingOrigin.localPosition=originLocal;loop.RestartRound();Call(loop,"ApplySettings",original);loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();}
  }
 }
}
#endif
