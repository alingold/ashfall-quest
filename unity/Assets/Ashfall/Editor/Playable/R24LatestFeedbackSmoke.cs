#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.IO;
using Ashfall.Core;using Ashfall.Modules.Player;using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.UI;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static IEnumerator Range257(R23PlayableLoop loop)
  {
   loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card");yield return null;
   float until=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<until)yield return null;
   Need(loop.Ready&&loop.Map.MapId=="range"&&loop.Gameplay.Dt11Ready,"Actual Range and DT11 presentation loaded");loop.StartPlaying();
  }
  static bool GunFrame257(R23PlayableLoop loop,int hand,float support=0,float trigger=0,float dt=.02f)
  {
   var frame=new R23HandsInputFrame{LeftTracked=true,RightTracked=true,LeftGrip=hand==0?.8f:support,RightGrip=hand==1?.8f:support,LeftTrigger=hand==0?trigger:0,RightTrigger=hand==1?trigger:0};
   Physics.SyncTransforms();return loop.Gameplay.TickInput(dt,frame);
  }
  static Transform Primary257(R23PlayableLoop loop,int hand){return hand==0?loop.Rig.Left:loop.Rig.Right;}
  static Transform Support257(R23PlayableLoop loop,int hand){return hand==0?loop.Rig.Right:loop.Rig.Left;}
  static void GunNote257(R23PlayableLoop loop,int hand,string label,Action<string> note)
  {var h=loop.Gameplay;var s=h.ReadWeaponState(h.ActiveWeaponActionId);note(label+" hand="+hand+" id="+h.ActiveWeaponActionId+" holder="+h.PrimaryHand+" held="+h.HasWeapon+" displayedAmmo="+h.Ammo+" loaded="+s.Loaded+" reserve="+h.Reserve+" phase="+s.Phase+" seated="+s.Seated+" shots="+s.Shots+" cooldown="+s.CooldownSeconds+" gripOwned="+h.ReloadHandOccupied+" actualInputs="+h.Dt11ControlStatus);}
  static string PickupDt257(R23PlayableLoop loop,int hand,Action<string> note)
  {
   var h=loop.Gameplay;h.NotifyFocus(true);h.SetGameplayPaused(false);h.SetWeaponPresentationSuppressed(false);
   var owned=h.OwnedWeaponActionIds;Need(owned.Length>0&&h.DropOwned(owned[owned.Length-1],loop.Rig.transform.position+Vector3.up+Vector3.right*4,Quaternion.identity),"Actual drop frees one slot");Call(loop,"RefreshSlotBudget");
   string id;var primary=Primary257(loop,hand);Need(h.SpawnWorldWeapon("dt11",1,primary.position,Quaternion.LookRotation(Vector3.forward),out id),"One declared finite source DT11 lifetime");
   string instance;Vector3 center,position;Quaternion rotation;Need(h.TryReadWorldWeapon(id,out instance,out center,out position,out rotation),"Actual world owner and center");
   primary.position=center;Support257(loop,hand).position=loop.Rig.Head.transform.position-Vector3.up*.35f+Vector3.right*(hand==0?.65f:-.65f);Physics.SyncTransforms();
   WorldGripFrame(loop,hand,0);var selected=WorldGripFrame(loop,hand,1);
   note("Actual world DT11 target="+selected.Id+" hand="+selected.Hand+" distance="+selected.Distance+" wanted="+id+" receiver="+h.PrimaryHand);
   Need(selected.Id==id&&h.OwnsWeapon(id)&&h.HasWeapon&&h.ActiveWeaponActionId==id&&h.PrimaryHand==hand,"Fresh actual Root pickup owns exact DT11");
   for(int i=0;i<24;i++)GunFrame257(loop,hand);return id;
  }
  static void EmptyDt257(R23PlayableLoop loop,int hand,Action<string> note)
  {
   var h=loop.Gameplay;long before=h.ReadWeaponState(h.ActiveWeaponActionId).Shots;
   GunNote257(loop,hand,"BEFORE first shot",note);GunFrame257(loop,hand,0,1,.25f);
   Need(h.Ammo==1&&h.ReadWeaponState(h.ActiveWeaponActionId).Shots==before+1,"Actual first trigger debit");var actualAudio=Read(h,"audioCues");var actualVoices=(AudioSource[])Read(actualAudio,"shots");int actualCursor=(int)Read(actualAudio,"cursor");var actualShot=actualVoices[(actualCursor+actualVoices.Length-1)%actualVoices.Length];Need(actualShot.clip!=null&&actualShot.clip.name=="dt11-recoil-0631-shot.wav","Actual carried source shot uses user reference recording");CheckDt11Voice(actualAudio,actualShot,.5623413f,1,1);
   GunFrame257(loop,hand);GunFrame257(loop,hand,0,1,.25f);GunNote257(loop,hand,"AFTER empty",note);
   Need(h.Ammo==0&&h.ReloadStage==ReloadPhase.NeedsMagazine&&h.ReadWeaponState(h.ActiveWeaponActionId).Shots==before+2,"Actual second trigger auto-opens source DT11");
   for(int i=0;i<24;i++)GunFrame257(loop,hand);
  }
  static void DownDt257(R23PlayableLoop loop,int hand,Action<string> note)
  {
   var h=loop.Gameplay;var support=Support257(loop,hand);support.position=h.ReloadOfferPosition;GunFrame257(loop,hand);GunFrame257(loop,hand,.8f);
   GunNote257(loop,hand,"BEFORE DOWN",note);Need(h.ReloadHandOccupied,"Actual offered pair held");
   var gun=Primary257(loop,hand).rotation*R23TrackedGripPose.LocalRotation;support.position-=gun*Vector3.up*.04f;GunFrame257(loop,hand,.8f);GunNote257(loop,hand,"AFTER DOWN",note);
   var inserted=h.ReadWeaponState(h.ActiveWeaponActionId);Need(inserted.Phase==ReloadPhase.NeedsRack&&inserted.Loaded==0&&inserted.Seated==2&&h.Ammo==2&&!h.ReloadHandOccupied,"Actual insertion seats two displayed shells but leaves zero closed loaded shells and no reload contact");
  }
  static IEnumerator BuildEmpty257(R23PlayableLoop loop,Action<string> note)
  {
   note("FEEDBACK257 BUILD_EMPTY actual Root fixture entered");var ready=Range257(loop);while(ready.MoveNext())yield return ready.Current;
   var settings=loop.UI.Settings;GameObject floor=null;
   try{floor=BuildControl(loop);foreach(int hand in new[]{1,0}){
    BeginBuildControl(loop,hand,note);string id=PickupDt257(loop,hand,note);EmptyDt257(loop,hand,note);int reserve=loop.Gameplay.Reserve;DownDt257(loop,hand,note);
    Need(loop.Gameplay.Reserve==reserve-2,"Exactly two finite shells inserted");Need(loop.Gameplay.HolsterCurrent(),"Actual holster suspends reload contact");loop.SmokeUiUpdate();
    for(int i=0;i<8;i++)BuildRootFrame(loop,hand,false,Vector2.zero);
    var state=loop.Gameplay.ReadWeaponState(id);note("BEFORE BUILD hand="+hand+" phase="+state.Phase+" ammo="+state.Loaded+" contact="+loop.Gameplay.ReloadHandOccupied+" holstered="+loop.Gameplay.IsHolstered+" allowed="+loop.Rig.ConstructionInputAllowed+" ready="+loop.Rig.LeftInputReady+"/"+loop.Rig.RightInputReady+" occupied="+loop.Rig.LeftHandOccupied+"/"+loop.Rig.RightHandOccupied+" status="+loop.Activities.BuildStatus);
    Need(state.Phase==ReloadPhase.NeedsRack&&state.Loaded==0&&loop.Rig.ConstructionInputAllowed,"Actual stale phase remains but no longer blocks free-hand construction");
    int panels=loop.Activities.PanelCount,balance=loop.Activities.Balance;BuildRootFrame(loop,hand,true,Vector2.zero);
    Need(loop.Activities.BuildActive&&loop.Activities.PreviewPanelCount>0,"Actual dominant trigger previews despite unloaded holstered gun");BuildRootFrame(loop,hand,false,Vector2.zero);
    Need(loop.Activities.PanelCount>panels&&balance-loop.Activities.Balance==loop.Activities.PanelCount-panels,"Actual release commits finite construction debit");
    Need(loop.Gameplay.ReadWeaponState(id).Loaded==0&&loop.Gameplay.ReadWeaponState(id).Seated==2&&loop.Gameplay.Reserve==reserve-2,"Build does not load gun or grant ammo");yield return null;
   }}finally{if(floor!=null){floor.SetActive(false);UnityEngine.Object.Destroy(floor);}loop.UI.SetSettings(settings);Call(loop,"ApplySettings",settings);loop.RestartRound();}
  }
  static IEnumerator DtGrip257(R23PlayableLoop loop,Action<string> note)
  {
   note("FEEDBACK257 DT_GRIP actual Root fixture entered");var ready=Range257(loop);while(ready.MoveNext())yield return ready.Current;
   var settings=loop.UI.Settings;GameObject floor=null;
   try{floor=BuildControl(loop);foreach(int hand in new[]{1,0}){
    BeginBuildControl(loop,hand,note);string id=PickupDt257(loop,hand,note);EmptyDt257(loop,hand,note);int reserve=loop.Gameplay.Reserve;DownDt257(loop,hand,note);
    var h=loop.Gameplay;var support=Support257(loop,hand);var gun=Primary257(loop,hand).rotation*R23TrackedGripPose.LocalRotation;
    support.position+=gun*Vector3.up*.044f;GunFrame257(loop,hand,.8f);GunNote257(loop,hand,"Held-grip reversal",note);
    Need(h.ReloadStage==ReloadPhase.NeedsRack&&h.Reserve==reserve-2,"One continuous DOWN/UP cannot close or debit again");
    support.position=h.ChargingGripPosition;GunFrame257(loop,hand);GunFrame257(loop,hand,.8f);Need(h.ReloadHandOccupied,"Observed release and fresh support grip reacquires fore-end");
    support.position+=gun*Vector3.up*.044f;GunFrame257(loop,hand,.8f);GunNote257(loop,hand,"New-grip UP",note);
    Need(h.ReloadStage==ReloadPhase.Ready&&h.Ammo==2&&h.Reserve==reserve-2&&h.ActiveWeaponActionId==id,"Separate UP closes same finite source owner");
    note("Actual BOTH handedness physical support DOWN/release/regrip/UP; original .04/.044m strokes, trigger/rate/filter policies unchanged. Gun-only flick and HMD feel/sound not qualified by this group.");yield return null;
   }}finally{if(floor!=null){floor.SetActive(false);UnityEngine.Object.Destroy(floor);}loop.UI.SetSettings(settings);Call(loop,"ApplySettings",settings);loop.RestartRound();}
  }
  static IEnumerator PaddleReady257(R23PlayableLoop loop,Action<string> note)
  {
   note("FEEDBACK257 PADDLE_READY actual Root fixture entered");var ready=Range257(loop);while(ready.MoveNext())yield return ready.Current;
   loop.StowPaddles();loop.Gameplay.HolsterCurrent();loop.Rig.SetSpawn(loop.Map.GameSpawn,loop.Map.GameSpawn+Vector3.forward);loop.Calibration.NotifyRootPoseReset();
   SetPaddlePose98254(loop,0,new Vector3(-.2f,1.15f,.6f),Vector3.right);SetPaddlePose98254(loop,1,new Vector3(.2f,1.15f,.6f),Vector3.left);PaddleFrame98254(loop,.02f);Need(loop.EquipPaddles(),"Actual Root reusable paddle equip");
   var p=loop.Paddles;var fill=(Transform)Read(p,"fill");var voice=(AudioSource)Read(p,"readyVoice");int completed=p.State.Completions;
   try{
    for(int i=0;i<600&&!p.State.Ready;i++){PaddleFrame98254(loop,.02f);if(i%60==0)yield return null;}
    note("Ready entered="+p.State.Ready+" remaining="+p.State.Remaining+" charge="+p.State.Charge+" completions="+p.State.Completions+" audio="+(voice.clip==null?"<null>":voice.clip.name)+" playing="+voice.isPlaying+" time="+voice.time);
    Need(p.State.Ready&&p.State.Completions==completed+1&&voice.clip!=null&&voice.clip.name=="revive-ready"&&voice.isPlaying,"First actual ready transition schedules exact source cue");
    float before=p.State.Remaining;for(int i=0;i<10;i++)PaddleFrame98254(loop,.02f);
    Need(p.State.Remaining<before&&p.State.Charge==1&&Mathf.Abs(fill.localScale.x/.286f-p.State.Remaining/R24RevivePaddlesState.ReadyWindow)<.0001f,"Actual fill drains against unchanged source ready clock");
    // A declared software interruption proves scheduling recovery, not its historical HMD cause.
    voice.Stop();PaddleFrame98254(loop,.02f);float elapsed=R24RevivePaddlesState.ReadyWindow-p.State.Remaining;
    note("Resumed actual ready voice playing="+voice.isPlaying+" offset="+voice.time+" expected="+elapsed+" fill="+fill.localScale.x/.286f);
    Need(voice.isPlaying&&Mathf.Abs(voice.time-elapsed)<.06f,"Stopped ready voice resumes original remaining-window position");
    var tracked=new HandSample{Tracked=true};loop.Rig.SmokeTrackedHands(tracked,tracked);before=p.State.Remaining;loop.StepItemInput(.02f,tracked,tracked,true,true);
    Need(p.State.Remaining==before&&!voice.isPlaying,"Actual blocked Root input freezes window and stops ready cue");PaddleFrame98254(loop,.02f);elapsed=R24RevivePaddlesState.ReadyWindow-p.State.Remaining;
    Need(voice.isPlaying&&Mathf.Abs(voice.time-elapsed)<.06f,"Actual Root unblock resumes original cue offset");
    for(int i=0;i<180&&p.State.Ready;i++)PaddleFrame98254(loop,.02f);
    Need(!p.State.Ready&&!voice.isPlaying&&p.State.Completions==completed+1,"Original ready expiry stops cue without repeated charge completion");
    note("Actual fill/source-clip/play-offset lifecycle only; no DSP/HMD audibility or completed revive target/HP proof.");
   }finally{loop.StowPaddles();}
  }
  static IEnumerator UiClutter257(R23PlayableLoop loop,Action<string> note)
  {
   note("FEEDBACK257 UI_CLUTTER actual Root fixture entered");var ready=Range257(loop);while(ready.MoveNext())yield return ready.Current;
   try{
    loop.ReturnToLobby();loop.UI.OpenMatchRules();yield return null;loop.UI.RefreshTextLayout();TextMesh footer=null;
    foreach(object row in (IEnumerable)Read(loop.UI,"labels")){var text=(TextMesh)Read(row,"Text");if(text.text.StartsWith("Use the same local network.",StringComparison.Ordinal))footer=text;}
    Need(footer!=null&&footer.gameObject.activeInHierarchy,"Actual offline LAN footer");var f=footer.GetComponent<MeshRenderer>().bounds;
    foreach(string caption in new[]{"HOST LAN","JOIN IP"}){var b=((MeshRenderer)Read(Button(loop.UI,caption),"Renderer")).bounds;bool overlap=f.min.x<b.max.x&&f.max.x>b.min.x&&f.min.y<b.max.y&&f.max.y>b.min.y;note("Actual footer="+f+" button="+caption+" bounds="+b+" overlaps="+overlap);Need(!overlap,"Actual glyph bounds clear button "+caption);}
    foreach(string preset in new[]{"free_for_all","team_respawn"}){
     loop.EndMatch();loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset(preset);Need(loop.UI.RequestMatchStart(),"Actual UI start "+preset);
     float until=Time.realtimeSinceStartup+35;while((loop.ActualMatchRules==null||!loop.MatchActive)&&loop.Failure==null&&Time.realtimeSinceStartup<until)yield return null;
     Need(loop.ActualMatchRules!=null&&loop.MatchActive,"Actual Root match ready "+preset);loop.TickMatch(.02f,true);
     note("Actual "+preset+" HUDvisible="+loop.UI.CompactMatchHudVisible+" clock="+loop.UI.CompactMatchClock+" personal="+loop.UI.CompactPersonalScore+" worldActive="+loop.ReadWorldScoreState().Active);
     Need(loop.UI.CompactMatchHudVisible&&loop.UI.CompactMatchClock=="TIME 10:00","Accepted finite TDM timer remains visible");
    }
    note("Actual glyph bounds and FFA/TDM UI flow only; no screenshot/HMD visual acceptance, LAN connection or timer-expiry replay.");
   }finally{loop.EndMatch();loop.ReturnToLobby();}
  }
 }
}
#endif
