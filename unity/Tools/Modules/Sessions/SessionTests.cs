using System;
using Ashfall.Core;
using Ashfall.Modules.Sessions;

sealed class TestPorts : IAuthorityGate, ISessionClock, IReconnectTokens, ISpawnWorld, ISessionManagement
{
    public double Time;
    public double Now { get { return Time; } }
    public bool Deny, Block;
    public int Queries, Serial;
    public SpawnSite[] Candidates = { new SpawnSite("inside",new Float3(0,0,0),-1,true,false) };
    public RuleResult Validate(CommandContext c) { return Deny ? RuleResult.Reject("transport-denied") : RuleResult.Accept(); }
    public bool CanManage(CommandContext c) { return c.ActorId=="host"; }
    public string Issue() { return "test-opaque-"+(++Serial); }
    public SpawnSite[] Sites(string map, bool warmup) { Queries++;return Candidates; }
    public bool IsSafe(string map, string actor, Float3 feet) { Queries++;return !Block && feet.Y==0; }
}
static class SessionTests
{
    static int checks, command;
    static TestPorts ports;
    static Session s;
    static void Check(bool condition,string label) { checks++; if(!condition)throw new Exception(label); }
    static CommandContext C(string actor) { return new CommandContext("test",actor,"cmd"+(++command),command,s.Epoch,true); }
    static SessionRules R(string map="arena",string mode="respawn",int teams=2,bool indoor=false,bool locked=false,bool late=true)
    { return new SessionRules(map,mode,"map-v1",teams,8,indoor,locked,late,true,5); }
    static void New(SessionRules rules=null)
    { ports=new TestPorts();s=new Session("test","content-v1",rules??R(),ports,ports,ports,ports,ports); }
    static JoinRequest Request(string token="",string content="content-v1",bool spectator=false)
    { return new JoinRequest(Protocol.Name,Protocol.Schema,content," [A]<B> ",token,spectator); }
    static string Join(string actor,string token="",bool spectator=false)
    { string issued;Check(s.Join(C(actor),Request(token,"content-v1",spectator),out issued).Accepted,"join "+actor);return issued; }
    static void Loaded(string actor) { Check(s.Loaded(C(actor),"map-v1").Accepted,"loaded "+actor); }
    static void Ready() { Check(s.WorldReady(C("host"),"map-v1").Accepted,"world ready"); }
    static void Start(params string[] actors)
    { Check(s.BeginLoading(C("host"),s.Rules,true).Accepted,"begin");foreach(string a in actors)Loaded(a);Ready();Check(s.Activate(C("host")).Accepted,"activate"); }
    static void CompatibilityAndAuthority()
    {
        New();string token;
        Check(!s.Join(C("bad"),Request("","wrong"),out token).Accepted,"content mismatch");
        Check(s.Count==0 && ports.Queries==0,"incompatible never queries world");
        Check(!s.Join(C("bad"),new JoinRequest(Protocol.Name,999,"content-v1","a","",false),out token).Accepted,"schema mismatch");
        Check(!s.Join(new CommandContext("test","bad","untrusted",1,0,false),Request(),out token).Accepted,"client authority");
        ports.Deny=true;Check(!s.Join(C("bad"),Request(),out token).Accepted,"transport gate");ports.Deny=false;
        Join("a");Check(s.Get("a").Name=="AB","source name cleaning");
        Check(!s.Spawn(C("a")).Accepted && ports.Queries==0,"no spawn before load");
        Check(!s.Loaded(C("a"),"other-map").Accepted && !s.Get("a").Loaded,"map fingerprint gate");
        Loaded("a");Check(!s.Spawn(C("a")).Accepted,"no spawn before world");Ready();
        Check(s.Spawn(C("a")).Accepted && s.Get("a").SpawnRevision==1,"warmup spawn");
        var replay=C("a");Check(s.RecordPosition(replay,new Float3(1,0,0)).Accepted,"root update");
        Check(!s.RecordPosition(replay,new Float3(99,0,0)).Accepted && s.Get("a").Position.X==1,"replay cannot mutate");
        Check(!s.BeginLoading(C("a"),R(),true).Accepted && s.Epoch==0,"non-admin load denied");
        Check(!s.WorldReady(C("a"),"map-v1").Accepted,"non-admin world denied");
        var old=C("a");Start("a");Check(!s.Spawn(old).Accepted,"previous epoch command");
        Check(!s.EnterResults(C("a")).Accepted,"non-admin results denied");
    }
    static void RespawnAndReconnect()
    {
        New();string token=Join("a");Join("b");Start("a","b");
        Check(s.Get("a").Team==0 && s.Get("b").Team==1,"least-populated teams");
        Check(s.Spawn(C("a")).Accepted,"match spawn");
        Check(s.RecordPosition(C("a"),new Float3(7,0,0)).Accepted,"replicated position");
        Check(s.Disconnect(C("a"),false).Accepted,"disconnect");
        Check(Join("new-peer",token)==token,"token identity retained");Loaded("new-peer");
        Check(s.Spawn(C("new-peer")).Accepted && s.Get("new-peer").Position.X==7,"same epoch position retained");
        Check(s.Get("new-peer").Team==0 && s.Get("new-peer").SpawnRevision==1,"same epoch team/revision");
        Check(s.Eliminate(C("new-peer")).Accepted,"eliminate");
        Check(!s.Spawn(C("new-peer")).Accepted,"delay respected");ports.Time=4.999;
        Check(!s.Spawn(C("new-peer")).Accepted,"before boundary");ports.Time=5;
        Check(s.Spawn(C("new-peer")).Accepted && s.Get("new-peer").Deaths==1 && s.Get("new-peer").SpawnRevision==2,"due respawn");
        Check(s.Disconnect(C("new-peer"),false).Accepted,"reserve prior round");
        Check(s.BeginLoading(C("host"),R("indoor","respawn",0,true),true).Accepted,"new map epoch");
        Join("third-peer",token);
        Check(!s.Get("third-peer").HasPosition && s.Get("third-peer").Deaths==0 && s.Get("third-peer").Team==-1,"cross epoch removes stale round state");
        Loaded("third-peer");Loaded("b");Ready();Check(s.Activate(C("host")).Accepted,"new round active");
        Check(s.Spawn(C("third-peer")).Accepted && s.Get("third-peer").SpawnRevision==1,"cross epoch fresh spawn");
    }
    static void ReservationsAndRevive()
    {
        New();string token=Join("a");Check(s.Disconnect(C("a"),false).Accepted,"reserve");
        Check(s.BeginLoading(C("host"),R("arena","revive",2,false,true,false),true).Accepted,"lock transition");
        ports.Time=30;string ignored;
        Check(!s.Join(C("expired"),Request(token),out ignored).Accepted,"expired token cannot bypass lock at 30 seconds");
        New();token=Join("a");Check(s.Disconnect(C("a"),true).Accepted,"kick");
        Check(s.BeginLoading(C("host"),R("arena","revive",2,false,true,false),true).Accepted,"locked");
        Check(!s.Join(C("kicked"),Request(token),out ignored).Accepted,"kick cannot bypass lock");
        New(R("arena","revive"));Join("a");Start("a");Check(s.Spawn(C("a")).Accepted,"revive deploy");
        Check(s.Eliminate(C("a")).Accepted,"revive eliminate");ports.Time=100;
        Check(!s.Spawn(C("a")).Accepted,"revive no timed respawn");
        Join("late");Loaded("late");Check(s.Get("late").WaitingRound && !s.Spawn(C("late")).Accepted,"revive late join waits");
        Start("a","late");Check(!s.Get("late").WaitingRound && s.Spawn(C("late")).Accepted,"next round releases waiting join");
        Join("watch","",true);Loaded("watch");Check(!s.Spawn(C("watch")).Accepted,"spectator never spawned");
        Check(s.EnterResults(C("host")).Accepted && !s.Spawn(C("late")).Accepted,"results spawn denied");
    }
    static void SafetyAndSelection()
    {
        New(R("archive:battlefield-paintball-gods-pitdevil","revive"));Join("a");Start("a");
        ports.Candidates=new[] {
            new SpawnSite("roof",new Float3(0,0,0),0,false,true),
            new SpawnSite("enemy",new Float3(10,0,0),1,true,false),
            new SpawnSite("bad",new Float3(float.NaN,0,0),0,true,false),
            new SpawnSite("interior",new Float3(3,0,0),0,true,false) };
        Check(s.Spawn(C("a")).Accepted && s.Get("a").Position.X==3,"Paintball interior only even without indoor flag");
        New(R("indoor","revive",2,true));Join("a");Start("a");ports.Candidates=new[]{new SpawnSite("deck",new Float3(0,0,0),0,false,true)};
        Check(!s.Spawn(C("a")).Accepted && !s.Get("a").HasPosition && s.Get("a").SpawnRevision==0,"no unchecked fallback");
        New(R("arena","revive"));Join("a");Start("a");
        ports.Candidates=new[]{new SpawnSite("floor",new Float3(2,0,0),-1,true,false),new SpawnSite("team-deck",new Float3(3,0,0),0,false,true)};
        Check(s.Spawn(C("a")).Accepted && s.Get("a").Position.X==3,"first team deployment prefers deck");
        New();Join("enemy");Join("a");Start("enemy","a");Check(s.Spawn(C("enemy")).Accepted,"enemy root");
        ports.Candidates=new[]{new SpawnSite("near",new Float3(1,0,0),-1,true,false),new SpawnSite("far",new Float3(30,0,0),-1,true,false)};
        Check(s.Spawn(C("a")).Accepted && s.Get("a").Position.X==30,"spawn maximizes nearest enemy distance");
        Check(s.Eliminate(C("a")).Accepted,"dead");ports.Time=5;ports.Block=true;
        Check(!s.Spawn(C("a")).Accepted && s.Get("a").Life==SessionLife.Eliminated && s.Get("a").SpawnRevision==1,"blocked respawn preserves state");
        ports.Block=false;Check(s.Spawn(C("a")).Accepted,"retry when clear");ports.Time=4;
        Check(!s.RecordPosition(C("a"),new Float3(9,0,0)).Accepted,"clock reversal fails closed");
    }
    public static int Main()
    {
        try { CompatibilityAndAuthority();RespawnAndReconnect();ReservationsAndRevive();SafetyAndSelection();
            Console.WriteLine("PASS Sessions: "+checks+" behavioral checks");return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }
}
