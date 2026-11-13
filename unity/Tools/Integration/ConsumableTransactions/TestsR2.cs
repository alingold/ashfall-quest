using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Player;
using Ashfall.Modules.Combat;
using Ashfall.Integration;
using Ashfall.Integration.ConsumableTransactions;
sealed class TestsR2 : IAuthorityGate,IHealthWorld,IInteractionQuery,ICombatEvidence,IPickupActors,IInventoryTransactions
{
    public double Now {get;set;}public bool InMatch {get{return true;}}public bool TeamChangesAllowed {get{return false;}}
    public bool TryGetPose(string actor,out RescuePose pose){pose=new RescuePose();return false;}
    public bool TryResolve(CommandContext c,string id,out HitEvidence e){e=null;return false;}
    public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
    public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float r){return true;}
    public bool TryRead(string actor,out ActorFrame frame){var h=health.Read(actor);frame=new ActorFrame{Allowed=true,HandEligible=true,TransferSafe=true,Desktop=true,Forward=new Float3(0,0,1),Health=h.Health,Shield=h.Shield,Stamina=6,MaxStamina=6};return true;}
    bool reject;
    public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance incoming,ItemInstance[] outgoing){return reject?RuleResult.Reject("fixture-eligibility"):RuleResult.Accept();}
    sealed class Receipt:IInventoryReceipt {public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
    public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt){receipt=new Receipt();return RuleResult.Accept();}
    HealthService health;long command;static int checks;
    CommandContext C(string actor){return new CommandContext("s",actor,"c"+(++command),0,1,true);}
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    HealthService.PreparedGain Prepare(string actor,string logical,CommandContext c)
    {var s=health.Read(actor);HealthService.PreparedGain t;Check(health.PrepareGain(c,logical,actor,s.Revision,s.LifeGeneration,s.ConnectionGeneration,10,0,out t),"prepare "+logical);return t;}
    void Run()
    {
        health=new HealthService("s",1,0,false,this,this,this);Check(health.Register(C("a"),"a",-1,false).Accepted,"register a");Check(health.Register(C("b"),"b",-1,false).Accepted,"register b");
        Check(health.ApplyLoss(C("a"),"a",health.Read("a").Revision,20,0,false).Accepted,"damage a");Check(health.ApplyLoss(C("b"),"b",health.Read("b").Revision,20,0,false).Accepted,"damage b");health.DrainTransitions();
        var bridge=new CombatHealthBridge(health,this,this,this,this,"s",1);
        var a=Prepare("a","ga",C("a"));HealthService.PreparedGain other;var bstate=health.Read("b");
        Check(!health.PrepareGain(C("b"),"gb","b",bstate.Revision,bstate.LifeGeneration,bstate.ConnectionGeneration,10,0,out other)&&other==null,"CT01 second actor rejects during Prepare");
        health.Serialize(delegate{Check(a.Validate(),"first validation");a.CommitValidated();return true;});
        var b=Prepare("b","gb",C("b"));Check(b.Validate(),"second after release validates");b.CommitValidated();
        var events=bridge.Pump(C("host"));Check(events.Result.Accepted&&events.Events.Length==2,"actual CombatHealth Pump accepts both");Check(events.Events[0].Sequence==5&&events.Events[1].Sequence==6,"unique actual transition identities");
        var c=C("a");a=Prepare("a","conflict-one",c);var state=health.Read("a");
        Check(!health.PrepareGain(c,"conflict-two","a",state.Revision,state.LifeGeneration,state.ConnectionGeneration,10,0,out other),"CT02 conflicting command rejected in Prepare");Check(health.Read("a").Health==90,"prepare never heals");Check(a.Validate(),"one valid token");var receipt=a.CommitValidated();a.Abort();Check(Object.ReferenceEquals(receipt,a.CommitValidated())&&health.Read("a").Health==100,"commit and abort retain receipt");
        a=Prepare("a","expires",C("a"));Now=5;Check(!a.Validate(),"explicit gain expiry");b=Prepare("b","reclaimed",C("b"));a.Abort();Check(b.Validate(),"old abort cannot release replacement lease");b.Abort();b.Abort();Now=6;
        a=Prepare("a","advance-conflict",C("a"));HealthService.PreparedAdvance advance;
        Check(!health.PrepareAdvance(C("host"),out advance).Accepted,"gain reservation excludes actual PreparedAdvance");a.Abort();
        Check(health.PrepareAdvance(C("host"),out advance).Accepted,"advance reserves after abort");state=health.Read("a");
        Check(!health.PrepareGain(C("a"),"advance-blocks","a",state.Revision,state.LifeGeneration,state.ConnectionGeneration,1,0,out other),"advance reservation excludes gain Prepare");
        Check(advance.Validate(),"actual advance validation");advance.CommitValidated();advance.Abort();
        var inv=new Inventory(SourceCatalog.Create(),this,"s","a",1,false);var movement=new Locomotion(new PlayerTuning());var consumables=new ConsumableBridge(health);var pickups=new PickupRules("s",1,60,this,this,consumables,this,64,4,128);consumables.Bind(pickups);consumables.AddActor("a",inv,movement);
        foreach(string id in new[]{"soda","banana"}){Check(pickups.Register(id,id,"owner-"+id,new Float3()),"register pickup");Check(consumables.Execute(C("a"),PickupAction.Reserve,id).Accepted,"reserve");Check(consumables.Execute(C("a"),PickupAction.Collect,id).Accepted,"collect");Check(inv.Holstered&&pickups.HeldInstance("a")==null,"collect projects stow");}
        Check(consumables.Execute(C("a"),PickupAction.Equip,"banana").Accepted,"equip banana");Check(inv.SelectedInstanceId=="banana"&&!inv.Holstered&&pickups.HeldInstance("a")=="banana","CT03 equip projection");
        reject=true;long revision=inv.Revision;Check(!consumables.Execute(C("a"),PickupAction.Equip,"soda").Accepted&&inv.Revision==revision&&pickups.HeldInstance("a")=="banana","rejected selection preserves both");reject=false;
        Check(consumables.Execute(C("a"),PickupAction.Stow,null).Accepted&&inv.Holstered&&pickups.HeldInstance("a")==null,"explicit stow projects");
        Check(inv.Apply(C("a"),InventoryOperation.Equip,null,"soda").Accepted&&!inv.Holstered,"draw via actual inventory");
        Check(pickups.Register("soda2","soda","owner2",new Float3()),"second soda");Check(consumables.Execute(C("a"),PickupAction.Reserve,"soda2").Accepted,"reserve second");Check(consumables.Execute(C("a"),PickupAction.Collect,"soda2").Accepted&&inv.Holstered&&pickups.HeldInstance("a")==null,"CT03 collect stows drawn inventory");
        Inventory.PreparedConsumables projection;Check(inv.PrepareConsumableSelection(C("a"),"banana",out projection),"prepare projection");Check(!inv.Apply(C("a"),InventoryOperation.Holster,null,null).Accepted,"reservation excludes writer");projection.Abort();Check(inv.Holstered,"abort leaves selection");
        Check(inv.Apply(C("a"),InventoryOperation.Remove,null,"banana").Accepted,"free slot");Check(inv.Apply(C("a"),InventoryOperation.Admit,new ItemInstance("weapon","longwatch",1),null).Accepted,"fixture actual weapon membership");Check(!inv.PrepareConsumableSelection(C("a"),"soda",out projection),"missing real weapon suspension fails closed");
        Console.WriteLine("PASS "+checks+" R2 assertions; actual Health transitions and CombatHealth Pump");
    }
    static void Main(){new TestsR2().Run();}
}
