using System;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Player;
using Ashfall.Integration.ConsumableTransactions;
sealed class Tests : IAuthorityGate,IHealthWorld,IInteractionQuery,IPickupActors,IInventoryTransactions
{
    public double Now {get{return 0;}}public bool InMatch {get{return true;}}public bool TeamChangesAllowed {get{return false;}}
    public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}
    public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
    public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float r){return true;}
    public bool TryRead(string id,out ActorFrame f){var h=health.Read(id);f=new ActorFrame{Allowed=h!=null&&h.Connected&&h.Life==LifeState.Alive,HandEligible=true,TransferSafe=true,Desktop=true,Forward=new Float3(0,0,1),Health=h==null?0:h.Health,Shield=h==null?0:h.Shield,Stamina=move.SprintStamina,MaxStamina=6};return true;}
    public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance incoming,ItemInstance[] outgoing){return RuleResult.Accept();}
    sealed class Receipt:IInventoryReceipt {public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
    public RuleResult Stage(CommandContext c,InventoryChange x,out IInventoryReceipt r){r=new Receipt();return RuleResult.Accept();}
    HealthService health;ConsumableBridge bridge;PickupRules pickups;Inventory inventory;Locomotion move;long seq;static int checks;
    CommandContext C(string actor="a"){return new CommandContext("s",actor,"t"+(++seq),0,1,true);}
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    void Init(){seq=0;health=new HealthService("s",1,0,false,this,this,this);Check(health.Register(C(),"a",-1,false).Accepted,"register");move=new Locomotion(new PlayerTuning());inventory=new Inventory(SourceCatalog.Create(),this,"s","a",1,false);bridge=new ConsumableBridge(health);pickups=new PickupRules("s",1,60,this,this,bridge,this,10000,4,100);bridge.Bind(pickups);bridge.AddActor("a",inventory,move);}
    void Action(PickupAction a,string id){var r=bridge.Execute(C(),a,id);Check(r.Accepted,a+":"+r.Reason);}
    void Collect(string id,string kind){Check(pickups.Register(id,kind,"owner-"+id,new Float3()),"register item");Action(PickupAction.Reserve,id);Action(PickupAction.Collect,id);Check(pickups.HeldInstance("a")==null,"collect stowed");}
    void Prepare(string id,string kind){Action(PickupAction.Equip,id);for(int i=0;i<(kind=="banana"?4:1);i++){Action(PickupAction.DesktopRelease,id);Action(PickupAction.DesktopPress,id);}}
    void Run(){Init();Collect("b","banana");Prepare("b","banana");var command=C();Check(!bridge.Execute(command,PickupAction.Consume,"b").Accepted,"full retains");Check(!pickups.Inspect("b").Removed&&inventory.Items.Length==1,"zero preserves");Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,20,0,false).Accepted,"loss");Action(PickupAction.Consume,"b");Check(health.Read("a").Health==100&&inventory.Items.Length==0&&pickups.Inspect("b").Removed,"actual banana composed");Check(pickups.Phase("a")==Preparation.Idle,"reset");Check(!bridge.Execute(command,PickupAction.Consume,"b").Accepted,"replay");
    Init();Collect("s1","soda");Collect("s2","soda");Prepare("s2","soda");Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,0,1,false).Accepted,"shield loss");Action(PickupAction.Consume,"s2");Check(inventory.Items.Length==1&&inventory.SelectedInstanceId=="s1"&&pickups.HeldInstance("a")=="s1","ordered survivor");bridge.Advance(new CommandContext("s","a","tick",60,1,true));Check(health.Read("a").Shield==100&&pickups.PendingSoda("a")==45,"partial shield");bridge.Advance(new CommandContext("s","a","tick2",120,1,true));Check(pickups.PendingSoda("a")==40,"cap drains");bridge.Advance(new CommandContext("s","a","tick3",120,1,true));Check(pickups.PendingSoda("a")==40,"tick once");
    Init();Collect("b","banana");Inventory.PreparedRemoval removal;Check(inventory.PrepareRemoval(C(),"b",out removal),"prepare remove");Check(inventory.Items.Length==1&&removal.Validate(),"prepare unchanged");Check(!inventory.Apply(C(),InventoryOperation.Holster,null,null).Accepted,"busy prevents mutation");removal.Abort();removal.Abort();Check(!removal.Validate()&&inventory.Items.Length==1,"abort retains");
    var state=health.Read("a");HealthService.PreparedGain gain;Check(health.PrepareGain(C(),"g","a",state.Revision,state.LifeGeneration,state.ConnectionGeneration,0,20,out gain),"prepare gain");Check(gain.Proposed.Outcome==GainOutcome.CappedNoOp,"cap explicit");Check(health.SetConnected(C(),"a",state.Revision,false).Accepted,"disconnect");Check(!gain.Validate(),"stale connection");gain.Abort();
    Init();state=health.Read("a");Check(health.ApplyLoss(C(),"a",state.Revision,20,20,false).Accepted,"loss");state=health.Read("a");var c=C();Check(health.PrepareGain(c,"once","a",state.Revision,state.LifeGeneration,state.ConnectionGeneration,10,50,out gain),"gain");Check(gain.Validate(),"validate");var receipt=gain.CommitValidated();gain.Abort();Check(receipt.Health==10&&receipt.Shield==20&&health.Read("a").Health==90&&health.Read("a").Shield==100,"partial gain");Check(Object.ReferenceEquals(receipt,health.FindGain(c,"once","a")),"receipt cached");Check(!health.PrepareGain(c,"once","a",state.Revision,state.LifeGeneration,state.ConnectionGeneration,10,50,out gain),"logical once");
        // Real stamina-only banana: sprint through the actual locomotion state machine.
    Init();var env=new MotionEnvironment{Grounded=true,IsXr=true};
    move.Step(new InputFrame{Tick=1,Sequence=1,Tracked=true,MoveY=1,Sprint=true},env,.1f);
    move.Step(new InputFrame{Tick=2,Sequence=2,Tracked=true,MoveY=1},env,.1f);
    for(int i=3;i<8;i++)move.Step(new InputFrame{Tick=i,Sequence=i,Tracked=true,MoveY=1,Sprint=true},env,.1f);
    Check(move.SprintStamina<6,"actual stamina depleted");Collect("b","banana");Prepare("b","banana");Action(PickupAction.Consume,"b");Check(move.SprintStamina==6&&health.Read("a").Health==100&&inventory.Items.Length==0,"stamina only positive effect");
    // Shaker requires both shake clicks and elapsed active time.
    Init();Collect("s","shaker");Action(PickupAction.Equip,"s");for(int i=0;i<3;i++){Action(PickupAction.DesktopRelease,"s");Action(PickupAction.DesktopPress,"s");}
    bridge.Advance(new CommandContext("s","a","shake-time",180,1,true));
    Check(health.ApplyLoss(new CommandContext("s","a","damage",180,1,true),"a",health.Read("a").Revision,0,7,false).Accepted,"shaker setup loss");
    var shakerCommand=new CommandContext("s","a","drink",180,1,true);Check(bridge.Execute(shakerCommand,PickupAction.Consume,"s").Accepted,"actual shaker consume");Check(health.Read("a").Shield==100&&inventory.Items.Length==0,"shaker partial cap");Check(!bridge.Execute(shakerCommand,PickupAction.Consume,"s").Accepted,"shaker replay");
    // Two actors contend for one world identity through the shared host lane.
    Init();Check(health.Register(C(),"b",-1,false).Accepted,"second actor");var other=new Inventory(SourceCatalog.Create(),this,"s","b",1,false);bridge.AddActor("b",other,new Locomotion(new PlayerTuning()));Check(pickups.Register("race","soda","semantic",new Float3()),"race item");
    RuleResult ra=new RuleResult(),rb=new RuleResult();var ca=C();var cb=C("b");var ta=new Thread(delegate(){ra=bridge.Execute(ca,PickupAction.Reserve,"race");});var tb=new Thread(delegate(){rb=bridge.Execute(cb,PickupAction.Reserve,"race");});ta.Start();tb.Start();ta.Join();tb.Join();Check(ra.Accepted!=rb.Accepted,"one reservation winner");string winner=ra.Accepted?"a":"b";Check(bridge.Execute(C(winner),PickupAction.Collect,"race").Accepted,"winner collect");Check(inventory.Items.Length+other.Items.Length==1,"one actual inventory mutation");
    // Stale lease does not collect or remove a world item.
    Init();Check(pickups.Register("stale","soda","semantic",new Float3()),"stale register");Action(PickupAction.Reserve,"stale");bridge.Advance(new CommandContext("s","a","later",61,1,true));Check(!bridge.Execute(new CommandContext("s","a","expired",61,1,true),PickupAction.Collect,"stale").Accepted&&inventory.Items.Length==0&&!pickups.Inspect("stale").Removed,"expired reservation retained");
    // Actual backpack capacity caps at six; failed seventh upgrade retains box.
    Init();for(int i=0;i<4;i++){string id="pack"+i;Check(pickups.Register(id,"backpack","semantic",new Float3()),"pack register");Action(PickupAction.Reserve,id);var rr=bridge.Execute(C(),PickupAction.Collect,id);Check(rr.Accepted==(i<3),"capacity result");Check(pickups.Inspect(id).Removed==(i<3),"pack removal result");}Check(inventory.Capacity==6,"capacity six");
    // Unsupported supplies are explicit failures with their real source box retained.
    Check(pickups.Register("supply","rifle","semantic",new Float3()),"supply register");Action(PickupAction.Reserve,"supply");Check(!bridge.Execute(C(),PickupAction.Collect,"supply").Accepted&&!pickups.Inspect("supply").Removed,"missing reserve API fails closed");
        // Gain publication pressure must reject before spending, then recover after drain.
    Init();Collect("pressure","banana");Prepare("pressure","banana");Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,1,0,false).Accepted,"pressure loss");
    for(int i=0;i<1022;i++){var r=health.SetConnected(C(),"a",health.Read("a").Revision,true);if(!r.Accepted)throw new Exception("pressure setup");}
    Check(health.PendingTransitionCount==1024,"bounded gain queue threshold");Check(!bridge.Execute(C(),PickupAction.Consume,"pressure").Accepted&&inventory.Items.Length==1&&health.Read("a").Health==99,"pressure retains all");health.DrainTransitions();Action(PickupAction.Consume,"pressure");Check(health.Read("a").Health==100&&inventory.Items.Length==0,"pressure recovery");
    // Prepared health cannot survive an intervening damage revision; abort keeps item.
    Init();Collect("abort","banana");state=health.Read("a");Check(inventory.PrepareRemoval(C(),"abort",out removal),"abort removal prepared");Check(health.PrepareGain(C(),"stalegain","a",state.Revision,state.LifeGeneration,state.ConnectionGeneration,10,0,out gain),"stale gain prepare");Check(health.ApplyLoss(C(),"a",state.Revision,1,0,false).Accepted,"intervening damage");Check(!gain.Validate(),"stale revision rejects");gain.Abort();removal.Abort();Check(inventory.Items.Length==1&&health.Read("a").Health==99,"failed final validation retains item");
    Console.WriteLine("PASS "+checks+" consumable checks");}
    static void Main(){new Tests().Run();}
}



