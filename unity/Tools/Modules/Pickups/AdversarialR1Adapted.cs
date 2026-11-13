using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.Pickups;

sealed class ReviewHost : IPickupActors, IPickupTransactions, IInteractionQuery, IAuthorityGate
{
    public readonly Dictionary<string,ActorFrame> Frames=new Dictionary<string,ActorFrame>();
    public readonly Dictionary<string,string> Inventory=new Dictionary<string,string>();
    public PickupRules Rules;
    public bool Fail, BlockHead, BlockTarget, RejectGate, Reenter, Replace, RejectCappedShield;
    public string NestedReason, Evicted;
    public int Commits;
    public ReviewHost() { Frames["a"]=Frame();Frames["b"]=Frame(); }
    static ActorFrame Frame() { return new ActorFrame { Allowed=true,HandEligible=true,TransferSafe=true,OtherHandEligible=true,OtherGrip=true,
        Head=new Float3(0,.2f,0),Forward=new Float3(0,0,1),OtherLocal=new Float3(0,.15f,0),Anchor=new Float3(0,.2f,.1f),
        HeadLocalAnchor=new Float3(0,0,.1f),Health=1,Shield=10,Stamina=0,MaxStamina=6 }; }
    public bool TryRead(string actor,out ActorFrame f) { return Frames.TryGetValue(actor,out f); }
    public RuleResult Validate(CommandContext c) { return new RuleResult(!RejectGate,"gate"); }
    public bool HasLineOfSight(string actor,string semantic,Float3 from,Float3 to,float range) {
        return semantic==actor ? !BlockHead : semantic=="trusted-fixture"&&!BlockTarget;
    }
    public bool TryPlaceDrop(string actor,string item,out Float3 p) { p=new Float3(0,0,1);return !Fail; }
    private sealed class Stage : IStagedStore {
        private readonly ReviewHost host;private readonly Transfer transfer;private bool done;
        public Stage(ReviewHost h,Transfer t) {
            host=h;transfer=t;
            Receipt=new StoreReceipt(t,h.Replace&&h.Evicted!=null ? new [] { new EvictedInstance(h.Evicted,"soda","trusted-fixture",new Float3(0,0,1)) } : new EvictedInstance[0]);
        }
        public StoreReceipt Receipt { get; private set; }
        public bool TryCommit() { if(done)return false;done=true;return host.TryCommit(transfer); }
        public void Abort() { done=true; }
    }
    public bool TryStageStore(Transfer t,out IStagedStore stage) { stage=new Stage(this,t);return true; }
    public bool TryCommit(Transfer t) {
        if(Reenter) NestedReason=Rules.Execute(new CommandContext("s","b","nested",0,1,true),PickupAction.Collect,t.ItemId).Reason;
        if(Fail)return false;
        if(t.Operation==TransferKind.Store) {
            // Model a successful source make_room: selected old slot is dropped atomically.
            if(Replace&&Evicted!=null)Inventory.Remove(Evicted);
            Inventory.Add(t.ItemId,t.ActorId);
        } else if(t.Operation==TransferKind.Consume||t.Operation==TransferKind.Drop) {
            string owner;if(!Inventory.TryGetValue(t.ItemId,out owner)||owner!=t.ActorId)return false;
            Inventory.Remove(t.ItemId);
            if(t.Operation==TransferKind.Consume) {
                ActorFrame f=Frames[t.ActorId];f.Health=Math.Min(100,f.Health+t.Health);f.Shield=Math.Min(100,f.Shield+t.Shield);
                if(t.RefillStamina)f.Stamina=f.MaxStamina;
            }
        }
        Commits++;return true;
    }
    public ShieldProcessing TryAddShield(string actor,float amount) {
        if(Fail)return ShieldProcessing.Failed;
        if(RejectCappedShield&&Frames[actor].Shield>=100)return ShieldProcessing.CappedNoOp;
        Frames[actor].Shield=Math.Min(100,Frames[actor].Shield+amount);return ShieldProcessing.Applied;
    }
}
static class Review
{
    static ReviewHost h;static PickupRules r;static long now;static int seq,pass,fail;
    static void Setup(int replay,int itemLimit) { h=new ReviewHost();r=new PickupRules("s",1,100,h,h,h,h,replay,4,itemLimit);h.Rules=r;now=0; }
    static void Setup() { Setup(10000,128); }
    static RuleResult Do(string actor,PickupAction op,string id) { return r.Execute(new CommandContext("s",actor,"r"+Interlocked.Increment(ref seq),now,1,true),op,id); }
    static void Assert(bool yes,string message) { if(!yes)throw new Exception(message); }
    static void Ok(PickupAction op,string id) { RuleResult v=Do("a",op,id);Assert(v.Accepted,op+": "+v.Reason); }
    static void Step(long ticks) { now+=ticks;r.Advance(now); }
    static void Spawn(string id,string kind) { Assert(r.Register(id,kind,"trusted-fixture",new Float3(0,0,1)),"register"); }
    static void Take(string id,string kind) { Spawn(id,kind);Ok(PickupAction.Reserve,id);Ok(PickupAction.Collect,id); }
    static void Soda() { Take("soda","soda");Ok(PickupAction.Equip,"soda");Ok(PickupAction.Open,"soda");Ok(PickupAction.Consume,"soda"); }
    static void Test(string label,Action test) { try { test();pass++;Console.WriteLine("PASS "+label); }catch(Exception e) {fail++;Console.WriteLine("FAIL "+label+": "+e.Message);} }
    public static int Main() {
        Test("lease boundary rechecks competing collector",delegate {
            Setup();Spawn("x","banana");Ok(PickupAction.Reserve,"x");Step(99);
            Assert(Do("b",PickupAction.Reserve,"x").Reason=="reserved","lease before 1s");Step(1);
            Assert(Do("b",PickupAction.Reserve,"x").Accepted,"lease after 1s");Assert(!Do("a",PickupAction.Collect,"x").Accepted,"old owner");
        });
        Test("failed store and drop preserve exact ownership",delegate {
            Setup();Spawn("x","banana");Ok(PickupAction.Reserve,"x");h.Fail=true;Assert(!Do("a",PickupAction.Collect,"x").Accepted,"store rejected");
            Assert(r.Inspect("x").Owner==null&&r.Inspect("x").ReservedBy=="a","world intact");h.Fail=false;Ok(PickupAction.Collect,"x");h.Fail=true;
            Assert(!Do("a",PickupAction.Drop,"x").Accepted&&r.Inspect("x").Owner=="a"&&h.Inventory["x"]=="a","drop intact");
        });
        Test("callback reentry rejected without double transfer",delegate {
            Setup();Spawn("x","soda");Ok(PickupAction.Reserve,"x");h.Reenter=true;Ok(PickupAction.Collect,"x");Assert(h.NestedReason=="reentry"&&h.Commits==1,"reentry");
        });
        Test("parallel collect commits once",delegate {
            Setup();Spawn("x","banana");Ok(PickupAction.Reserve,"x");int wins=0;Thread[] threads=new Thread[8];
            for(int i=0;i<threads.Length;i++)threads[i]=new Thread(delegate(){if(Do("a",PickupAction.Collect,"x").Accepted)Interlocked.Increment(ref wins);});
            foreach(Thread t in threads)t.Start();foreach(Thread t in threads)t.Join();Assert(wins==1&&h.Commits==1,"one commit");
        });
        Test("trusted semantic LOS and finite input",delegate {
            Setup();Spawn("x","soda");h.RejectGate=true;Assert(Do("a",PickupAction.Reserve,"x").Reason=="gate","host gate");h.RejectGate=false;h.BlockHead=true;Assert(!Do("a",PickupAction.Reserve,"x").Accepted,"head wall");h.BlockHead=false;h.BlockTarget=true;
            Assert(!Do("a",PickupAction.Reserve,"x").Accepted,"target wall");h.BlockTarget=false;h.Frames["a"].LocalHand=new Float3(float.NaN,0,0);
            Assert(Do("a",PickupAction.Reserve,"x").Reason=="actor-ineligible","finite");
        });
        Test("four XR peels refill health and stamina",delegate {
            Setup();Take("x","banana");Ok(PickupAction.Equip,"x");for(int i=0;i<4;i++) {h.Frames["a"].OtherLocal=new Float3(0,.15f,0);Ok(PickupAction.BeginPeel,"x");h.Frames["a"].OtherLocal=new Float3(0,0,0);Ok(PickupAction.SamplePeel,"x");}
            Ok(PickupAction.Consume,"x");Assert(h.Frames["a"].Health==100&&h.Frames["a"].Stamina==6&&r.Inspect("x").Removed,"effects");
        });
        Test("invalid peel resets and XR shaker teleport does not charge",delegate {
            Setup();Take("x","banana");Ok(PickupAction.Equip,"x");Ok(PickupAction.BeginPeel,"x");h.Frames["a"].OtherLocal=new Float3(0,-1,0);
            Assert(!Do("a",PickupAction.SamplePeel,"x").Accepted,"teleport peel");Assert(Do("a",PickupAction.SamplePeel,"x").Reason=="peel-inactive","reset");
            Take("y","shaker");Ok(PickupAction.Equip,"y");for(int i=0;i<50;i++){Step(10);h.Frames["a"].LocalHand=new Float3(i%2==0?10:0,0,0);Ok(PickupAction.SampleShake,"y");}Assert(r.Phase("a")==Preparation.Shake,"teleport shake");
        });
        Test("tracking disarms prepared item",delegate {
            Setup();Take("x","soda");Ok(PickupAction.Equip,"x");Ok(PickupAction.Open,"x");h.Frames["a"].HandEligible=false;Step(1);h.Frames["a"].HandEligible=true;
            Assert(Do("a",PickupAction.Consume,"x").Reason=="not-held","disarmed");Ok(PickupAction.Equip,"x");Assert(r.Phase("a")==Preparation.Open,"reset");
        });
        Test("XR source shaker finite sustained reversals",delegate {
            Setup();Take("x","shaker");Ok(PickupAction.Equip,"x");Ok(PickupAction.SampleShake,"x");
            for(int i=0;i<23;i++) {Step(10);h.Frames["a"].LocalHand=new Float3(i%2==0?.1f:0,0,0);Ok(PickupAction.SampleShake,"x");}
            Assert(r.Phase("a")==Preparation.Shake,"less than source charged duration");Step(10);h.Frames["a"].LocalHand=new Float3(0,0,0);Ok(PickupAction.SampleShake,"x");
            Assert(r.Phase("a")==Preparation.Consume,"24 finite samples satisfy source duration");Ok(PickupAction.Consume,"x");Assert(h.Frames["a"].Shield==60,"shield");
        });
        Test("desktop release edges and held duration",delegate {
            Setup();h.Frames["a"].Desktop=true;Take("x","shaker");Ok(PickupAction.Equip,"x");Step(1000);
            for(int i=0;i<3;i++) {Ok(PickupAction.DesktopRelease,"x");Ok(PickupAction.DesktopPress,"x");}
            Assert(r.Phase("a")==Preparation.Shake,"no idle time bank");Step(231);Assert(r.Phase("a")==Preparation.Consume,"held time");
        });
        Test("consumed tombstone cannot be reregistered or free capacity",delegate {
            Setup(100,1);Soda();Assert(r.Inspect("soda").Removed,"consumed tombstone");
            Assert(!r.Register("soda","soda","trusted-fixture",new Float3()),"same instance");Assert(!r.Register("new","soda","trusted-fixture",new Float3()),"capacity retained");
        });
        Test("soda cumulative time pause and failed effect retry",delegate {
            Setup();Soda();h.Fail=true;Step(100);Assert(r.PendingSoda("a")==50,"failed effect preserved");h.Fail=false;h.Frames["a"].Allowed=false;Step(100);Assert(r.PendingSoda("a")==50,"paused");
            h.Frames["a"].Allowed=true;Step(1000);Assert(r.PendingSoda("a")==0&&h.Frames["a"].Shield==60,"total");r.Advance(now);Assert(h.Frames["a"].Shield==60,"idempotent");
        });
        Test("replay capacity age epoch and tombstone admission",delegate {
            Setup(2,1);Spawn("x","banana");Ok(PickupAction.Reserve,"x");Ok(PickupAction.Collect,"x");Assert(Do("a",PickupAction.Drop,"x").Reason=="replay-capacity","capacity");Step(1001);
            Assert(r.Execute(new CommandContext("s","a","old",0,1,true),PickupAction.Drop,"x").Reason=="authority-context","age");Assert(r.Execute(new CommandContext("s","a","wrong",now,2,true),PickupAction.Drop,"x").Reason=="authority-context","epoch");
            Assert(!r.Register("y","soda","trusted-fixture",new Float3()),"item limit");
        });
        Test("source make_room replacement reconciles dropped instance",delegate {
            Setup();Take("old","soda");h.Replace=true;h.Evicted="old";Take("new","banana");
            Assert(!h.Inventory.ContainsKey("old"),"inventory old dropped");Console.WriteLine("OBSERVE old module owner="+r.Inspect("old").Owner+", recollect="+Do("b",PickupAction.Reserve,"old").Reason);
            Assert(r.Inspect("old").Owner==null,"source drops old item but module still owns it; no receipt/reconciliation API");
        });
        Test("soda expires at shield cap even if capped add returns false",delegate {
            Setup();Soda();h.Frames["a"].Shield=100;h.RejectCappedShield=true;Step(1000);float pending=r.PendingSoda("a");h.Frames["a"].Shield=0;Step(100);
            Console.WriteLine("OBSERVE pending after 10s capped="+pending+", shield after later damage="+h.Frames["a"].Shield);
            Assert(pending==0&&h.Frames["a"].Shield==0,"capped no-op retained expired soda");
        });
        Console.WriteLine("RESULT "+pass+" passed, "+fail+" failed");return fail==0?0:1;
    }
}
