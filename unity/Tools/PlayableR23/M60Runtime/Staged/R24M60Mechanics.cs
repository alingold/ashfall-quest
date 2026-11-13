using System;using Ashfall.Core;using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Modules.WeaponHandling {
 public sealed partial class WeaponHandling {
  bool m60,m60Contact,m60Previous=true,m60NeedsNeutral=true,m60BeltFed=true;R24M60Stage m60Stage,m60MotionStage;Float3 m60Start;float m60Progress;int m60Spool;readonly R24M60Feedback m60Recoil=new R24M60Feedback();
  internal void InitializeM60(){if(mechanism!=WeaponMechanism.ServiceRifle||capacity<75||capacity>135||(capacity-75)%15!=0||interval!=1f/7.2f)throw new InvalidOperationException("M60 source lifetime required");m60=true;m60Stage=R24M60Stage.Ready;seated=loaded;}
  public R24M60Stage M60Stage{get{return m60Stage;}}public bool M60Contact{get{return m60Contact;}}public R24M60Stage M60MotionStage{get{return m60MotionStage;}}public float M60Progress{get{return m60Progress;}}public bool M60BeltFed{get{return m60BeltFed;}}public int M60SpoolShots{get{return m60Spool;}}public bool M60LastShotSupported{get;private set;}
  public void SuspendM60Controls(){if(!m60||retired)return;Suspend();M60Cancel();m60Recoil.ClearPresentation();recoil=pitch=yaw=0;overrun=0;revision++;}
  public RuleResult ApplyM60(CommandContext command,HandlingAction action,HandlingInput input)
  {
   if(!m60||FireReservation!=null||!Enum.IsDefined(typeof(HandlingAction),action))return RuleResult.Reject("M60 unavailable");long before=revision;bool oldRelease=releaseTrigger,newTick=command.Tick!=tick;float oldCooldown=cooldown;var native=input;native.Trigger=false;
   // Native Apply validates/records the actual envelope, recovers time and samples
   // real support. The dedicated M60 state machine alone fires/reloads below.
   var result=Apply(command,HandlingAction.Fire,native);if(revision==before)return result;releaseTrigger=input.Trigger?oldRelease:false;
   if(newTick&&!input.Paused)m60Recoil.Advance(input.DeltaSeconds);
   if(!input.Enabled||input.Paused||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed){releaseTrigger=true;M60Cancel();M60SyncRecoil();return RuleResult.Reject("input unavailable");}
   if(!input.SupportTracked||input.SupportReserved)M60Cancel();
   if(action==HandlingAction.Eject)result=M60Begin(input);
   else if(action==HandlingAction.ReloadStep)result=input.Desktop?M60Desktop(input):RuleResult.Reject("physical box/feed/cover/charge required");
   if(action!=HandlingAction.ReloadStep&&input.Trigger&&!releaseTrigger)
   {
    if(idle>.6f)m60Spool=0;float sourceInterval=1f/(R24M60Definition.MaximumRps*(.5f+.5f*Math.Min(m60Spool/10f,1)));
    float sourceOverrun=newTick&&oldCooldown>0?Math.Max(0,Math.Min(sourceInterval,input.DeltaSeconds-oldCooldown)):0;
    if(m60Stage==R24M60Stage.Ready&&loaded>0&&cooldown<=.000001f&&lastShotTick!=tick){bool held=supported;loaded--;seated=loaded;shots++;pattern++;lastShotTick=tick;cooldown=Math.Max(.001f,sourceInterval-sourceOverrun);overrun=0;idle=0;M60LastShotSupported=held;m60Recoil.AcceptedShot(shots,held);m60Spool++;if(loaded==0)M60Begin(input);result=RuleResult.Accept();}
   }
   if(!input.Desktop&&input.SupportTracked&&!input.SupportReserved)M60Gesture(input);
   if(m60Stage!=R24M60Stage.Ready){supported=false;latched=false;}M60SyncRecoil();return action==HandlingAction.Sample?RuleResult.Accept():result;
  }
  void M60SyncRecoil(){recoil=m60Recoil.Distance/1.25f;pitch=m60Recoil.PitchRadians/1.25f;yaw=m60Recoil.YawRadians/1.25f;}
  void M60Set(R24M60Stage stage){m60Stage=stage;phase=stage==R24M60Stage.Ready?ReloadPhase.Ready:stage==R24M60Stage.BoxRemoved?ReloadPhase.NeedsMagazine:stage==R24M60Stage.HoldingBox?ReloadPhase.HeldMagazine:stage==R24M60Stage.NeedsCharge?ReloadPhase.NeedsBolt:ReloadPhase.NeedsRack;chambered=stage==R24M60Stage.Ready&&loaded>0;cooldown=.12f;overrun=0;if(stage!=R24M60Stage.Ready){supported=false;latched=false;}}
  RuleResult M60Begin(HandlingInput i){if(m60Stage!=R24M60Stage.Ready||i.SupportReserved&&loaded>0)return RuleResult.Reject("M60 reload unavailable");int remaining=loaded,returned=reserve.Refund(remaining);if(returned<0||returned>remaining)throw new InvalidOperationException("reserve refund contract");loaded=remaining-returned;seated=loaded;if(returned<remaining)return RuleResult.Reject("reserve refused remaining rounds");m60BeltFed=false;m60Contact=false;m60Previous=true;m60Progress=0;releaseTrigger=true;M60Set(R24M60Stage.BoxRemoved);return RuleResult.Accept();}
  void M60Cancel(){m60Contact=false;m60Previous=true;m60NeedsNeutral=true;m60Progress=0;boltTravel=0;if(m60Stage==R24M60Stage.HoldingBox)M60Set(R24M60Stage.BoxRemoved);}
  void M60Insert(){int taken=reserve.Take(capacity);if(taken<0||taken>capacity)throw new InvalidOperationException("reserve take contract");if(taken<=0){M60Set(R24M60Stage.BoxRemoved);return;}seated=taken;m60BeltFed=false;M60Set(R24M60Stage.NeedsFeed);}
  RuleResult M60Desktop(HandlingInput i){if(i.SupportReserved)return RuleResult.Reject("support unavailable");switch(m60Stage){case R24M60Stage.Ready:return M60Begin(i);case R24M60Stage.BoxRemoved:if(reserve.Available!=0){M60Set(R24M60Stage.HoldingBox);M60Insert();}break;case R24M60Stage.HoldingBox:M60Insert();break;case R24M60Stage.NeedsFeed:m60BeltFed=true;M60Set(R24M60Stage.BoxSeated);break;case R24M60Stage.BoxSeated:M60Set(R24M60Stage.NeedsCharge);break;case R24M60Stage.NeedsCharge:loaded=seated;releaseTrigger=true;M60Set(R24M60Stage.Ready);break;case R24M60Stage.CoverOpen:int remaining=seated,returned=reserve.Refund(remaining);if(returned<0||returned>remaining)throw new InvalidOperationException("reserve refund contract");loaded=seated=remaining-returned;if(returned<remaining)return RuleResult.Reject("reserve refused remaining rounds");M60Set(R24M60Stage.BoxRemoved);break;}return RuleResult.Accept();}
  void M60Gesture(HandlingInput i)
  {
   var point=i.SupportLocal;bool grip=i.Grip;if(releaseGrip){if(grip)return;releaseGrip=false;}
   if(!grip){m60Contact=false;m60Progress=0;m60NeedsNeutral=false;boltTravel=0;if(m60Stage==R24M60Stage.HoldingBox)M60Set(R24M60Stage.BoxRemoved);}
   else if(!m60Previous&&!m60Contact&&!m60NeedsNeutral&&R24M60Definition.Reach(point,R24M60Definition.Grab(m60Stage,i.LeftHanded),.105f)){m60Contact=true;m60Start=point;m60Progress=0;m60MotionStage=m60Stage;if(m60Stage==R24M60Stage.BoxRemoved&&reserve.Available!=0)M60Set(R24M60Stage.HoldingBox);}
   if(m60Contact&&grip){float travel;switch(m60Stage){case R24M60Stage.Ready:m60Contact=false;break;case R24M60Stage.HoldingBox:if(point.Y-m60Start.Y>=.13f&&R24M60Definition.Reach(point,R24M60Definition.Seat(i.LeftHanded),.09f)){M60Insert();m60Contact=false;}break;case R24M60Stage.NeedsFeed:travel=(point.X-m60Start.X)*(i.LeftHanded?-1:1);m60Progress=Math.Max(0,Math.Min(1,travel/.08f));if(travel>=.08f){m60BeltFed=true;M60Set(R24M60Stage.BoxSeated);m60Contact=false;}break;case R24M60Stage.BoxSeated:travel=m60Start.Y-point.Y;m60Progress=Math.Max(0,Math.Min(1,travel/.075f));if(travel>=.075f){M60Set(R24M60Stage.NeedsCharge);m60Contact=false;}break;case R24M60Stage.NeedsCharge:travel=m60Start.Z-point.Z;m60Progress=Math.Max(0,Math.Min(1,travel/.11f));boltTravel=.11f*m60Progress;if(travel>=.10f){loaded=seated;M60Set(R24M60Stage.Ready);releaseTrigger=true;m60Contact=false;boltTravel=0;}break;case R24M60Stage.CoverOpen:travel=m60Start.Y-point.Y;m60Progress=Math.Max(0,Math.Min(1,travel/.09f));if(travel>=.09f){int remaining=seated,returned=reserve.Refund(remaining);if(returned<0||returned>remaining)throw new InvalidOperationException("reserve refund contract");loaded=seated=remaining-returned;if(returned==remaining){M60Set(R24M60Stage.BoxRemoved);m60Contact=false;}}break;}}
   m60Previous=grip;
  }
 }
}
