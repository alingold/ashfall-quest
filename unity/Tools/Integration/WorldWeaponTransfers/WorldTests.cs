using System;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.InventoryTransfers;
using Ashfall.Integration.WorldWeaponTransfers;
public class WorldTests
{
 static int checks,serial;static void A(bool b,string message){checks++;if(!b)throw new Exception(message);}
 sealed class Host:IAuthorityGate,IWeaponLifetimeAuthority,IInventoryTransactions{
  public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("host");}
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Validate(c);}public RuleResult AuthorizeRestore(CommandContext c,string id,string a,long e){return Validate(c);}
  public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] outgoing){return Validate(c);}public RuleResult Stage(CommandContext c,InventoryChange ch,out IInventoryReceipt receipt){receipt=new Receipt();return Validate(c);}
  sealed class Receipt:IInventoryReceipt{public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
 }
 sealed class Clock:IWeaponHostClock{public long Now=10;public long Tick{get{return Now;}}}
 sealed class Principal:IWeaponPrincipal{public string Actor="a";public long Connection=1,Generation=1;public bool Allowed=true;public string CanonicalActor{get{return Actor;}}public long ConnectionLife{get{return Connection;}}public long Life{get{return Generation;}}public bool Validate(CommandContext c,long deadline){return Allowed&&c.ActorId==Actor;}}
 sealed class Geometry:IWeaponGeometry{
  public bool DenySecond,Valid=true;public int Commits,Aborts;
  public bool Prepare(CommandContext c,IWeaponPrincipal p,long deadline,ItemInstance[] drops,WorldWeaponRecord[] collect,out IWeaponGeometryLease lease){var a=new WeaponPlacement[drops.Length];for(int n=0;n<a.Length;n++)a[n]=new WeaponPlacement(drops[n].InstanceId,"body:"+drops[n].InstanceId,"semantic:"+drops[n].InstanceId,"lease:"+c.CommandId,1,new Float3(n,0,0));lease=new Lease(this,a);return !(DenySecond&&a.Length==2);}
  sealed class Lease:IWeaponGeometryLease{readonly Geometry owner;readonly WeaponPlacement[] a;bool done,aborted;public Lease(Geometry o,WeaponPlacement[] p){owner=o;a=p;}public WeaponPlacement[] Placements{get{return (WeaponPlacement[])a.Clone();}}public bool Validate(){return owner.Valid&&!aborted&&!done;}public void CommitValidated(){if(done)return;done=true;owner.Commits++;}public void Abort(){if(!done&&!aborted){aborted=true;owner.Aborts++;}}}
 }
 static CommandContext C(Clock clock,string actor="a",long epoch=1){return new CommandContext("s",actor,"cmd"+(++serial),clock.Now,epoch,true);}
 static ItemCatalog PairCatalog(){var original=SourceCatalog.Create().Get("longwatch");var levels=new WeaponStats[5];for(int n=0;n<5;n++)levels[n]=original.Stats(n+1);return new ItemCatalog(new[]{new ItemDefinition("longwatch","awp","fixture pair",5,true,5,"sniper","bolt",1,true,"fixture-pair",levels)});}
 static void Main(){Run();}
 public static void Run(){
  var clock=new Clock();var host=new Host();var geometry=new Geometry();var principal=new Principal();var weapons=new SessionWeapons("s",60,host,host);var source=new Inventory(PairCatalog(),host,"s","a",1,false);var target=new Inventory(PairCatalog(),host,"s","b",2,false);
  var items=new[]{new ItemInstance("one","longwatch",1),new ItemInstance("two","longwatch",1)};var ammo=new[]{new FiniteAmmoReserve(20,11),new FiniteAmmoReserve(20,13)};
  for(int n=0;n<2;n++){A(weapons.Grant(C(clock),items[n],WeaponMechanism.PrecisionRifle,3,ammo[n]).Accepted,"grant actual");A(source.Apply(C(clock),InventoryOperation.Admit,items[n],null).Accepted,"admit actual");}
  A(source.SlotCount==1&&source.Items.Length==2,"actual whole pair fixture");var lane=new object();A(weapons.BindPreparedWorldActionOwner(lane,delegate(string actor){return actor=="a"?source:actor=="b"?target:null;}),"root binds actual inventories action owner");var coordinator=new WorldWeaponCoordinator(lane,weapons,clock,geometry,100);WorldWeaponCoordinator.Operation op,other;
  A(!coordinator.PrepareDrop(C(clock),principal,30,source,new[]{"one"},out op),"partial pair rejected");A(source.Items.Length==2&&coordinator.Records.Length==0,"partial pair unchanged");
  geometry.DenySecond=true;A(!coordinator.PrepareDrop(C(clock),principal,30,source,new[]{"one","two"},out op),"second placement denial");A(source.Items.Length==2&&weapons.IsActive(items[0],"a",1)&&geometry.Commits==0&&geometry.Aborts==1,"denial aborts all actual owners");geometry.DenySecond=false;
  A(coordinator.PrepareDrop(C(clock),principal,30,source,new[]{"one","two"},out op),"prepare actual pair");A(source.Items.Length==2&&coordinator.Records.Length==0,"prepare no protected mutation");clock.Now=30;A(!op.TryCommit(),"trusted expiry denies");A(source.Items.Length==2&&weapons.IsActive(items[1],"a",1),"expiry no retirement");op.Abort();op.Abort();
  clock.Now=31;A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"prepare generation failure");principal.Connection++;A(!op.TryCommit(),"old connection denied");A(source.Items.Length==2,"old connection preserves pair");
  A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"prepare lease loss");geometry.Valid=false;A(!op.TryCommit(),"lost geometry");geometry.Valid=true;
  A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"life-bound prepare");principal.Generation++;A(!op.TryCommit()&&source.Items.Length==2,"old life final denial");
  A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"revocation prepare");principal.Allowed=false;A(!op.TryCommit()&&source.Items.Length==2,"final target principal mandatory");principal.Allowed=true;
  A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"reserve ABA prepare");ammo[0].Take(1);ammo[0].Refund(1);A(!op.TryCommit()&&source.Items.Length==2&&ammo[0].Available==11,"world composition detects reserve ABA");
  A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"drop prepare");A(op.TryCommit(),"real drop publish");op.Abort();A(op.TryCommit(),"idempotent commit");A(source.Items.Length==0&&coordinator.Records.Length==2&&!weapons.IsActive(items[0],"a",1),"inventory lifetime world publish");A(!weapons.Capture(C(clock),"one").Accepted,"legacy capture cannot steal world");
  var records=coordinator.Records;A(records[0].Mechanics.Loaded==3&&records[0].Reserve==11&&records[1].Reserve==13,"exact record reserve and mechanics");A(coordinator.Pending.Length==1&&coordinator.Pending[0].Sequence==1,"retained event retry");A(coordinator.Pending[0].Sequence==1,"lost ack leaves same event");
  principal.Actor="b";clock.Now=32;A(coordinator.PrepareCollect(C(clock,"b",2),principal,60,target,records,out op),"authenticated remap collect prepare");A(!coordinator.PrepareCollect(C(clock,"b",2),principal,60,target,records,out other),"competing recollector rejected");A(target.Items.Length==0&&coordinator.Records.Length==2,"collect prepare untouched");A(op.TryCommit(),"collect publish");A(target.Items.Length==2&&target.Holstered&&coordinator.Records.Length==0&&weapons.IsActive(items[0],"b",2),"real target inventory and lifetime");A(weapons.Inspect("one").Loaded==3&&ammo[0].Available==11&&ammo[1].Available==13,"no ammo mint remap");A(!coordinator.PrepareCollect(C(clock,"b",2),principal,60,target,records,out other),"old record tombstone");A(coordinator.Pending.Length==2&&coordinator.Ack(1)&&!coordinator.Ack(1)&&coordinator.Pending[0].Sequence==2,"retained ack once");
  A(target.Apply(C(clock,"b",2),InventoryOperation.Equip,null,"one").Accepted,"equip actual target");weapons.Selection(target.Capture(),clock.Now);
  clock.Now+=20;var input=new HandlingInput{Sequence=1,Enabled=true,PrimaryTracked=true,Desktop=true,DeltaSeconds=.05f};
  A(weapons.Apply(target.Capture(),C(clock,"b",2),"one",HandlingAction.Sample,input).Accepted,"release actual new owner");clock.Now++;input.Sequence=2;input.Trigger=true;
  A(weapons.Apply(target.Capture(),C(clock,"b",2),"one",HandlingAction.Fire,input).Accepted,"transferred actual shot");
  A(weapons.Inspect("one").Loaded==2&&weapons.Inspect("one").SpentChamber&&ammo[0].Available==11,"shot preserved chamber spent exact reserve");
  principal.Actor="b";A(coordinator.PrepareDrop(C(clock,"b",2),principal,clock.Now+20,target,new[]{"one","two"},out op)&&op.TryCommit(),"redrop spent pair");
  var spent=coordinator.Records;A(spent[0].Mechanics.Loaded==2&&spent[0].Mechanics.SpentChamber&&!spent[0].Mechanics.Chambered,"world exact spent chamber");
  clock.Now=spent[0].Deadline;A(!coordinator.PrepareCollect(C(clock,"b",2),principal,clock.Now+10,target,spent,out other),"expired immutable world lease denies recollect");
  A(target.Items.Length==0&&coordinator.Records.Length==2&&weapons.Inspect("one").Loaded==2,"expired recollect preserves actual owners and rounds");
  A(ammo[0].Take(100)==11&&ammo[0].Available==0&&ammo[0].Refund(100)==20&&ammo[0].Available==20,"finite bounded entitlement");
  Console.WriteLine("PASS "+checks+" real world pair/inventory/session/lifetime checks; auth and geometry fixtures; durability OPEN");
 }
 sealed class Eligibility:ITransferEligibility{public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
 sealed class EmptyPlanner:IDropPlanner{public bool Prepare(CommandContext c,ItemInstance[] i,out IDropBatch b){b=null;return false;}}
}


