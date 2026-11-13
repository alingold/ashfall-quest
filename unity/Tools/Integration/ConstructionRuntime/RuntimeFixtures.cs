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
public static class RuntimeFixtures
{
    static readonly FixtureHostClock Clock=new FixtureHostClock("session");
    static readonly Dictionary<RuntimeHost,ConstructionLease> Leases=new Dictionary<RuntimeHost,ConstructionLease>();
    static RuleResult Place(RuntimeHost h,ConstructionCommand c){ConstructionLease l;Leases.TryGetValue(h,out l);return h.Place(c,l);}
    static BuildDecision Preview(RuntimeHost h,string actor){return h.Preview(actor,Leases[h]);}

    sealed class Policy:IWorldApproval,IWorldBuildPolicy,IGeometryAttestation,IAuthorityGate
    {
        public bool Enabled=true;public string Hash;public SurfaceRole Role=SurfaceRole.Structure;
        public bool IsApproved(ContentIdentity id){return Enabled&&id.PayloadSha256==Hash;}
        public bool ApproveMaterial(ContentIdentity id,LoadedSurface s){return IsApproved(id)&&s.MaterialPath=="fixture-material";}
        public bool AllowsSpawn(ContentIdentity id,string mode,string team,Float3 feet){return false;}
        public bool Resolve(ContentIdentity id,string source,string owner,out string body,out SurfaceRole role){body="body/"+source;role=Role;return IsApproved(id);}
        public bool Current(ContentIdentity id){return IsApproved(id);}
        public RuleResult Validate(CommandContext c){return c.ActorId=="actor"?RuleResult.Accept():RuleResult.Reject("actor");}
    }
    sealed class Occupants:IRuntimeSnapshot
    {
        public bool Available=true;public ProtectedBox[] Players=new ProtectedBox[0];public Float3[] Objectives=new Float3[0];
        public long Generation{get{return 1;}}public bool IsCurrent(long g){return Available&&g==1;}
        public bool TrySnapshot(out ProtectedBox[] p,out Float3[] o){p=Players;o=Objectives;return Available;}
    }
    // External authorization fixture only; not a fake Combat implementation or Combat PASS.
    sealed class DamageReceipt:IConstructionDamageAuthorization
    {
        public string Piece;public bool Validate(CommandContext c,string instance,string piece,float amount,string capability)
        {return instance=="fixture-host"&&c.SessionId=="session"&&c.AuthorityEpoch==3&&c.IsAuthority&&c.ActorId=="actor"&&piece==Piece&&amount==200&&capability=="registered-fixture-receipt";}
    }
    static int count;static float S=Geometry.Side;static string H=new string('a',64);
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);count++;Console.WriteLine("PASS "+name);}
    static Surface Deck(string id,float y,float x0,float x1,float z0,float z1)
    {return new Surface{id=id,semanticOwner="owner",colliderRole="structure",materialPath="fixture-material",matrix=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1},vertices=new float[]{x0,y,-z0,x0,y,-z1,x1,y,-z0,x1,y,-z1},normals=new float[]{0,1,0,0,1,0,0,1,0,0,1,0},indices=new int[]{0,2,1,2,3,1},uv=new float[0],color=new float[]{.3f,.4f,.5f,1},roughness=.6f};}
    static AttestedWorld Binding(Policy p,Occupants o,Surface[] surfaces,bool archive=false)
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
    public static int Run()
    {
        Clock.Now=0;count=0;var p=new Policy();var o=new Occupants();var w=Binding(p,o,new[]{Deck("deck",-.08f,-20,20,-20,20)});var host=Host(p,w);
        Check(Claim(host,"actor","left",Pose(S/2,S/2)),"left claim");
        Check(!Claim(host,"actor","right",Pose(S/2,S/2)),"hand arbitration");
        Check(Preview(host,"actor").Accepted,"actual service preview");
        for(int x=0;x<3;x++)for(int z=0;z<3;z++)
        {Claim(host,"actor","left",Pose((x+.5f)*S,(z+.5f)*S));int n=x*3+z;Check(Place(host,Command(n)).Accepted,"source-sized floor "+n);}
        Check(host.Snapshot().Length==9&&host.Charges("actor")==990,"real service IDs and resource mutation");
        Check(!Place(host,Command(8)).Accepted&&host.Charges("actor")==990,"retry no spend");
        long serial=0;while(host.Pending()!=null){var e=host.Pending();Check(e.Serial==++serial&&e.MapHash==p.Hash,"ordered publication "+serial);var copy=e.Panels;if(copy.Length>0)copy[0]=null;Check(e.Panels[0]!=null,"immutable publication");Check(!host.Acknowledge(e.Serial+1),"exact ack");Check(host.Acknowledge(e.Serial),"ack");}Check(serial==9,"one publication each place");
        Check(!host.Damage(Command(10),host.Snapshot()[0].Id,10,"fake").Accepted,"unbound Combat fails closed");
        Check(!host.AwardSupply(Command(11),3).Accepted&&host.Charges("actor")==990,"unbound inventory fails closed");
        host.Release("actor");Check(!Place(host,Command(12)).Accepted,"release cancels placement");
        Claim(host,"actor","left",Pose(S*4.5f,S/2));Claim(host,"actor","left",new BuilderState(new Float3(),new Float3(),new Float3(),true,false));Check(!Place(host,Command(13)).Accepted,"tracking loss releases stale pose");
        o.Available=false;Check(!host.Advance(.5f,false),"missing complete snapshot fails closed");o.Available=true;
        p.Enabled=false;Check(!Place(host,Command(14)).Accepted,"approval revocation");
        p=new Policy();o=new Occupants();w=Binding(p,o,new[]{Deck("deck",-.08f,-20,20,-20,20),Deck("ceiling",.5f,-20,20,-20,20)});host=Host(p,w);Claim(host,"actor","left",Pose(S/2,S/2));Check(!Place(host,Command(0)).Accepted&&host.Charges("actor")==999,"ceiling occludes no spend");
        p=new Policy();o=new Occupants();w=Binding(p,o,new[]{Deck("deck",-.08f,-20,20,-20,20)});host=Host(p,w);o.Players=new[]{new ProtectedBox("other",new Float3(S/2,0,S/2),new Float3(.2f,.8f,.2f))};Claim(host,"actor","left",Pose(S/2,S/2));Check(!Place(host,Command(0)).Accepted,"all actors block");
        o.Players=new ProtectedBox[0];o.Objectives=new[]{new Float3(S/2,0,S/2)};Check(!Place(host,Command(1)).Accepted,"objective blocks");
        // Frozen finite triangle geometry scenarios, same classes as runtime fallback.
        var wall=new Panel("wall",new Float3(S/2,-S/2,0),2,false,1);
        p=new Policy{Role=SurfaceRole.ConstructionTerrain};o=new Occupants();w=Binding(p,o,new[]{Deck("roof",0,-20,20,-20,20)},true);Check(!w.IsBuried(wall),"archive roof not terrain");
        p=new Policy{Role=SurfaceRole.TerrainSurface};w=Binding(p,o,new[]{Deck("terrain",0,-20,20,-20,20)},true);Check(w.IsBuried(wall),"four corners terrain wall burial");
        p=new Policy{Role=SurfaceRole.TerrainSurface};w=Binding(p,o,new[]{Deck("terrain",0,0,S/2,-20,20)},true);Check(!w.IsBuried(wall),"exposed corner prevents burial");
        p=new Policy();w=Binding(p,o,new[]{Deck("moved",-.08f,100,110,100,110)});host=Host(p,w);Claim(host,"actor","left",Pose(S/2,S/2));Check(!Place(host,Command(0)).Accepted,"moved finite geometry cannot support old location");
        var pieces=AttestedWorld.PanelTriangles(new PlacedPanel("id","owner",new Panel("floor",new Float3(S/2,0,S/2),1,false,0),140),H);
        var geo=new StructuralWorld(new CollisionAllocation(8,9,10,11),pieces,o,false,FloorBurialPolicy.LocalAllSamples025);
        Check(!geo.HasStaticContact(new Panel("floor",new Float3(S/2,0,S/2),1,false,0),.045f),"construction never permanent anchor");
        Check(geo.IsBuried(new Panel("floor",new Float3(S/2,0,S/2),1,false,0))==false,"seam samples exceed single panel footprint");
        p=new Policy();o=new Occupants();w=Binding(p,o,new[]{Deck("anchor",-.08f,.1f,S-.1f,.1f,S-.1f)});var auth=new DamageReceipt();host=new RuntimeHost("session",3,"fixture-host",p,w,auth,Clock);
        for(int i=0;i<3;i++){Claim(host,"actor","left",Pose((i+.5f)*S,S/2));Check(Place(host,Command(i)).Accepted,"connected chain actual service "+i);}
        auth.Piece=host.Snapshot()[0].Id;
        Check(!host.Damage(Command(3),auth.Piece,200,"forged").Accepted&&host.Snapshot().Length==3,"forged damage receipt preserves panels");
        Check(host.Damage(Command(3),auth.Piece,200,"registered-fixture-receipt").Accepted&&host.Snapshot().Length==2,"authorized fixture damage mutates actual service");
        Check(!host.Damage(Command(3),auth.Piece,200,"registered-fixture-receipt").Accepted,"damage retry cannot reapply");
        Check(host.Advance(.29f,false)&&host.Snapshot().Length==2,"support collapse delay");
        Check(host.Advance(.02f,false)&&host.Snapshot().Length==1,"first unsupported depth collapses");
        Check(host.Advance(.3f,true)&&host.Snapshot().Length==1,"paused collapse clock");
        Check(host.Advance(.3f,false)&&host.Snapshot().Length==0,"second unsupported depth collapses");
        int publications=0;while(host.Pending()!=null){publications++;Check(host.Acknowledge(host.Pending().Serial),"damage collapse ack");}Check(publications==6,"three places one damage two collapse publications");
        ConstructionLease expired;Check(host.Claim("actor","left",Pose(S/2,S/2),20,out expired),"bounded expiry claim");Clock.Now=21;Check(!host.Place(Command(21),expired).Accepted&&host.Preview("actor",expired)==null,"expired host pose lease fails closed");
        bool reused=false;try{Host(p,w);}catch(ArgumentException){reused=true;}Check(reused,"world binding exclusive host instance");
        host.Close();Check(!Claim(host,"actor","left",Pose(S/2,S/2)),"closed lifetime");
        Console.WriteLine("PASS "+count+" checks; native rendering/physics not executed");return count;
    }
    public static void Main(){Run();}
}
