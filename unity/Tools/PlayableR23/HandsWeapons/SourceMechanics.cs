using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.DesktopPlaytest.Combat;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Modules.WeaponHandling;
static class SourceMechanics
{
 sealed class Sight:IInteractionQuery { public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return true;} }
 sealed class Grant:IAuthorityGate,IWeaponLifetimeAuthority {
  public RuleResult Validate(CommandContext command){return command.IsAuthority?RuleResult.Accept():RuleResult.Reject("local authority");}
  public RuleResult AuthorizeNew(CommandContext command,string id,WeaponMechanism m,int rounds){return Validate(command);}
  public RuleResult AuthorizeRestore(CommandContext command,string id,string actor,long epoch){return RuleResult.Reject("not needed");}
 }
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static LocalRifleSandbox Gun(SourceRifleDefinition profile,R23RifleReserve reserve=null){return new LocalRifleSandbox(new string[0],new Sight(),profile,reserve??new R23RifleReserve(),"test/"+profile.Id);}
 static HandlingInput Hand(SourceRifleDefinition profile,bool grip,Float3 point,bool reserved=false){return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,SupportReserved=reserved,Grip=grip,SupportLocal=point};}
 public static void Run(){
  var mk=Gun(SourceRifleDefinition.Mk18);var times=new List<float>();float time=0;
  if(mk.Step(0,true,false,false))times.Add(time);
  for(int i=1;i<=50;i++){time=i*.01f;if(mk.Step(.01f,true,false,false))times.Add(time);}
  Check(times.Count==3&&Math.Abs(times[1]-.08f)<.001&&Math.Abs(times[2]-.16f)<.001&&mk.Snapshot.Loaded==15,"MK18 exact source 18-round committed burst at 0/80/160ms; held trigger does not repeat");
  mk.Step(.01f,false,false,false);Check(mk.Step(.01f,true,false,false)&&mk.Snapshot.Loaded==14,"MK18 release rearms next burst without ammunition grant");
  var committed=Gun(SourceRifleDefinition.Mk18);committed.Step(0,true,false,false);
  for(int i=0;i<16;i++)committed.Step(.01f,false,false,false);
  Check(committed.Snapshot.Shots==3&&Math.Abs(committed.Snapshot.CooldownSeconds-.24f)<.001,"MK18 completes burst after trigger release and installs source 240ms recovery");
  committed.Step(.01f,false,false,false);bool recoveredEarly=false;for(int i=0;i<22;i++)recoveredEarly|=committed.Step(.01f,true,false,false);Check(!recoveredEarly,"MK18 post-burst recovery rejects early trigger attempts");
  Check(committed.Step(.01f,true,false,false),"MK18 next burst becomes available at source recovery boundary");
  var interrupted=Gun(SourceRifleDefinition.Mk18);interrupted.Step(0,true,false,false);interrupted.Step(.01f,true,false,true);
  Check(!interrupted.Step(.25f,true,false,false)&&interrupted.Snapshot.Shots==1,"pause cancels committed burst and requires actual trigger release");
  interrupted.Step(0,false,false,false);Check(interrupted.Step(.01f,true,false,false),"released trigger resumes source burst after pause");
  var ak=Gun(SourceRifleDefinition.Akm);int shots=ak.Step(0,true,false,false)?1:0;
  for(int i=0;i<100;i++)if(ak.Step(.01f,true,false,false))shots++;
  Check(shots==12&&ak.Snapshot.Loaded==8,"AKM source 20-round automatic11rps including initial zero-time shot");
  for(int i=0;i<150;i++)ak.Step(.01f,true,false,false);
  Check(ak.Snapshot.Shots==20&&ak.Snapshot.Loaded==0&&ak.Reserve==2400&&ak.Snapshot.Phase==ReloadPhase.NeedsMagazine,"AKM auto empty eject preserves finite family reserve");
  var topup=Gun(SourceRifleDefinition.Akm);for(int i=0;i<5;i++)topup.Step(.1f,true,false,false);
  topup.Step(0,false,true,false);
  Check(topup.Snapshot.Phase==ReloadPhase.NeedsMagazine&&topup.Reserve==2415&&topup.Snapshot.Loaded==0,"source tactical eject refunds exactly15 live rounds without cap");
  topup.Step(.25f,false,false,false);Check(topup.Snapshot.Phase==ReloadPhase.NeedsMagazine,"source reload does not silently advance without next click/gesture");
  topup.Step(0,false,true,false);Check(topup.Snapshot.Phase==ReloadPhase.NeedsRack&&topup.Snapshot.Seated==20&&!topup.Snapshot.Chambered&&topup.LoadedRounds==20&&topup.Reserve==2395,"source insert debits exact20, reports seated rounds, remains unchambered");
  topup.Step(0,false,true,false);Check(topup.Snapshot.Chambered&&topup.Snapshot.Loaded==20&&topup.Snapshot.Phase==ReloadPhase.Ready&&topup.Reserve==2395,"source charging step chambers existing seated ammunition without extra debit");
  var p=SourceRifleDefinition.Akm;var physical=Gun(p);physical.Step(0,Hand(p,false,p.Offer),true,false);physical.Step(.01f,Hand(p,false,p.Offer),false,false);physical.Step(.01f,Hand(p,true,p.Offer),false,false);
  Check(physical.Snapshot.Phase==ReloadPhase.HeldMagazine&&physical.Reserve==2420,"source physical offer acquires magazine without premature ammo debit");
  physical.Step(.01f,Hand(p,true,new Float3(p.Offer.X,p.Offer.Y+.04f,p.Offer.Z)),false,false);Check(physical.Snapshot.Phase==ReloadPhase.HeldMagazine,"physical insertion rejects insufficient upward travel");
  physical.Step(.01f,Hand(p,true,p.Insert),false,false);Check(physical.Snapshot.Phase==ReloadPhase.NeedsRack&&physical.Snapshot.Seated==20&&physical.Reserve==2400,"source upward insertion debits reserve once");
  physical.Step(.01f,Hand(p,true,p.Charging),false,false);physical.Step(.01f,Hand(p,true,new Float3(p.Charging.X,p.Charging.Y,p.Charging.Z-.09f)),false,false);
  Check(!physical.Snapshot.Chambered,"source rack requires release before new charging-handle grip");
  physical.Step(.01f,Hand(p,false,p.Charging),false,false);physical.Step(.01f,Hand(p,true,p.Charging),false,false);physical.Step(.01f,Hand(p,true,new Float3(p.Charging.X,p.Charging.Y,p.Charging.Z-.08f)),false,false);
  Check(physical.Snapshot.Chambered&&physical.Snapshot.Loaded==20&&physical.Reserve==2400,"source actual AKM charging-grip geometry and rearward80mm pull chamber magazine");
  var lost=Gun(p);lost.Step(0,Hand(p,false,p.Offer),true,false);lost.Step(.01f,Hand(p,false,p.Offer),false,false);lost.Step(.01f,Hand(p,true,p.Offer),false,false);lost.Step(.01f,Hand(p,true,p.Offer,true),false,false);
  Check(lost.Snapshot.Phase==ReloadPhase.NeedsMagazine&&lost.Reserve==2420&&!lost.Snapshot.ClaimsSupport,"support reservation cancels unseated contact without consuming ammunition");
  lost.Step(.01f,Hand(p,true,p.Offer),false,false);Check(lost.Snapshot.Phase==ReloadPhase.NeedsMagazine,"released reservation cannot reacquire magazine with stale held grip");
  lost.Step(.01f,Hand(p,false,p.Offer),false,false);lost.Step(.01f,Hand(p,true,p.Offer),false,false);Check(lost.Snapshot.Phase==ReloadPhase.HeldMagazine,"actual support release/regrip rearms source magazine gesture");
  var low=Gun(p,new R23RifleReserve(2));for(int i=0;i<20;i++)low.Step(.1f,true,false,false);low.Step(0,false,true,false);low.Step(0,false,true,false);
  Check(low.Snapshot.Loaded==2&&low.Reserve==0&&low.Snapshot.Chambered,"source exhausted reserve inserts partial two-round magazine without invented refill");
  var round=new R23RoundState(new string[0],new Sight(),true);var original=round.Rifle;round.Step(0,true,false,false);round.Step(.08f,false,false,false);round.Step(.08f,false,false,false);
  Check(round.SelectWeapon("akm")&&round.Rifle.Snapshot.Loaded==20&&round.Rifle.Reserve==2400,"selection equips separate persistent AKM instance with shared source rifle pool");
  round.Step(0,false,false,false);round.Step(.1f,true,false,false);round.Step(0,false,true,false);round.Step(0,false,true,false);
  Check(round.SelectWeapon("mk18")&&Object.ReferenceEquals(round.Rifle,original)&&round.Rifle.Snapshot.Loaded==15&&round.Rifle.Reserve==2399,"switch returns exact MK18 magazine and reflects only real shared reserve debit");
  Check(round.SelectWeapon("akm")&&round.Rifle.Snapshot.Seated==20&&round.Rifle.Snapshot.Phase==ReloadPhase.NeedsRack&&!round.Rifle.Snapshot.Chambered,"selection preserves independent seated magazine, chamber and reload stage");
  round.Damage(25);Check(round.Health==100&&round.Shield==75&&round.MaxHealth==100&&round.MaxShield==100,"source player shield getter and damage come from actual native health snapshot");
  round.Damage(100);Check(round.Health==75&&round.Shield==0,"source damage carries through exhausted shield into health");
  var gate=new Grant();var ledger=new WeaponLifetimes("source-test",gate);var reserve=new R23RifleReserve();WeaponHandling native,duplicate;
  Check(ledger.CreateSourceRifle(new CommandContext("source-test","actor","grant",0,0,true),"stable/source",SourceRifleDefinition.Mk18,reserve,gate,out native).Accepted&&!ledger.CreateSourceRifle(new CommandContext("source-test","actor","duplicate",1,0,true),"stable/source",SourceRifleDefinition.Mk18,reserve,gate,out duplicate).Accepted&&duplicate==null,"source lifetime cannot grant the same stable instance twice");
  var frame=new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Sequence=1,DeltaSeconds=.01f,Trigger=true};var fire=new CommandContext("source-test","actor","fire",1,0,true);
  Check(native.ApplySourceRifle(fire,HandlingAction.Fire,frame,SourceRifleDefinition.Mk18,false).Accepted,"source owner accepts authority-bound native fire");
  frame.Sequence=2;Check(!native.ApplySourceRifle(fire,HandlingAction.Fire,frame,SourceRifleDefinition.Mk18,false).Accepted&&native.Snapshot.Loaded==17&&native.Snapshot.Shots==1,"source owner rejects replay without shadow ammo debit");
  Check(!native.ApplySourceRifle(new CommandContext("source-test","actor","duplicate-time",1,0,true),HandlingAction.Fire,frame,SourceRifleDefinition.Mk18,false).Accepted&&native.Snapshot.Loaded==17,"source owner rejects duplicate time advance in same tick");
 }
}
