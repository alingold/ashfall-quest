using System;
using Ashfall.Core;
using Ashfall.Modules.Sessions;

sealed class R2Ports : IAuthorityGate, ISessionClock, IReconnectTokens, ISpawnWorld, ISessionManagement
{
    public double Time;
    public double Now { get { return Time; } }
    public string Host="host";
    public bool Deny;
    public int Queries;
    private int token;
    public RuleResult Validate(CommandContext c) { return Deny ? RuleResult.Reject("denied") : RuleResult.Accept(); }
    public bool CanManage(CommandContext c) { return c.ActorId==Host; }
    public string Issue() { return "opaque-"+(++token); }
    public SpawnSite[] Sites(string map,bool warmup) { Queries++; return new[]{new SpawnSite("safe",new Float3(0,0,0),-1,true,false)}; }
    public bool IsSafe(string map,string actor,Float3 feet) { Queries++; return true; }
}
static class R2Tests
{
    static Session s; static R2Ports p; static int sequence,checks;
    static CommandContext C(string actor) { ++sequence;return new CommandContext("r2",actor,"c"+sequence,sequence,s.Epoch,true); }
    static SessionRules R(int teams=2,string mode="respawn",bool locked=false)
    { return new SessionRules("arena",mode,"map",teams,16,false,locked,true,true,5); }
    static void New(int teams=2,string mode="respawn") { p=new R2Ports(); s=new Session("r2","content",R(teams,mode),p,p,p,p,p); }
    static void Check(bool ok,string label) { checks++;if(!ok)throw new Exception(label); }
    static void Yes(RuleResult r) { Check(r.Accepted,"expected acceptance: "+r.Reason); }
    static void No(RuleResult r,string reason) { Check(!r.Accepted && r.Reason==reason,"expected "+reason+", got "+r.Reason); }
    static string Join(string actor,string token="",bool spectator=false)
    { string result;Yes(s.Join(C(actor),new JoinRequest(Protocol.Name,1,"content",actor,token,spectator),out result));return result; }
    static void Load(string actor) { Yes(s.Loaded(C(actor),"map")); }
    static void Begin(bool match=true,int teams=2,string mode="respawn") { Yes(s.BeginLoading(C(p.Host),R(teams,mode),match)); }
    static void Ready() { Yes(s.WorldReady(C(p.Host),"map")); }
    static void BalanceAndReservations()
    {
        foreach(int previous in new[]{0,2,4})foreach(int next in new[]{0,2,4})
        {
            New(previous); string[] actors={"z","b","a","c","e","d","f","g"};
            foreach(string actor in actors)Join(actor);
            string token=Join("reserved");int saved=s.Get("reserved").Team;
            Yes(s.Disconnect(C("reserved"),false));
            int[] old=new int[actors.Length];for(int i=0;i<actors.Length;i++)old[i]=s.Get(actors[i]).Team;
            Begin(true,next);
            string[] sorted=(string[])actors.Clone();Array.Sort(sorted,StringComparer.Ordinal);
            for(int i=0;i<sorted.Length;i++)
                Check(s.Get(sorted[i]).Team==(previous==next ? old[Array.IndexOf(actors,sorted[i])] : next==0 ? -1 : i%next),"stable redistribution or unchanged membership");
            Join("restored",token);
            Check(s.Get("restored").Team==(next==0 ? -1 : previous==next ? saved : 0),"changed-layout reconnect uses least populated team");
            Check(s.Count==9 && !s.Get("restored").HasPosition,"membership retained without stale placement");
        }
        New(2);string credential=Join("a");Join("b");Yes(s.Disconnect(C("a"),false));
        Begin(true,4);Begin(true,2);Join("new",credential);
        Check(s.Get("new").Team==1,"layout generation detects 2->4->2, not just equal counts");
        New(0);Join("a");long epoch=s.Epoch;
        No(s.BeginLoading(C("intruder"),R(2),true),"manager-required");
        Check(s.Epoch==epoch && s.Get("a").Team==-1,"unauthorized transition cannot rebalance");
        Yes(s.BeginLoading(C("host"),R(2),true));
        p.Host="replacement";
        No(s.BeginLoading(C("host"),R(4),true),"manager-required");
        Yes(s.BeginLoading(C("replacement"),R(4),true));
        Check(s.Rules.Teams==4,"management follows trusted host migration port");
    }
    static void LoadingRecovery()
    {
        New();Join("a");Join("missing");Begin();Load("a");Ready();
        Check(s.LoadingDeadline==60,"deadline published");
        p.Time=59.999;Yes(s.AdvanceLoading(C("host")));Check(s.Stage==SessionStage.Loading,"before deadline");
        p.Time=60;No(s.Activate(C("host")),"players-loading");
        Check(s.Stage==SessionStage.Loading,"source exact 60 is not expired");
        CommandContext stale=C("missing");long epoch=s.Epoch;
        p.Time=60.001;No(s.AdvanceLoading(C("a")),"manager-required");
        Check(s.Epoch==epoch && s.Stage==SessionStage.Loading,"unauthorized heartbeat cannot recover");
        p.Deny=true;No(s.AdvanceLoading(C("host")),"denied");p.Deny=false;
        Yes(s.AdvanceLoading(C("host")));
        Check(s.Stage==SessionStage.Lobby && s.Epoch==epoch+1 && s.LastLoadingRecovery=="loading-timeout" && s.LoadingDeadline==0,"timeout abort has explicit status and fresh epoch");
        Check(s.Count==2 && !s.Get("a").Loaded && !s.Get("a").HasPosition && s.Get("a").SpawnRevision==0,"abort retains membership and clears round readiness");
        No(s.Loaded(stale,"map"),"context");No(s.Spawn(C("a")),"not-ready");
        Check(p.Queries==0,"no false spawn or world queries during recovery");
        string token;No(s.Join(C("bad"),new JoinRequest(Protocol.Name,1,"wrong","bad","",false),out token),"incompatible-content");
        No(s.Loaded(C("a"),"wrong"),"map-cache-mismatch");Load("a");
        No(s.Spawn(C("a")),"not-ready");Ready();Yes(s.Spawn(C("a")));
        long after=s.Epoch;Yes(s.AdvanceLoading(C("host")));Check(s.Epoch==after,"abort is not repeated");

        New();Join("a");Begin();Load("a");p.Time=60.001;
        No(s.WorldReady(C("host"),"map"),"loading-timeout");
        Check(s.Stage==SessionStage.Lobby && !s.Get("a").Loaded,"missing world readiness aborts");
        New();Join("a");Begin();Ready();p.Time=60.001;
        No(s.Loaded(C("a"),"map"),"loading-timeout");
        Check(!s.Get("a").Loaded && s.Stage==SessionStage.Lobby,"late ack loses race to expired deadline");
        New();Join("a");Begin();Ready();p.Time=60;Load("a");Yes(s.Activate(C("host")));
        p.Time=100;Yes(s.AdvanceLoading(C("host")));Check(s.Stage==SessionStage.Match,"exact-deadline completion stays active");
        New();Join("a");Join("gone");Begin();Load("a");Ready();
        Yes(s.Disconnect(C("gone"),false));Yes(s.Activate(C("host")));
        Check(s.Stage==SessionStage.Match,"disconnected pending ack no longer blocks activation");
        New();Join("a");Begin();p.Time=61;No(s.Activate(C("host")),"loading-timeout");
        p.Time=60;No(s.AdvanceLoading(C("host")),"clock");
    }
    static void ReviveEligibility()
    {
        foreach(bool match in new[]{false,true})
        {
            New(2,"revive");Join("a");Begin(match,2,"revive");
            string token=Join("late");Join("watch","",true);
            Check(s.Get("late").WaitingRound==match,"loading target sets revive waiting intent");
            Yes(s.Disconnect(C("late"),false));Join("late-again",token);
            Check(s.Get("late-again").WaitingRound==match,"same epoch reconnect retains wait eligibility");
            Load("a");Load("late-again");Load("watch");Ready();Yes(s.Activate(C("host")));
            if(match)No(s.Spawn(C("late-again")),"spectator");else Yes(s.Spawn(C("late-again")));
            No(s.Spawn(C("watch")),"spectator");
            if(match)
            {
                Yes(s.EnterResults(C("host")));token=Join("results");
                Check(s.Get("results").WaitingRound,"revive results join waits");
                Yes(s.Disconnect(C("results"),false));Begin(false,2,"revive");Join("returned",token);
                Check(!s.Get("returned").WaitingRound && s.Get("returned").Life==SessionLife.Pending,"cross epoch reconnect can enter lobby");
                Load("a");Load("late-again");Load("watch");Load("returned");Ready();Yes(s.Activate(C("host")));
                Yes(s.Spawn(C("returned")));Yes(s.Spawn(C("late-again")));No(s.Spawn(C("watch")),"spectator");
            }
        }
        New(2,"revive");Join("a");Begin(true,2,"revive");Join("late");Join("watch","",true);
        p.Time=61;Yes(s.AdvanceLoading(C("host")));
        Check(!s.Get("late").WaitingRound && s.Get("late").Life==SessionLife.Pending,"timeout releases involuntary spectator");
        Check(s.Get("watch").Life==SessionLife.Spectator,"timeout preserves opt-in spectator");
    }
    public static int Main()
    {
        try { BalanceAndReservations();LoadingRecovery();ReviveEligibility();Console.WriteLine("PASS Sessions r2: "+checks+" behavioral checks");return 0; }
        catch(Exception e) { Console.Error.WriteLine(e);return 1; }
    }
}
