using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration;
using Ashfall.Integration.ProjectileWeapons;
using Ashfall.Modules.Combat;
using Ashfall.Modules.Projectiles;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.HealthStates;
static class LaunchTests
{
 static int n;static void Need(bool b,string s){if(!b)throw new Exception(s);n++;}
 sealed class Binding:IWeaponFireBinding,IWeaponLifetimeAuthority
 {
  public HealthService Health;public bool Allowed=true;
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int rounds){return RuleResult.Accept();}
  public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return RuleResult.Accept();}
  public RuleResult Validate(CommandContext c,string id,long equip,long life,long connection,long frame){var h=Health.Read(c.ActorId);return Allowed&&h!=null&&h.Connected&&h.Life==LifeState.Alive&&h.LifeGeneration==life&&h.ConnectionGeneration==connection&&id=="w"&&equip==1&&frame>0?RuleResult.Accept():RuleResult.Reject("binding");}
 }
 sealed class Facts:ICombatEvidence
 {public HitEvidence Hit;public bool TryResolve(CommandContext c,string id,out HitEvidence e){e=Hit;return e!=null;}}
 static CommandContext Actor(string id,long tick=0){return new CommandContext("s","a",id,tick,3,true);}
 static HandlingInput Input(long sequence){return new HandlingInput{Sequence=sequence,Enabled=true,PrimaryTracked=true,Trigger=true};}
 static void Main(string[] args)
 {
  var f=new Fixture();var facts=new Facts();var bridge=new CombatHealthBridge(f.H,f.W,facts,f.W,f.W,"s",3);
  var lease=bridge.Equip(f.C(),"w","a","service_rifle",1,0);var b=new Binding{Health=f.H};var weapons=new WeaponLifetimes("s",b);WeaponHandling gun;
  var reserve=new FiniteAmmoReserve(30,20);Need(weapons.CreateNew(Actor("new"),"w",WeaponMechanism.ServiceRifle,2,reserve,f.W,out gun).Accepted,"actual gun");
  var a=f.H.Read("a");PreparedShotLaunch p;
  Need(PreparedShotLaunch.Prepare(weapons,b,f.C(),Actor("shot"),lease,"bullet",Input(1),a.LifeGeneration,a.ConnectionGeneration,100,out p).Accepted,"real fire plus central capacity prepared");
  Need(gun.Snapshot.Loaded==2&&p.Shot==null&&p.Launch==null,"prepare no ammo or authority publication");p.Abort();p.CommitValidated();Need(gun.Snapshot.Loaded==2,"abort preserves ammo");
  Need(PreparedShotLaunch.Prepare(weapons,b,f.C(),Actor("shot"),lease,"bullet",Input(1),a.LifeGeneration,a.ConnectionGeneration,100,out p).Accepted,"same aborted command retry");
  b.Allowed=false;Need(!p.Validate(f.C()).Accepted&&gun.Snapshot.Loaded==2,"binding revocation rejects all before debit");p.Abort();b.Allowed=true;
  Need(PreparedShotLaunch.Prepare(weapons,b,f.C(),Actor("shot"),lease,"bullet",Input(1),a.LifeGeneration,a.ConnectionGeneration,100,out p).Accepted,"retry binding");Need(p.Validate(f.C()).Accepted,"validate both actual owners");p.CommitValidated();p.CommitValidated();p.Abort();
  Need(gun.Snapshot.Loaded==1&&p.Shot.After.Shots==1&&reserve.Available==20,"commit exactly one actual loaded round");
  Need(p.Launch.InstanceId==p.Shot.InstanceId&&p.Launch.SourceCommand==p.Shot.Command.CommandId&&p.Launch.EquipGeneration==p.Shot.EquipGeneration&&p.Launch.ConnectionGeneration==p.Shot.ConnectionGeneration&&p.Launch.LifeGeneration==p.Shot.LifeGeneration,"exact immutable handoff identities");
  bridge.Retire(f.C(),lease,0);WeaponTransfer tr;Need(weapons.Capture(Actor("retire"),"w",out tr).Accepted,"actual weapon retires before first physical contact");
  Need(!PreparedShotLaunch.Prepare(weapons,b,f.C(),Actor("extra"),lease,"extra",Input(2),a.LifeGeneration,a.ConnectionGeneration,100,out p).Accepted,"retired no extra ammo shot");
  // New fixture: token retained separately from failed out parameter.
  f=new Fixture();facts=new Facts();bridge=new CombatHealthBridge(f.H,f.W,facts,f.W,f.W,"s",3);lease=bridge.Equip(f.C(),"w","a","service_rifle",1,0);b=new Binding{Health=f.H};weapons=new WeaponLifetimes("s",b);weapons.CreateNew(Actor("create"),"w",WeaponMechanism.ServiceRifle,1,reserve,f.W,out gun);a=f.H.Read("a");
  Need(PreparedShotLaunch.Prepare(weapons,b,f.C(),Actor("bullet"),lease,"bullet",Input(1),a.LifeGeneration,a.ConnectionGeneration,100,out p).Accepted&&p.Validate(f.C()).Accepted,"second real aggregate");p.CommitValidated();bridge.Retire(f.C(),lease,0);weapons.Capture(Actor("retire"),"w",out tr);
  f.H.SetConnected(f.C("owner-leave"),"a",f.H.Read("a").Revision,false);
  var impact=p.Launch.PublishImpact(f.C(),0,new Float3(),10,5,SourceRadialFalloff.Constant);
  var hit=bridge.PublishHit(f.C(),"hit",impact,"0","b",0,0);facts.Hit=new HitEvidence{Identity=hit,Origin=new Float3(),Point=new Float3(),TargetHead=new Float3(0,.5f,0),MaximumRange=10,BaseDamage=5,DamageMultiplier=1,HeadRatio=1};
  f.W.Block=true;Need(bridge.Apply(f.C("blocked"),"hit",0,true).Result.Reason=="occluded"&&f.H.Read("b").Shield==100,"prepared bullet still requires structural LOS");f.W.Block=false;
  Need(bridge.Apply(f.C("delivery"),"hit",0,true).Result.Accepted&&f.H.Read("b").Shield==95,"first impact after exact retirement and owner leave actual Health");long rev=f.H.Read("b").Revision;
  Need(bridge.Apply(f.C("duplicate"),"hit",0,true).Result.Accepted&&f.H.Read("b").Revision==rev,"physical target once");
  Need(Object.ReferenceEquals(impact,p.Launch.PublishImpact(f.C(),0,new Float3(),10,5,SourceRadialFalloff.Constant)),"same immutable impact event");
  f.Tick=101;bool denied=false;try{p.Launch.PublishImpact(f.C(),0,new Float3(),10,5,SourceRadialFalloff.Constant);}catch(ArgumentException){denied=true;}Need(denied,"original expiry cannot renew");
  // Real central reservation pressure rejects before any firearm reservation can publish.
  var rules=new CombatRules("s",3,256,2,f.W,facts,f.W);var w=rules.Equip(new CommandContext("s","host","equip",0,3,true),"w","a","smaw",1,0);LaunchedProjectileCapability cap;
  var host=new CommandContext("s","host","launch",0,3,true);var provenance=new SourceRadialProvenance("source","catalog",CatalogHash.Value);
  Need(!CombatRules.PrepareLaunchedProjectile(host,w,"zone","command",1,1,1,100,21,DamageKind.Area,provenance,out cap).Accepted,"bounded whole reservation capacity");
  Need(CombatRules.PrepareLaunchedProjectile(host,w,"radial","command",1,1,1,100,1,DamageKind.Area,provenance,out cap).Accepted&&cap.Validate(host).Accepted,"actual radial reservation");
  bool checkpointRejected=false;try{using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))rules.WriteCheckpoint(writer,new HostHit[0]);}catch(InvalidOperationException e){checkpointRejected=e.Message=="projectile-producer-checkpoint-unsupported";}Need(checkpointRejected,"cold capture cannot silently omit in-flight producer");
  LaunchedProjectileCapability duplicate;
  Need(!CombatRules.PrepareLaunchedProjectile(host,w,"other-id","command",1,1,1,100,1,DamageKind.Area,provenance,out duplicate).Accepted,"same source command cannot mint a second launch");
  cap.CommitValidated();rules.RetireWeapon(host,w,0);var ev=cap.PublishImpact(host,0,new Float3(),4,60,SourceRadialFalloff.Linear);Need(ev.SourceExplosion.Power==60&&ev.SourceExplosion.Provenance==provenance,"first source radial after retirement typed immutable policy");
  denied=false;try{cap.PublishImpact(host,0,new Float3(),4,61,SourceRadialFalloff.Linear);}catch(ArgumentException){denied=true;}Need(denied,"changed impact cannot rebound");
  checkpointRejected=false;try{using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))rules.WriteCheckpoint(writer,new HostHit[0]);}catch(InvalidOperationException e){checkpointRejected=e.Message=="projectile-producer-checkpoint-unsupported";}Need(checkpointRejected,"cold capture retains launch tombstone after first impact");
  Need(!CombatRules.PrepareLaunchedProjectile(host,w,"new","command",1,1,1,100,1,DamageKind.Area,provenance,out cap).Accepted,"retired original cannot reserve new launch");
  // Source zones retain later physical slots after retirement, independent of target replay.
  f=new Fixture();f.A=new CombatProjectileAdapter(f.H,f.W,f.W,f.W,"s",3,"host",f.W.MapRevision,false,1,provenance);w=f.A.Equip(f.C(),"z","a","zone",1);Need(f.A.RegisterSource(f.C(),"z",1,600,"z","a","zone",1).Accepted,"zone launch");f.A.Retire(f.C(),w);
  for(int sub=1;sub<20;sub++)Need(f.A.Radial(f.C(),"z",sub,new Float3(),15,1,false,new[]{f.T("b")},RadialPolicy.SourceRadialV1).Accepted,"retired zone original subhit "+sub);
  Need(f.H.Read("b").Shield==81,"nineteen original target identities");
  // Physical policy is frozen before delivery pressure and later team/match changes.
  f=new Fixture();f.A=new CombatProjectileAdapter(f.H,f.W,f.W,f.W,"s",3,"host",f.W.MapRevision,false,1,provenance);w=f.A.Equip(f.C(),"p","a","smaw",1);Need(f.A.RegisterSource(f.C("launch-p"),"p",1,600,"p","a","smaw",1).Accepted,"policy launch");
  Need(f.A.FreezeRadial(f.C(),"p",0,new Float3(),4,5,false,new[]{f.T("b")},RadialPolicy.SourceRadialV1).Accepted,"freeze actual enemy policy");
  f.W.Match=false;Need(f.H.SetTeam(f.C("change-team"),"b",f.H.Read("b").Revision,0).Accepted,"actual target team changes after contact");f.W.Match=true;
  Need(f.A.Radial(f.C(),"p",0,new Float3(),4,5,false,new[]{f.T("b")},RadialPolicy.SourceRadialV1).Accepted&&f.H.Read("b").Shield==95,"late team change cannot rewrite frozen physical policy");
  Need(!f.A.ValidateProducerRetirement(f.C()).Accepted,"producer retirement honestly unsupported");
  var cold=new Fixture();cold.A=new CombatProjectileAdapter(cold.H,cold.W,cold.W,cold.W,"s",3,"host",cold.W.MapRevision,false,1,provenance,new Ashfall.Modules.Persistence.AtomicSettingsStore(Path.Combine(args[0],"cold-admission.json")),CatalogHash.Value);cold.A.Equip(cold.C(),"w","a","smaw",1);
  Need(cold.A.RegisterSource(cold.C(),"cold",1,600,"w","a","smaw",1).Reason=="projectile-producer-checkpoint-unsupported"&&cold.H.Read("b").Shield==100,"coherent checkpoint mode rejects unsupported producer before admission");
  // Real ammo cadence rejection leaves command available for corrected retry.
  f=new Fixture();facts=new Facts();bridge=new CombatHealthBridge(f.H,f.W,facts,f.W,f.W,"s",3);lease=bridge.Equip(f.C(),"w","a","service_rifle",1,0);b=new Binding{Health=f.H};weapons=new WeaponLifetimes("s",b);weapons.CreateNew(Actor("create"),"w",WeaponMechanism.ServiceRifle,3,new FiniteAmmoReserve(30,20),f.W,out gun);a=f.H.Read("a");
  Need(PreparedShotLaunch.Prepare(weapons,b,f.C(),Actor("first"),lease,"first",Input(1),a.LifeGeneration,a.ConnectionGeneration,100,out p).Accepted&&p.Validate(f.C()).Accepted,"cadence first aggregate");p.CommitValidated();
  f.Tick=1;Need(!PreparedShotLaunch.Prepare(weapons,b,f.C(),Actor("retry",1),lease,"retry",Input(2),a.LifeGeneration,a.ConnectionGeneration,100,out p).Accepted&&gun.Snapshot.Loaded==2,"ammo cadence rejection before launch reservation");
  var corrected=Input(2);corrected.DeltaSeconds=.12f;
  Need(PreparedShotLaunch.Prepare(weapons,b,f.C(),Actor("retry",1),lease,"retry",corrected,a.LifeGeneration,a.ConnectionGeneration,100,out p).Accepted&&p.Validate(f.C()).Accepted,"same rejected ammo command corrected retry");p.CommitValidated();Need(gun.Snapshot.Loaded==1,"retry debits one round only");
  f.Tick=2;Need(PreparedShotLaunch.Prepare(weapons,b,f.C(),Actor("retire-before-commit",2),lease,"retire-before-commit",new HandlingInput{Sequence=3,DeltaSeconds=.12f,Enabled=true,PrimaryTracked=true,Trigger=true},a.LifeGeneration,a.ConnectionGeneration,100,out p).Accepted,"prepare before retirement");bridge.Retire(f.C(),lease,f.Tick);Need(!p.Validate(f.C()).Accepted&&gun.Snapshot.Loaded==1,"retirement before aggregate commit rejects new shot");p.Abort();
  Console.WriteLine("RESULT "+n+" PASS 0 FAIL; canonical fixture binding synthetic; source projectile ammo and cold reconstruction OPEN");
 }
}




