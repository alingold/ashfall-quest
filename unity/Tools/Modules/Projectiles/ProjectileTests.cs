using System;
using Ashfall.Core;
using Ashfall.Modules.Projectiles;
using Ashfall.Modules.HealthStates;
class World : IProjectileWorld,IHealthWorld,IAuthorityGate,IInteractionQuery
{
    public string MapRevision {get;set;} public double Now {get;set;}
    public bool Match=true;public bool InMatch {get{return Match;}}public bool TeamChangesAllowed{get{return true;}}
    public bool Contact,Block,QueryFail;public string Excluded;public Target[] Targets=new Target[0];public float LastRadius;
    public World(){MapRevision="fixture-map:sha256:sealed";}
    public bool Sweep(Float3 a,Float3 b,float r,string owner,out Contact hit){Excluded=owner;hit=Contact?new Contact(0,new Float3(0,1,0),"floor-body"):null;return !QueryFail;}
    public bool Overlap(Float3 center,float radius,out Target[] targets){LastRadius=radius;targets=Targets;return !QueryFail;}
    public bool TryGetPose(string id,out RescuePose pose){pose=new RescuePose();return false;}
    public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("gate");}
    public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float r){return !Block;}
}
class Fixture
{
    public World W=new World();public HealthService H;public CombatProjectileAdapter A;public ProjectileSimulation S;public long Tick;
    public CommandContext C(string id="step"){return new CommandContext("s","host",id,Tick,3,true);}
    public Fixture(bool ff=false,RadialPolicy policy=RadialPolicy.StructuralLineOfSightStaged)
    {
        H=new HealthService("s",3,2,false,W,W,W);H.Register(C("reg-a"),"a",0,false);H.Register(C("reg-b"),"b",1,false);H.Register(C("reg-c"),"c",0,false);
        A=new CombatProjectileAdapter(H,W,W,W,"s",3,"host",W.MapRevision,ff,1);
        S=new ProjectileSimulation("s",3,"host",W.MapRevision,W,W,A,policy);
    }
    public RuleResult Launch(string kind="frag",Float3? velocity=null,int rarity=1,string id="shot")
    {A.Equip(C("equip"),"weapon-"+id,"a",kind,1);return S.Launch(C("launch"),id,id=="shot"?1:2,"weapon-"+id,"a",1,kind,rarity,new Float3(),velocity??new Float3());}
    public RuleResult Step(){Tick++;W.Now=Tick*.05;return S.Advance(C());}
    public void Steps(int n){for(int i=0;i<n;i++)Step();}
    public Target T(string id,float x=0){return new Target(id,new Float3(x,0,0),W.MapRevision);}
    public RuleResult Blast(Target[] targets,int sub=0,float power=150,RadialPolicy policy=RadialPolicy.StructuralLineOfSightStaged)
    {return A.Radial(C(),"shot",sub,new Float3(),10,power,true,targets,policy);}
}
class ProjectileTests
{
    static int passed;
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);passed++;Console.WriteLine("PASS "+name);}
    static void Main()
    {
        var f=new Fixture();f.W.Contact=true;f.W.Targets=new[]{f.T("b"),f.T("b"),f.T("c"),f.T("a")};Check(f.Launch().Accepted,"authoritative launch");f.Steps(69);Check(f.H.Read("b").Shield==100,"frag no early fuse");f.Step();Check(f.H.Read("b").Health==50&&f.H.Read("b").Shield==0,"frag actual Combat Health shield and health");Check(f.H.Read("a").Health==50,"self radial allowed");Check(f.H.Read("c").Health==100&&f.H.Read("c").Shield==100,"ally rejected without stopping other targets");Check(f.W.Excluded=="a","owner collision exclusion frozen");long rev=f.H.Read("b").Revision;f.Steps(5);Check(f.H.Read("b").Revision==rev,"finished repeated contacts no damage");
        Check(!f.S.Launch(f.C(),"shot",99,"weapon-shot","a",1,"frag",1,new Float3(),new Float3()).Accepted,"late physical launch replay cannot mint serial");
        f=new Fixture();Check(f.Launch().Accepted,"retry launch");var targets=new[]{f.T("b"),f.T("b")};Check(f.Blast(targets).Accepted,"overlap duplicates one registered hit");rev=f.H.Read("b").Revision;Check(f.Blast(targets).Accepted&&f.H.Read("b").Revision==rev,"physical retry same capability cached outcome");Check(!f.Blast(new[]{f.T("b"),f.T("c")}).Accepted&&f.H.Read("c").Shield==100,"batch retry cannot add new target");Check(!f.Blast(targets,0,160).Accepted,"retry cannot change damage");
        f=new Fixture();f.Launch();Check(f.Blast(new[]{f.T("b",5)}).Accepted&&f.H.Read("b").Shield==25,"linear radial falloff");Check(!f.Blast(new[]{f.T("c",11)},1).Accepted,"adapter rejects out of range");
        f=new Fixture();f.Launch();f.W.Block=true;Check(!f.Blast(new[]{f.T("b")}).Accepted&&f.H.Read("b").Shield==100,"actual Combat structural LOS rejects");Check(!f.Blast(new[]{f.T("b")},1,150,RadialPolicy.SourceThroughWallsUnresolved).Accepted&&f.H.Read("b").Shield==100,"unsupported source policy fail closed");f.W.Block=false;Check(!f.Blast(new[]{f.T("b")}).Accepted&&f.H.Read("b").Shield==100,"late retry cannot reopen occluded physical outcome");
        f=new Fixture(true);f.Launch();Check(f.Blast(new[]{f.T("c")}).Accepted&&f.H.Read("c").Health==50,"friendly fire enabled actual health");
        f=new Fixture();f.Launch();f.W.Match=false;Check(f.H.Spawn(f.C("spawn"),"b",f.H.Read("b").Revision,100,100).Accepted,"actual protection setup");f.W.Match=true;Check(!f.Blast(new[]{f.T("b")}).Accepted&&f.H.Read("b").Shield==100,"spawn protection enforced actual Health");f.Tick=80;f.W.Now=4;Check(!f.Blast(new[]{f.T("b")}).Accepted&&f.H.Read("b").Shield==100,"late retry cannot outlast protection");
        f=new Fixture();f.Launch();f.H.SetConnected(f.C("leave"),"a",f.H.Read("a").Revision,false);Check(f.Blast(new[]{f.T("b")}).Accepted&&f.H.Read("b").Health==50,"owner disconnect retains launched damage");
        f=new Fixture();f.Launch();f.H.ApplyLoss(f.C("kill"),"a",f.H.Read("a").Revision,100,100,false);Check(f.Blast(new[]{f.T("b")}).Accepted&&f.H.Read("b").Health==50,"owner death retains launched damage");
        f=new Fixture();f.Launch();var retired=f.A.Equip(f.C("equip-new"),"new-weapon","a","frag",1);f.A.Retire(f.C("retire"),retired);Check(f.Blast(new[]{f.T("b")}).Accepted,"unrelated retirement preserves launched attribution");
        f=new Fixture();f.Launch();Check(!f.A.Register(f.C(),"new-shot",1,600,"weapon-shot","a","frag",1).Accepted,"Combat event serial high watermark rejects duplicate");
        f=new Fixture();f.Launch();Check(!f.Blast(new[]{f.T("b"),f.T("b",1)}).Accepted&&f.H.Read("b").Shield==100,"conflicting overlap duplicates fail before mutation");
        f=new Fixture();f.Launch();f.Blast(new[]{f.T("b")});rev=f.H.Read("b").Revision;f.H.SetConnected(f.C("disconnect-target"),"b",rev,false);f.H.SetConnected(f.C("reconnect-target"),"b",f.H.Read("b").Revision,true);rev=f.H.Read("b").Revision;Check(f.Blast(new[]{f.T("b")}).Accepted&&f.H.Read("b").Revision==rev,"completed retry never redamages reconnected target");
        f=new Fixture();f.W.Contact=true;f.W.Targets=new[]{f.T("b")};f.Launch("smaw",new Float3(0,0,200),5);f.Step();Check(f.H.Read("b").Health==40&&f.H.Read("b").Shield==0,"SMAW rarity5 earliest sweep blast");Float3 pos;bool ended;f.S.Read("shot",out pos,out ended);Check(ended&&pos.Z==0,"structural contact prevents travel through floor");
        f=new Fixture();f.Launch("smaw",new Float3(0,0,200));f.Step();f.S.Read("shot",out pos,out ended);Check(pos.Z==10&&pos.Y==0&&!ended,"rocket SI speed zero gravity");f.Steps(159);f.S.Read("shot",out pos,out ended);Check(ended&&pos.Z==1600,"rocket eight second lifetime");
        f=new Fixture();f.Launch("zone");f.Steps(160);f.S.Read("shot",out pos,out ended);Check(ended,"unstuck zone expires eight seconds");
        f=new Fixture();f.W.Contact=true;f.W.Targets=new[]{f.T("b")};f.Launch("zone");f.Steps(64);Check(f.H.Read("b").Shield==100,"zone impact delay and first tick");f.Step();Check(f.H.Read("b").Shield==95&&Math.Abs(f.W.LastRadius-14.25)<.001,"zone tick1 shrink and shield semantics");f.Steps(190);Check(f.H.Read("b").Shield==5,"zone 19 positive radius ticks");f.S.Read("shot",out pos,out ended);Check(ended,"zone final zero radius tick ends");
        f=new Fixture();f.Launch();Check(!f.Blast(new[]{new Target("b",new Float3(),"wrong-map")}).Accepted&&f.H.Read("b").Shield==100,"content target provenance rejected");
        f=new Fixture();Check(!f.Launch("frag",new Float3(Single.NaN,0,0)).Accepted,"NaN velocity rejected");
        f=new Fixture();Check(!f.Launch("frag",new Float3(Single.MaxValue,0,0)).Accepted,"finite overflow velocity bounded");
        f=new Fixture();f.Launch();f.W.QueryFail=true;Check(!f.Step().Accepted,"query failure cancels shot");
        f=new Fixture();f.W.Contact=true;f.W.Targets=new Target[257];f.Launch("smaw");Check(!f.Step().Accepted&&f.H.Read("b").Shield==100,"overlap capacity fails before damage");
        f=new Fixture();f.Launch();f.W.MapRevision="drift";Check(!f.Step().Accepted,"map revision drift rejects step");
        f=new Fixture();Check(!f.S.Advance(new CommandContext("s","host","bad",2,3,true)).Accepted,"fixed step skip rejected");Check(!f.S.Advance(new CommandContext("s","host","bad",1,3,false)).Accepted,"non host authority rejected");
        f=new Fixture();f.Launch("frag",new Float3(0,1,-1));f.Step();f.S.Read("shot",out pos,out ended);Check(pos.Z<0&&pos.Y>0,"SI gravity launch motion");var reflected=Float3.FromGodot(1,2,-3);Check(reflected.Z==3&&reflected.X==1,"source Z reflected exactly once");
        Console.WriteLine("RESULT "+passed+" PASS 0 FAIL; engine/runtime/source-through-walls UNKNOWN");
    }
}
