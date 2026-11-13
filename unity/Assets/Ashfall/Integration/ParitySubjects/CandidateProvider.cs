using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.ParityHarness;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Construction;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Combat;
using Gun=Ashfall.Modules.WeaponHandling.WeaponHandling;
namespace Ashfall.Integration.ParitySubjects
{
    // Deterministic analytic observations, not a Unity physics implementation.
    // Only a finite slab may occlude a segment; floor contacts use panel extents.
    internal sealed class AnalyticWorld : IFlightWorld, ICapsuleRouteValidation, IInteractionQuery, IConstructionWorld
    {
        public bool Wall; public double WallMin=.05,WallMax=.55;
        public bool Ground=true;
        public int Queries;
        public bool Clear(Float3 a,Float3 b,float range)
        {
            Queries++;
            if(!a.IsFinite||!b.IsFinite)return false;
            double dx=b.X-a.X,dy=b.Y-a.Y,dz=b.Z-a.Z;
            if(dx*dx+dy*dy+dz*dz>range*range+.00001)return false;
            if(!Wall)return true;
            // Closed slab z=.05..55, x=-2..2, y=-2..3.
            double lo=0,hi=1;
            double[] p={a.X,a.Y,a.Z},d={dx,dy,dz},min={-2,-2,WallMin},max={2,3,WallMax};
            for(int n=0;n<3;n++) {
                if(Math.Abs(d[n])<1e-12) {if(p[n]<min[n]||p[n]>max[n])return true;}
                else {double x=(min[n]-p[n])/d[n],y=(max[n]-p[n])/d[n];lo=Math.Max(lo,Math.Min(x,y));hi=Math.Min(hi,Math.Max(x,y));if(lo>hi)return true;}
            }
            return false;
        }
        public bool ClearPadContact(string id,Float3 face,Float3 contact){return id=="pad"&&Clear(face,contact,20);}
        public bool ClearCapsuleExit(string actor,Float3 p){return p.IsFinite&&p.Y>=0&&Clear(new Float3(p.X,p.Y,p.Z-1),p,2);}
        public bool ValidateRoute(string actor,CapsulePlan p){return p!=null&&Clear(p.Start,p.Clear,100)&&Clear(p.Clear,p.Cruise,100)&&Clear(p.Cruise,p.End,100);}
        public bool HasLineOfSight(string actor,string owner,Float3 from,Float3 to,float range){return (owner=="fixture"||owner=="b"||owner==actor)&&Clear(from,to,range);}
        public bool HasLineOfSight(string actor,Float3 from,Float3 to){return Clear(from,to,20);}
        public bool HasStaticContact(Panel p,float tolerance){return Ground&&Math.Abs(p.Center.Y-Geometry.HalfSize(p).Y)<=tolerance;}
        public bool IsBuried(Panel p){return Ground&&p.Center.Y+Geometry.HalfSize(p).Y<0;}
        public string Clearance(Panel p,string actor){return "";}
    }
    internal sealed class Gate : IAuthorityGate
    {
        public RuleResult Validate(CommandContext c){return c.IsAuthority&&c.SessionId=="s"&&c.AuthorityEpoch>=0&&(c.ActorId=="a"||c.ActorId=="b"||c.ActorId=="host")?RuleResult.Accept():RuleResult.Reject("principal");}
    }
    internal sealed class Sequence
    {
        long n; public long Epoch=2;
        public CommandContext Next(string actor){n++;return new CommandContext("s",actor,"p"+n,n,Epoch,true);}
    }
    internal sealed class Builder : IConstructionActors
    {
        public BuilderState State;
        public bool TryRead(string id,out BuilderState state){state=State;return id=="a"&&state!=null;}
        public void Wall(int level){float s=Geometry.Side;State=new BuilderState(new Float3(s/2,s*(level+.5f),-1),new Float3(0,0,1),new Float3(s/2,s*(level+.5f),-1),true,true);}
    }
    internal sealed class PickupActor : IPickupActors
    {
        public bool TryRead(string actor,out ActorFrame f){f=new ActorFrame{Allowed=true,HandEligible=true,TransferSafe=true,Forward=new Float3(0,0,1),Head=new Float3(0,.2f,0),Health=100,Shield=100,MaxStamina=6};return actor=="a"||actor=="b";}
    }
    // Explicitly UNBOUND transaction endpoint: no pretend inventory/effect success.
    internal sealed class MissingInventory : IPickupTransactions
    {
        public int Attempts;
        public bool TryStageStore(Transfer t,out IStagedStore s){Attempts++;s=null;return false;}
        public bool TryCommit(Transfer t){Attempts++;return false;}
        public bool TryPlaceDrop(string a,string i,out Float3 p){p=new Float3();return false;}
        public ShieldProcessing TryAddShield(string a,float n){return ShieldProcessing.Failed;}
    }
    internal sealed class SessionPorts : ISessionClock,IReconnectTokens,ISessionManagement,ISpawnWorld
    {
        int token;public int Queries;
        public double Now{get{return 0;}}
        public string Issue(){return "token-"+(++token);}
        public bool CanManage(CommandContext c){return c.ActorId=="host";}
        public SpawnSite[] Sites(string map,bool warmup){Queries++;return new[]{new SpawnSite("interior",new Float3(0,0,0),-1,true,false),new SpawnSite("roof-platform",new Float3(0,8,0),-1,false,true)};}
        public bool IsSafe(string map,string actor,Float3 p){Queries++;return p.Y==0&&p.IsFinite;}
    }
    internal sealed class SpawnSink : ICommandSink
    {
        public Session Session;public int Calls;
        public RuleResult Apply(CommandContext c,string feature,byte[] payload){Calls++;return feature=="spawn"?Session.Spawn(c):RuleResult.Reject("feature");}
    }
    internal sealed class Ammo : IAmmoReserve
    {
        public int Count;public int Available{get{return Count;}}
        public int Take(int n){int k=Math.Min(n,Count);Count-=k;return k;}
        public int Refund(int n){Count+=n;return n;}
    }
    internal sealed class CombatFacts : IHealthWorld,ICombatEvidence
    {
        public double Time {get;set;}public readonly Dictionary<string,HitEvidence> Facts=new Dictionary<string,HitEvidence>();
        public double Now{get{return Time;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
        public bool TryGetPose(string a,out RescuePose p){p=new RescuePose();return false;}
        public bool TryResolve(CommandContext c,string id,out HitEvidence e){return Facts.TryGetValue(id,out e);}
    }
    public sealed class CandidateProvider : IParityExpansionProvider
    {
        public IEnumerable<string> RequiredIds{get{return new[]{"EXP-PAD-CONTACT","EXP-PAD-OCCLUSION","EXP-CAPSULE-INDOOR","EXP-CAPSULE-LIFECYCLE","EXP-AUTH-SPAWN","EXP-PICKUP-LOS-REPLAY","EXP-PICKUP-TRANSFER-BINDING","EXP-CONSTRUCTION-SUPPORT","EXP-CONSTRUCTION-OCCLUSION","EXP-PRECISION-RELOAD","EXP-COMBAT-ONCE","EXP-AUTH-IDENTITY-BINDING"};}}
        static Scenario Case(string id,string source,string expected,Action body){return new Scenario(id,source,expected,delegate(Subjects ignored){body();});}
        static void Yes(RuleResult r){Verify.That(r.Accepted,"Expected acceptance: "+r.Reason);}
        static void No(RuleResult r){Verify.That(!r.Accepted,"Expected rejection");}
        static RuleResult PickupSend(PickupRules r,Sequence q,PickupAction action){var c=q.Next("a");r.Advance(c.Tick);return r.Execute(c,action,"item");}
        static FlightMachine Flight(Gate g){return new FlightMachine("s","a",2,g,6.5f,1);}
        static CapsulePlan Plan(string id){return new CapsulePlan(id,0,0,new Float3(),new Float3(0,10,0),new Float3(10,10,0),new Float3(10,0,0),new Float3(12,.3f,0),new Float3(0,0,-1),3);}
        static CapsuleEligibility Eligible(){return new CapsuleEligibility{MapId="archive:alpha",Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,Team=0,TeamCount=2};}
        public IEnumerable<Scenario> CreateScenarios()
        {
            yield return Case("EXP-PAD-CONTACT","FlightMachine.cs:LaunchPad.Contains/PadContacts.TryLaunch; player.gd:launch_from_pad_r10","Swept sideways contact launches once, cooldown and teleport reject, later reuse retains orientation",delegate{
                var q=new Sequence();var w=new AnalyticWorld();var m=Flight(new Gate());var p=new LaunchPad("pad",new Float3(),new Float3(1,0,0),1,45);var t=new PadContacts();
                Yes(t.TryLaunch(q.Next("a"),m,p,w,new Float3(2,0,0),new Float3(-2,0,0),true,0,true,false,false,false));Verify.Near(m.Velocity.X,45,"sideways speed");
                No(t.TryLaunch(q.Next("a"),m,p,w,new Float3(),new Float3(),false,.5,true,false,false,false));
                No(t.TryLaunch(q.Next("a"),m,p,w,new Float3(100,0,0),new Float3(-100,0,0),true,2,true,false,false,false));
                Yes(t.TryLaunch(q.Next("a"),m,p,w,new Float3(),new Float3(),false,2,true,false,false,false));Verify.Near(m.Velocity.X,45,"repeat velocity");
            });
            yield return Case("EXP-PAD-OCCLUSION","FlightMachine.cs:PadContacts.TryLaunch; COLLISION-LAYERS-v1.md","Finite wall slab blocks actual contact segment without consuming cooldown; retry after clearance succeeds",delegate{
                var q=new Sequence();var w=new AnalyticWorld{Wall=true};var m=Flight(new Gate());var p=new LaunchPad("pad",new Float3(),new Float3(0,0,1),1,45);var t=new PadContacts();
                No(t.TryLaunch(q.Next("a"),m,p,w,new Float3(),new Float3(),false,0,true,false,false,false));Verify.Near(m.Velocity.Z,0,"denied velocity");Verify.That(w.Queries==1,"geometry query missing");w.Wall=false;
                Yes(t.TryLaunch(q.Next("a"),m,p,w,new Float3(),new Float3(),false,0,true,false,false,false));Verify.Near(m.Velocity.Z,45,"cleared launch");
            });
            yield return Case("EXP-CAPSULE-INDOOR","Capsules.cs:AllowsCapsules/Begin; CONTRACTS-v1.md","Both Paintball IDs and arbitrary indoor maps reject before reservation; outdoor retry succeeds",delegate{
                var f=new CapsuleFleet("s",2,new Gate());var q=new Sequence();var w=new AnalyticWorld();CapsuleUpdate u;
                foreach(string id in new[]{"archive:battlefield-paintball-gods-pitdevil","archive:skb-paintball-gods-skblong","other-indoor"}){var e=Eligible();e.MapId=id;e.Indoor=id=="other-indoor";No(f.Begin(q.Next("a"),e,Plan("pod"),0,w,out u));}
                Yes(f.Begin(q.Next("a"),Eligible(),Plan("pod"),0,w,out u));Verify.That(u.State==CapsuleState.Flying,"not flying");
            });
            yield return Case("EXP-CAPSULE-LIFECYCLE","Capsules.cs:Begin/Advance","Two riders; death releases one, other lands; duplicate terminal advance cannot move again",delegate{
                var f=new CapsuleFleet("s",2,new Gate());var q=new Sequence();var w=new AnalyticWorld();CapsuleUpdate u;
                Yes(f.Begin(q.Next("a"),Eligible(),Plan("one"),0,w,out u));Yes(f.Begin(q.Next("b"),Eligible(),Plan("two"),0,w,out u));
                Yes(f.Advance(q.Next("a"),.5,true,false,false,w,out u));Verify.That(u.State==CapsuleState.Released,"death release");
                No(f.Advance(q.Next("a"),1,true,true,true,w,out u));Yes(f.Advance(q.Next("b"),3,true,true,false,w,out u));Verify.That(u.State==CapsuleState.Landed,"other rider disturbed");Verify.Near(u.RootPosition.X,12,"safe exit");
            });
            yield return Case("EXP-AUTH-SPAWN","Authority.cs:Admit/Receive/DispatchOne; Session.cs:Join/Spawn","Compatibility checked before queued spawn; actual Session rejects unjoined spawn and selects interior after readiness",delegate{
                var g=new Gate();var p=new SessionPorts();var rules=new SessionRules("indoor","respawn","map",0,4,true,false,true,true,5);var s=new Session("s",new string('a',64),rules,g,p,p,p,p);var host=new AuthorityHost("s",new string('a',64),s.Epoch,4,8,8,256);var sink=new SpawnSink{Session=s};
                var packet=new Packet{Kind=PacketKind.Command,SessionId="s",ActorId="a",ContentHash=new string('a',64),Epoch=s.Epoch,Sequence=1,Tick=1,CommandId="spawn-before",FeatureId="spawn"};
                No(host.Receive("peer",Wire.Encode(packet),1));Verify.That(host.PendingCount==0&&p.Queries==0,"unadmitted spawn touched world");
                var hello=new Packet{Kind=PacketKind.Hello,SessionId="s",ActorId="a",ContentHash=new string('b',64),Epoch=s.Epoch};No(host.Admit("peer","a",Wire.Encode(hello)));hello.ContentHash=new string('a',64);Yes(host.Admit("peer","a",Wire.Encode(hello)));
                Yes(host.Receive("peer",Wire.Encode(packet),1));RuleResult result;Verify.That(host.DispatchOne(sink,out result),"not dispatched");No(result);Verify.That(p.Queries==0,"unjoined queried spawn");
                var q=new Sequence{Epoch=s.Epoch};string token;No(s.Join(q.Next("a"),new JoinRequest("wrong",1,new string('a',64),"a","",false),out token));Verify.That(s.Count==0,"incompatible member admitted");
                Yes(s.Join(q.Next("a"),new JoinRequest(Ashfall.Core.Protocol.Name,1,new string('a',64),"a","",false),out token));Yes(s.BeginLoading(q.Next("host"),rules,true));q.Epoch=s.Epoch;Yes(s.Loaded(q.Next("a"),"map"));Yes(s.WorldReady(q.Next("host"),"map"));Yes(s.Activate(q.Next("host")));Yes(s.Spawn(q.Next("a")));Verify.That(s.Get("a").HasPosition&&s.Get("a").Position.Y==0,"indoor spawn policy");
            });
            yield return Case("EXP-PICKUP-LOS-REPLAY","PickupRules.cs:Execute/Reserve/Collect; COLLISION-LAYERS-v1.md","Wall blocks reservation and revalidated collection; reservation replay rejected; unbound inventory cannot mutate pickup",delegate{
                var g=new Gate();var q=new Sequence();var w=new AnalyticWorld{Wall=true};var tx=new MissingInventory();var r=new PickupRules("s",2,100,g,new PickupActor(),tx,w,2000,4,128);Verify.That(r.Register("item","banana","fixture",new Float3(0,0,1)),"register");
                No(PickupSend(r,q,PickupAction.Reserve));Verify.That(tx.Attempts==0,"wall reached transaction");w.Wall=false;var c=q.Next("a");r.Advance(c.Tick);Yes(r.Execute(c,PickupAction.Reserve,"item"));No(r.Execute(c,PickupAction.Reserve,"item"));w.Wall=true;No(PickupSend(r,q,PickupAction.Collect));Verify.That(tx.Attempts==0,"collect ignored LOS");w.Wall=false;
                No(PickupSend(r,q,PickupAction.Collect));Verify.That(tx.Attempts==1&&r.Inspect("item").Owner==null&&!r.Inspect("item").Removed,"failed store changed world");
            });
            yield return Case("EXP-PICKUP-TRANSFER-BINDING","Pickups/Ports.cs:IPickupTransactions.TryStageStore","Actual InventoryTransfers composition required",delegate{throw new UnknownEvidenceException("No consumed InventoryTransfers candidate available at freeze; successful inventory publication/effects not simulated");});
            yield return Case("EXP-CONSTRUCTION-SUPPORT","ConstructionService.cs:Place/Damage/Advance; Geometry.cs:Connected","Three actual walls collapse sequentially after root loss; paused time does not collapse",delegate{
                var a=new Builder();var w=new AnalyticWorld();var g=new Gate();var q=new Sequence();var s=new ConstructionService("s",2,g,a,w,false);
                for(int i=0;i<3;i++){a.Wall(i);Verify.That(s.Place(q.Next("a")).Accepted,"wall "+i);}
                string root=null;foreach(var p in s.Snapshot())if(p.Shape.Center.Y<Geometry.Side)root=p.Id;
                Yes(s.Damage(q.Next("a"),root,180));Verify.That(s.Snapshot().Length==2,"root remains");s.Advance(10,true);Verify.That(s.Snapshot().Length==2,"pause collapse");s.Advance(.29f,false);Verify.That(s.Snapshot().Length==2,"early collapse");s.Advance(.02f,false);Verify.That(s.Snapshot().Length==1,"first cascade");s.Advance(.30f,false);Verify.That(s.Snapshot().Length==0,"cascade incomplete");
            });
            yield return Case("EXP-CONSTRUCTION-OCCLUSION","ConstructionService.cs:Preview/Place; CONTRACTS-v1.md","Real placement revalidates wall slab after preview without cost; removed obstruction permits placement",delegate{
                var a=new Builder();a.Wall(0);var w=new AnalyticWorld{WallMin=-.55,WallMax=-.05};var q=new Sequence();var s=new ConstructionService("s",2,new Gate(),a,w,false);Verify.That(s.Preview("a").Accepted,"preview");w.Wall=true;Verify.That(!s.Place(q.Next("a")).Accepted&&s.Snapshot().Length==0&&s.Charges("a")==999,"occluded placement mutated");w.Wall=false;Verify.That(s.Place(q.Next("a")).Accepted,"clear placement");
            });
            yield return Case("EXP-PRECISION-RELOAD","WeaponHandling.cs:Apply/Sample; weapon.gd:precision bolt cycle","Empty precision seats finite reserve, needs 12cm bolt; short pull cannot chamber; spent chamber requires next bolt",delegate{
                var reserve=new Ammo{Count=2};var gun=new Gun("s","a",2,WeaponMechanism.PrecisionRifle,reserve,new Gate());var q=new Sequence();long seq=0;
                var input=new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,ForegripDistance=1,DeltaSeconds=.2f,Desktop=true};
                Action<HandlingAction> send=delegate(HandlingAction action){input.Sequence=++seq;Yes(gun.Apply(q.Next("a"),action,input));};
                send(HandlingAction.ReloadStep);Verify.That(gun.Snapshot.Loaded==2&&gun.Snapshot.Seated==0&&gun.Snapshot.Phase==ReloadPhase.NeedsBolt&&!gun.Snapshot.Chambered&&reserve.Count==0,"seat conservation");
                input.Desktop=false;Action<float,bool> at=delegate(float z,bool grip){input.SupportLocal=new Float3(.072f,.045f,z);input.Grip=grip;send(HandlingAction.Sample);};at(.07f,false);at(.07f,true);at(-.03f,true);Verify.That(!gun.Snapshot.Chambered,"10cm chambered");at(-.05f,true);Verify.That(gun.Snapshot.Chambered&&gun.Snapshot.Loaded==2,"12cm did not chamber");input.Trigger=false;send(HandlingAction.Sample);input.Trigger=true;send(HandlingAction.Fire);Verify.That(gun.Snapshot.SpentChamber&&gun.Snapshot.Loaded==1&&gun.Snapshot.Phase==ReloadPhase.NeedsBolt,"precision spent state");
            });
            yield return Case("EXP-COMBAT-ONCE","CombatHealthBridge.cs:PublishHit/Apply/Acknowledge; HealthService.ApplyDamage","Sixteen retries preserve one health mutation and transition ID; host acknowledgment clears reliable outbox",delegate{
                var g=new Gate();var w=new AnalyticWorld();var facts=new CombatFacts();var h=new HealthService("s",2,2,false,g,facts,w,60,5,0);Yes(h.Register(new CommandContext("s","host","ra",1,2,true),"a",0,false));Yes(h.Register(new CommandContext("s","host","rb",1,2,true),"b",1,false));
                var bridge=new CombatHealthBridge(h,facts,facts,g,w,"s",2);var lease=bridge.Equip(new CommandContext("s","host","equip",1,2,true),"rifle-a","a","kestrel",1,1);var ev=bridge.PublishEvent(new CommandContext("s","host","event",1,2,true),1,100,DamageSource.CurrentActorIntent,DamageKind.Bullet,lease,1);var hit=bridge.PublishHit(new CommandContext("s","host","hit",1,2,true),"opaque",ev,"sub","b",0,1);
                facts.Facts.Add("opaque",new HitEvidence{Identity=hit,ActorInputReady=true,BaseDamage=25,DamageMultiplier=1,HeadRatio=1,Origin=new Float3(0,1,0),ViewOrigin=new Float3(0,1,0),Point=new Float3(0,1,1),TargetHead=new Float3(0,1.5f,1),MaximumRange=100});
                long revision=-1,eventId=-1,last=0;for(int i=0;i<16;i++){var r=bridge.Apply(new CommandContext("s","a","delivery-"+i,1,2,true),"opaque",1,true);Yes(r.Result);Verify.Near(h.Read("b").Shield,75,"duplicate damage");if(i==0)revision=h.Read("b").Revision;Verify.That(h.Read("b").Revision==revision,"retry changed revision");int damage=0;foreach(var e in r.Events){last=Math.Max(last,e.Sequence);if(e.Kind=="damage"){damage++;if(eventId<0)eventId=e.Sequence;Verify.That(e.Sequence==eventId,"event identity changed");}}Verify.That(damage==1,"damage event count");}
                No(bridge.Acknowledge(new CommandContext("s","a","bad-ack",1,2,true),last));Yes(bridge.Acknowledge(new CommandContext("s","host","ack",1,2,true),last));Verify.That(bridge.Apply(new CommandContext("s","a","retry-after-ack",1,2,true),"opaque",1,true).Events.Length==0,"ack lost");
            });
            yield return Case("EXP-AUTH-IDENTITY-BINDING","Networking/Authority.cs:Admit coordinator identity parameter","Actual authenticated identity-to-session admission adapter required",delegate{throw new UnknownEvidenceException("AuthorityHost consumes coordinator actor identity; real authentication provider and shared Sessions transport composition not bound by analytic checks");});
        }
    }
}



