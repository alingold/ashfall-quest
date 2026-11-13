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
sealed class TestsR3 : IAuthorityGate,IHealthWorld,IInteractionQuery,IPickupActors,IInventoryTransactions,IDropPlanner
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
  readonly TestsR3 host;readonly EvictedInstance[] placements;bool done,aborted;
  public Batch(TestsR3 h,ItemInstance[] members){host=h;placements=new EvictedInstance[members.Length];for(int i=0;i<members.Length;i++)placements[i]=new EvictedInstance(members[i].InstanceId,members[i].DefinitionId,h.malformed?"forged":"owner-"+members[i].InstanceId,new Float3(.1f*i,0,0));}
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
 void Run(){
  // Safety path executes against actual public Pickups/Inventory views.
  foreach(string loss in new[]{"tracking","allowed","missing","disconnect","death"}){
   Init();Collect("b","banana");PrepareBanana("b");long revision=inventory.Revision;denied=true;
   if(loss=="tracking")tracked=false;if(loss=="allowed")allowed=false;if(loss=="missing")exists=false;
   if(loss=="disconnect")Check(health.SetConnected(C(),"a",health.Read("a").Revision,false).Accepted,"disconnect actual");
   if(loss=="death")Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,100,100,true).Accepted,"death actual");
   Advance(1);CheckStowed(loss+" both projections disarmed despite gameplay eligibility denial");Check(inventory.Items.Length==1&&inventory.Revision==revision+1,"safety only holsters once");Advance(2);Check(inventory.Revision==revision+1,"repeat safety no extra revision");
   if(loss=="tracking"){tracked=true;denied=false;Action(PickupAction.Equip,"b");Check(pickups.Phase("a")==Preparation.Open&&!inventory.Holstered,"recovery explicitly equips/reset preparation");}
  }
  Init();Collect("b","banana");PrepareBanana("b");tracked=false;Check(!bridge.Execute(C(),PickupAction.Consume,"b").Accepted,"ineligible Execute rejected");CheckStowed("Execute cancellation composed");
  // Failed preflight must not invoke local safety mutation; abort then retry.
  Init();Collect("b","banana");Action(PickupAction.Equip,"b");Inventory.PreparedConsumables pending;Check(inventory.PrepareConsumableSelection(C(),null,out pending),"reserve competing selection");long old=inventory.Revision;tracked=false;bool threw=false;try{Advance(1);}catch(InvalidOperationException){threw=true;}Check(threw&&inventory.Revision==old&&!inventory.Holstered&&pickups.HeldInstance("a")=="b","failed safety reservation changes neither view");pending.Abort();Advance(1);CheckStowed("safety retry");
  // Multi-actor Advance prepares every cancellation before any local mutation.
  Init();Collect("b","banana");Action(PickupAction.Equip,"b");Check(health.Register(C("z"),"z",-1,false).Accepted,"second actor");var z=new Inventory(SourceCatalog.Create(),this,"s","z",1,false);bridge.AddActor("z",z,new Locomotion(new PlayerTuning()));Register("zfood","banana");Check(bridge.Execute(C("z"),PickupAction.Reserve,"zfood").Accepted,"z reserve");Check(bridge.Execute(C("z"),PickupAction.Collect,"zfood").Accepted,"z collect");Check(bridge.Execute(C("z"),PickupAction.Equip,"zfood").Accepted,"z equip");Check(z.PrepareConsumableSelection(C("z"),null,out pending),"z busy");tracked=false;threw=false;try{Advance(1);}catch(InvalidOperationException){threw=true;}Check(threw&&!inventory.Holstered&&pickups.HeldInstance("a")=="b","all actor preflight preserves first");pending.Abort();Advance(1);CheckStowed("all actor retry a");Check(z.Holstered&&pickups.HeldInstance("z")==null,"all actor retry z");
  // Whole selected pair replacement, with real membership and world ownership.
  Init();bridge.BindDrops(this);Collect("s1","soda");Collect("s2","soda");Collect("b1","banana");Collect("b2","banana");Action(PickupAction.Equip,"s2");Register("incoming","banana");Action(PickupAction.Reserve,"incoming");
  foreach(string failure in new[]{"plan","expiry","commit","semantic","los","eligibility"}){
   plan=failure!="plan";valid=failure!="expiry";commit=failure!="commit";malformed=failure=="semantic";los=failure!="los";denied=failure=="eligibility";old=inventory.Revision;
   Check(!bridge.Execute(C(),PickupAction.Collect,"incoming").Accepted,"replacement rejects "+failure);Check(inventory.Revision==old&&inventory.Items.Length==4&&pickups.HeldInstance("a")=="s2"&&!inventory.Holstered&&pickups.Inspect("s1").Owner=="a"&&pickups.Inspect("incoming").Owner==null&&drops==0,"replacement zero mutation "+failure);
  }
  plan=valid=commit=los=true;malformed=denied=false;Action(PickupAction.Collect,"incoming");Check(inventory.Items.Length==3&&drops==2&&pickups.Inspect("s1").Owner==null&&pickups.Inspect("s2").Owner==null&&pickups.Inspect("incoming").Owner=="a","whole slot exact drop publication");CheckStowed("replacement stows");Check(inventory.SelectedInstanceId=="incoming","replacement selected source identity");Check(aborts==3,"expiry/commit/semantic failures each release one reservation; earlier gates allocate none");
  // Explicit drop preserves exact member, then recollect preserves identity.
  Init();bridge.BindDrops(this);Collect("s1","soda");Collect("s2","soda");Action(PickupAction.Equip,"s2");valid=false;old=inventory.Revision;Check(!bridge.Execute(C(),PickupAction.Drop,"s2").Accepted&&inventory.Revision==old&&pickups.HeldInstance("a")=="s2"&&drops==0,"expired drop preserves selected/preparation");valid=true;commit=false;Check(!bridge.Execute(C(),PickupAction.Drop,"s2").Accepted&&inventory.Items.Length==2,"drop commit refusal retains");commit=true;Action(PickupAction.Drop,"s2");CheckStowed("drop disarms rather than drawing survivor");Check(inventory.Items.Length==1&&pickups.Inspect("s2").Owner==null&&drops==1,"drop exact member");Action(PickupAction.Reserve,"s2");Action(PickupAction.Collect,"s2");Check(inventory.Items.Length==2&&inventory.SelectedInstanceId=="s2","recollect exact identity/order");
  // Concrete production reserve, positive partial accepts whole source box.
  Init();var reserve=new FiniteAmmoReserve(10,8);Check(bridge.BindAmmoReserve("a","rifle",reserve),"bind actual finite reserve");Check(!bridge.BindAmmoReserve("a","pistol",reserve),"no reserve aliases");Register("box","rifle");Action(PickupAction.Reserve,"box");var c=C();Check(bridge.Execute(c,PickupAction.Collect,"box").Accepted,"partial supply collect");Check(reserve.Available==10&&bridge.LastAcceptedSupply("a","rifle")==2&&pickups.Inspect("box").Removed,"partial2 removes full32 box");Check(!bridge.Execute(c,PickupAction.Collect,"box").Accepted&&reserve.Available==10,"supply replay no mint");Register("full","rifle");Action(PickupAction.Reserve,"full");Check(!bridge.Execute(C(),PickupAction.Collect,"full").Accepted&&!pickups.Inspect("full").Removed&&reserve.Available==10,"cap retains supply");Check(reserve.Take(7)==7,"real reserve spend");Action(PickupAction.Collect,"full");Check(reserve.Available==10&&bridge.LastAcceptedSupply("a","rifle")==7&&pickups.Inspect("full").Removed,"fresh command retry exact partial7");
  // Full shaker50, banana health100/stamina6 and soda50 over ten active seconds.
  Init();Collect("shake","shaker");Action(PickupAction.Equip,"shake");for(int i=0;i<3;i++){Action(PickupAction.DesktopRelease,"shake");Action(PickupAction.DesktopPress,"shake");}Advance(180);Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,0,80,false).Accepted,"shaker full setup");Action(PickupAction.Consume,"shake");Check(health.Read("a").Shield==70&&inventory.Items.Length==0,"full shaker exact50");
  Init();Collect("soda","soda");Action(PickupAction.Equip,"soda");Action(PickupAction.DesktopRelease,"soda");Action(PickupAction.DesktopPress,"soda");Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,0,100,false).Accepted,"soda setup");Action(PickupAction.Consume,"soda");Check(pickups.PendingSoda("a")==50,"soda schedules50");Advance(60);Check(health.Read("a").Shield==5&&pickups.PendingSoda("a")==45,"soda exact5 per second");Advance(600);Check(health.Read("a").Shield==50&&pickups.PendingSoda("a")==0,"soda exact total50 ten seconds");Advance(660);Check(health.Read("a").Shield==50,"soda no excess duration");
  Init();Collect("ban","banana");PrepareBanana("ban");Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,90,0,false).Accepted,"banana setup");Action(PickupAction.Consume,"ban");Check(health.Read("a").Health==100&&move.SprintStamina==6&&inventory.Items.Length==0,"banana exact capped health/stamina and removal");
  // Collect's actual expiry and competing source ownership.
  Init();Register("expires","banana");Action(PickupAction.Reserve,"expires");Advance(60);Check(!bridge.Execute(C(),PickupAction.Collect,"expires").Accepted&&inventory.Items.Length==0,"exact lease expiry retains source");Action(PickupAction.Reserve,"expires");Action(PickupAction.Collect,"expires");
  Init();Register("race","soda");Action(PickupAction.Reserve,"race");var ca=C();var cb=C();RuleResult ra=new RuleResult(),rb=new RuleResult();var ta=new Thread(delegate(){ra=bridge.Execute(ca,PickupAction.Collect,"race");});var tb=new Thread(delegate(){rb=bridge.Execute(cb,PickupAction.Collect,"race");});ta.Start();tb.Start();ta.Join();tb.Join();Check(ra.Accepted!=rb.Accepted&&inventory.Items.Length==1,"concurrent collect once");
  Console.WriteLine("PASS "+checks+" R3 composed assertions; geometry/actor/auth ports are fixtures, actual Inventory/Pickups/Health/Locomotion/FiniteAmmoReserve");
 }
 static void Main(){new TestsR3().Run();}
}


