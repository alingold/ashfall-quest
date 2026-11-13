using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Modules.InventoryItems;
using Ashfall.Integration.ConstructionTransactions;
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Modules.Construction;
using Ashfall.Integration.ConstructionGeometry;
using Ashfall.Integration.ConstructionRuntime;

// Native callable via RuntimeFixtures.Run(); no engine launch or stubs required.
public static class RetainedPreparedTests
{
    sealed class R4Clock:IConstructionHostState {
        public long Now,Input=1;public long Tick {get{return Now;}}
        public bool TryRead(string actor,out HostBuilderStamp stamp){stamp=new HostBuilderStamp("session",3,actor,"authority/1",1,1,1,1,true,Input);return true;}
    }
    static readonly R4Clock Clock=new R4Clock();
    static readonly Dictionary<RuntimeHost,ConstructionLease> Leases=new Dictionary<RuntimeHost,ConstructionLease>();
    static RuleResult Place(RuntimeHost h,ConstructionCommand c){ConstructionLease l;Leases.TryGetValue(h,out l);return h.Place(c,l);}
    static BuildDecision Preview(RuntimeHost h,string actor){return h.Preview(actor,Leases[h]);}

    public sealed class Policy:IWorldApproval,IWorldBuildPolicy,IGeometryAttestation,IAuthorityGate
    {
        public bool FailReads;public bool Enabled=true;public string Hash;public SurfaceRole Role=SurfaceRole.Structure;
        public bool IsApproved(ContentIdentity id){return Enabled&&id.PayloadSha256==Hash;}
        public bool ApproveMaterial(ContentIdentity id,LoadedSurface s){return IsApproved(id)&&s.MaterialPath=="fixture-material";}
        public bool AllowsSpawn(ContentIdentity id,string mode,string team,Float3 feet){return false;}
        public bool Resolve(ContentIdentity id,string source,string owner,out string body,out SurfaceRole role){body="body/"+source;role=Role;return IsApproved(id);}
        public bool Current(ContentIdentity id){return IsApproved(id);}
        public RuleResult Validate(CommandContext c){if(FailReads)throw new Exception("external authority callback during commit");return c.ActorId=="actor"?RuleResult.Accept():RuleResult.Reject("actor");}
    }
    public sealed class Occupants:IRuntimeSnapshot
    {
        public bool Available=true;public ProtectedBox[] Players=new ProtectedBox[0];public Float3[] Objectives=new Float3[0];
        public long Generation{get{return 1;}}public bool IsCurrent(long g){return Available&&g==1;}
        public bool TrySnapshot(out ProtectedBox[] p,out Float3[] o){p=Players;o=Objectives;return Available;}
    }
    // External authorization fixture only; not a fake Combat implementation or Combat PASS.
    sealed class DamageReceipt:IConstructionDamageAuthorization
    {
        public string Piece=null;public bool Validate(CommandContext c,string instance,string piece,float amount,string capability)
        {return instance=="fixture-host"&&c.SessionId=="session"&&c.AuthorityEpoch==3&&c.IsAuthority&&c.ActorId=="actor"&&piece==Piece&&amount==200&&capability=="registered-fixture-receipt";}
    }
    static int count;static float S=Geometry.Side;static string H=new string('a',64);
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);count++;Console.WriteLine("PASS "+name);}
    public static Surface Deck(string id,float y,float x0,float x1,float z0,float z1)
    {return new Surface{id=id,semanticOwner="owner",colliderRole="structure",materialPath="fixture-material",matrix=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1},vertices=new float[]{x0,y,-z0,x0,y,-z1,x1,y,-z0,x1,y,-z1},normals=new float[]{0,1,0,0,1,0,0,1,0,0,1,0},indices=new int[]{0,2,1,2,3,1},uv=new float[0],color=new float[]{.3f,.4f,.5f,1},roughness=.6f};}
    public static AttestedWorld Binding(Policy p,Occupants o,Surface[] surfaces,bool archive=false)
    {
        var semantics=new List<SemanticRecord>();foreach(var s in surfaces)semantics.Add(new SemanticRecord{sourceId=s.id,ownerId="owner",kind="structure",evidence="synthetic controlled fixture, not content approval"});
        var d=new MapDocument{schema=1,id="fixture",version="fixture/1",sourcePath="C:/fixture.gd",sourceSha256=H,sourceRevision="fixture-r1",approval="accepted",surfaces=surfaces,spawns=new Spawn[0],pads=new Pad[0],semantics=semantics.ToArray(),semanticsVersion="world-semantics/1"};
        byte[] bytes;using(var m=new MemoryStream()){new DataContractJsonSerializer(typeof(MapDocument)).WriteObject(m,d);bytes=m.ToArray();}p.Hash=WorldLoader.Hash(bytes);
        var approved=ApprovedWorld.Load(bytes,p.Hash,p);return new AttestedWorld(approved,p,p,Layer,p,o,archive,FloorBurialPolicy.LocalAllSamples025);
    }
    static string Layer(int i){return i==8?"WorldStructure":i==9?"WorldInteraction":i==10?"PlayerActor":"WorldDecoration";}
    static BuilderState Pose(float x,float z){return new BuilderState(new Float3(x,1,z),new Float3(0,-1,0),new Float3(x,1,z),true,true);}
    static ConstructionCommand Command(int n){return new ConstructionCommand(new CommandContext("session","actor","command/"+n,n,3,true),n);}
    static bool Claim(RuntimeHost host,string actor,string hand,BuilderState state){ConstructionLease l;bool ok=host.Claim(actor,hand,state,1000,out l);if(ok)Leases[host]=l;return ok;}
    static RuntimeHost Host(Policy p,AttestedWorld w){return new RuntimeHost("session",3,"fixture-host",p,w,null,Clock);}

    sealed class Grants:IBuildGrantAuthority
    {public bool Enabled=true;public bool Validate(CommandContext c,string receipt,string kind,int amount){return Enabled&&receipt.StartsWith("trusted/",StringComparison.Ordinal);}}
    sealed class Actors:IConstructionActors
    {public BuilderState Pose;public bool TryRead(string actor,out BuilderState pose){pose=Pose;return actor=="actor";}}
    sealed class RulesWorld:IConstructionWorld
    {
        public bool Enabled=true;
        public bool HasStaticContact(Panel p,float t){return Enabled&&Math.Abs(p.Center.Y)<.01f&&p.Center.X<S;}
        public bool IsBuried(Panel p){return false;}public string Clearance(Panel p,string actor){return "";}
        public bool HasLineOfSight(string actor,Float3 a,Float3 b){return true;}
    }
    static CommandContext C(int n){return new CommandContext("session","actor","r4/"+n,0,3,true);}
    static void Equal(PlacedPanel[] a,PlacedPanel[] b,string label)
    {Check(a.Length==b.Length,label+" count");for(int i=0;i<a.Length;i++)Check(a[i].Id==b[i].Id&&a[i].Owner==b[i].Owner&&a[i].Shape.Slot==b[i].Shape.Slot&&a[i].Health==b[i].Health,label+" exact panel "+i);}
    public static int Main()
    {
        // Exact accepted service as independent algorithm oracle; candidate has no balance.
        var policy=new Policy();var actors=new Actors();var rw=new RulesWorld();
        var source=new ConstructionService("session",3,policy,actors,rw,false);
        var candidate=new CandidateService("session",3,policy,rw,false);
        for(int i=0;i<3;i++){
            actors.Pose=Pose((i+.5f)*S,S/2);var cmd=new ConstructionCommand(C(i),i);
            CandidateService.PreparedChange q;Check(candidate.PreparePlace(cmd,actors.Pose,out q).Accepted,"prepare source plan");
            Check(candidate.Snapshot().Length==i,"prepare has no panels");Check(q.Validate(),"candidate validation");
            var old=source.Place(cmd);Check(old.Accepted&&old.Cost==q.Cost,"source exact cost");q.CommitValidated();q.CommitValidated();q.Abort();Equal(source.Snapshot(),candidate.Snapshot(),"source placement");
        }
        CandidateService.PreparedChange d;string id=source.Snapshot()[0].Id;
        Check(candidate.PrepareDamage(C(4),id,200,candidate.Revision,out d).Accepted,"prepare damage");Check(candidate.Snapshot().Length==3,"damage prepare preserves");
        Check(source.Damage(new ConstructionCommand(C(4),4),id,200).Accepted,"source damage");d.CommitValidated();Equal(source.Snapshot(),candidate.Snapshot(),"source damage exact");
        foreach(float seconds in new[]{.29f,.02f,.3f}){
            CandidateService.PreparedChange q;Check(candidate.PrepareAdvance(seconds,false,out q).Accepted,"prepare collapse");source.Advance(seconds,false);q.CommitValidated();Equal(source.Snapshot(),candidate.Snapshot(),"source depth collapse");
        }
        CandidateService.PreparedChange aborted;Check(candidate.PrepareAdvance(2,false,out aborted).Accepted,"abort advance");long revision=candidate.Revision;aborted.Abort();aborted.Abort();Check(!aborted.Validate()&&candidate.Revision==revision,"abort unchanged");
        // Actual canonical per-actor Inventory ledger. Grant authority is external fixture only.
        var grants=new Grants();var accounts=new BuildChargeAccounts("session",3,policy,grants);var ledger=accounts.ForActor("actor");
        Check(Object.ReferenceEquals(ledger,accounts.ForActor("actor"))&&ledger.Balance==0,"one ledger zero constructor grant");
        BuildChargeLedger.PreparedCharge seed;
        Check(!ledger.PrepareGrant(C(10),"forged","source-initial999",999,100,out seed).Accepted,"untrusted seed rejects");
        Check(ledger.PrepareGrant(C(10),"trusted/seed","source-initial999",999,100,out seed).Accepted,"trusted explicit seed");Check(ledger.Balance==0&&seed.Validate(0),"seed prepare pure");seed.CommitValidated();seed.Abort();Check(ledger.Balance==999,"seed once");
        Check(!ledger.PrepareGrant(C(11),"trusted/seed2","source-initial999",999,100,out seed).Accepted,"no seed refill");
        BuildChargeLedger.PreparedCharge supply;
        Check(Ashfall.Modules.Pickups.PickupRules.SupplyAmount("builds")==6,"actual source supply API six");
        Check(ledger.PrepareGrant(C(12),"trusted/box","builds",6,100,out supply).Accepted&&supply.Validate(0),"source supply six");grants.Enabled=false;Check(!supply.Validate(0)&&ledger.Balance==999,"revoked source no credit");supply.Abort();grants.Enabled=true;
        Check(ledger.PrepareGrant(C(12),"trusted/box","builds",6,100,out supply).Accepted,"aborted source retry");supply.CommitValidated();supply.CommitValidated();supply.Abort();Check(ledger.Balance==1005,"source six once");
        Check(!ledger.PrepareGrant(C(13),"trusted/box","builds",6,100,out supply).Accepted,"source receipt replay");
        Check(!ledger.PrepareGrant(C(13),"trusted/wrong","builds",7,100,out supply).Accepted,"wrong source amount");
        Check(ledger.PrepareDebit(C(14),2,0,out supply).Accepted&&!supply.Validate(1),"debit expiry");supply.Abort();Check(ledger.Balance==1005,"expiry unchanged");
        // Actual prepared backend, actual World geometry, actual ledger, actual publisher.
        Clock.Now=0;var p=new Policy();var occupants=new Occupants();
        var ingress=Host(p,Binding(p,occupants,new[]{Deck("anchor",-.08f,.1f,S-.1f,.1f,S-.1f)}));
        var geometry=Binding(p,occupants,new[]{Deck("anchor",-.08f,.1f,S-.1f,.1f,S-.1f)});
        var backend=new PreparedConstruction("session",3,"candidate-host",p,ingress,geometry,Clock,accounts,false);
        var charges=new BuildChargePreparation(accounts,Clock);var pub=new PreparedPublisher(Clock,2);
        ConstructionLease lease;Check(ingress.Claim("actor","left",Pose(S/2,S/2),100,out lease),"actual ingress lease");
        Check(pub.Place(backend,charges,new ConstructionCommand(C(20),20),lease,100).Accepted,"actual prepared place debit publish");
        Check(backend.Snapshot().Length==1&&ledger.Balance==1004&&pub.Pending.Charges==1004,"actual panel charge publication atomic");
        var first=pub.Pending;var copy=first.Panels;copy[0]=null;Check(first.Panels[0]!=null,"immutable prepared publication");
        Check(!pub.Place(backend,charges,new ConstructionCommand(C(20),20),lease,100).Accepted&&ledger.Balance==1004,"actual replay no double spend");
        Check(pub.Advance(backend,0,true,100).Accepted&&backend.Revision==2,"explicit paused no-op publication");
        Check(!pub.Advance(backend,1,false,100).Accepted&&backend.Revision==2,"full publication rejects unchanged");
        Check(pub.Acknowledge(first),"ack exact publication");Check(pub.Acknowledge(pub.Pending),"ack paused publication");
        IPreparedStructure prepared;Check(backend.PrepareDamage(C(21),backend.Body(backend.Snapshot()[0].Id),200,backend.Revision,100,out prepared).Accepted,"actual typed damage preparation");
        Check(backend.Snapshot().Length==1&&prepared.Validate(),"damage final validation preserves panel");
        Clock.Input++;Check(!prepared.Validate()&&backend.Snapshot().Length==1,"damage lifecycle generation stale preserves panels");prepared.Abort();
        Check(backend.PrepareDamage(C(21),backend.Body(backend.Snapshot()[0].Id),200,backend.Revision,100,out prepared).Accepted,"damage exact abort retry");
        PlacedPanel actualBody;long actualRevision;Check(backend.TryReadBody(backend.Body(backend.Snapshot()[0].Id),out actualBody,out actualRevision)&&actualBody.Health==140&&actualRevision==backend.Revision,"actual typed body health and revision evidence");
        Check(pub.Publish(prepared,null,100).Accepted&&backend.Snapshot().Length==0&&ledger.Balance==1004,"damage publish no refund");pub.Acknowledge(pub.Pending);
        Check(!backend.PrepareDamage(C(22),"owner",200,backend.Revision,100,out prepared).Accepted,"semantic alias rejected");
        Check(ingress.Claim("actor","left",Pose(S/2,S/2),100,out lease),"fresh claim");
        Check(backend.PreparePlace(new ConstructionCommand(C(23),23),lease,100,out prepared).Accepted,"prepared queued placement");Clock.Now=101;
        Check(!prepared.Validate()&&!pub.Publish(prepared,null,100).Accepted&&backend.Snapshot().Length==0&&ledger.Balance==1004,"expired prepared unchanged");
        Clock.Now=101;
        Check(ingress.Claim("actor","left",Pose(S/2,S/2),200,out lease),"claim for charge binding");
        Check(backend.PreparePlace(new ConstructionCommand(C(24),24),lease,200,out prepared).Accepted,"binding prepared");
        Check(!pub.Publish(prepared,null,200).Accepted&&ledger.Balance==1004&&backend.Snapshot().Length==0,"missing real debit rejects");
        var otherAccounts=new BuildChargeAccounts("session",3,p,grants);var other=otherAccounts.ForActor("actor");
        Check(other.PrepareGrant(C(30),"trusted/otherseed","source-initial999",999,200,out seed).Accepted,"other domain seed");seed.CommitValidated();
        var wrong=new BuildChargePreparation(otherAccounts,Clock);
        Check(!pub.Place(backend,wrong,new ConstructionCommand(C(25),25),lease,200).Accepted&&other.Balance==999&&ledger.Balance==1004&&backend.Snapshot().Length==0,"different canonical ledger rejects before both effects");
        Check(backend.PreparePlace(new ConstructionCommand(C(26),26),lease,200,out prepared).Accepted,"generation prepared");
        Clock.Input++;Check(!prepared.Validate()&&!pub.Publish(prepared,null,200).Accepted&&backend.Snapshot().Length==0,"explicit InputGeneration stale queued state unchanged");
        // Failed prepares do not consume actual command/replay state; exact retry is valid.
        Check(ingress.Claim("actor","left",Pose(S/2,S/2),200,out lease),"fresh generation rearm");
        Check(pub.Place(backend,charges,new ConstructionCommand(C(26),26),lease,200).Accepted&&ledger.Balance==1003,"exact aborted command retry");pub.Acknowledge(pub.Pending);
        Check(ingress.Claim("actor","left",Pose(S*1.5f,S/2),200,out lease),"no callback commit claim");
        Check(backend.PreparePlace(new ConstructionCommand(C(27),27),lease,200,out prepared).Accepted,"concrete no callback prepare");
        IPreparedBuildCharge debit;Check(charges.Prepare(C(27),((IPreparedPlacement)prepared).Cost,200,out debit).Accepted&&prepared.Validate()&&debit.Validate(),"concrete validate-all");
        // If final commit reauthorizes or invokes grant providers, this deliberately throws.
        p.FailReads=true;grants.Enabled=false;prepared.CommitValidated();debit.CommitValidated();prepared.CommitValidated();debit.CommitValidated();prepared.Abort();debit.Abort();p.FailReads=false;
        Check(backend.Snapshot().Length==2&&ledger.Balance==1002,"actual commits no callbacks and idempotent");
        Console.WriteLine("RESULT "+count+" PASS; source-only actual participants; root auth/content/lane binding open");return 0;
    }
}

