using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Projectiles;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Combat;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.Persistence;
using Ashfall.Integration.ProjectileWeapons;
class R3Tests
{
 static int n;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);n++;Console.WriteLine("PASS "+name);}
 sealed class Approval:IWeaponLifetimeAuthority
 {public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int rounds){return RuleResult.Accept();}public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return RuleResult.Accept();}}
 static Fixture Source(AtomicSettingsStore store=null,bool respawn=false){var f=new Fixture();if(respawn){f.H=new HealthService("s",3,2,true,f.W,f.W,f.W);f.H.Register(f.C("a"),"a",0,false);f.H.Register(f.C("b"),"b",1,false);}f.A=new CombatProjectileAdapter(f.H,f.W,f.W,f.W,"s",3,"host",f.W.MapRevision,false,1,new SourceRadialProvenance("bdba6e30012e1733efd5021fd8ded593f82f26e6","one_star_050",CatalogHash.Value),store);return f;}
 static WeaponLease Register(Fixture f){var w=f.A.Equip(f.C("equip"),"w","a","smaw",1);Check(f.A.RegisterSource(f.C("launch"),"shot",1,600,"w","a","smaw",1).Accepted,"typed launch registered");return w;}
 static RuleResult Freeze(Fixture f){return f.A.FreezeRadial(f.C(),"shot",0,new Float3(),4,60,true,new[]{f.T("b")},RadialPolicy.SourceRadialV1);}
 static RuleResult Blast(Fixture f){return f.A.Radial(f.C(),"shot",0,new Float3(),4,60,true,new[]{f.T("b")},RadialPolicy.SourceRadialV1);}
 static void Main(string[] args){
 var f=Source();var w=Register(f);long rev=f.H.Read("b").Revision;Check(Freeze(f).Accepted&&f.H.Read("b").Revision==rev,"physical freeze publishes no Health debit");
 f.A.Retire(f.C("unequip"),w);Check(Blast(f).Accepted&&f.H.Read("b").Shield==40,"known impact frozen before exact lease retirement delivers");rev=f.H.Read("b").Revision;Check(Blast(f).Accepted&&f.H.Read("b").Revision==rev,"same logical explosion once after retirement");
 Check(!f.A.RegisterSource(f.C(),"illegal",2,600,"w","a","smaw",1).Accepted,"retired lease cannot admit new typed launch");
 f=Source();w=Register(f);Check(Freeze(f).Accepted,"freeze before owner lifecycle");f.H.SetConnected(f.C("disconnect"),"a",f.H.Read("a").Revision,false);f.A.Retire(f.C("retire"),w);Check(Blast(f).Accepted&&f.H.Read("b").Shield==40,"frozen physical event survives owner disconnect and unequip");
 f=Source();w=Register(f);f.A.Retire(f.C("early-retirement"),w);Check(Blast(f).Reason=="weapon-event"&&f.H.Read("b").Shield==100,"OPEN: unknown impact after retirement fails closed on actual Combat API");
 f=Source();w=Register(f);Check(Freeze(f).Accepted,"freeze pressure event");for(int i=0;i<4093;i++)if(!f.H.Advance(f.C("pressure/"+i)).Accepted)throw new Exception("pressure setup");f.A.Retire(f.C("retire-pressure"),w);Check(!Blast(f).Accepted&&f.H.Read("b").Shield==100,"actual Health pressure preserves frozen hit");f.Tick=300;f.W.Now=15;Check(Blast(f).Accepted&&f.H.Read("b").Shield==40,"same retired explosion retries after Health pressure");rev=f.H.Read("b").Revision;Check(Blast(f).Accepted&&f.H.Read("b").Revision==rev,"recovered pressure damage once");
 f=Source();w=Register(f);Freeze(f);f.A.Retire(f.C("retire"),w);f.H.SetConnected(f.C("leave-target"),"b",f.H.Read("b").Revision,false);f.H.SetConnected(f.C("return-target"),"b",f.H.Read("b").Revision,true);rev=f.H.Read("b").Revision;Check(Blast(f).Reason=="collision-lifecycle"&&f.H.Read("b").Revision==rev,"frozen hit preserves replacement target connection");
 f=Source();w=Register(f);Freeze(f);HitEvidence evidence;Check(f.A.TryResolve(f.C(),"4:shot0:b",out evidence),"frozen trusted evidence available");evidence.BaseDamage=10000;Check(Blast(f).Accepted&&f.H.Read("b").Shield==40,"public evidence copy cannot rewrite trusted damage");
 var world=new World();var ledger=new WeaponLifetimes("s",new Approval());var reserve=new FiniteAmmoReserve(50,20);WeaponHandling handler;var c=new CommandContext("s","a","create",0,3,true);Check(ledger.CreateNew(c,"actual",WeaponMechanism.ServiceRifle,24,reserve,world,out handler).Accepted,"actual WeaponHandling lifetime created");var adapter=new PreparedProjectileWeapons(ledger,world,"s",3);var snapshot=handler.Snapshot;long ammo=reserve.Revision;
 for(int i=0;i<3;i++)Check(adapter.PrepareFire(new CommandContext("s","a","prepare/"+i,0,3,true),"actual","shot",600).Reason==PreparedProjectileWeapons.MissingFireApi,"actual prepared ownership checks then rejects missing fire API");
 Check(handler.Snapshot.Loaded==snapshot.Loaded&&handler.Snapshot.Shots==snapshot.Shots&&reserve.Available==20&&reserve.Revision==ammo,"rejection preserves real magazine and reserve without rollback");
 Check(!adapter.PrepareFire(new CommandContext("s","other","forge",0,3,true),"actual","shot",600).Accepted,"foreign equipped owner rejected");
 Check(!adapter.PrepareFire(new CommandContext("s","a","expiry",0,3,true),"actual","shot",601).Accepted,"bad expiry before debit");
 Check(handler.Apply(new CommandContext("s","a","still-playable",1,3,true),HandlingAction.Fire,new HandlingInput{Sequence=1,Enabled=true,PrimaryTracked=true,Trigger=true}).Accepted&&handler.Snapshot.Loaded==23,"aborted real reservation retains playable original handler");

 foreach(string actor in new[]{"a","b"}){
 f=Source(null,true);w=Register(f);Check(Freeze(f).Accepted,"freeze before "+actor+" respawn");f.A.Retire(f.C("retire"),w);
 Check(f.H.ApplyLoss(f.C("death"),actor,f.H.Read(actor).Revision,100,100,false).Accepted,"actual death "+actor);
 f.Tick=101;f.W.Now=5.05;var dead=f.H.Read(actor);HealthService.PreparedRespawn token;
 Check(f.H.PrepareRespawn(f.C("respawn"),actor,dead.Revision,dead.LifeGeneration,dead.ConnectionGeneration,6,out token),"actual prepared respawn "+actor);
 Check(f.H.Serialize(()=>{if(!token.Validate())return false;token.CommitValidated();return true;}),"actual respawn commit "+actor);
 rev=f.H.Read("b").Revision;var result=Blast(f);Check(actor=="a"?result.Accepted&&f.H.Read("b").Shield==40:!result.Accepted&&f.H.Read("b").Revision==rev,"owner attribution survives; replacement target protected "+actor);
 }
 var path=Path.Combine(args[0],"journal.json");f=Source(new AtomicSettingsStore(path));w=Register(f);Freeze(f);f.A.Retire(f.C("retire"),w);Check(Blast(f).Accepted&&f.H.Read("b").Shield==40,"actual Persistence publication commits damage");var offer=f.A.OfferPublication(f.C("offer"));Check(offer.Length>0,"durable offer retains actual Health transitions");Check(!f.A.Acknowledge(f.C("raw-ack"),offer[offer.Length-1].Sequence).Accepted,"raw bridge ACK prohibited for durable ownership");Check(!f.A.AcknowledgePublication(f.C("future"),offer[offer.Length-1].Sequence+1).Accepted,"durable future ACK rejected");Check(f.A.AcknowledgePublication(f.C("ack"),offer[offer.Length-1].Sequence).Accepted&&f.A.OfferPublication(f.C("empty")).Length==0,"durable prefix ACK forwarded");

 var pressurePath=Path.Combine(args[0],"pressure-journal.json");var pressured=Source(new AtomicSettingsStore(pressurePath));var pressureWeapon=Register(pressured);Check(Freeze(pressured).Accepted,"durable pressure frozen physical event");
 for(int i=0;i<1984;i++){if(!pressured.H.SetConnected(pressured.C("journal-fill/"+i),"c",pressured.H.Read("c").Revision,true).Accepted)throw new Exception("journal pressure setup");if(i%64==63)pressured.A.ServicePublication(pressured.C("service/"+i));}
 rev=pressured.H.Read("b").Revision;Check(Blast(pressured).Reason=="publication-pressure"&&pressured.H.Read("b").Revision==rev,"actual durable capacity rejects before damage");
 Check(adapter.PrepareFire(new CommandContext("s","a","journal-pressure-fire",1,3,true),"actual","pressure-shot",601).Reason==PreparedProjectileWeapons.MissingFireApi&&handler.Snapshot.Loaded==23&&reserve.Available==20,"missing fire composition preserves actual ammo under publication pressure");
 var pressureOffer=pressured.A.OfferPublication(pressured.C("pressure-offer"));Check(pressured.A.AcknowledgePublication(pressured.C("pressure-ack"),pressureOffer[pressureOffer.Length-1].Sequence).Accepted,"durable capacity recovery ACK");pressured.A.Retire(pressured.C("pressure-retire"),pressureWeapon);Check(Blast(pressured).Accepted&&pressured.H.Read("b").Shield==40,"same frozen explosion resumes once after durable capacity recovery");
 var fresh=Source(new AtomicSettingsStore(path));Check(fresh.A.RecoveryRequired&&!fresh.A.ServicePublication(fresh.C()).Accepted,"reopen refuses empty-state producer recovery");
 Console.WriteLine("RESULT "+n+" PASS 0 FAIL; missing launch/fire and cold producer APIs remain OPEN");
 }
}
