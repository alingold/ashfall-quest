using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Ashfall.Core;
using Ashfall.Integration.HostEpochAuthority;
using Ashfall.Integration.InventoryRecovery;
using Ashfall.Integration.CombatPublication;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Combat;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Persistence;
using Handler=Ashfall.Modules.WeaponHandling.WeaponHandling;
class Tests
{
 static readonly string hash=new string('a',64);
 static EpochBinding Binding(long epoch){return new EpochBinding("s",epoch,SourceProfiles.Build,hash,hash);}
 static CommandContext C(string actor,string id,long epoch=0){return new CommandContext("s",actor,id,1,epoch,true);}
 static int count;
 static void Check(bool b,string label){if(!b)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
 static bool Throws(Action work){try{work();return false;}catch(InvalidOperationException){return true;}catch(ArgumentException){return true;}catch(IOException){return true;}catch(InvalidDataException){return true;}}
 sealed class World:IHealthWorld,IInteractionQuery,ICombatEvidence,ISessionClock,ISpawnWorld,IReconnectTokens,ISessionManagement
 {
  int token;
  public double Now {get{return 0;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return true;}}
  public bool TryGetPose(string id,out RescuePose pose){pose=new RescuePose();return false;}
  public bool HasLineOfSight(string actor,string owner,Float3 origin,Float3 target,float range){return true;}
  public bool TryResolve(CommandContext c,string id,out HitEvidence e){e=null;return false;}
  public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("spawn",new Float3(),-1,true,false)};}
  public bool IsSafe(string map,string actor,Float3 feet){return true;}
  public string Issue(){return "token"+(++token);}
  public bool CanManage(CommandContext c){return c.IsAuthority;}
 }
 sealed class Boundary:IInventoryTransactions
 {
  public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance i,ItemInstance[] outgoing){return RuleResult.Accept();}
  public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt r){r=new Receipt();return RuleResult.Accept();}
  sealed class Receipt:IInventoryReceipt{public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
 }
 static void Main(string[] args)
 {
  if(args.Length==2){using(var cold=new OwnerRegistry(Binding(0),args[1],false))cold.Serialize(delegate{Check(cold.IngressClosed,"cold subprocess closed");Check(!cold.OpenFreshSubset().Accepted,"cold subprocess cannot open");Check(!cold.PrepareGlobalRetirement().Accepted,"cold subprocess no global receipt");return true;});return;}
  string path=Path.Combine(args[0],"epoch.journal");var world=new World();var catalog=SourceCatalog.Create();HealthService health=null;Inventory inventory=null;Handler weapon=null;CombatRules combat=null;Session session=null;HealthPublicationLane producer=null;
  using(var root=new OwnerRegistry(Binding(0),path,true)) {
   Check(root.IngressClosed,"new authority starts quarantined");
   root.Serialize(delegate {
    health=root.CreateHealth(0,true,world,world);inventory=root.CreateInventory("p",catalog,new Boundary());var consumables=root.CreateInventory("q",catalog,new Boundary());
    weapon=root.CreateWeapon("p","gun","reserve/gun",WeaponMechanism.ServiceRifle,20,10);
    combat=root.CreateCombat(world,world,64);session=root.CreateSession(new SessionRules("map","respawn",hash,0,8,true,false,true,true,5),world,world,world,world);
    Check(!health.Register(C("host","q"),"p",0,false).Accepted,"raw health quarantined before open");
    Check(root.OpenFreshSubset().Accepted,"explicit fresh subset open");
    producer=root.CreateHealthProducer(new AtomicSettingsStore(Path.Combine(args[0],"health.json")),world,world,world);
    Check(producer.Register(C("host","register"),"p",-1,false).Accepted,"actual durable producer register");
    Check(!root.InspectHealthPrefix(C("host","inspect")).Accepted,"actual unacknowledged prefix blocks local check");
    var packets=producer.Offer(C("host","offer"));Check(packets.Length>0,"actual producer retains offered prefix");
    Check(!producer.Acknowledge(C("host","badack"),packets[packets.Length-1].Sequence+1).Accepted,"ACK cannot skip unoffered suffix");
    Check(producer.Acknowledge(C("host","ack"),packets[packets.Length-1].Sequence).Accepted,"actual producer ACK prefix");
    Check(root.InspectHealthPrefix(C("host","inspect2")).Accepted,"actual Health checkpoint matches actual ACK prefix locally");
    Check(!health.Advance(C("host","wrong",1)).Accepted,"authority epoch rejected");
    Check(inventory.Apply(C("p","banana"),InventoryOperation.Admit,new ItemInstance("banana","banana",1),null).Accepted,"actual inventory admission");
    var checkpoint=new CheckpointBinding(SourceProfiles.Build,ColdInventoryCheckpoint.CatalogFingerprint(catalog),"s","p",0,1,1,1);
    // Weapon is deliberately not admitted: exact relationship capture must fail.
    Check(Throws(delegate{root.CaptureInventory("p",checkpoint);}),"actual checkpoint rejects weapon membership mismatch");
    Check(inventory.Apply(C("p","gun"),InventoryOperation.Admit,new ItemInstance("gun","kestrel",1),null).Accepted,"actual inventory weapon member");
    Check(root.CaptureInventory("p",checkpoint).Length>0,"actual InventoryRecovery checkpoint from registry members");
    Check(consumables.Apply(C("q","banana"),InventoryOperation.Admit,new ItemInstance("banana","banana",1),null).Accepted,"actual consumable-only owner admission");
    Inventory.PreparedRemoval removal;Check(consumables.PrepareRemoval(C("q","remove"),"banana",out removal),"actual removal prepared");
    var consumableCheckpoint=new CheckpointBinding(SourceProfiles.Build,ColdInventoryCheckpoint.CatalogFingerprint(catalog),"s","q",0,1,1,1);
    Check(Throws(delegate{root.CaptureInventory("q",consumableCheckpoint);}),"actual pending Inventory token blocks checkpoint");
    Check(removal.Validate(),"actual removal validated before close");
    Check(health.ApplyLoss(C("host","loss"),"p",health.Read("p").Revision,0,1,false).Accepted,"actual Health loss before prepared gain");
    var snapshot=health.Read("p");HealthService.PreparedGain gain;
    Check(health.PrepareGain(C("p","gain"),"gain","p",snapshot.Revision,snapshot.LifeGeneration,snapshot.ConnectionGeneration,0,1,out gain),"actual Health gain prepared");
    Check(Throws(delegate{health.CreateCheckpoint(C("host","checkpoint"),hash);}),"actual pending Health token blocks checkpoint");
    Check(gain.Validate(),"actual Health token validated before close");
    IPreparedWeaponSuspension suspension;Check(weapon.PrepareSuspension(out suspension).Accepted&&suspension.Validate().Accepted,"actual untracked weapon suspension prepared");
    long revision=root.Revision;Check(root.CloseIngress().Accepted,"physical ingress tombstone flushed");Check(root.Revision==revision,"closure preserves registration revision");
    Check(!root.OpenFreshSubset().Accepted,"closed authority cannot reopen");
    Check(!health.Advance(C("host","after")).Accepted,"retained raw Health new ingress rejected");
    Check(!inventory.Apply(C("p","after"),InventoryOperation.Holster,null,null).Accepted,"retained raw Inventory new ingress rejected");
    Check(!weapon.Apply(C("p","after"),HandlingAction.Sample,new HandlingInput()).Accepted,"retained raw Weapon new ingress rejected");
    Check(Throws(delegate{combat.Equip(C("host","after"),"other","p","kestrel",1,1);}),"retained raw Combat new ingress rejected");
    string token;Check(!session.Join(C("p","after"),new JoinRequest(Protocol.Name,Protocol.Schema,hash,"P",null,false),out token).Accepted,"retained raw Session new ingress rejected");
    Check(!producer.Advance(C("host","after2")).Accepted,"retained actual producer new ingress rejected");
    long before=consumables.Revision;removal.CommitValidated();Check(consumables.Revision>before,"exposed legacy removal commits after close: global blocker");
    before=health.Read("p").Revision;gain.CommitValidated();Check(health.Read("p").Revision>before,"exposed legacy Health commit after close: global blocker");
    suspension.CommitValidated();Check(!suspension.Validate().Accepted,"legacy suspension commits after close: global blocker");
    Check(!root.PrepareGlobalRetirement().Accepted,"global retirement rejected despite closure and local checkpoint");
    Check(Throws(delegate{root.CreateInventory("late",catalog,new Boundary());}),"new objects cannot escape closed registry");return true;
   });
   Check(!health.Advance(C("host","offlane")).Accepted,"raw off-lane command rejected");
   bool rejected=false;var thread=new Thread(delegate(){rejected=Throws(delegate{root.Serialize(()=>true);});});thread.Start();thread.Join();Check(rejected,"wrong thread cannot enter host lane");
  }
  var child=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"cold \""+path+"\""){UseShellExecute=false});child.WaitForExit();Check(child.ExitCode==0,"physical cold subprocess quarantine");
  byte[] image=File.ReadAllBytes(path);
  foreach(int cut in new[]{0,1,55,56,57,80,111,112}) {
   string cutPath=Path.Combine(args[0],"cut"+cut);byte[] bytes=new byte[cut];Array.Copy(image,bytes,cut);File.WriteAllBytes(cutPath,bytes);
   bool safe=false;try{using(var cutRoot=new OwnerRegistry(Binding(0),cutPath,false)){safe=cutRoot.IngressClosed&&cutRoot.Serialize(()=>!cutRoot.OpenFreshSubset().Accepted);}}catch(IOException){safe=true;}catch(InvalidDataException){safe=true;}
   Check(safe,"physical cut/reopen fail closed "+cut);
  }
  Check(Throws(delegate{using(var wrong=new OwnerRegistry(Binding(1),path,false)){};}),"tombstone rejects different epoch");
  Check(Throws(delegate{using(var duplicate=new OwnerRegistry(Binding(0),path,true)){};}),"CreateNew cannot overwrite tombstone");
  using(var empty=new OwnerRegistry(Binding(0),Path.Combine(args[0],"empty"),true))empty.Serialize(delegate{Check(!empty.PrepareGlobalRetirement().Accepted,"empty caller inventory never establishes global quiescence");return true;});
  Console.WriteLine("Assertions "+count);
 }
}
