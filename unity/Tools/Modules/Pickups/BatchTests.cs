using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.Pickups;

// Transactional stand-in: exercises the production receipt path, not InventoryItems.
sealed class BatchHost : IPickupActors, IPickupTransactions, IInteractionQuery, IAuthorityGate
{
    public readonly Dictionary<string,string> Inventory=new Dictionary<string,string>();
    public readonly Dictionary<string,ActorFrame> Frames=new Dictionary<string,ActorFrame>();
    public EvictedInstance[] Evictions=new EvictedInstance[0];
    public bool BlockSecond, FailCommit, RevisionConflict, FailStage, WrongRequest, Reenter;
    public int Commits, Aborts, StageCalls;
    public PickupRules Rules;
    public string ReentryReason;
    public BatchHost() {
        foreach(string actor in new [] {"a","b"})Frames[actor]=new ActorFrame {
            Allowed=true,HandEligible=true,TransferSafe=true,OtherHandEligible=true,
            Head=new Float3(0,.2f,0),Forward=new Float3(0,0,1),OtherLocal=new Float3(0,.1f,0),
            Anchor=new Float3(0,.2f,.1f),HeadLocalAnchor=new Float3(0,0,.1f),Health=1,Shield=10,MaxStamina=6 };
    }
    public RuleResult Validate(CommandContext c) { return RuleResult.Accept(); }
    public bool TryRead(string a,out ActorFrame f) { return Frames.TryGetValue(a,out f); }
    public bool HasLineOfSight(string a,string semantic,Float3 from,Float3 to,float range) { return semantic==a||semantic=="fixture"; }
    public bool TryPlaceDrop(string a,string i,out Float3 p) { p=new Float3(0,0,1);return true; }
    public ShieldProcessing TryAddShield(string a,float amount) {
        if(FailCommit)return ShieldProcessing.Failed;
        if(Frames[a].Shield>=100)return ShieldProcessing.CappedNoOp;
        Frames[a].Shield=Math.Min(100,Frames[a].Shield+amount);return ShieldProcessing.Applied;
    }
    public bool TryCommit(Transfer t) {
        if(t.Operation==TransferKind.Store)throw new Exception("Store bypassed stage");
        if(FailCommit)return false;
        return Inventory.Remove(t.ItemId);
    }
    private sealed class Stage : IStagedStore {
        readonly BatchHost host;readonly Transfer incoming;readonly EvictedInstance[] evictions;bool done;
        public StoreReceipt Receipt { get; private set; }
        public Stage(BatchHost h,Transfer t) {
            host=h;incoming=t;evictions=(EvictedInstance[])h.Evictions.Clone();
            Receipt=new StoreReceipt(h.WrongRequest?null:t,evictions);
        }
        public bool TryCommit() {
            if(done)return false;done=true;
            if(host.Reenter)host.ReentryReason=host.Rules.Execute(new CommandContext("s","b","nested",0,1,true),PickupAction.Reserve,incoming.ItemId).Reason;
            if(host.FailCommit||host.RevisionConflict)return false;
            // Validate whole transaction before any externally visible change.
            foreach(EvictedInstance e in evictions) {
                string owner;if(!host.Inventory.TryGetValue(e.ItemId,out owner)||owner!=incoming.ActorId)return false;
            }
            if(host.Inventory.ContainsKey(incoming.ItemId))return false;
            foreach(EvictedInstance e in evictions)host.Inventory.Remove(e.ItemId);
            host.Inventory.Add(incoming.ItemId,incoming.ActorId);host.Commits++;return true;
        }
        public void Abort() { done=true;host.Aborts++; }
    }
    public bool TryStageStore(Transfer t,out IStagedStore stage) {
        StageCalls++;stage=new Stage(this,t);
        // Simulates staging first position then failing second placement. Caller must
        // release the non-null partial stage even when staging returns false.
        return !FailStage&&!(BlockSecond&&Evictions.Length==2);
    }
}
static class BatchTests
{
    static BatchHost h;static PickupRules r;static int sequence,passed,assertions;static long tick;
    static void Check(bool value,string message) { assertions++;if(!value)throw new Exception(message); }
    static void Setup() { h=new BatchHost();r=new PickupRules("s",1,100,h,h,h,h,10000,4,100);h.Rules=r;tick=0; }
    static RuleResult Do(string actor,PickupAction action,string id) { return r.Execute(new CommandContext("s",actor,"b"+Interlocked.Increment(ref sequence),tick,1,true),action,id); }
    static void Ok(PickupAction a,string id) { RuleResult result=Do("a",a,id);Check(result.Accepted,a+" "+result.Reason); }
    static void Spawn(string id,string kind) { Check(r.Register(id,kind,"fixture",new Float3(0,0,1)),"register "+id); }
    static void Take(string id,string kind) { Spawn(id,kind);Ok(PickupAction.Reserve,id);Ok(PickupAction.Collect,id); }
    static EvictedInstance E(string id,string kind,float x) { return new EvictedInstance(id,kind,"fixture",new Float3(x,0,1)); }
    static void Pair() {
        Setup();Take("old1","soda");Take("old2","soda");Ok(PickupAction.Equip,"old1");Ok(PickupAction.Open,"old1");
        Spawn("incoming","banana");Ok(PickupAction.Reserve,"incoming");
        h.Evictions=new [] {E("old1","soda",.1f),E("old2","soda",-.1f)};
    }
    static void Intact() {
        Check(h.Inventory.Count==2&&h.Inventory.ContainsKey("old1")&&h.Inventory.ContainsKey("old2"),"inventory intact");
        Check(r.Inspect("old1").Owner=="a"&&r.Inspect("old2").Owner=="a","world pair intact");
        Check(r.Inspect("incoming").Owner==null&&r.Inspect("incoming").ReservedBy=="a","incoming lease intact");
        Check(r.Phase("a")==Preparation.Consume,"prepared state intact");
    }
    static void Test(string name,Action test) { test();passed++;Console.WriteLine("PASS "+name); }
    public static int Main() { try {
        Test("pair replacement publishes every exact instance and permits recollection",delegate {
            Pair();h.Reenter=true;Ok(PickupAction.Collect,"incoming");
            Check(h.Inventory.Count==1&&h.Inventory.ContainsKey("incoming"),"inventory replacement");
            Check(h.ReentryReason=="reentry"&&r.Phase("a")==Preparation.Idle,"reentry and reset");
            foreach(string id in new [] {"old1","old2"}) {
                Check(r.Inspect(id).Owner==null&&!r.Inspect(id).Removed,"exact instance on ground");
                Check(Do("b",PickupAction.Reserve,id).Accepted,"other actor reserve");
                h.Evictions=new EvictedInstance[0];Check(Do("b",PickupAction.Collect,id).Accepted,"other actor collect");
            }
            Check(r.Inspect("old1").Position.X==.1f&&r.Inspect("old2").Position.X==-.1f,"settled positions retained");
        });
        Test("blocked second placement aborts whole pair and retries",delegate {
            Pair();int before=h.Aborts;h.BlockSecond=true;Check(!Do("a",PickupAction.Collect,"incoming").Accepted,"second blocked");Intact();
            Check(h.Aborts==before+1&&h.Commits==2,"partial staging released");h.BlockSecond=false;Ok(PickupAction.Collect,"incoming");
        });
        Test("failed commit preserves pair and preparation then retry succeeds",delegate {
            Pair();h.FailCommit=true;Check(!Do("a",PickupAction.Collect,"incoming").Accepted,"commit failed");Intact();
            h.FailCommit=false;Ok(PickupAction.Collect,"incoming");Check(h.Commits==3,"one retry commit");
        });
        Test("explicit stage abort releases partial preparation and permits retry",delegate {
            Pair();h.FailStage=true;int before=h.Aborts;
            Check(!Do("a",PickupAction.Collect,"incoming").Accepted,"staging failed");Intact();
            Check(h.Aborts==before+1,"abort invoked on failed non-null stage");h.FailStage=false;Ok(PickupAction.Collect,"incoming");
        });
        Test("inventory revision conflict aborts before publication",delegate {
            Pair();h.RevisionConflict=true;Check(!Do("a",PickupAction.Collect,"incoming").Accepted,"stale inventory");Intact();
            h.RevisionConflict=false;Ok(PickupAction.Collect,"incoming");
        });
        Test("malformed receipts never commit or damage preparation",delegate {
            for(int mode=0;mode<10;mode++) {
                Pair();
                switch(mode) {
                    case 0:h.Evictions=new [] {E("old1","soda",0),E("old1","soda",0)};break;
                    case 1:h.Evictions=new [] {E("old1","banana",0)};break;
                    case 2:h.Evictions=new [] {E("missing","soda",0)};break;
                    case 3:h.Evictions=new [] {E("old1","soda",float.NaN)};break;
                    case 4:h.Evictions=new [] {new EvictedInstance("old1","soda","wrong",new Float3(0,0,1))};break;
                    case 5:h.Evictions=new [] {E("incoming","banana",0)};break;
                    case 6:h.WrongRequest=true;break;
                    case 7:h.Evictions=new [] {E("old1","soda",10)};break;
                    case 8:h.Evictions=new [] {E("old1","soda",0),E("old2","soda",float.NaN)};break;
                    case 9:h.Evictions=new [] {E("old1","soda",0),E("old2","soda",0),E("third","soda",0)};break;
                }
                int before=h.Aborts;Check(!Do("a",PickupAction.Collect,"incoming").Accepted,"invalid receipt "+mode);Intact();
                Check(h.Commits==2&&h.Aborts==before+1,"invalid stage aborted");
            }
        });
        Test("another actor consumable cannot be evicted",delegate {
            Pair();Check(Do("a",PickupAction.Drop,"old2").Accepted,"drop");Check(Do("b",PickupAction.Reserve,"old2").Accepted,"reserve b");
            h.Evictions=new EvictedInstance[0];Check(Do("b",PickupAction.Collect,"old2").Accepted,"collect b");
            h.Evictions=new [] {E("old2","soda",0)};Check(!Do("a",PickupAction.Collect,"incoming").Accepted,"reject wrong owner");
            Check(r.Inspect("old2").Owner=="b"&&h.Inventory["old2"]=="b","other owner intact");
        });
        Test("weapon eviction is committed by inventory without invented pickup record",delegate {
            Setup();h.Inventory["weapon-exact"]="a";h.Evictions=new [] {E("weapon-exact","rifle-weapon",0)};Take("new","banana");
            Check(!h.Inventory.ContainsKey("weapon-exact")&&h.Inventory["new"]=="a","weapon replaced");Check(r.Inspect("weapon-exact")==null,"weapon world belongs to adapter");
        });
        Test("eight concurrent replacement collectors commit exactly once",delegate {
            Pair();int wins=0;Thread[] threads=new Thread[8];
            for(int i=0;i<8;i++)threads[i]=new Thread(delegate(){if(Do("a",PickupAction.Collect,"incoming").Accepted)Interlocked.Increment(ref wins);});
            foreach(Thread t in threads)t.Start();foreach(Thread t in threads)t.Join();
            Check(wins==1&&h.Commits==3&&r.Inspect("old1").Owner==null&&r.Inspect("old2").Owner==null,"single atomic replacement");
        });
        Test("capped success expires soda while genuine failure preserves retry",delegate {
            Setup();Take("s","soda");Ok(PickupAction.Equip,"s");Ok(PickupAction.Open,"s");Ok(PickupAction.Consume,"s");
            h.FailCommit=true;tick=100;r.Advance(tick);Check(r.PendingSoda("a")==50,"failed add retains queue");
            h.FailCommit=false;h.Frames["a"].Shield=100;tick+=1000;r.Advance(tick);Check(r.PendingSoda("a")==0,"cap drains duration");
            h.Frames["a"].Shield=0;tick+=100;r.Advance(tick);Check(h.Frames["a"].Shield==0,"no expired benefit after damage");
        });
        Console.WriteLine("PASS "+passed+" batch scenarios, "+assertions+" assertions");return 0;
    } catch(Exception e) { Console.WriteLine("FAIL "+e);return 1; } }
}
