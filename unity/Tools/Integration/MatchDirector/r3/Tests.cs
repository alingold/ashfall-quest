using System;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.MatchDirector;
sealed class Tests
{
    static int assertions;
    static void Check(bool yes,string name){assertions++;if(!yes)throw new Exception(name);}
    sealed class Environment : IMatchClock,ISessionClock,IHealthWorld,IAuthorityGate,ISessionManagement,IReconnectTokens,ISpawnWorld,IInteractionQuery,IKnockdownAttribution
    {
        public double Now {get;set;} public long Tick {get {return (long)(Now*100)+1;}}
        public bool InMatch {get;set;} public bool TeamChangesAllowed {get {return !InMatch;}}
        int token; public string Killer="b";
        public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("authority");}
        public bool CanManage(CommandContext c){return c.ActorId=="host";}
        public string Issue(){return "token:"+(++token);}
        public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("floor",new Float3(0,0,0),-1,true,false)};}
        public bool IsSafe(string map,string actor,Float3 feet){return feet.IsFinite;}
        public bool HasLineOfSight(string actor,string target,Float3 a,Float3 b,float range){return true;}
        public bool TryGetPose(string actor,out RescuePose pose){pose=new RescuePose {Tracked=true,PaddlesActive=true,Desktop=true,Facing=true,Left=new Float3(0,0,0),Right=new Float3(0,0,0),Chest=new Float3(0,0,0)};return true;}
        public string KillerFor(HealthTransition transition){return Killer;}
    }
    sealed class Rig
    {
        public Environment E=new Environment();public Session S;public HealthService H;public Director D;int cmd;
        public CommandContext C(string actor){return new CommandContext("test",actor,"test:"+(++cmd),E.Tick,S.Epoch,true);}
        public Rig(string mode,int teams,int limit,int time,int rounds,int capacity)
        {
            S=new Session("test","content",new SessionRules("map",mode,"maphash",teams,64,true,false,true,true,1),E,E,E,E,E);
            D=new Director(S,new ModeRules(mode,teams,limit,time,rounds),"host",E,E,E,capacity);
            H=Health();
        }
        HealthService Health(){return new HealthService("test",S.Epoch,S.Rules.Teams,S.Rules.Mode=="respawn",E,E,E,60,1,0);}
        public string Join(string id,bool spectator,string token)
        {string result;Check(S.Join(C(id),new JoinRequest(Protocol.Name,1,"content",id,token,spectator),out result).Accepted,"join "+id);Check(D.ObserveMember(id).Accepted,"observe");return result;}
        public void Register(string id)
        {MemberSnapshot m=S.Get(id);Check(H.Register(C("host"),id,m.Team,m.Life==SessionLife.Spectator).Accepted,"register");}
        public void Deliver(){foreach(HealthTransition e in H.DrainTransitions()){Check(D.Deliver(e).Accepted,"deliver "+e.Kind);Check(D.Deliver(e).Accepted,"duplicate delivery");}}
        public void Load(params string[] ids)
        {
            E.InMatch=false;Check(D.BeginLoading().Accepted,"begin loading");H=Health();
            foreach(string id in ids){Check(S.Loaded(C(id),"maphash").Accepted,"loaded");}
            Check(S.WorldReady(C("host"),"maphash").Accepted,"world ready");Check(S.Activate(C("host")).Accepted,"activate");
            foreach(string id in ids)if(S.Get(id).Life!=SessionLife.Spectator)Check(S.Spawn(C(id)).Accepted,"spawn session");
            foreach(string id in ids)Register(id);Deliver();E.InMatch=true;Check(D.StartLoadedRound().Accepted,"start round");
        }
        public void Loss(string id,bool revivable)
        {Check(H.ApplyLoss(C("b"),id,H.Read(id).Revision,100,0,revivable).Accepted,"damage");Deliver();}
    }
    static int Score(Director d,string id){foreach(ScoreRow row in d.Scores())if(row.Id==id)return row.Score;return -1;}
    static void WarmupAndRounds()
    {
        var r=new Rig("respawn",0,0,1,2,2);
        r.Join("a",false,null);Check(r.S.Loaded(r.C("a"),"maphash").Accepted,"warmup loaded");Check(r.S.WorldReady(r.C("host"),"maphash").Accepted,"warmup world");Check(r.S.Spawn(r.C("a")).Accepted,"warmup spawn");r.Register("a");r.Deliver();
        Check(r.D.Ping(r.C("a"),new Float3(0,0,0),new Float3(0,1,0),0,-1).Accepted,"warmup ping lobby stage");
        Check(r.D.VisiblePings("a").Length==1,"visible warmup");Check(r.H.Spawn(r.C("host"),"a",r.H.Read("a").Revision,100,100).Accepted,"warmup relocation health");r.Deliver();Check(r.D.LifeSequence("a")==1,"alive relocation not new life");
        r.Load("a");Check(r.D.VisiblePings("a").Length==0,"epoch clears pings");Check(Score(r.D,"a")==0,"zero row");
        Check(r.D.UpdateLimits(r.C("host"),new LiveLimits(0,1)).Accepted,"limits one");Check(r.D.UpdateLimits(r.C("host"),new LiveLimits(0,1)).Accepted,"limits two");
        Check(!r.D.UpdateLimits(r.C("host"),new LiveLimits(0,1)).Accepted,"journal saturated");
        r.E.Now=1;Check(r.D.Heartbeat().Accepted,"timer bypasses saturated journal");Check(r.S.Stage==SessionStage.Results&&r.D.EndReason=="time_limit","results binding");
        r.Load("a");Check(r.D.RoundIndex==2&&r.D.Epoch==2,"second round epoch");r.E.Now=2;Check(r.D.Heartbeat().Accepted,"second timer");Check(r.D.AcknowledgeComplete().Accepted,"complete ack");Check(r.D.Stage==RoundStage.Complete,"complete stage");
    }
    static void HealthLives()
    {
        var r=new Rig("revive",2,0,0,1,128);
        string token=r.Join("a",false,null);r.Join("b",false,null);r.Join("c",false,null);r.Join("d",false,null);r.Join("s",true,null);
        r.Load("a","b","c","d","s");Check(r.D.Scores().Length==2,"two team rows");Check(r.D.LifeSequence("a")==1,"registered life");
        r.Loss("a",true);Check(Score(r.D,"team:1")==1,"one score down");Check(r.D.LifeSequence("a")==1,"down no new life");
        for(int i=0;i<65;i++){r.E.Now+=.1;Check(r.H.SamplePaddles(r.C("c")).Accepted,"charge paddles");}
        Check(r.H.Revive(r.C("c"),"a",r.H.Read("a").Revision).Accepted,"real revive");r.Deliver();Check(r.D.LifeSequence("a")==2,"revive new life");
        r.Loss("a",true);Check(Score(r.D,"team:1")==2,"score revived second life");
        r.E.Now+=61;Check(r.H.Synchronize(r.C("host")).Accepted,"bleedout clock");r.Deliver();Check(Score(r.D,"team:1")==2,"no bleedout score");Check(r.S.Get("a").Life==SessionLife.Eliminated,"dead session binding");
        Check(r.S.Disconnect(r.C("a"),false).Accepted,"disconnect");Check(r.H.SetConnected(r.C("host"),"a",r.H.Read("a").Revision,false).Accepted,"health disconnect");r.Deliver();
        r.Join("a",false,token);Check(r.H.SetConnected(r.C("host"),"a",r.H.Read("a").Revision,true).Accepted,"health reconnect");r.Deliver();Check(r.D.LifeSequence("a")==2,"reconnect preserves life");
        r.Join("late",false,null);Check(r.S.Get("late").WaitingRound,"late revive join waits");Check(r.S.Loaded(r.C("late"),"maphash").Accepted,"late loaded");r.Register("late");r.Deliver();ModeActor late;Check(r.D.TryGet("late",out late)&&late.Spectator,"late roster spectator");Check(r.D.LifeSequence("late")==0,"spectator no life");
        Check(!r.D.Ping(r.C("late"),new Float3(),new Float3(0,1,0),0,-1).Accepted,"spectator ping rejected");
    }
    static void AuthorityAndSpawn()
    {
        var r=new Rig("respawn",0,0,0,1,32);r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");
        Check(!r.D.UpdateLimits(r.C("a"),new LiveLimits(1,1)).Accepted,"non director limits");
        CommandContext forged=new CommandContext("test","host","forged",r.E.Tick,r.S.Epoch,false);Check(!r.D.UpdateLimits(forged,new LiveLimits(1,1)).Accepted,"non authority limits");
        r.Loss("a",false);Check(Score(r.D,"b")==1,"direct death score");r.E.Now=1;Check(r.S.Spawn(r.C("a")).Accepted,"session respawn");Check(r.H.Spawn(r.C("host"),"a",r.H.Read("a").Revision,100,100).Accepted,"health respawn");r.Deliver();Check(r.D.LifeSequence("a")==2,"spawn new life");
        Check(r.H.SetConnected(r.C("host"),"a",r.H.Read("a").Revision,true).Accepted,"connection event");r.Deliver();Check(r.D.LifeSequence("a")==2,"connection no new life");
        Check(r.D.UpdateLimits(r.C("host"),new LiveLimits(1,0)).Accepted,"live limit routes existing score");Check(r.D.Stage==RoundStage.Results,"lower score limit finishes");Check(r.D.Heartbeat().Accepted&&r.S.Stage==SessionStage.Results,"limit result session route");
        r.E.Now=Double.NaN;Check(!r.D.Heartbeat().Accepted,"NaN clock");r.E.Now=0;Check(!r.D.Heartbeat().Accepted,"rollback clock");
    }
    static void EventBindingAndDependencyGate()
    {
        var r=new Rig("respawn",0,0,1,1,16);r.Join("a",false,null);r.Register("a");
        HealthTransition initial=r.H.DrainTransitions()[0];Check(!r.D.Deliver(initial).Accepted,"registration alone not genuine spawn");
        Check(r.S.Loaded(r.C("a"),"maphash").Accepted,"bound loaded");Check(r.S.WorldReady(r.C("host"),"maphash").Accepted,"bound world");Check(r.S.Spawn(r.C("a")).Accepted,"bound spawn");Check(r.D.Deliver(initial).Accepted,"spawn evidence admits initial life");
        r.Load("a");Check(!r.D.Deliver(initial).Accepted,"old epoch event rejected");
        RuleResult budget=RuleResult.Accept();
        for(int i=0;i<65540;i++){budget=r.S.RecordPosition(r.C("a"),new Float3());if(budget.Reason=="epoch-command-budget")break;}
        Check(!budget.Accepted&&budget.Reason=="epoch-command-budget","actual pinned Sessions budget saturates");
        r.E.Now=1;RuleResult heartbeat=r.D.Heartbeat();
        Check(!heartbeat.Accepted&&heartbeat.Reason=="epoch-command-budget","honest Sessions results incompatibility");
        Check(r.D.Stage==RoundStage.Results&&r.S.Stage==SessionStage.Match,"mode timer completes but session blocked gate");
    }
    static int Main(){try{WarmupAndRounds();HealthLives();AuthorityAndSpawn();EventBindingAndDependencyGate();Console.WriteLine("PASS "+assertions+" deterministic assertions; actual Modes/2 Sessions/2 HealthStates/2");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
}



