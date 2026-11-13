using System;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.Combat;
using Ashfall.Integration.ProjectileWeapons;
using Ashfall.Integration.SourceExplosiveAmmo;
static class Tests
{
 static int n;static void Need(bool b,string s){if(!b)throw new Exception("FAIL "+s);n++;}
 sealed class Ports:IInventoryTransactions,IWeaponLifetimeAuthority,IWeaponFireBinding,IAuthorityGate,ICombatEvidence,IInteractionQuery
 {
  public bool Allowed=true;
  public RuleResult Validate(CommandContext c){return Allowed&&c.IsAuthority?RuleResult.Accept():RuleResult.Reject("revoked");}
  public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance i,ItemInstance[] items){return Validate(c);}
  public RuleResult Stage(CommandContext c,InventoryChange ch,out IInventoryReceipt r){r=new Receipt();return Validate(c);}
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int count){return Validate(c);}
  public RuleResult AuthorizeRestore(CommandContext c,string id,string a,long e){return Validate(c);}
  public RuleResult Validate(CommandContext c,string id,long equip,long life,long connection,long frame){return Validate(c);}
  public bool TryResolve(CommandContext c,string id,out HitEvidence e){e=null;return false;}
  public bool HasLineOfSight(string a,string b,Float3 p,Float3 q,float r){return true;}
 }
 sealed class Receipt:IInventoryReceipt{public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
 static CommandContext C(string command="c",long tick=1,string actor="a"){return new CommandContext("s",actor,command,tick,3,true);}
 static void Main()
 {
  var port=new Ports();var catalog=SourceCatalog.Create();var weapons=new WeaponLifetimes("s",port);
  foreach(var kind in new[]{"frag","zone","smaw"})
  {
   var inv=new Inventory(catalog,port,"s","a",3,false);Need(inv.Apply(C("admit",0),InventoryOperation.Admit,new ItemInstance("w",kind,1),null).Accepted,"admit source instance");Need(inv.Apply(C("equip",0),InventoryOperation.Equip,null,"w").Accepted,"equip source instance");
   var rules=new CombatRules("s",3,256,24,port,port,port);var lease=rules.Equip(C("equip",0,"host"),"w","a",kind,1,0);PreparedShotLaunch result;
   var input=new HandlingInput{Sequence=1,Enabled=true,PrimaryTracked=true,Trigger=true};long rev=inv.Revision;
   var r=ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",1,"host"),C(),lease,"launch",input,1,1,100,out result);
   Need(!r.Accepted&&r.Reason==(kind=="smaw"?ExplosiveAmmoAdmission.MissingRocket:ExplosiveAmmoAdmission.MissingThrow)&&result==null,"missing actual mechanic refuses "+kind);
   Need(inv.Revision==rev&&inv.Items.Length==1&&!inv.Holstered,"no instance debit or selection effect");
   Need(!ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",1,"host"),C(),lease,"launch",input,1,1,100,out result).Accepted,"retry remains unsupported no command spent");
   port.Allowed=false;Need(ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",1,"host"),C(),lease,"launch",input,1,1,100,out result).Reason=="revoked","actual binding revocation");port.Allowed=true;
   Need(ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",1,"host"),C(),lease,"launch",input,1,1,0,out result).Reason=="source-ammo-envelope","expired rejects");
   Need(ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",1,"host"),C(),lease,"launch",input,1,1,122,out result).Reason=="source-ammo-envelope","overlong expiry rejects");
   Need(ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",2,"host"),C(),lease,"launch",input,1,1,100,out result).Reason=="source-ammo-envelope","mixed authoritative clocks refuse");
   Need(ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",1,"host"),C(),lease,"launch",input,-1,1,100,out result).Reason=="source-ammo-envelope","negative life refuses");
   Need(ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",1,"host"),C("foreign",1,"b"),lease,"launch",input,1,1,100,out result).Reason=="source-ammo-envelope","cross actor lease refuses");
   var forged=rules.Equip(C("forged",1,"host"),"w","a","service_rifle",2,1);
   Need(ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",1,"host"),C(),forged,"launch",input,1,1,100,out result).Reason=="source-ammo-kind","rifle lease cannot authorize source inventory");
   Inventory.PreparedWeaponHandoff handoff;Need(inv.PrepareWeaponHandoff(C("transfer"),new[]{"w"},new ItemInstance[0],out handoff),"real inventory reservation");Need(inv.Revision==rev&&inv.Items.Length==1,"prepare retains instance");Need(!inv.Apply(C("other"),InventoryOperation.Holster,null,null).Accepted,"inventory owner excludes mutation");Need(handoff.Validate(),"real handoff validate");handoff.Abort();handoff.Abort();Need(inv.Apply(C("after"),InventoryOperation.Holster,null,null).Accepted,"abort retry releases real reservation");
   Need(ExplosiveAmmoAdmission.Prepare(inv,weapons,port,C("host",1,"host"),C(),lease,"launch",input,1,1,100,out result).Reason=="source-ammo-inventory-binding","holstered refuses");
   Need(!PreparedShotLaunch.Prepare(weapons,port,C("host",1,"host"),C(),lease,"launch",input,1,1,100,out result).Accepted,"actual launch composition also refuses source");
  }
  // Demonstrate why neither inventory removal API is an ammo token.
  var stack=new Inventory(catalog,port,"s","a",3,false);stack.Apply(C("one",0),InventoryOperation.Admit,new ItemInstance("f1","frag",1),null);stack.Apply(C("two",0),InventoryOperation.Admit,new ItemInstance("f2","frag",1),null);
  Inventory.PreparedRemoval removal;Need(!stack.PrepareRemoval(C(),"f1",out removal),"consumable removal rejects weapon frag");Inventory.PreparedWeaponHandoff group;
  Need(!stack.PrepareWeaponHandoff(C(),new[]{"f1"},new ItemInstance[0],out group),"weapon handoff cannot remove one frag stack member");Need(stack.PrepareWeaponHandoff(C(),new[]{"f1","f2"},new ItemInstance[0],out group),"whole group reservation exists");group.Abort();Need(stack.Items.Length==2,"whole group abort conserves identities");
  WeaponHandling gun;var reserve=new FiniteAmmoReserve(30,20);Need(weapons.CreateNew(C("new",0),"gun",WeaponMechanism.ServiceRifle,2,reserve,port,out gun).Accepted,"actual firearm fixture");
  IPreparedWeaponFire fire;IPreparedWeaponTransfer transfer;IPreparedWeaponSuspension suspension;var fi=new HandlingInput{Sequence=1,Enabled=true,PrimaryTracked=true,Trigger=true};
  Need(weapons.PrepareFire(C(),"gun",fi,1,1,1,100,port,out fire).Accepted,"actual fire reservation");Need(!weapons.PrepareTransfer(C("transfer"),"gun","a",3,out transfer).Accepted,"fire excludes transfer");Need(!gun.PrepareSuspension(out suspension).Accepted,"fire excludes suspension");Need(gun.Snapshot.Loaded==2&&reserve.Available==20,"prepare preserves actual rounds");fire.Abort();
  Need(gun.PrepareSuspension(out suspension).Accepted,"abort fire allows suspension");Need(!weapons.PrepareFire(C(),"gun",fi,1,1,1,100,port,out fire).Accepted,"suspension excludes fire");suspension.Abort();
  Need(weapons.PrepareTransfer(C("transfer"),"gun","a",3,out transfer).Accepted,"real transfer reservation");Need(!weapons.PrepareFire(C(),"gun",fi,1,1,1,100,port,out fire).Accepted,"transfer excludes fire");transfer.Abort();
  Need(weapons.PrepareFire(C(),"gun",fi,1,1,1,100,port,out fire).Accepted,"retry after abort");Need(!fire.Validate(101).Accepted,"expired prepared fire");fire.CommitValidated();Need(gun.Snapshot.Loaded==2,"expired fire no debit");
  Need(weapons.PrepareFire(C(),"gun",fi,1,1,1,100,port,out fire).Accepted&&fire.Validate(1).Accepted,"actual successful firearm control");fire.CommitValidated();fire.CommitValidated();fire.Abort();Need(gun.Snapshot.Loaded==1&&gun.Snapshot.Shots==1&&reserve.Available==20,"actual control commits once without reserve mint");
  Console.WriteLine("PASS "+n+" assertions; explosive handoff unsupported, firearm positive control only");
 }
}
