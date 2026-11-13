#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Bots;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
using UnityEditor;

namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  // Public checkpoint restore is declared fixture setup, not campaign progression
  // proof. The earlier four-mission fixture owns that separate qualification.
  static IEnumerator LoadWardenEncounter(R23PlayableLoop loop,Action<string> note)
  {
   loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("valley"),"Actual Valley card for Warden checkpoint");
   float until=Time.realtimeSinceStartup+90;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<until)yield return null;
   Need(loop.Ready&&loop.Map.MapId=="valley","Actual Valley loaded");loop.ValleyBots.Mission.Restore(3,18,0);loop.StartPlaying();loop.RestartRound();loop.StepValleyBots(0,true);
   until=Time.realtimeSinceStartup+40;while(!loop.ValleyBots.Ready&&loop.ValleyBots.Failure==null&&Time.realtimeSinceStartup<until){loop.StepValleyBots(0,true);yield return null;}
   var squad=loop.ValleyBots;Need(squad.Ready&&squad.Count==6&&squad.Mission.Chapter==3,"Actual six-member source extraction encounter");squad.SetDifficulty(.5f);
   for(int i=0;i<5;i++){R24KestrelBrain peer;Vector3 p;bool floor;Need(squad.TryRead(i,out peer,out p,out floor),"Actual companion");var body=FindValleyBody(squad,peer.Variant,i);Need(body!=null&&loop.ApplyTargetDamage(squad.TargetId(body),20000,new Float3(0,1,-8)),"Declared companion retirement uses actual Root damage API");}
   R24KestrelBrain boss;Vector3 at;bool grounded;Need(squad.TryRead(5,out boss,out at,out grounded)&&boss.Variant==3&&boss.Health==350&&!boss.CoreOpen&&squad.LivingCount==1,"Actual unmodified source Warden/350HP/closed armor");
   var collider=FindValleyBody(squad,3,5);Need(collider!=null&&Mathf.Abs(collider.radius-.85f)<.00001f&&Mathf.Abs(collider.height-2.2f)<.00001f,"Actual source Warden capsule");
   PlaceNearRealBot(loop,at);WardenNeutral(loop);loop.Gameplay.HolsterCurrent();
   note("Declared source chapter3 checkpoint/companions retired only; actual Warden index5 generation="+squad.Generation+" phase="+boss.Phase+" position="+at+" player="+loop.Rig.transform.position+" difficulty="+boss.Difficulty);
  }
  static void WardenNeutral(R23PlayableLoop loop)
  {var neutral=new HandSample{Tracked=true};for(int i=0;i<8;i++){loop.Rig.SmokeTrackedHands(neutral,neutral);loop.Rig.SmokeXrMove(Vector2.zero,.02f);loop.SmokeUiUpdate();}}
  static void TickWarden(R23PlayableLoop loop)
  {var neutral=new HandSample{Tracked=true};loop.StepItemInput(.02f,neutral,neutral,true,false);loop.StepValleyBots(.02f,true);}
  static void WardenShaderOracle(R24ValleyBots squad)
  {foreach(var renderer in squad.EncounterTransform.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)if(material!=null)foreach(var error in ShaderUtil.GetShaderMessages(material.shader))Need(error.severity.ToString()!="Error",error.message);}

  static void WardenCarriedShot(R23PlayableLoop loop,CharacterController target,R24KestrelBrain boss,string action,float multiplier,Action<string> note)
  {
   var neutral=new HandSample{Tracked=true};loop.Rig.Right.SetPositionAndRotation(loop.Rig.Head.transform.position+Vector3.right*.25f-Vector3.up*.25f,Quaternion.identity);loop.Rig.Left.position=loop.Rig.Head.transform.position-Vector3.right*.5f;
   R23ShotPose pose=new R23ShotPose();float damping=loop.Gameplay.MotionDamping;int settleTicks=Mathf.Max(8,Mathf.CeilToInt((.006f+.09f*damping*damping)*8/.02f));
   // Reuse the carried-CX4 fixture's actual displayed-bore corrections and eight
   // unchanged damping time constants. No aim/calibration/filter policy edit.
   for(int correction=0;correction<=6;correction++){
    for(int i=0;i<settleTicks;i++)loop.StepItemInput(.02f,neutral,neutral,true,false);
    loop.Gameplay.RefreshFeedbackPresentation();Need(loop.Gameplay.TryReadDisplayedShotPose(out pose),"Actual carried CX4 displayed pose");
    Vector3 toward=(target.bounds.center-V3(pose.Origin)).normalized;if(correction<6)loop.Rig.Right.rotation=Quaternion.FromToRotation(V3(pose.Forward),toward)*loop.Rig.Right.rotation;
   }
   RaycastHit precondition;Need(target.Raycast(new Ray(V3(pose.Origin),V3(pose.Forward)),out precondition,180),"Actual displayed bore intersects intended Warden capsule before fire");
   float health=boss.Health;var before=loop.Gameplay.ReadWeaponState(action);string targetId=loop.ValleyBots.TargetId(target);int hits=0;Collider actual=null;float damage=0;
   Action<Collider,float> handler=delegate(Collider c,float value){hits++;actual=c;damage=value;};loop.Gameplay.WorldHit+=handler;
   bool accepted;try{accepted=loop.StepItemInput(.001f,neutral,new HandSample{Tracked=true,Trigger=1},true,false);}finally{loop.Gameplay.WorldHit-=handler;}
   var after=loop.Gameplay.ReadWeaponState(action);Need(accepted&&after.Shots==before.Shots+1&&after.Loaded==before.Loaded-1,"One actual carried discharge and round");
   Need(hits==1&&actual!=null&&loop.ValleyBots.TargetId(actual)==targetId&&damage>0,"Actual ballistic trace reaches source Warden owner exactly once");
   Need(Mathf.Abs(health-boss.Health-damage*multiplier)<.0001f&&boss.LastDamageMultiplier==multiplier,"Actual Root armor/core damage multiplier "+multiplier);
   note("Actual carried CX4 shots="+before.Shots+"->"+after.Shots+" loaded="+before.Loaded+"->"+after.Loaded+" rawDamage="+damage+" multiplier="+multiplier+" HP="+health+"->"+boss.Health+" phase="+boss.BossPhase+" core="+boss.CoreOpen+" collider="+actual.name);
  }
  static IEnumerator WardenCarriedDamage(R23PlayableLoop loop,Action<string> note)
  {
   var mission=loop.ValleyBots.Mission;int chapter=mission.Chapter,kills=mission.Kills;double seconds=mission.PlaySeconds;float difficulty=loop.ValleyBots.Difficulty;
   try{
    yield return LoadWardenEncounter(loop,note);var squad=loop.ValleyBots;R24KestrelBrain boss;Vector3 p;bool floor;Need(squad.TryRead(5,out boss,out p,out floor),"Actual Warden readable");var target=FindValleyBody(squad,3,5);
    string action=null;foreach(string id in loop.Gameplay.OwnedWeaponActionIds)if(loop.Gameplay.ResolveWeaponProfile(id)=="cx4")action=id;
    note("Actual source carried CX4="+action+" inputReady="+loop.Rig.RightInputReady+" paused="+loop.Gameplay.BotProbePaused+" focused="+loop.Gameplay.BotProbeFocused+" page="+loop.UI.Page);
    Need(action!=null&&loop.Gameplay.EquipOwned(action,1),"Actual owned CX4, no grant");string inventory=String.Join("|",loop.Gameplay.OwnedWeaponActionIds);int capacity=loop.InventoryCapacity;
    WardenCarriedShot(loop,target,boss,action,.25f,note);Need(!boss.CoreOpen&&boss.BossPhase==1,"First real shot used original closed armor");
    bool opened=false;for(int i=0;i<1500;i++){TickWarden(loop);if(boss.State==R24KestrelState.Windup&&!boss.AreaAttack&&boss.CoreOpen&&boss.StateTime>=1.2f){opened=true;break;}if(i%25==0)yield return null;}
    Need(opened,"Production perception/cooldown/windup opens actual Warden core; no private state forcing");var art=squad.EncounterTransform.GetComponentInChildren<R24WardenVisual>(true);Need(art!=null&&art.CoreOpen&&art.LeftShutterRadians<-.9f&&art.RightShutterRadians>.9f,"Actual source shutter hinges publish open-core state");
    // Manual actor clock is deliberately held after reaching real production
    // Windup; the actual carried weapon/input clock runs normally for these hits.
    int shots=0;while(boss.Health>boss.MaximumHealth*.5f&&shots<20){WardenCarriedShot(loop,target,boss,action,1,note);shots++;}
    Need(boss.BossPhase==2&&boss.Health<=175&&boss.Health>0,"Physical carried hits cross source half-health phase boundary");
    Need(String.Join("|",loop.Gameplay.OwnedWeaponActionIds)==inventory&&loop.InventoryCapacity==capacity,"Actual carried identities/capacity retained without grants");WardenShaderOracle(squad);
    note("Actual closed/core/half-health physical CX4 routing passed; phase transition uses actual weapon damage. Manual actor clock held during aim/hit controls; no real-time encounter, every-weapon, appearance/HMD acceptance.");
   }finally{loop.Gameplay.HolsterCurrent();loop.ReturnToLobby();mission.Restore(chapter,kills,seconds);loop.ValleyBots.SetDifficulty(difficulty);}
  }

  static IEnumerator WaitWardenWindup(R23PlayableLoop loop,R24KestrelBrain boss,bool area,float time,Action<string> note)
  {
   bool ready=false;for(int i=0;i<2000;i++){TickWarden(loop);if(boss.State==R24KestrelState.Windup&&boss.AreaAttack==area&&boss.StateTime>=time){ready=true;break;}if(i%25==0)yield return null;}
   note("Actual Warden windup expectedArea="+area+" state="+boss.State+" time="+boss.StateTime+" actualArea="+boss.AreaAttack+" core="+boss.CoreOpen+" HP="+boss.Health+" player="+loop.Rig.transform.position);
   Need(ready,"Actual production Warden windup reached");
  }
  static IEnumerator WaitWardenDischarge(R23PlayableLoop loop,int sequence,Action<R24KestrelDischarge> accept,Action<string> note)
  {
   R24KestrelDischarge report=null;for(int i=0;i<2000;i++){TickWarden(loop);report=loop.ValleyBots.ReadDischarge(5);if(report!=null&&report.sequence>sequence)break;if(i%25==0)yield return null;}
   note("Actual Warden discharge="+(report==null?"NULL":JsonUtility.ToJson(report)));
   Need(report!=null&&report.sequence==sequence+1&&report.inRange&&!report.queryOverflow,"Exactly next actual in-range non-overflow Warden discharge");accept(report);
  }
  static void MoveWardenPlayerToFloor(R23PlayableLoop loop,Vector3 candidate,Vector3 look)
  {Vector3 supported;Need(ClearBotPlayerSpot(loop,candidate,out supported),"Declared actual supported/head-clear player control");loop.Rig.SetSpawn(supported,look);loop.Calibration.NotifyRootPoseReset();WardenNeutral(loop);}
  static IEnumerator WardenAttacks(R23PlayableLoop loop,Action<string> note)
  {
   var mission=loop.ValleyBots.Mission;int chapter=mission.Chapter,kills=mission.Kills;double seconds=mission.PlaySeconds;float difficulty=loop.ValleyBots.Difficulty;GameObject cover=null;
   try{
    yield return LoadWardenEncounter(loop,note);var squad=loop.ValleyBots;R24KestrelBrain boss;Vector3 at;bool grounded;Need(squad.TryRead(5,out boss,out at,out grounded),"Actual Warden readable");loop.Gameplay.HolsterCurrent();
    squad.NotifyGunshot(loop.Rig.transform.position);float before=loop.Gameplay.Health+loop.Gameplay.Shield;R24KestrelDischarge last=null;
    yield return WaitWardenDischarge(loop,0,r=>last=r,note);Need(!boss.AreaAttack&&last.clear&&!last.constructed&&!last.guarded&&Mathf.Abs(last.appliedPlayerDamage-16)<.0001f&&Mathf.Abs(before-loop.Gameplay.Health-loop.Gameplay.Shield-16)<.0001f,"First actual direct attack applies source16 damage");

    yield return WaitWardenWindup(loop,boss,true,1.6f,note);Vector3 mark=V3(boss.ImpactPoint);RaycastHit ground;
    Need(Physics.Raycast(mark+Vector3.up*.2f,Vector3.down,out ground,1,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore)&&Mathf.Abs(mark.y-ground.point.y-.06f)<.002f,"Actual source ground marker is supported+.06");
    Vector3 airborne=loop.Rig.transform.position+Vector3.up*2.2f;loop.Rig.SetSpawn(airborne,at);loop.Calibration.NotifyRootPoseReset();WardenNeutral(loop);Need(Mathf.Abs(loop.Rig.transform.position.y-mark.y)>1.5f,"Declared actual airborne control clears source vertical blast bound");
    before=loop.Gameplay.Health+loop.Gameplay.Shield;yield return WaitWardenDischarge(loop,1,r=>last=r,note);Need(boss.AreaAttack&&last.appliedPlayerDamage==0&&loop.Gameplay.Health+loop.Gameplay.Shield==before,"Actual marked area attack spares airborne player");

    MoveWardenPlayerToFloor(loop,mark,at);yield return WaitWardenWindup(loop,boss,false,.72f,note);Vector3 locked=V3(boss.LockedAim);Vector3 sideways=Vector3.Cross(Vector3.up,locked-squad.ReadDischarge(5).muzzle).normalized;if(sideways.sqrMagnitude<.5f)sideways=Vector3.right;
    MoveWardenPlayerToFloor(loop,loop.Rig.transform.position+sideways,at);Need(Vector3.Distance(loop.Rig.Head.transform.position,locked)>.42f,"Actual late head displacement exceeds unchanged locked-aim radius");before=loop.Gameplay.Health+loop.Gameplay.Shield;
    yield return WaitWardenDischarge(loop,2,r=>last=r,note);Need(!boss.AreaAttack&&last.appliedPlayerDamage==0&&loop.Gameplay.Health+loop.Gameplay.Shield==before,"Actual locked direct attack spares late dodge");

    yield return WaitWardenWindup(loop,boss,true,1.6f,note);mark=V3(boss.ImpactPoint);Vector3 feet=loop.Rig.transform.position;float horizontal=Vector2.Distance(new Vector2(feet.x,feet.z),new Vector2(mark.x,mark.z));Need(horizontal<2&&Mathf.Abs(feet.y-mark.y)<=1.5f,"Actual stationary player inside source blast bounds");float expected=Mathf.Lerp(18,8,horizontal/2);before=loop.Gameplay.Health+loop.Gameplay.Shield;
    yield return WaitWardenDischarge(loop,3,r=>last=r,note);Need(boss.AreaAttack&&last.clear&&!last.constructed&&Mathf.Abs(last.appliedPlayerDamage-expected)<.001f&&Mathf.Abs(before-loop.Gameplay.Health-loop.Gameplay.Shield-expected)<.001f,"Actual grounded area damage follows source distance curve");

    yield return WaitWardenWindup(loop,boss,false,.72f,note);var art=squad.EncounterTransform.GetComponentInChildren<R24WardenVisual>(true);Need(art!=null&&art.Muzzle!=null,"Actual Warden muzzle marker");Vector3 from=art.Muzzle.position,to=V3(boss.LockedAim);
    cover=new GameObject("DECLARED Warden direct-world-cover control");cover.layer=0;cover.transform.SetParent(loop.Map.transform,false);cover.transform.SetPositionAndRotation((from+to)*.5f,Quaternion.LookRotation(to-from,Vector3.up));cover.AddComponent<BoxCollider>().size=new Vector3(2,2,.3f);Physics.SyncTransforms();before=loop.Gameplay.Health+loop.Gameplay.Shield;
    yield return WaitWardenDischarge(loop,4,r=>last=r,note);Need(!boss.AreaAttack&&!last.clear&&last.obstruction==cover.name&&last.appliedPlayerDamage==0&&loop.Gameplay.Health+loop.Gameplay.Shield==before,"Actual finite world cover absorbs direct Warden attack");
    WardenShaderOracle(squad);note("Five production discharges alternate direct/area; real Health loss, actual supported ground mark, airborne/late-dodge/world-cover refusals verified. Declared player-pose controls, no forced enemy attack state. Constructed-cover damage, melee guard, phase2 discharge cadence, real-time combat, appearance/HMD remain separate.");
   }finally{if(cover!=null)UnityEngine.Object.Destroy(cover);loop.ReturnToLobby();mission.Restore(chapter,kills,seconds);loop.ValleyBots.SetDifficulty(difficulty);}
  }
 }
}
#endif
