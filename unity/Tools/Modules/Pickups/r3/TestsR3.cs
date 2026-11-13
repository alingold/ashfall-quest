using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.Pickups;

internal sealed class Host : ISelectedPickupTransactions, IAtomicPickupEffects, IPickupActors, IPickupTransactions, IInteractionQuery, IAuthorityGate
{
    public readonly Dictionary<string,ActorFrame> Actors=new Dictionary<string,ActorFrame>();
    public readonly Dictionary<string,string> Carried=new Dictionary<string,string>();
    public readonly Dictionary<string,int> Capacity=new Dictionary<string,int>();
    public readonly Dictionary<string,string> Semantic=new Dictionary<string,string>();
    public string[] Slot; public string Held; public string SelectedOverride; public bool OverrideSelection, StaleSelection; public AtomicOutcome Outcome=AtomicOutcome.Applied; public bool ExplicitEffects; public int Aborts; public StoreReceipt ForcedStore=null;
    public bool Wall=false, HeadWall=false, FailCommit, FailDrop, WrongOwner=false, RejectAuthority=false;
    public int Commits, Supplies, ShieldCalls;
    public Float3 DropPosition=new Float3(0,0,1);
    public ActorFrame Add(string id)
    {
        ActorFrame f=new ActorFrame {Allowed=true,HandEligible=true,TransferSafe=true,OtherHandEligible=true,OtherGrip=true,
            Forward=new Float3(0,0,1),Head=new Float3(0,.2f,0),Hand=new Float3(),Anchor=new Float3(0,.2f,.1f),
            OtherLocal=new Float3(0,.1f,0),HeadLocalAnchor=new Float3(0,0,.1f),Health=10,Shield=10,Stamina=0,MaxStamina=6,ItemUpDot=0};
        Actors.Add(id,f);Capacity[id]=3;return f;
    }
    public bool TryRead(string id,out ActorFrame f) { return Actors.TryGetValue(id,out f); }
    public RuleResult Validate(CommandContext c) { return new RuleResult(!RejectAuthority,"host-gate"); }
    public bool HasLineOfSight(string actor,string owner,Float3 from,Float3 to,float range)
    {
        double dx=to.X-from.X,dy=to.Y-from.Y,dz=to.Z-from.Z;
        if(Math.Sqrt(dx*dx+dy*dy+dz*dz)>range+.00001)return false;
        // A real wall plane between ray endpoints. A child collider resolves through
        // the semantic registry; it cannot be relabeled by the requesting client.
        if(Wall&&from.Z<.5&&to.Z>=.5)return false;
        if(HeadWall&&from.Y>.1f&&to.Y<.1f)return false;
        if(owner==actor)return true;
        string actual;return !WrongOwner&&Semantic.TryGetValue("fixture-child",out actual)&&actual==owner;
    }
    private sealed class Stage : IStagedStore {
        private readonly Host host;private readonly Transfer transfer;private bool done;
        public Stage(Host h,Transfer t) { host=h;transfer=t;Receipt=h.ForcedStore??new StoreReceipt(t,new EvictedInstance[0],h.Slot==null?null:new StackSelection(h.Held,h.OverrideSelection?h.SelectedOverride:(Array.IndexOf(h.Slot,h.Held)>=0?h.Held:null),h.Slot)); }
        public StoreReceipt Receipt { get; private set; }
        public bool TryCommit() { if(done)return false;done=true;return !host.StaleSelection&&host.TryCommit(transfer); }
        public void Abort() { done=true; }
    }
    public bool TryStageStore(Transfer t,string held,out IStagedStore stage) { Held=held; if(Slot==null) { Slot=new[]{t.ItemId};stage=new Stage(this,t);Slot=null;return true; } stage=new Stage(this,t);return true; }
    public bool TryStageStore(Transfer t,out IStagedStore stage) { stage=new Stage(this,t);return true; }
    public bool TryCommit(Transfer t)
    {
        if(FailCommit)return false;
        string key=t.ActorId+":"+t.ItemId;
        if(t.Operation==TransferKind.Store) {
            if(Carried.ContainsKey(key))return false;
            int count=0;foreach(string k in Carried.Keys)if(k.StartsWith(t.ActorId+":"))count++;
            if(count>=Capacity[t.ActorId])return false;Carried.Add(key,t.Kind);
        } else if(t.Operation==TransferKind.UpgradeBackpack) {
            if(Capacity[t.ActorId]>=6)return false;Capacity[t.ActorId]++;
        } else if(t.Operation==TransferKind.Supply)Supplies+=t.Amount;
        else {
            if(!Carried.Remove(key))return false;
            if(t.Operation==TransferKind.Consume) {
                ActorFrame f=Actors[t.ActorId];f.Health=Math.Min(100,f.Health+t.Health);f.Shield=Math.Min(100,f.Shield+t.Shield);
                if(t.RefillStamina)f.Stamina=f.MaxStamina;
            }
        }
        Commits++;return true;
    }
    private sealed class Consumption : IStagedConsume {
        private readonly Host host; private readonly Transfer request; private bool done;
        public ConsumeReceipt Receipt { get; private set; }
        public Consumption(Host h,Transfer t,string held) {
            host=h;request=t;string[] members=h.Slot??new string[]{t.ItemId};string next=null;
            foreach(string id in members)if(id!=t.ItemId) { next=id;break; }
            Receipt=new ConsumeReceipt(t,new StackSelection(h.StaleSelection?"stale":held,h.OverrideSelection?h.SelectedOverride:next,members));
        }
        public AtomicOutcome TryCommit() { if(done||host.StaleSelection)return AtomicOutcome.Failed;done=true;
            if(host.Outcome!=AtomicOutcome.Applied)return host.Outcome;
            return host.TryCommit(request)?AtomicOutcome.Applied:AtomicOutcome.Failed; }
        public void Abort() { done=true;host.Aborts++; }
    }
    public bool TryStageConsume(Transfer t,string held,out IStagedConsume stage) { stage=new Consumption(this,t,held);return true; }
    public AtomicOutcome CommitEffect(Transfer t) {
        ExplicitEffects=true;if(Outcome!=AtomicOutcome.Applied)return Outcome;
        return TryCommit(t)?AtomicOutcome.Applied:AtomicOutcome.Failed;
    }
    public bool TryPlaceDrop(string actor,string item,out Float3 p) { p=DropPosition;return !FailDrop; }
    public ShieldProcessing TryAddShield(string actor,float amount) { if(FailCommit)return ShieldProcessing.Failed;ShieldCalls++;if(Actors[actor].Shield>=100)return ShieldProcessing.CappedNoOp;Actors[actor].Shield=Math.Min(100,Actors[actor].Shield+amount);return ShieldProcessing.Applied; }
}
internal static class TestsR3 {
    static Host h; static PickupRules r; static int seq, assertions, cases; static long tick;
    static void Check(bool ok,string why) { assertions++;if(!ok)throw new Exception(why); }
    static void Setup() { h=new Host();h.Add("a");h.Add("b");h.Semantic["fixture-child"]="fixture";r=new PickupRules("s",2,100,h,h,h,h,4000,4,128);tick=0; }
    static bool Do(PickupAction a,string id) { return r.Execute(new CommandContext("s","a","r3-"+(++seq),tick,2,true),a,id).Accepted; }
    static void Ok(PickupAction a,string id) { Check(Do(a,id),a+" "+id); }
    static void Collect(string id,string kind) { Check(r.Register(id,kind,"fixture",new Float3(0,0,1)),"register");Ok(PickupAction.Reserve,id);Ok(PickupAction.Collect,id); }
    static void Open(string id) { Ok(PickupAction.Equip,id);Ok(PickupAction.Open,id); }
    static void Step(long dt) { tick+=dt;r.Advance(tick); }
    static void Test(string name,Action body) { body();cases++;Console.WriteLine("PASS "+name); }
    public static int Main() { try {
        Test("held soda stack collection retains exact current member and resets opening",delegate {
            Setup();Collect("a1","soda");Open("a1");h.Held="a1";h.Slot=new[]{"a1","a2"};
            Collect("a2","soda");Check(r.HeldInstance("a")=="a1"&&r.Phase("a")==Preparation.Open,"redraw current");
            Check(!Do(PickupAction.Consume,"a1"),"must reopen");Check(r.Inspect("a2").Owner=="a","exact incoming owned");
        });
        Test("shaker collection resets accumulated desktop preparation",delegate {
            Setup();h.Actors["a"].Desktop=true;Collect("q1","shaker");Ok(PickupAction.Equip,"q1");
            for(int i=0;i<3;i++){Ok(PickupAction.DesktopRelease,"q1");Ok(PickupAction.DesktopPress,"q1");}Step(231);
            Check(r.Phase("a")==Preparation.Consume,"prepared before");h.Held="q1";h.Slot=new[]{"q2","q1"};Collect("q2","shaker");
            Check(r.HeldInstance("a")=="q1"&&r.Phase("a")==Preparation.Shake,"exact redraw");Step(1000);
            Check(r.Phase("a")==Preparation.Shake&&!Do(PickupAction.Consume,"q1"),"pressed and time reset");
        });
        Test("consume either pair order auto equips first survivor",delegate {
            foreach(bool reverse in new[]{false,true}) { Setup();Collect("s1","soda");Collect("s2","soda");Open("s2");
                h.Slot=reverse?new[]{"s2","s1"}:new[]{"s1","s2"};Ok(PickupAction.Consume,"s2");
                Check(r.HeldInstance("a")=="s1"&&r.Phase("a")==Preparation.Open,"survivor");
                Check(r.Inspect("s2").Removed&&r.Inspect("s1").Owner=="a"&&r.PendingSoda("a")==50,"once exact"); }
        });
        Test("other slot not chosen and no survivor stows",delegate {
            Setup();Collect("s1","soda");Collect("s2","soda");Open("s1");h.Slot=new[]{"s1"};Ok(PickupAction.Consume,"s1");
            Check(r.HeldInstance("a")==null&&r.Inspect("s2").Owner=="a","other slot remains");
        });
        Test("failed, capped and stale consume preserve prepared instance and queue",delegate {
            foreach(AtomicOutcome outcome in new[]{AtomicOutcome.Failed,AtomicOutcome.CappedNoOp}) { Setup();Collect("s1","soda");Collect("s2","soda");Open("s1");h.Slot=new[]{"s1","s2"};h.Outcome=outcome;
                Check(!Do(PickupAction.Consume,"s1"),"outcome rejected");Check(r.HeldInstance("a")=="s1"&&r.Phase("a")==Preparation.Consume&&!r.Inspect("s1").Removed&&r.PendingSoda("a")==0,"no partial state");
                h.Outcome=AtomicOutcome.Applied;h.StaleSelection=true;Check(!Do(PickupAction.Consume,"s1"),"stale selection");h.StaleSelection=false;Ok(PickupAction.Consume,"s1");Check(h.Aborts==3,"release all stages"); }
        });
        Test("malformed membership, wrong owner and duplicate member fail before commit",delegate {
            foreach(string[] bad in new[]{new[]{"s1","s1"},new[]{"s1","missing"},new[]{"s1","b1"},new[]{"s2"},new string[0]}) {
                Setup();Collect("s1","soda");Collect("s2","soda");Check(r.Register("b1","soda","fixture",new Float3(0,0,1)),"ground");Open("s1");h.Slot=bad;int commits=h.Commits;
                Check(!Do(PickupAction.Consume,"s1")&&h.Commits==commits&&!r.Inspect("s1").Removed,"reject malformed"); }
        });
        Test("selection cannot switch to incoming or another current member",delegate {
            Setup();Collect("s1","soda");Open("s1");h.Held="s1";h.Slot=new[]{"s1","s2"};h.OverrideSelection=true;h.SelectedOverride="s2";
            Check(r.Register("s2","soda","fixture",new Float3(0,0,1)),"register");Ok(PickupAction.Reserve,"s2");Check(!Do(PickupAction.Collect,"s2"),"no selected substitution");
            Check(r.Phase("a")==Preparation.Consume&&r.Inspect("s2").Owner==null,"preparation preserved");
        });
        Test("shaker survivor restarts motion and banana cannot borrow a slot",delegate {
            Setup();h.Actors["a"].Desktop=true;Collect("q1","shaker");Collect("q2","shaker");Ok(PickupAction.Equip,"q1");
            for(int i=0;i<3;i++){Ok(PickupAction.DesktopRelease,"q1");Ok(PickupAction.DesktopPress,"q1");}Step(231);
            h.Slot=new[]{"q1","q2"};Ok(PickupAction.Consume,"q1");Check(r.HeldInstance("a")=="q2"&&r.Phase("a")==Preparation.Shake,"shaker survivor reset");
            Step(1000);Check(!Do(PickupAction.Consume,"q2")&&h.Actors["a"].Shield==60,"no inherited gesture");
            Setup();Collect("b1","banana");Collect("b2","banana");Ok(PickupAction.Equip,"b1");
            for(int i=0;i<4;i++){h.Actors["a"].OtherLocal=new Float3(0,.15f,0);Ok(PickupAction.BeginPeel,"b1");h.Actors["a"].OtherLocal=new Float3();Ok(PickupAction.SamplePeel,"b1");}
            h.Slot=new[]{"b1","b2"};Check(!Do(PickupAction.Consume,"b1")&&h.Actors["a"].Health==10,"banana nonstack");
            h.Slot=new[]{"b1"};h.FailCommit=true;Check(!Do(PickupAction.Consume,"b1")&&h.Actors["a"].Health==10&&h.Carried.Count==2,"atomic banana failure");
            h.FailCommit=false;Ok(PickupAction.Consume,"b1");Check(h.Actors["a"].Health==100&&h.Actors["a"].Stamina==6&&r.HeldInstance("a")==null,"banana dispatched capped health and refill");
        });
        Test("store revision failure preserves current selection then retries",delegate {
            Setup();Collect("s1","soda");Open("s1");h.Slot=new[]{"s1","s2"};Check(r.Register("s2","soda","fixture",new Float3(0,0,1)),"register");Ok(PickupAction.Reserve,"s2");
            h.StaleSelection=true;Check(!Do(PickupAction.Collect,"s2")&&r.Phase("a")==Preparation.Consume&&r.Inspect("s2").Owner==null,"revision failure");
            h.StaleSelection=false;Ok(PickupAction.Collect,"s2");Check(r.HeldInstance("a")=="s1"&&r.Phase("a")==Preparation.Open,"retry redraw");
        });
        Test("backpack reaches six only, no consume at capacity",delegate {
            Setup();for(int i=0;i<3;i++)Collect("pack"+i,"backpack");Check(h.Capacity["a"]==6,"six");
            Check(r.Register("full","backpack","fixture",new Float3(0,0,1)),"register full");Ok(PickupAction.Reserve,"full");Check(!Do(PickupAction.Collect,"full")&&!r.Inspect("full").Removed,"retain full backpack");Check(h.ExplicitEffects,"explicit outcome dispatch");
        });
        Test("all supplies custom, default and zero quantities",delegate {
            foreach(string kind in new[]{"pistol","smg","rifle","shells","sniper","builds"}) { Setup();Collect("default",kind);Check(h.Supplies==PickupRules.SupplyAmount(kind),"default amount");
                Check(r.Register("custom",kind,"fixture",new Float3(0,0,1),3),"custom");Ok(PickupAction.Reserve,"custom");Ok(PickupAction.Collect,"custom");Check(h.Supplies==PickupRules.SupplyAmount(kind)+3,"custom amount");
                Check(r.Register("zero",kind,"fixture",new Float3(0,0,1),0),"zero source");Ok(PickupAction.Reserve,"zero");Check(!Do(PickupAction.Collect,"zero")&&!r.Inspect("zero").Removed,"zero remains"); }
        });
        Test("capped supply outcome retains box and failed drop preserves held state",delegate {
            Setup();Check(r.Register("box","rifle","fixture",new Float3(0,0,1),2),"register");Ok(PickupAction.Reserve,"box");h.Outcome=AtomicOutcome.CappedNoOp;Check(!Do(PickupAction.Collect,"box")&&!r.Inspect("box").Removed,"capped box");h.Outcome=AtomicOutcome.Applied;
            Collect("s1","soda");Open("s1");h.FailDrop=true;Check(!Do(PickupAction.Drop,"s1")&&r.Phase("a")==Preparation.Consume&&r.HeldInstance("a")=="s1","failed placement");
        });
        Test("soda cap drains full duration and genuine failure banks no elapsed time",delegate {
            Setup();Collect("s1","soda");Open("s1");Ok(PickupAction.Consume,"s1");h.Actors["a"].Shield=99;Step(100);Check(h.Actors["a"].Shield==100&&r.PendingSoda("a")==45,"partial capped gain");Step(900);Check(r.PendingSoda("a")==0,"expires capped");h.Actors["a"].Shield=0;Step(100);Check(h.Actors["a"].Shield==0,"no later gain");
            Collect("s2","soda");Open("s2");Ok(PickupAction.Consume,"s2");h.FailCommit=true;Step(1000);Check(r.PendingSoda("a")==50,"failed retains");h.FailCommit=false;Step(1);Check(Math.Abs(h.Actors["a"].Shield-.05f)<.0001,"no banked time");r.Advance(tick);Check(Math.Abs(h.Actors["a"].Shield-.05f)<.0001,"repeat idempotent");
        });
        Console.WriteLine(cases+" scenarios / "+assertions+" assertions");return 0;
    } catch(Exception e) {Console.WriteLine(e);return 1;} }
}

