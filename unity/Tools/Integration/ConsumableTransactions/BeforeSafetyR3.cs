using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Player;
using Ashfall.Integration.ConsumableTransactions;
sealed class BeforeSafety:IAuthorityGate,IHealthWorld,IInteractionQuery,IPickupActors,IInventoryTransactions {
 public double Now{get{return 0;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
 public bool TryGetPose(string a,out RescuePose p){p=new RescuePose();return false;}
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float r){return true;}
 bool tracking=true;public bool TryRead(string a,out ActorFrame f){f=new ActorFrame{Allowed=true,HandEligible=tracking,TransferSafe=true,Desktop=true,Forward=new Float3(0,0,1),Health=100,Shield=100,Stamina=6,MaxStamina=6};return true;}
 public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance i,ItemInstance[] o){return RuleResult.Accept();}
 sealed class R:IInventoryReceipt{public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
 public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt r){r=new R();return RuleResult.Accept();}
 int seq;CommandContext C(long tick){return new CommandContext("s","a","before"+(++seq),tick,1,true);}
 void Run(){var h=new HealthService("s",1,0,false,this,this,this);h.Register(C(0),"a",-1,false);var inv=new Inventory(SourceCatalog.Create(),this,"s","a",1,false);var b=new ConsumableBridge(h);var p=new PickupRules("s",1,60,this,this,b,this,128,4,10);b.Bind(p);b.AddActor("a",inv,new Locomotion(new PlayerTuning()));p.Register("banana","banana","semantic",new Float3());foreach(var a in new[]{PickupAction.Reserve,PickupAction.Collect,PickupAction.Equip})if(!b.Execute(C(0),a,"banana").Accepted)throw new Exception("setup");tracking=false;b.Advance(C(1));Console.WriteLine("Pickups held="+(p.HeldInstance("a")??"null")+"; Inventory holstered="+inv.Holstered+" selected="+inv.SelectedInstanceId);if(p.HeldInstance("a")!=null||inv.Holstered)throw new Exception("expected baseline defect absent");Console.WriteLine("REPRODUCED original safety projection defect (expected failure; no acceptance)");}
 static void Main(){new BeforeSafety().Run();}
}
