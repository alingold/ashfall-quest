using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.Pickups;

internal sealed class Host : IPickupActors, IPickupTransactions, IInteractionQuery, IAuthorityGate
{
    public readonly Dictionary<string,ActorFrame> Actors=new Dictionary<string,ActorFrame>();
    public readonly Dictionary<string,string> Carried=new Dictionary<string,string>();
    public readonly Dictionary<string,int> Capacity=new Dictionary<string,int>();
    public readonly Dictionary<string,string> Semantic=new Dictionary<string,string>();
    public bool Wall, HeadWall, FailCommit, FailDrop, WrongOwner, RejectAuthority;
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
        public Stage(Host h,Transfer t) { host=h;transfer=t;Receipt=new StoreReceipt(t,new EvictedInstance[0]); }
        public StoreReceipt Receipt { get; private set; }
        public bool TryCommit() { if(done)return false;done=true;return host.TryCommit(transfer); }
        public void Abort() { done=true; }
    }
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
    public bool TryPlaceDrop(string actor,string item,out Float3 p) { p=DropPosition;return !FailDrop; }
    public ShieldProcessing TryAddShield(string actor,float amount) { if(FailCommit)return ShieldProcessing.Failed;ShieldCalls++;if(Actors[actor].Shield>=100)return ShieldProcessing.CappedNoOp;Actors[actor].Shield=Math.Min(100,Actors[actor].Shield+amount);return ShieldProcessing.Applied; }
}
internal static class Tests
{
    private static int assertions, scenarios, sequence;
    private static long tick;
    private static Host host;
    private static PickupRules rules;
    private static void Check(bool pass,string label) { assertions++;if(!pass)throw new Exception(label); }
    private static void Setup(int replay) { host=new Host();host.Add("a");host.Add("b");host.Semantic["fixture-child"]="fixture";tick=0;rules=new PickupRules("s",2,100,host,host,host,host,replay,4,128); }
    private static void Setup() { Setup(2000); }
    private static RuleResult Do(string actor,PickupAction action,string id) { return rules.Execute(new CommandContext("s",actor,"c"+Interlocked.Increment(ref sequence),tick,2,true),action,id); }
    private static void Ok(string a,PickupAction op,string id) { RuleResult r=Do(a,op,id);Check(r.Accepted,op+" "+r.Reason); }
    private static void No(string a,PickupAction op,string id,string reason) { RuleResult r=Do(a,op,id);Check(!r.Accepted&&r.Reason==reason,op+" expected "+reason+" got "+r.Reason); }
    private static void Step(long n) { tick+=n;rules.Advance(tick); }
    private static void Spawn(string id,string kind) { Check(rules.Register(id,kind,"fixture",new Float3(0,0,1)),"register"); }
    private static void Collect(string id,string kind) { Spawn(id,kind);Ok("a",PickupAction.Reserve,id);Ok("a",PickupAction.Collect,id); }
    private static void Test(string name,Action body) { body();scenarios++;Console.WriteLine("PASS "+name); }
    public static int Main()
    {
        try {
            Test("competing reservations and concurrent collection",delegate {
                Setup();Spawn("one","banana");int winners=0;
                Thread a=new Thread(delegate() { if(Do("a",PickupAction.Reserve,"one").Accepted&&Do("a",PickupAction.Collect,"one").Accepted)Interlocked.Increment(ref winners); });
                Thread b=new Thread(delegate() { if(Do("b",PickupAction.Reserve,"one").Accepted&&Do("b",PickupAction.Collect,"one").Accepted)Interlocked.Increment(ref winners); });
                a.Start();b.Start();a.Join();b.Join();Check(winners==1&&host.Carried.Count==1&&host.Commits==1,"exactly one owner");
            });
            Test("walls, head-hand occlusion and semantic ownership",delegate {
                Setup();Spawn("x","soda");host.Wall=true;No("a",PickupAction.Reserve,"x","blocked-interaction");host.Wall=false;
                host.HeadWall=true;No("a",PickupAction.Reserve,"x","blocked-interaction");host.HeadWall=false;
                host.WrongOwner=true;No("a",PickupAction.Reserve,"x","blocked-interaction");host.WrongOwner=false;
                Ok("a",PickupAction.Reserve,"x");host.Wall=true;No("a",PickupAction.Collect,"x","blocked-interaction");Check(host.Commits==0,"revalidate before transfer");
            });
            Test("aim cone, direct reach and finite geometry",delegate {
                Setup();Spawn("x","banana");host.Actors["a"].Forward=new Float3(1,0,0);No("a",PickupAction.Reserve,"x","blocked-interaction");
                host.Actors["a"].Hand=new Float3(0,0,.8f);Ok("a",PickupAction.Reserve,"x");
                host.Actors["a"].Hand=new Float3(float.NaN,0,0);No("a",PickupAction.Collect,"x","actor-ineligible");
                Check(!rules.Register("nan","soda","fixture",new Float3(float.PositiveInfinity,0,0)),"reject infinite registration");
            });
            Test("reservation expiry and explicit release",delegate {
                Setup();Spawn("x","banana");Ok("a",PickupAction.Reserve,"x");No("b",PickupAction.Reserve,"x","reserved");Step(100);
                No("a",PickupAction.Collect,"x","reservation-required");Ok("b",PickupAction.Reserve,"x");Ok("b",PickupAction.Release,"x");Ok("a",PickupAction.Reserve,"x");Ok("a",PickupAction.Collect,"x");
            });
            Test("inventory failure preserves pickup and retries",delegate {
                Setup();Spawn("x","banana");Ok("a",PickupAction.Reserve,"x");host.FailCommit=true;No("a",PickupAction.Collect,"x","inventory-rejected");
                Check(rules.Inspect("x").Owner==null&&!rules.Inspect("x").Removed,"no transfer on failure");host.FailCommit=false;Ok("a",PickupAction.Collect,"x");
            });
            Test("drop obstruction, exact instance transfer and redraw reset",delegate {
                Setup();Collect("x","soda");Ok("a",PickupAction.Equip,"x");Ok("a",PickupAction.Open,"x");host.FailDrop=true;No("a",PickupAction.Drop,"x","drop-blocked");
                host.FailDrop=false;host.Wall=true;No("a",PickupAction.Drop,"x","drop-blocked");host.Wall=false;Ok("a",PickupAction.Drop,"x");
                Check(rules.Phase("a")==Preparation.Idle&&host.Carried.Count==0,"drop resets");Ok("b",PickupAction.Reserve,"x");Ok("b",PickupAction.Collect,"x");
                No("a",PickupAction.Consume,"x","not-owner");Ok("b",PickupAction.Equip,"x");Check(rules.Phase("b")==Preparation.Open,"preparation not transferred");
            });
            Test("banana four separate peels, immediate contact and stamina refill",delegate {
                Setup();Collect("x","banana");Ok("a",PickupAction.Equip,"x");No("a",PickupAction.Open,"x","not-prepared");
                for(int i=0;i<4;i++) { host.Actors["a"].OtherLocal=new Float3(0,.15f,0);Ok("a",PickupAction.BeginPeel,"x");host.Actors["a"].OtherLocal=new Float3(0,0,0);Ok("a",PickupAction.SamplePeel,"x"); }
                Check(rules.Phase("a")==Preparation.Consume,"four peels");Ok("a",PickupAction.Consume,"x");Check(host.Actors["a"].Health==100&&host.Actors["a"].Stamina==6&&host.Carried.Count==0,"banana effects");
                No("a",PickupAction.Consume,"x","item-missing");
            });
            Test("soda delayed shield, pause safety, idempotent clock",delegate {
                Setup();Collect("x","soda");Ok("a",PickupAction.Equip,"x");Ok("a",PickupAction.Open,"x");Ok("a",PickupAction.Consume,"x");
                Check(host.Actors["a"].Shield==10&&rules.PendingSoda("a")==50,"no instant soda");Step(200);Check(host.Actors["a"].Shield==20,"5 per second");
                rules.Advance(tick);Check(host.Actors["a"].Shield==20,"no repeated tick benefit");host.Actors["a"].Allowed=false;Step(100);Check(rules.PendingSoda("a")==40,"paused");
                host.Actors["a"].Allowed=true;Step(800);Check(host.Actors["a"].Shield==60&&rules.PendingSoda("a")==0,"50 total over ten active seconds");
            });
            Test("mouth volume reflected Z, tilt and full-resource no waste",delegate {
                Setup();Collect("x","soda");Ok("a",PickupAction.Equip,"x");Ok("a",PickupAction.Open,"x");ActorFrame f=host.Actors["a"];
                f.Anchor=new Float3(0,0,3);f.HeadLocalAnchor=new Float3(0,0,-.5f);No("a",PickupAction.Consume,"x","mouth-contact");
                f.HeadLocalAnchor=new Float3(0,-.3f,.49f);f.ItemUpDot=1;No("a",PickupAction.Consume,"x","mouth-contact");f.ItemUpDot=0;f.Shield=100;No("a",PickupAction.Consume,"x","full");
                f.Shield=99;Ok("a",PickupAction.Consume,"x");Step(1000);Check(f.Shield==100,"shield cap");
            });
            Test("XR shaker requires motion/time, no teleport charging",delegate {
                Setup();Collect("x","shaker");Ok("a",PickupAction.Equip,"x");Ok("a",PickupAction.SampleShake,"x");
                Step(10);host.Actors["a"].LocalHand=new Float3(10,0,0);Ok("a",PickupAction.SampleShake,"x");Check(rules.Phase("a")==Preparation.Shake,"teleport not charge");
                host.Actors["a"].LocalHand=new Float3();Step(10);Ok("a",PickupAction.SampleShake,"x");
                for(int i=0;i<24;i++) { Step(10);host.Actors["a"].LocalHand=new Float3(i%2==0?.1f:0,0,0);Ok("a",PickupAction.SampleShake,"x"); }
                Check(rules.Phase("a")==Preparation.Consume,"real alternating shake ready");Ok("a",PickupAction.Consume,"x");Check(host.Actors["a"].Shield==60,"50 shield");
            });
            Test("desktop edges and held time",delegate {
                Setup();host.Actors["a"].Desktop=true;Collect("x","shaker");Ok("a",PickupAction.Equip,"x");
                Step(1000);Check(rules.Phase("a")==Preparation.Shake,"uneventful equip does not charge");
                Ok("a",PickupAction.DesktopRelease,"x");Step(1000);Ok("a",PickupAction.DesktopPress,"x");Check(rules.Phase("a")==Preparation.Shake,"released time not counted");
                Step(100);Ok("a",PickupAction.DesktopRelease,"x");Ok("a",PickupAction.DesktopPress,"x");Step(100);
                Ok("a",PickupAction.DesktopRelease,"x");Ok("a",PickupAction.DesktopPress,"x");Step(31);Check(rules.Phase("a")==Preparation.Consume,"three presses and held time");Ok("a",PickupAction.Consume,"x");
            });
            Test("tracking loss disarms and resets preparation",delegate {
                Setup();Collect("x","soda");Ok("a",PickupAction.Equip,"x");Ok("a",PickupAction.Open,"x");host.Actors["a"].HandEligible=false;Step(1);
                host.Actors["a"].HandEligible=true;No("a",PickupAction.Consume,"x","not-held");Ok("a",PickupAction.Equip,"x");Check(rules.Phase("a")==Preparation.Open,"redraw resets");
            });
            Test("idle desktop does not bank shaker time",delegate {
                Setup();host.Actors["a"].Desktop=true;Collect("x","shaker");Ok("a",PickupAction.Equip,"x");Step(1000);
                for(int i=0;i<3;i++) { Ok("a",PickupAction.DesktopRelease,"x");Ok("a",PickupAction.DesktopPress,"x"); }
                Check(rules.Phase("a")==Preparation.Shake,"three instantaneous taps after idle cannot charge");
            });
            Test("failed consumption preserves item and prepared state",delegate {
                Setup();Collect("x","soda");Ok("a",PickupAction.Equip,"x");Ok("a",PickupAction.Open,"x");host.FailCommit=true;
                No("a",PickupAction.Consume,"x","effect-rejected");Check(host.Carried.Count==1&&rules.PendingSoda("a")==0&&rules.Phase("a")==Preparation.Consume,"no partial consume");
                host.FailCommit=false;Ok("a",PickupAction.Consume,"x");
            });
            Test("bounded replay, session epoch and host authorization",delegate {
                Setup(2);Spawn("x","banana");CommandContext c=new CommandContext("s","a","same",0,2,true);
                Check(rules.Execute(c,PickupAction.Reserve,"x").Accepted,"first");Check(rules.Execute(c,PickupAction.Collect,"x").Reason=="replay","same id changed payload rejected");
                Check(!rules.Execute(new CommandContext("s","a","other",0,3,true),PickupAction.Collect,"x").Accepted,"epoch");
                Check(!rules.Execute(new CommandContext("s","a","other",0,2,false),PickupAction.Collect,"x").Accepted,"client");
                Ok("a",PickupAction.Release,"x");No("a",PickupAction.Reserve,"x","replay-capacity");Step(1001);
                Check(rules.Execute(c,PickupAction.Reserve,"x").Reason=="authority-context","expired timestamp rejected after cache eviction");Ok("a",PickupAction.Reserve,"x");
                host.RejectAuthority=true;No("a",PickupAction.Collect,"x","host-gate");
            });
            Test("source supplies and backpack cap",delegate {
                Setup();Collect("riflebox","rifle");Check(host.Supplies==32&&rules.Inspect("riflebox").Removed,"32 rifle");
                Collect("pack1","backpack");Collect("pack2","backpack");Collect("pack3","backpack");Spawn("pack4","backpack");Ok("a",PickupAction.Reserve,"pack4");No("a",PickupAction.Collect,"pack4","inventory-rejected");Check(host.Capacity["a"]==6,"six slots max");
            });
            Console.WriteLine("PASS "+scenarios+" scenarios, "+assertions+" assertions");return 0;
        } catch(Exception e) { Console.Error.WriteLine("FAIL "+e);return 1; }
    }
}
