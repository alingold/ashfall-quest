using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.InventoryTransfers;
using Ashfall.Integration.WorldWeaponTransfers;
public class RecoveryTests
{
 static int count,serial; static void A(bool b,string s){count++;if(!b)throw new Exception(s);}
 sealed class Host:IAuthorityGate,IWeaponLifetimeAuthority,IInventoryTransactions {
  public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("authority");}
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Validate(c);}
  public RuleResult AuthorizeRestore(CommandContext c,string id,string previous,long epoch){return Validate(c);}
  public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] outgoing){return Validate(c);}
  public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt){receipt=new Receipt();return Validate(c);}
  sealed class Receipt:IInventoryReceipt{public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
 }
 sealed class Clock:IWeaponHostClock{public long Now=10;public long Tick{get{return Now;}}}
 sealed class Principal:IWeaponPrincipal{public string CanonicalActor{get{return "a";}}public long ConnectionLife{get{return 1;}}public long Life{get{return 1;}}public bool Validate(CommandContext c,long d){return c.ActorId=="a";}}
 sealed class Geometry:IWeaponGeometry {
  public bool Prepare(CommandContext c,IWeaponPrincipal p,long d,ItemInstance[] drop,WorldWeaponRecord[] collect,out IWeaponGeometryLease lease){var places=new WeaponPlacement[drop.Length];for(int i=0;i<drop.Length;i++)places[i]=new WeaponPlacement(drop[i].InstanceId,"body","owner","lease"+c.CommandId,c.Tick,new Float3(1,2,3));lease=new Lease(places);return true;}
  sealed class Lease:IWeaponGeometryLease{readonly WeaponPlacement[] places;public Lease(WeaponPlacement[] p){places=p;}public WeaponPlacement[] Placements{get{return places;}}public bool Validate(){return true;}public void CommitValidated(){}public void Abort(){}}
 }
 static CommandContext C(Clock clock){return new CommandContext("s","a","c"+(++serial),clock.Now,1,true);}
 sealed class Fixture:IDisposable {
  public readonly Clock clock=new Clock();public readonly Inventory inventory;public readonly SessionWeapons weapons;public readonly WorldWeaponCoordinator world;public readonly SessionWeaponWorldAdapter adapter;public readonly WeaponTransferJournal journal;public readonly Principal principal=new Principal();public readonly ItemInstance item=new ItemInstance("stable","longwatch",1);
  public Fixture(string path,long capacity){var h=new Host();inventory=new Inventory(SourceCatalog.Create(),h,"s","a",1,false);weapons=new SessionWeapons("s",60,h,h);A(weapons.Grant(C(clock),item,WeaponMechanism.PrecisionRifle,3,new FiniteAmmoReserve(20,11)).Accepted,"grant");A(inventory.Apply(C(clock),InventoryOperation.Admit,item,null).Accepted,"admit");var lane=new object();A(weapons.BindPreparedWorldActionOwner(lane,delegate(string actor){return inventory;}),"bind");journal=new WeaponTransferJournal(path,capacity);world=new WorldWeaponCoordinator(lane,weapons,clock,new Geometry(),100,journal);adapter=new SessionWeaponWorldAdapter(lane,delegate(string actor){return inventory;},world);}
  public bool Drop(out WorldWeaponCoordinator.Operation op){clock.Now++;return adapter.PrepareDrop(C(clock),principal,clock.Now+90,new[]{"stable"},out op);}
  public bool Collect(out WorldWeaponCoordinator.Operation op){clock.Now++;return adapter.PrepareCollect(C(clock),principal,clock.Now+90,world.Records,out op);}
  public void Dispose(){journal.Dispose();}
 }
 public static void Main(string[] args){string mode=args[0],path=args[1];
  if(mode=="inspect"||mode=="inspectack"||mode=="inspectprepare"){
   using(var journal=new WeaponTransferJournal(path,10000000)){var frames=journal.ReadFrames();A(frames.Length==(mode=="inspect"?3:mode=="inspectack"?4:1),"cold frames retained");A(frames[0][0]==1,"prepared before irreversible publication");if(mode!="inspectprepare")A(frames[2][0]==3,"published durable");if(mode=="inspectack")A(frames[3][0]==5,"ack durable");
    bool denied=false;try{var h=new Host();new WorldWeaponCoordinator(new object(),new SessionWeapons("s",60,h,h),new Clock(),new Geometry(),100,journal);}catch(InvalidOperationException){denied=true;}A(denied,"missing module recovery refuses empty restart");
   }Console.WriteLine("PASS "+count+" cold retained evidence; actual module restoration BLOCKED");return;
  }
  if(mode=="corrupt"){bool denied=false;try{using(var j=new WeaponTransferJournal(path,10000000)){} }catch(InvalidDataException){denied=true;}A(denied,"torn journal refuses reopen");Console.WriteLine("PASS corruption refuses reopen");return;}
  if(mode=="write"||mode=="writeack"||mode=="prepare"){
   var f=new Fixture(path,10000000);WorldWeaponCoordinator.Operation op;A(f.Drop(out op),"prepared");if(mode!="prepare"){A(op.TryCommit(),"committed");A(f.inventory.Items.Length==0&&f.world.Records.Length==1,"actual drop");if(mode=="writeack")A(f.world.Ack(1),"ack");}Console.WriteLine("PASS subprocess boundary "+mode);Environment.Exit(0);
  }
  using(var f=new Fixture(path,mode=="capacity"?4096:10000000)){
   WorldWeaponCoordinator.Operation op,second;
   bool claimed=false;try{new WorldWeaponCoordinator(new object(),f.weapons,f.clock,new Geometry(),100,f.journal);}catch(InvalidOperationException){claimed=true;}A(claimed,"journal object cannot have two active owners");
   if(mode=="decisionfail"||mode=="ackfail"){
    A(f.Drop(out op),"failure prepare");
    if(mode=="decisionfail"){f.journal.Dispose();A(!op.TryCommit(),"uncertain decision rejects");A(f.inventory.Items.Length==1&&f.world.Records.Length==0&&f.weapons.IsActive(f.item,"a",1),"decision IO failure no retirement");}
    else{A(op.TryCommit(),"ack failure initial commit");f.journal.Dispose();A(!f.world.Ack(1)&&f.world.Pending.Length==1,"ack IO failure preserves retry");}
    A(f.world.RecoveryRequired&&!f.Drop(out second),"journal failure quarantines subsequent admission");
   }else if(mode=="capacity"){
    int cycles=0;while(f.Drop(out op)){A(op.TryCommit(),"capacity drop commit");if(!f.Collect(out op))break;A(op.TryCommit(),"capacity collect commit");cycles++;A(cycles<10,"bounded capacity");}
    A(!f.world.RecoveryRequired,"capacity refusal is prepublication");A(f.world.Records.Length+f.inventory.Items.Length==1,"capacity preserves one weapon");A(f.weapons.Inspect("stable").Loaded==3,"capacity no ammo mint");
   }else{
    A(f.Drop(out op),"abandoned prepare");f.clock.Now+=91;A(f.Drop(out second),"next admission expires abandoned token");A(!op.TryCommit(),"expired token rejected");second.Abort();
    for(int i=0;i<128;i++){A(f.Drop(out op)&&op.TryCommit(),"pressure drop");A(f.Collect(out op)&&op.TryCommit(),"pressure collect");}
    A(f.world.Pending.Length==256,"pending cap");A(!f.Drop(out op)&&f.inventory.Items.Length==1,"pressure rejects before retirement");A(f.world.Ack(1)&&!f.world.Ack(1),"ack once");A(f.Drop(out op)&&op.TryCommit(),"ack frees pressure");A(f.weapons.Inspect("stable").Loaded==3&&f.world.Records[0].Reserve==11,"exact ammo under pressure");
   }
  }Console.WriteLine("PASS "+count+" journal/expiry/adapter/pressure checks");
 }
}
