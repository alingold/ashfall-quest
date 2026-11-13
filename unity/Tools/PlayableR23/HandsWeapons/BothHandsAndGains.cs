using System;
using Ashfall.Core;
using Ashfall.Integration.DesktopPlaytest.Combat;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.WeaponHandling;
static class BothHandsAndGains
{
 sealed class Sight:IInteractionQuery { public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return true;} }
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static R23HandInput Hand(float grip=0,float trigger=0,bool reserved=false,bool primary=false,bool tracked=true){return new R23HandInput(tracked,reserved,grip,trigger,false,false,primary);}
 static HandlingInput Project(R23HandInput left,R23HandInput right,int primary,Float3 point){return R23HandRoleRules.Project(new R23HandsInputSnapshot(left,right),primary,point,.5f);}
 public static void Run(){
  var frame=new R23HandsInputFrame{LeftTracked=true,RightTracked=true,Grip=.7f,Trigger=.8f,ReloadPressed=true,SupportGrip=.6f};var legacy=frame.Snapshot;
  Check(legacy.Right.GripHeld&&legacy.Right.TriggerHeld&&legacy.Right.Primary&&legacy.Left.GripHeld&&!legacy.Left.TriggerHeld&&!legacy.Left.Primary,"legacy grip/trigger/reload remain right; support grip remains physical left");
  frame.LeftGrip=.9f;frame.LeftTrigger=.8f;frame.LeftPrimary=true;var frozen=frame.Snapshot;frame.LeftGrip=frame.LeftTrigger=0;frame.LeftPrimary=false;
  Check(frozen.Left.Grip==.9f&&frozen.Left.TriggerHeld&&frozen.Left.Primary,"resolved hand snapshot is immutable after compatibility frame changes");
  var both=new R23HandsInputSnapshot(Hand(.8f),Hand(.8f));
  Check(R23HandRoleRules.ChoosePickupHand(both,.05f,.2f)==0,"nearest free tracked LEFT grip wins source pickup");
  Check(R23HandRoleRules.ChoosePickupHand(both,.2f,.05f)==1&&R23HandRoleRules.ChoosePickupHand(both,.1f,.1f)==1,"nearest right grip wins; equal-distance legacy tie stays right");
  Check(R23HandRoleRules.ChoosePickupHand(new R23HandsInputSnapshot(Hand(.8f,0,true),Hand(.8f)),.01f,.2f)==1,"climbing reservation prevents nearer left hand stealing pickup");
  Check(R23HandRoleRules.ChoosePickupHand(new R23HandsInputSnapshot(Hand(.8f,0,true),Hand(.8f,0,true)),.01f,.01f)==-1&&R23HandRoleRules.ChoosePickupHand(both,float.NaN,.5f)==-1,"reserved/out-of-reach/invalid candidates cannot pick up");
  var profile=SourceRifleDefinition.Akm;var gun=new LocalRifleSandbox(new string[0],new Sight(),profile,new R23RifleReserve(),"two-hands/akm");
  var leftFire=Project(Hand(.8f,.8f),Hand(0,0),0,profile.Offer);
  Check(leftFire.LeftHanded&&leftFire.Trigger&&!leftFire.Grip&&gun.Step(.1f,leftFire,false,false)&&gun.Snapshot.Loaded==19&&gun.Reserve==2400,"LEFT-primary projected source fire debits exact existing AKM once");
  var leftOnlyTrigger=Project(Hand(.8f,.8f),Hand(0,0),1,profile.Charging);gun.SuspendControls();
  Check(!leftOnlyTrigger.Trigger&&leftOnlyTrigger.Grip&&!leftOnlyTrigger.LeftHanded&&!gun.Step(.1f,leftOnlyTrigger,false,false),"primary hand switch routes old firing hand into support without cross-hand trigger");
  var rightFire=Project(Hand(.8f,0),Hand(.8f,.8f),1,profile.Charging);
  Check(gun.Step(.1f,rightFire,false,false)&&gun.Snapshot.Loaded==18&&gun.InstanceId=="two-hands/akm"&&gun.Reserve==2400,"RIGHT hand takes same source instance after release with no switch grant");
  gun.SuspendControls();gun.Step(.1f,Project(Hand(.8f,0),Hand(0),0,profile.Offer),false,false);
  gun.Step(0,Project(Hand(.8f,0),Hand(0),0,profile.Offer),true,false);
  gun.Step(.01f,Project(Hand(.8f,0),Hand(.8f,0,true),0,profile.Offer),false,false);
  Check(gun.Snapshot.Phase==ReloadPhase.NeedsMagazine&&gun.Reserve==2418&&!gun.Snapshot.ClaimsSupport,"RIGHT climb reservation blocks LEFT-primary support reload claim without ammo debit");
  gun.Step(.01f,Project(Hand(.8f,0),Hand(0),0,profile.Offer),false,false);
  gun.Step(.01f,Project(Hand(.8f,0),Hand(.8f),0,profile.Offer),false,false);
  Check(gun.Snapshot.Phase==ReloadPhase.HeldMagazine&&gun.Reserve==2418,"left-primary physical reload uses RIGHT support grip on real source offer");
  gun.Step(.01f,Project(Hand(.8f,0),Hand(.8f),0,profile.Insert),false,false);
  gun.Step(.01f,Project(Hand(.8f,0),Hand(0),0,profile.Charging),false,false);gun.Step(.01f,Project(Hand(.8f,0),Hand(.8f),0,profile.Charging),false,false);
  var pulled=new Float3(profile.Charging.X,profile.Charging.Y,profile.Charging.Z-.08f);gun.Step(.01f,Project(Hand(.8f,0),Hand(.8f),0,pulled),false,false);
  Check(gun.Snapshot.Chambered&&gun.Snapshot.Loaded==20&&gun.Reserve==2398,"right support inserts and charges LEFT-held rifle in unchanged source geometry");
  long before=gun.Snapshot.Shots;gun.Step(.1f,Project(Hand(.8f,.8f,true),Hand(0),0,profile.Offer),false,false);
  Check(gun.Snapshot.Shots==before&&gun.Snapshot.Loaded==20,"left-primary climb reservation suspends actual native gun without ammo loss");
  var round=new R23RoundState(new string[0],new Sight(),true);long revision=round.PlayerHealthState.Revision;
  Check(!round.ApplyPlayerGain(20,20)&&round.PlayerHealthState.Revision==revision,"full native player rejects consumable gain without mutation");
  round.Damage(125);int ammo=round.Rifle.Snapshot.Loaded,reserve=round.Rifle.Reserve;
  Check(round.Health==75&&round.Shield==0&&round.ApplyPlayerGain(20,30)&&round.Health==95&&round.Shield==30,"positive effect applies actual prepared native health and shield gain");
  Check(round.ApplyPlayerGain(20,100)&&round.Health==100&&round.Shield==100&&round.Rifle.Snapshot.Loaded==ammo&&round.Rifle.Reserve==reserve,"native gains cap to missing health/shield and never alter weapon ammunition");
  revision=round.PlayerHealthState.Revision;
  Check(!round.ApplyPlayerGain(0,0)&&!round.ApplyPlayerGain(-1,0)&&!round.ApplyPlayerGain(float.NaN,10)&&!round.ApplyPlayerGain(0,float.PositiveInfinity)&&!round.ApplyPlayerGain(101,0)&&round.PlayerHealthState.Revision==revision,"invalid/empty positive effects fail without loss or state mutation");
  round.Damage(25);revision=round.PlayerHealthState.Revision;Check(!round.ApplyPlayerGain(20,0)&&round.Shield==75&&round.PlayerHealthState.Revision==revision,"health-only effect rejects no-effect without silently converting to shield");
  round.Damage(1000);revision=round.PlayerHealthState.Revision;Check(!round.ApplyPlayerGain(100,100)&&round.PlayerHealthState.Life==LifeState.Dead&&round.PlayerHealthState.Revision==revision,"positive consumable effect cannot resurrect dead native player");
 }
}
