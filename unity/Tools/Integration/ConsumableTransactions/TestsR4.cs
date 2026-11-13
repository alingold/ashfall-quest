using System;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Player;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.ConsumableTransactions;
using Ashfall.Integration.InventoryTransfers;
sealed class TestsR4 : IAuthorityGate,IHealthWorld,IInteractionQuery,IPickupActors,IInventoryTransactions,IDropPlanner
{
 public double Now {get;set;}public bool InMatch {get{return true;}}public bool TeamChangesAllowed {get{return false;}}
 public bool TryGetPose(string actor,out RescuePose pose){pose=new RescuePose();return false;}
 public bool denied,tracked=true,allowed=true,exists=true,los=true,plan=true,valid=true,commit=true,malformed;
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance incoming,ItemInstance[] outgoing){return denied?RuleResult.Reject("fixture-denied"):RuleResult.Accept();}
 sealed class Receipt:IInventoryReceipt {public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
 public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt r){r=new Receipt();return RuleResult.Accept();}
 public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float range){return los;}
 public bool TryRead(string actor,out ActorFrame frame){var h=health.Read(actor);frame=new ActorFrame{Allowed=allowed&&h!=null&&h.Connected&&h.Life==LifeState.Alive,HandEligible=tracked,TransferSafe=true,Desktop=true,Forward=new Float3(0,0,1),Health=h==null?0:h.Health,Shield=h==null?0:h.Shield,Stamina=move.SprintStamina,MaxStamina=6};return exists;}
 sealed class Batch:IDropBatch {
  readonly TestsR4 host;readonly EvictedInstance[] placements;bool done,aborted;
  public Batch(TestsR4 h,ItemInstance[] members){host=h;placements=new EvictedInstance[members.Length];for(int i=0;i<members.Length;i++)placements[i]=new EvictedInstance(members[i].InstanceId,members[i].DefinitionId,h.malformed?"forged":"owner-"+members[i].InstanceId,new Float3(.1f*i,0,0));}
  public EvictedInstance[] Placements {get{return (EvictedInstance[])placements.Clone();}}
  public bool StillValid {get{return !aborted&&host.valid;}}
  public bool Commit(){if(done)return true;if(!StillValid||!host.commit)return false;done=true;host.drops+=placements.Length;return true;}
  public void Abort(){if(done||aborted)return;aborted=true;host.aborts++;}
 }
 public bool Prepare(CommandContext c,ItemInstance[] members,out IDropBatch batch){batch=plan?new Batch(this,members):null;return plan;}
 HealthService health;Inventory inventory;ConsumableBridge bridge;PickupRules pickups;Locomotion move;long seq,tick;int drops,aborts;static int checks;
 CommandContext C(string actor="a"){return new CommandContext("s",actor,"r3-"+Interlocked.Increment(ref seq),tick,1,true);}
 static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
 void Init(){Now=0;tick=0;seq=0;denied=false;tracked=allowed=exists=los=plan=valid=commit=true;malformed=false;drops=aborts=0;health=new HealthService("s",1,0,false,this,this,this);Check(health.Register(C(),"a",-1,false).Accepted,"register");move=new Locomotion(new PlayerTuning());inventory=new Inventory(SourceCatalog.Create(),this,"s","a",1,false);bridge=new ConsumableBridge(health);pickups=new PickupRules("s",1,60,this,this,bridge,this,10000,8,100);bridge.Bind(pickups);bridge.AddActor("a",inventory,move);}
 void Action(PickupAction action,string id){var r=bridge.Execute(C(),action,id);Check(r.Accepted,action+":"+r.Reason);}
 void Collect(string id,string kind){Register(id,kind);Action(PickupAction.Reserve,id);Action(PickupAction.Collect,id);Check(inventory.Holstered&&pickups.HeldInstance("a")==null&&inventory.SelectedInstanceId==id,"source store selects incoming stowed");}
 void Register(string id,string kind){Check(pickups.Register(id,kind,"owner-"+id,new Float3()),"register item");}
 void Advance(long t){tick=t;Now=t/60.0;bridge.Advance(C());}
 void PrepareBanana(string id){Action(PickupAction.Equip,id);for(int i=0;i<4;i++){Action(PickupAction.DesktopRelease,id);Action(PickupAction.DesktopPress,id);}}
 void CheckStowed(string name){Check(inventory.Holstered&&pickups.HeldInstance("a")==null&&pickups.Phase("a")==Preparation.Idle,name);}

 sealed class Approval:IWeaponLifetimeAuthority {
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism mechanism,int rounds){return RuleResult.Accept();}
  public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return RuleResult.Accept();}
 }
 sealed class Grants:IBuildGrantAuthority {public bool deny;public bool Validate(CommandContext c,string receipt,string kind,int amount){return !deny&&receipt=="box"&&kind=="builds"&&amount==6;}}
 void Run(){
  Init();Collect("s1","soda");Collect("s2","soda");
  Inventory.PreparedConsumables old,next;Check(inventory.PrepareConsumableSelection(C(),"s1",out old),"prepare old selection");
  tick=120;Check(inventory.AdvanceConsumableReservations(C()),"trusted expiry advance");Check(!old.Validate(),"expiry at deadline");
  Check(inventory.PrepareConsumableSelection(C(),"s2",out next),"expired reservation reclaimed");old.Abort();Check(next.Validate(),"old Abort cannot release new token");next.CommitValidated();old.Abort();Check(inventory.SelectedInstanceId=="s2"&&!inventory.Holstered,"new selection retained");
  Inventory.PreparedRemoval removal;Check(inventory.PrepareRemoval(C(),"s2",out removal),"prepare removal");denied=true;Check(!removal.Validate(),"second stage eligibility rejection");removal.Abort();Check(inventory.Items.Length==2,"rejection no removal");denied=false;
  Check(inventory.PrepareConsumableSelection(C(),"s1",out next),"prepare selection revalidation");denied=true;Check(!next.Validate(),"second stage selection denied");next.Abort();denied=false;
  Check(inventory.PrepareRemoval(C(),"s2",out removal),"prepare expiry removal");tick=240;Check(inventory.AdvanceConsumableReservations(C())&&!removal.Validate(),"removal expires");Check(inventory.PrepareConsumableSelection(C(),"s1",out next),"new token after removal expiry");removal.Abort();Check(next.Validate(),"old removal abort identity safe");next.Abort();
  var invalid=new CommandContext("s","a","bad",100000,2,true);Check(!inventory.AdvanceConsumableReservations(invalid),"wrong epoch cannot advance lease clock");
  Init();var registry=new SessionWeapons("s",60,new Approval(),this);var gun=new ItemInstance("gun","longwatch",1);var reserve=new FiniteAmmoReserve(100,11);
  Check(registry.Grant(C(),gun,WeaponMechanism.PrecisionRifle,3,reserve).Accepted,"actual lifetime grant fixture");Check(inventory.Apply(C(),InventoryOperation.Admit,gun,null).Accepted,"actual mixed inventory");
  Register("b","banana");Action(PickupAction.Reserve,"b");Check(!bridge.Execute(C(),PickupAction.Collect,"b").Accepted,"mixed without registry rejected");Check(bridge.BindWeapons(registry),"bind actual registry once");Check(!bridge.BindWeapons(registry),"registry rebinding rejected");
  Check(inventory.Apply(C(),InventoryOperation.Equip,null,"gun").Accepted,"fixture selects real gun");registry.Selection(inventory.Capture(),tick,false,true);var before=registry.Inspect("gun");
  Action(PickupAction.Collect,"b");var after=registry.Inspect("gun");Check(before.Loaded==after.Loaded&&before.Chambered==after.Chambered&&after.Shots==before.Shots&&reserve.Available==11,"suspension preserves mechanical quantities");Check(!after.Supported&&!after.ClaimsSupport&&after.TriggerReleaseRequired,"physical support/trigger suspension");
  Check(!bridge.ApplyWeapon(C(),"gun",HandlingAction.Fire,new HandlingInput{Enabled=true,PrimaryTracked=true,Trigger=true,Sequence=100}).Accepted,"consumable selection rejects actual gun action");PrepareBanana("b");tracked=false;Advance(1);CheckStowed("mixed tracking loss");tracked=true;Action(PickupAction.Equip,"b");Check(pickups.Phase("a")==Preparation.Open,"tracking return resets preparation");
  Init();var grants=new Grants();var accounts=new BuildChargeAccounts("s",1,this,grants);Check(bridge.BindBuilds(accounts),"bind actual root charge accounts");Check(pickups.Register("box","builds","box-owner",new Float3(),6),"source build quantity6");Action(PickupAction.Reserve,"box");grants.deny=true;Check(!bridge.Execute(C(),PickupAction.Collect,"box").Accepted&&accounts.ForActor("a").Balance==0&&!pickups.Inspect("box").Removed,"grant rejection retains actual source");grants.deny=false;Action(PickupAction.Collect,"box");Check(accounts.ForActor("a").Balance==6&&pickups.Inspect("box").Removed,"actual charge publication and source removal");Check(!bridge.Execute(C(),PickupAction.Collect,"box").Accepted&&accounts.ForActor("a").Balance==6,"once only build source");
  Check(pickups.Register("wrong","builds","wrong-owner",new Float3(),7),"explicit source7");Action(PickupAction.Reserve,"wrong");Check(!bridge.Execute(C(),PickupAction.Collect,"wrong").Accepted&&!pickups.Inspect("wrong").Removed,"unsupported source amount never coerced to six");
  Init();Collect("drink","soda");Action(PickupAction.Equip,"drink");Action(PickupAction.DesktopRelease,"drink");Action(PickupAction.DesktopPress,"drink");Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,0,5,false).Accepted,"soda cap setup");Action(PickupAction.Consume,"drink");Advance(60);Check(health.Read("a").Shield==100&&pickups.PendingSoda("a")==45,"soda reaches cap");Advance(120);Check(pickups.PendingSoda("a")==40,"duration drains at cap");allowed=false;Advance(180);Check(pickups.PendingSoda("a")==40,"paused eligibility retains unapplied duration");allowed=true;Advance(660);Check(pickups.PendingSoda("a")==0,"remaining duration expires exactly");Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,0,20,false).Accepted,"damage after duration");Advance(720);Check(health.Read("a").Shield==80,"exhausted soda no revival");Check(!bridge.Execute(C(),PickupAction.Consume,"drink").Accepted&&pickups.Inspect("drink").Removed,"removed consumable tombstone prevents reuse");
  Init();Collect("food","banana");PrepareBanana("food");long generation=health.Read("a").ConnectionGeneration;Check(health.SetConnected(C(),"a",health.Read("a").Revision,false).Accepted,"actual disconnect");Advance(1);CheckStowed("disconnect reset");Check(health.SetConnected(C(),"a",health.Read("a").Revision,true).Accepted,"actual reconnect");Check(health.Read("a").ConnectionGeneration>generation,"connection generation changed");Check(!bridge.Execute(C(),PickupAction.Consume,"food").Accepted,"old preparation cannot resume on reconnect");Action(PickupAction.Equip,"food");Check(pickups.Phase("a")==Preparation.Open,"explicit reconnect equip fresh preparation");
  Console.WriteLine("PASS "+checks+" R4 actual module assertions; authority/poses/geometry are CPU fixtures; no production custody recovery claim");
 }
 static void Main(){new TestsR4().Run();}
}

