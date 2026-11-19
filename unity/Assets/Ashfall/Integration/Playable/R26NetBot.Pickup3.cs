#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using UnityEngine;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable {
 public sealed partial class R25NetBot {
  readonly HashSet<long> pickup3ActiveEpochs=new HashSet<long>(),pickup3DropEpochs=new HashSet<long>(),pickup3GrabEpochs=new HashSet<long>();
  readonly HashSet<string> pickup3PreviousOwned=new HashSet<string>();long pickup3ObservedEpoch=-1;bool pickup3RecoveringLoss;
  int pickup3Cycles,pickup3Drops,pickup3Grabs,pickup3Wheels,pickup3Swaps,pickup3WheelDrops;string pickup3WheelPending;bool pickup3WasFull;float pickup3NextLog,pickup3NextFixture;string pickup3Target,pickup3Drop,pickup3LoggedTarget;int pickup3LastCycle=-1;bool pickup3Queued,pickup3Wheel;long pickup3Epoch=-1;
  // Opt in independently of the stock performance/combat routine; ordinary bots and XR are untouched.
  void OnEnable(){if(Arg("-ashfallNetBotPickup3")=="true"&&!UnityEngine.XR.XRSettings.isDeviceActive)StartCoroutine(Pickup3Run());}
  IEnumerator Pickup3Run(){
   while(root==null||log==null||root.LanSession.Lobby==null){if(closed)yield break;yield return null;}
   root.WeaponWheel.RequestAccepted+=Pickup3WheelAccepted;enabled=false;requestedMap=true;
   float started=Time.realtimeSinceStartup,entered=-1;int transition=0;bool opened=false,fallen=false,faulted=false;var checks=new List<string>();
   while(!closed&&Time.realtimeSinceStartup-started<duration+120){
    if(probe!=null)probe.Frame();float now=Time.realtimeSinceStartup,dt=Mathf.Clamp(Time.unscaledDeltaTime,0,.1f);var lobby=root.LanSession.Lobby;
    if(lobby==null){if(entered>=0){Write("pickup3-session-ended",root.UI.LanStatus);break;}yield return null;continue;}
    if(lobby.MemberCount>1)connected=true;
    if(mode=="host"&&connected&&!opened&&now-started>3){opened=root.LanSession.Pickup3OpenWarmup(root.Map.MapId);Write("pickup3-transition","warmup requested="+opened);}
    bool active=root.Ready&&!root.Lobby.Visible&&!root.LanSession.MapEntryBlocked&&lobby.SelectedMap!=null&&lobby.SelectedMap.Active;
    if(active&&entered<0){entered=now;playBegan=now;if(probe!=null)probe.PlayBegan=now;played=true;Write("pickup3-entry","warmup");}
    float age=entered<0?0:now-entered;float stageAge=Arg("-ashfallNetBotPickup3Quick")=="true"?age*4:age;
    if(lobby.Epoch!=pickup3Epoch){pickup3Epoch=lobby.Epoch;pickup3LastCycle=-1;pickup3Target=pickup3Drop=null;Write("pickup3-epoch",root.Pickup3State);}
    if(mode=="host"&&entered>=0){
     if(transition==0&&stageAge>=45){root.LanSession.RouteRoundRequest(Pickup3Rules());transition++;Write("pickup3-transition","START MATCH");}
     else if(transition==1&&stageAge>=90){root.RestartMatch();transition++;Write("pickup3-transition","round restart");}
     else if(transition==2&&stageAge>=160){root.LanSession.Pickup3OpenWarmup("archive:battlefield-paintball-gods-pitdevil");transition++;Write("pickup3-transition","map change paintball");}
     else if(transition==3&&stageAge>=205){root.ReturnToLobby();transition++;Write("pickup3-transition","Home room lobby");}
     else if(transition==4&&stageAge>=220){root.LanSession.Pickup3OpenWarmup(root.Map.MapId);transition++;Write("pickup3-transition","same map after Home");}
    }
    active=root.Ready&&!root.Lobby.Visible&&!root.LanSession.MapEntryBlocked&&lobby.SelectedMap!=null&&lobby.SelectedMap.Active&&!lobby.SelectedMap.Cancelled&&!lobby.SelectedMap.Home;
    if(active){pickup3ActiveEpochs.Add(lobby.Epoch);Pickup3ObserveOwned(lobby);
     if(age>=75&&!faulted&&Arg("-ashfallNetBotPickup3Fault")=="true"){faulted=true;pickup3RecoveringLoss=true;root.Pickup3LoseLocalCustody();Write("pickup3-injected-local-loss",root.Pickup3State);}
     if(mode=="host"&&stageAge>=125&&!fallen){fallen=true;foreach(var member in lobby.Members){R24LanHealthState life;if(lobby.TryReadHealth(member.Actor,out life)){var result=lobby.ApplyHostHealthFall(member.Actor,life.Revision);Write("pickup3-authority-fall",member.Actor+" accepted="+result.Accepted+" reason="+result.Reason);}}Write("pickup3-transition","host Health owner fall/death for both peers; normal respawn");}
     bool alive=root.Gameplay.Health>0;R24LanHealthState health;
     if(lobby.TryReadHealth(lobby.OwnActor,out health)&&now>=pickup3NextLog)Write("pickup3-health","life="+health.Life+" deaths="+health.Deaths+" spawn="+health.SpawnRevision);
     Vector3 head=root.Rig.transform.position+Vector3.up*1.7f,left=head+root.Rig.transform.right*-.3f+root.Rig.transform.forward*.3f-Vector3.up*.4f,right=head+root.Rig.transform.right*.3f+root.Rig.transform.forward*.4f-Vector3.up*.4f;
     Quaternion aim=root.Rig.transform.rotation;float grip=0;bool primary=false;
     if(alive){
      if(pickup3WheelPending!=null&&!root.Gameplay.OwnsWeapon(pickup3WheelPending)){pickup3WheelDrops++;pickup3DropEpochs.Add(lobby.Epoch);Write("pickup3-wheel-drop-confirmed",pickup3WheelPending);pickup3WheelPending=null;}
      int cycle=(int)(age/20);float phase=age%20;
      if(cycle!=pickup3LastCycle){pickup3Cycles++;pickup3LastCycle=cycle;pickup3Queued=pickup3Wheel=false;pickup3Target=pickup3Drop=null;var owned=root.Gameplay.OwnedWeaponActionIds;if(owned.Length>0){root.NetBotEquipOwned(owned[0]);pickup3Drop=root.Gameplay.ActiveWeaponActionId;}Write("pickup3-cycle","cycle="+cycle+" "+root.Pickup3State);}
      // Test fixture is a native world lifetime. Discovery and every collect still pass host custody.
      if(mode=="host"&&now>=pickup3NextFixture&&age<260){pickup3NextFixture=now+20;foreach(var member in lobby.Members){R24LanPoseSnapshot pose;if(lobby.TryReadPresence(member.Actor,out pose)){string id;Vector3 p=V(pose.Pose.Root.Position)+Vector3.up*1.1f;root.Gameplay.SpawnWorldWeapon("cx4",1,p,Quaternion.identity,out id);Write("pickup3-fixture",member.Actor+" "+id);}}}
      if(phase>=2&&phase<3&&!pickup3Queued&&pickup3Drop!=null){pickup3Queued=true;root.Pickup3QueueDrop(pickup3Drop);Write("pickup3-drop-request",pickup3Drop);}
      if(pickup3Drop!=null&&!root.Gameplay.OwnsWeapon(pickup3Drop)&&pickup3Queued){pickup3Drops++;pickup3DropEpochs.Add(lobby.Epoch);Write("pickup3-drop-confirmed",pickup3Drop);pickup3Drop=null;}
      if(phase>=5&&phase<14){
       if(pickup3Target==null){float best=4;foreach(string id in root.Gameplay.WorldWeaponActionIds){string instance;Vector3 center,p;Quaternion q;if(root.Gameplay.TryReadWorldWeapon(id,out instance,out center,out p,out q)){float d=(center-root.Rig.transform.position).sqrMagnitude;if(d<best&&!Physics.Linecast(root.Rig.Head.transform.position,center,1<<0,QueryTriggerInteraction.Ignore)){best=d;pickup3Target=id;pickup3WasFull=root.Gameplay.OwnedWeaponActionIds.Length>=3;}}}}
       if(pickup3Target!=null&&pickup3Target!=pickup3LoggedTarget){pickup3LoggedTarget=pickup3Target;Write("pickup3-target",pickup3Target+" "+root.Pickup3TargetState(pickup3Target));}
       string ident;Vector3 target,p0;Quaternion q0;if(pickup3Target!=null&&root.Gameplay.TryReadWorldWeapon(pickup3Target,out ident,out target,out p0,out q0)){
        if(Physics.Linecast(root.Rig.Head.transform.position,target,1<<0,QueryTriggerInteraction.Ignore)){pickup3Target=null;grip=0;}else{right=target-root.Rig.transform.forward*.12f;aim=Quaternion.LookRotation(target-right)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);grip=phase%2<1.6f?.9f:0;}
       }
       if(pickup3Target!=null&&root.Gameplay.OwnsWeapon(pickup3Target))pickup3Target=null;else if(pickup3Target!=null&&!root.Gameplay.TryReadWorldWeapon(pickup3Target,out ident,out target,out p0,out q0))pickup3Target=null;
      }
      if(phase>=15&&phase<19.3f){primary=true;if(root.WeaponWheel.Active){var point=root.WeaponWheel.Pickup3SlotPoint(0);aim=Quaternion.LookRotation(point-right)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);}if(!pickup3Wheel){pickup3Wheel=true;Write("pickup3-wheel-hold","slot0");}}
      if(phase>=19.3f&&pickup3Wheel){pickup3Wheels++;pickup3Wheel=false;Write("pickup3-wheel-ended",root.Pickup3State);}
     }
     root.Rig.NetBotSample(Vector2.zero,1.7f,left,right,aim,grip,0,dt);root.Rig.Pickup3Primary(primary);
     if(now>=pickup3NextLog){pickup3NextLog=now+1;Write("pickup3-state",root.Pickup3State);}
     if(captures<3&&age>=(captures==0?12:captures==1?105:245))Capture(400+captures++);
    }
    if(entered>=0&&age>=duration)break;
    yield return null;
   }
   checks.Add("cycles="+pickup3Cycles);checks.Add("drops="+pickup3Drops);checks.Add("grabs="+pickup3Grabs);checks.Add("wheelGestures="+pickup3Wheels);checks.Add("wheelDrops="+pickup3WheelDrops);checks.Add("fullInventories="+pickup3Swaps);
   int covered=0;foreach(long ep in pickup3ActiveEpochs)if(pickup3DropEpochs.Contains(ep)&&pickup3GrabEpochs.Contains(ep))covered++;checks.Add("epochsWithDropsAndGrabs="+covered+"/"+pickup3ActiveEpochs.Count);bool matrix=Arg("-ashfallNetBotPickup3Quick")=="true"||covered==pickup3ActiveEpochs.Count&&covered>=5;
   bool ok=matrix&&pickup3Drops>0&&pickup3Grabs>0&&pickup3WheelDrops>0&&entered>=0&&Time.realtimeSinceStartup-entered>=duration-(mode=="host"?.1f:3f);Write("pickup3-summary",String.Join(" ",checks.ToArray()),ok?checks.ToArray():new string[0],ok?new string[0]:new[]{"pickup3-operation-missing"});
   closed=true;Passed=ok;Finished=true;root.LanSession.Leave();if(probe!=null){probe.Dispose();probe=null;}log.Dispose();log=null;if(!Application.isEditor)Application.Quit(ok?0:2);
  }
  void Pickup3ObserveOwned(R24LanLobby lobby){
   var owned=root.Gameplay.OwnedWeaponActionIds;
   if(pickup3ObservedEpoch!=lobby.Epoch||pickup3RecoveringLoss){pickup3ObservedEpoch=lobby.Epoch;if(pickup3RecoveringLoss&&owned.Length>0){pickup3RecoveringLoss=false;Write("pickup3-recovery-observed",root.Pickup3State);}pickup3PreviousOwned.Clear();foreach(string id in owned)pickup3PreviousOwned.Add(id);return;}
   foreach(string id in owned)if(!pickup3PreviousOwned.Contains(id)&&root.Rig.RightInput.Grip>=.65f){pickup3Grabs++;pickup3GrabEpochs.Add(lobby.Epoch);if(pickup3WasFull||pickup3PreviousOwned.Count>=3)pickup3Swaps++;Write("pickup3-grab-confirmed",id+" slots="+owned.Length+" requested="+(pickup3Target??"none"));}
   pickup3PreviousOwned.Clear();foreach(string id in owned)pickup3PreviousOwned.Add(id);
  }
  void Pickup3WheelAccepted(Ashfall.Integration.Playable.Input.R23WeaponWheelRequest request){if(request.Action==Ashfall.Integration.Playable.Input.R23WeaponWheelAction.Drop){pickup3WheelPending=request.Id;Write("pickup3-wheel-drop-accepted",request.Id);}}
  R24PlayableMatchRules Pickup3Rules(){return R24PlayableMatchRules.ForPreset("free_for_all",root.Map.MapId).With("zone_enabled",false).With("damage",0f).With("protection",0f).With("respawn_delay",2f);}
 }
}
#endif
