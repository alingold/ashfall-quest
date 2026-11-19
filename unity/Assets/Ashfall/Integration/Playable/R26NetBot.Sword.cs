#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable {
 // SWORD r15 evidence (-ashfallNetBotSword attacker|target). Host starts a swords-loadout match. The attacker walks to
 // the stationary target, then runs: open (10 swings), behind (10 swings with a test wall .35 m behind the target, so
 // the blade tip enters the wall mid-swing), stab (blade pushed into that wall beside the target for 3 s: the local sword
 // must stay drawn), between (6 swings with the wall between attacker and target: zero hits allowed). The attacker's left
 // hand signals the phase (raised = wall behind, low = wall between) so both peers build the same wall. Every swing logs
 // whether a host hit confirm reached the attacker's damage-number presenter.
 public sealed partial class R25NetBot {
  sealed class SwordSegment{internal string Name;internal int Swings,Hits,Confirms;}
  string swordRole;bool swordRequested,swordInPlace,swordSelected,swordDone;float swordAttackerSeen,swordProbeAt,swordEquipAt,swordLiveAt=-1,swordSegmentAt=-1,swordNextLog,swordHp=-1;int swordSegment,swordSwing=-1,swordSwingStartCount,swordTipInWall,swordHidden,swordShownFrames,swordWallSignal;
  string swordWallKind="none";GameObject swordWall;BoxCollider swordWallBox;Transform swordWeaponRoot;Vector3 swordFacing;
  readonly List<SwordSegment> swordSegments=new List<SwordSegment>{new SwordSegment{Name="open",Swings=10},new SwordSegment{Name="behind",Swings=10},new SwordSegment{Name="stab",Swings=0},new SwordSegment{Name="between",Swings=6}};
  const float SwordRest=.9f,SwordStroke=.3f,SwordSettle=1.5f,SwordReach=1.0f,SwordYaw=70;
  partial void TickSwordEvidence(ref bool handled){handled=TickSwordEvidence();}
  bool TickSwordEvidence(){
   if(swordRole==null){swordRole=Arg("-ashfallNetBotSword");if(swordRole!="attacker"&&swordRole!="target")swordRole="";else Write("sword-boot","role="+swordRole+" mode="+mode);}
   if(swordRole.Length==0)return false;if(closed)return true;
   float now=Time.realtimeSinceStartup,dt=Mathf.Min(Time.unscaledDeltaTime,.1f);var lobby=root.LanSession.Lobby;
   bool live=lobby!=null&&(lobby.State==R24LanLobbyState.Hosting||lobby.State==R24LanLobbyState.Connected);
   if(live&&lobby.Members.Length>1)connected=true;
   if(now-began>duration+90){SwordFinish("timeout");return true;}
   if(lobby==null&&swordInPlace&&swordRole=="target"){SwordFinish("session-ended");return true;}
   if(swordRole=="target"&&swordInPlace&&swordAttackerSeen>0&&now-swordAttackerSeen>5){Write("sword-target-done","attacker left; hp="+swordHp.ToString("F2"));SwordFinish(null);return true;}
   if(mode=="host"&&live&&lobby.Members.Length>1&&!swordRequested&&now-began>10){swordRequested=true;var rules=R24PlayableMatchRules.ForPreset("free_for_all",root.Map.MapId).With("zone_enabled",false).With("loadout","swords").With("protection",0).With("starting_shield",0).With("damage",.04f);Write("sword-start-round","routed="+root.LanSession.RouteRoundRequest(rules)+" map="+root.Map.MapId);}
   R24LanPoseSnapshot other=null;if(live)foreach(var member in lobby.Members)if(member.Actor!=lobby.OwnActor){R24LanPoseSnapshot pose;if(lobby.TryReadPresence(member.Actor,out pose)&&now-pose.ReceivedAt<=1&&pose.Pose.Tracked){other=pose;presence=true;}}
   Vector3 rootAt=root.Rig.transform.position;Vector2 move=Vector2.zero;
   Vector3 head=rootAt+Vector3.up*1.7f,left=head+root.Rig.transform.rotation*new Vector3(-.32f,-.6f,.1f),right=head+root.Rig.transform.rotation*new Vector3(.32f,-.6f,.1f);Quaternion aim=root.Rig.transform.rotation*Quaternion.Euler(80,0,0);
   bool matchLive=live&&root.Pickup4Live;if(matchLive&&!played){played=true;playBegan=now;Write("sword-live","weapon="+root.Gameplay.ActiveWeaponId+" primary="+root.Gameplay.PrimaryHand);}
   // Equip through the normal owned-item path (wheel equivalent): HasWeapon must be true, not just a selected profile.
   if(matchLive&&swordRole=="attacker"&&(!root.Gameplay.HasWeapon||R24MeleeProfiles.ForId(root.Gameplay.ActiveWeaponId)==null)&&now>=swordEquipAt){swordEquipAt=now+1;foreach(var id in root.Gameplay.OwnedWeaponActionIds){var profile=R24MeleeProfiles.ForId(id);if(profile==null||profile.Id=="knife")continue;bool ok=root.NetBotEquipOwned(id);Write("sword-equip",id+" ok="+ok+" has="+root.Gameplay.HasWeapon+" active="+root.Gameplay.ActiveWeaponId);if(ok)break;}}
   if(matchLive&&swordRole=="target"&&root.Gameplay.HasWeapon&&now>=swordEquipAt){swordEquipAt=now+1;root.Gameplay.HolsterCurrent();}
   // The motor needs a neutral stick after spawn/focus before it accepts motion again.
   if(matchLive&&swordLiveAt<0)swordLiveAt=now;
   if(matchLive&&!swordSelected&&root.Gameplay.HasWeapon&&R24MeleeProfiles.ForId(root.Gameplay.ActiveWeaponId)!=null){swordSelected=true;picked=true;Write("sword-selected",root.Gameplay.ActiveWeaponId+" action="+root.Gameplay.ActiveWeaponActionId);}
   if(matchLive&&other!=null){
    Vector3 otherRoot=V(other.Pose.Root.Position);Vector3 toward=Vector3.ProjectOnPlane(otherRoot-rootAt,Vector3.up);float distance=toward.magnitude;
    if(toward.sqrMagnitude>.01f)root.Rig.transform.rotation=Quaternion.RotateTowards(root.Rig.transform.rotation,Quaternion.LookRotation(toward),dt*180);
    if(swordRole=="target"){swordAttackerSeen=now;if(!swordInPlace&&distance>4)move=Vector2.ClampMagnitude(new Vector2(0,1)*.3f*distance,.6f);else if(!swordInPlace){swordInPlace=true;Write("sword-target-ready","distance="+distance.ToString("F2"));}
     // Phase signal from the attacker's left hand: raised = wall behind me, low = wall between us.
     float lift=other.Pose.Left.Position.Y-other.Pose.Head.Position.Y;int signal=lift>.1f?1:lift<-1f?2:0;if(signal!=swordWallSignal){swordWallSignal=signal;SwordPlaceWall(signal==1?"behind":signal==2?"between":"none",otherRoot,rootAt);}
     R24LanHealthState h;if(AiHealth(lobby,lobby.OwnActor,out h)&&h.Health+h.Shield!=swordHp){swordHp=h.Health+h.Shield;Write("sword-target-health","hp="+swordHp.ToString("F2")+" life="+h.Life);}}
    else SwordAttack(now,dt,lobby,otherRoot,distance,ref move,ref left,ref right,ref aim);
   }
   if(now>=swordNextLog){if(mode!="host"||!live)swordNextLog=now+3;R24LanPoseSnapshot any=null;if(live)foreach(var member in lobby.Members)if(member.Actor!=lobby.OwnActor){R24LanPoseSnapshot pose;if(lobby.TryReadPresence(member.Actor,out pose))any=pose;}Write("sword-status","live="+live+" matchLive="+matchLive+" other="+(other!=null)+" anyPresence="+(any!=null)+(any!=null?" age="+(now-any.ReceivedAt).ToString("F2")+" tracked="+any.Pose.Tracked+" otherRoot="+V(any.Pose.Root.Position).ToString("F1"):"")+" root="+rootAt.ToString("F1")+" weapon="+root.Gameplay.ActiveWeaponId+" hasWeapon="+root.Gameplay.HasWeapon+" inPlace="+swordInPlace+" seg="+swordSegment+(lobby!=null&&mode=="host"?" rawRejected="+lobby.RejectedRawTracking+" lastRawReject="+lobby.LastRawTrackingRejection:""));}
   if(mode=="host"&&live&&now>=swordNextLog){swordNextLog=now+3;Write("sword-host-melee","samples="+lobby.MeleeHostSamples+" resets="+lobby.MeleeHostResets+" lastReset="+lobby.MeleeHostLastReset+" bunched="+lobby.MeleeHostBunched+" split="+lobby.MeleeHostSplit+" occluded="+lobby.MeleeHostOccluded+" rewound="+lobby.MeleeHostRewound+" entries="+lobby.MeleeHostEntries+" queued="+lobby.MeleeHostQueued+" applied="+lobby.MeleeHostApplied+" confirmsQueued="+lobby.HitConfirmsQueued+" confirmsSent="+lobby.HitConfirmsSent);}
   if(swordLiveAt<0||now-swordLiveAt<1.5f||(now-swordLiveAt)%12<.25f)move=Vector2.zero;
   moved|=move.sqrMagnitude>0;root.Rig.NetBotSample(move,1.7f,left,right,aim,0,0,dt);
   if(swordDone&&now-swordSegmentAt>2)SwordFinish(null);
   return true;
  }
  int SwordConfirmCount{get{return root.LanSession.HitConfirmsPresented;}}
  void SwordAttack(float now,float dt,R24LanLobby lobby,Vector3 target,float distance,ref Vector2 move,ref Vector3 left,ref Vector3 right,ref Quaternion aim){
   if(!swordInPlace){if(distance>SwordReach+.02f){var local=root.Rig.transform.InverseTransformDirection(Vector3.ProjectOnPlane(target-root.Rig.transform.position,Vector3.up));move=Vector2.ClampMagnitude(new Vector2(local.x,local.z)*.4f,distance<2?.2f:.6f);return;}
    if(!swordSelected)return;swordInPlace=true;swordFacing=Vector3.ProjectOnPlane(target-root.Rig.transform.position,Vector3.up).normalized;swordSegmentAt=now;Write("sword-attacker-ready","distance="+distance.ToString("F2")+" weapon="+root.Gameplay.ActiveWeaponId);}
   if(swordDone)return;var seg=swordSegments[swordSegment];var rig=root.Rig.transform;Vector3 rootAt=rig.position,fwd=swordFacing,side=Vector3.Cross(Vector3.up,fwd);
   string wall=seg.Name=="stab"?"behind":seg.Name=="open"?"none":seg.Name;if(swordWallKind!=wall)SwordPlaceWall(wall,rootAt,target);
   Vector3 head=rootAt+Vector3.up*1.7f;left=seg.Name=="behind"||seg.Name=="stab"?head+side*-.3f+Vector3.up*.3f:seg.Name=="between"?head+side*-.3f-Vector3.up*1.15f:head+side*-.32f-Vector3.up*.35f+fwd*.2f;
   Vector3 hand=rootAt+Vector3.up*1.35f+fwd*.35f;float t=now-swordSegmentAt-SwordSettle,cycle=SwordRest+SwordStroke;float yaw;
   if(seg.Name=="stab"){float push=Mathf.Clamp01(t/1f);hand+=side*.55f+fwd*(.15f*push);yaw=0;if(t>4){SwordNextSegment(now);}}
   else{int index=t<0?-1:Mathf.FloorToInt(t/cycle);float within=t<0?0:t-index*cycle;
    if(index>=seg.Swings){if(within>1||index>seg.Swings){if(swordSwing>=0)SwordEndSwing(seg);SwordNextSegment(now);}index=seg.Swings;}
    float s=index<0||(index&1)==0?1:-1;yaw=s*SwordYaw;
    if(index>=0&&index<seg.Swings&&within>=SwordRest){if(swordSwing!=index){if(swordSwing>=0)SwordEndSwing(seg);swordSwing=index;swordSwingStartCount=SwordConfirmCount;}float k=Mathf.SmoothStep(0,1,(within-SwordRest)/SwordStroke);yaw=Mathf.Lerp(s*SwordYaw,-s*SwordYaw,k);hand+=side*Mathf.Lerp(s*-.12f,s*.12f,k);}
    else if(index>0&&index<=seg.Swings)yaw=-(((index-1)&1)==0?1:-1)*SwordYaw;}
   var dir=Quaternion.AngleAxis(yaw,Vector3.up)*fwd;right=hand;aim=Quaternion.LookRotation(dir,Vector3.up);
   // SWORD-VANISH probe: the held blade must stay drawn while its local tip is inside the wall.
   var diag=root.Gameplay.KnifeDiagnostics;if(SwordBladeInWall(diag)){swordTipInWall++;if(SwordDrawn())swordShownFrames++;else{swordHidden++;if(swordHidden<=5)Write("sword-vanish","hidden while tip in wall frame="+swordTipInWall+" seg="+seg.Name+" "+SwordProbe());}}
   if(now>=swordProbeAt){swordProbeAt=now+1;bool inWall=SwordBladeInWall(diag);Write("sword-probe",seg.Name+" t="+t.ToString("F1")+" usable="+diag.Usable+" tip="+diag.Tip.ToString("F2")+" hand="+hand.ToString("F2")+" inWall="+inWall+" drawn="+SwordDrawn()+" "+SwordProbe());}
  }
  void SwordEndSwing(SwordSegment seg){int delta=SwordConfirmCount-swordSwingStartCount;if(delta>0){seg.Hits++;seg.Confirms+=delta;}Write("sword-swing",seg.Name+" #"+swordSwing+" confirmed="+(delta>0)+" confirms="+delta+" presented="+SwordConfirmCount+" received="+root.LanSession.Lobby.HitConfirmsReceived+" damageNumbers="+(root.CombatDamageNumbers!=null));swordSwing=-1;}
  void SwordNextSegment(float now){var seg=swordSegments[swordSegment];Write("sword-segment",seg.Name+" swings="+seg.Swings+" hits="+seg.Hits+" confirms="+seg.Confirms+" tipInWall="+swordTipInWall+" hidden="+swordHidden);swordSwing=-1;swordSegmentAt=now;swordSegment++;if(swordSegment>=swordSegments.Count){swordSegment=swordSegments.Count-1;swordDone=true;}}
  string SwordProbe(){var m=SwordModel();if(m==null)return "model=missing";var w=m.parent;int all=0,on=0,forced=0,inactive=0;if(m!=null)foreach(var r in m.GetComponentsInChildren<Renderer>(true)){all++;if(!r.gameObject.activeInHierarchy)inactive++;else if(r.forceRenderingOff)forced++;else if(r.enabled)on++;}
   return "rootActive="+w.gameObject.activeInHierarchy+" rootSelf="+w.gameObject.activeSelf+" parentActive="+(w.parent==null||w.parent.gameObject.activeInHierarchy)+" model="+(m!=null)+" modelActive="+(m!=null&&m.gameObject.activeInHierarchy)+" renderers="+all+" on="+on+" forced="+forced+" inactive="+inactive+" obstructed="+root.Gameplay.WeaponPresentationObstructed+" has="+root.Gameplay.HasWeapon+" view="+(root.Gameplay.ViewObstructed!=null&&root.Gameplay.ViewObstructed());}
  // The held model: the "R23 source <id>" transform nearest the weapon hand (the lobby gallery has its own copies).
  Transform SwordModel(){string name="R23 source "+root.Gameplay.ActiveWeaponId;if(swordWeaponRoot!=null&&swordWeaponRoot.name==name)return swordWeaponRoot;Transform best=null;float d=4;foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name==name){float e=(t.position-root.Rig.Right.position).sqrMagnitude;if(e<d){d=e;best=t;}}swordWeaponRoot=best;return best;}
  bool SwordBladeInWall(R24KnifeDiagnostics diag){if(swordWallBox==null||!swordWall.activeInHierarchy||!diag.Usable)return false;for(int i=0;i<=10;i++){var p=Vector3.Lerp(diag.Base,diag.Tip,i/10f);if((swordWallBox.ClosestPoint(p)-p).sqrMagnitude<1e-6f)return true;}return false;}
  bool SwordDrawn(){if(root.Gameplay.WeaponPresentationObstructed)return false;var model=SwordModel();if(model==null||!model.gameObject.activeInHierarchy)return false;foreach(var r in model.GetComponentsInChildren<Renderer>())if(r.enabled&&!r.forceRenderingOff)return true;return false;}
  // One 3 x 2.5 x .1 m world box (layer 0, so host ballistic/raw world queries see it): .35 m behind the target's back, or .6 m in front of the attacker.
  void SwordPlaceWall(string kind,Vector3 attacker,Vector3 target){swordWallKind=kind;if(kind=="none"){if(swordWall!=null)swordWall.SetActive(false);Physics.SyncTransforms();Write("sword-wall","none");return;}
   if(swordWall==null){swordWall=GameObject.CreatePrimitive(PrimitiveType.Cube);swordWall.name="R26 sword evidence wall";swordWall.layer=0;swordWallBox=swordWall.GetComponent<BoxCollider>();}
   var n=Vector3.ProjectOnPlane(target-attacker,Vector3.up).normalized;var at=kind=="behind"?target+n*.40f:attacker+n*.6f;swordWall.transform.SetPositionAndRotation(new Vector3(at.x,target.y+1.25f,at.z),Quaternion.LookRotation(n));swordWall.transform.localScale=new Vector3(3,2.5f,.1f);swordWall.SetActive(true);Physics.SyncTransforms();Write("sword-wall",kind+" at="+swordWall.transform.position.ToString("F2"));}
  void SwordFinish(string failure){if(closed)return;var pass=new List<string>();var fail=new List<string>();
   Check(pass,fail,"second-member",connected);Check(pass,fail,"match-live",played);
   if(swordRole=="attacker"){var open=swordSegments[0];var behind=swordSegments[1];var between=swordSegments[3];
    Check(pass,fail,"sword-held",swordSelected);Check(pass,fail,"all-segments",swordDone);Check(pass,fail,"open-hit-rate>=80%",open.Hits*5>=open.Swings*4);Check(pass,fail,"wall-behind-hit-rate>=80%",behind.Hits*5>=behind.Swings*4);Check(pass,fail,"wall-between-no-hits",between.Hits==0);
    Check(pass,fail,"blade-entered-wall",swordTipInWall>0);Check(pass,fail,"sword-never-hidden-in-wall",swordHidden==0);Check(pass,fail,"damage-number-presenter",root.CombatDamageNumbers!=null);
    Write("sword-result","open="+open.Hits+"/"+open.Swings+" behind="+behind.Hits+"/"+behind.Swings+" between="+between.Hits+"/"+between.Swings+" confirmsPresented="+SwordConfirmCount+" bladeInWallFrames="+swordTipInWall+" shown="+swordShownFrames+" hidden="+swordHidden);}
   if(swordRole=="target")Check(pass,fail,"took-blade-damage",swordHp>=0&&swordHp<100);
   if(failure!=null)fail.Add(failure);bool passed=fail.Count==0;Write("sword-summary","passed="+passed,pass.ToArray(),fail.ToArray());
   closed=true;Passed=passed;Finished=true;root.LanSession.Leave();if(probe!=null){probe.Dispose();probe=null;}log.Dispose();log=null;if(!Application.isEditor)Application.Quit(passed?0:2);}
 }
}
#endif
