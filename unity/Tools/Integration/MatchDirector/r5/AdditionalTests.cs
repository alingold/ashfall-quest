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
    sealed class Environment : IMatchClock,ISessionClock,IHealthWorld,IAuthorityGate,ISessionManagement,IReconnectTokens,ISpawnWorld,IInteractionQuery,IKnockdownAttribution,ILogicalPlayerIdentity
    {
        public bool TryResolve(string s,long e,string c,out string id){id=c.StartsWith("new-")?"a":c;return s=="test";}
        public double Now {get;set;} public long Tick {get {return (long)(Now*100)+1;}}
        public bool InMatch {get;set;} public bool TeamChangesAllowed {get {return !InMatch;}}
        int token; public string Killer="b"; public CommittedAttribution Attribution;
        public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("authority");}
        public bool CanManage(CommandContext c){return c.ActorId=="host";}
        public string Issue(){return "token:"+(++token);}
        public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("floor",new Float3(0,0,0),-1,true,false)};}
        public bool IsSafe(string map,string actor,Float3 feet){return feet.IsFinite;}
        public bool HasLineOfSight(string actor,string target,Float3 a,Float3 b,float range){return true;}
        public bool TryGetPose(string actor,out RescuePose pose){pose=new RescuePose {Tracked=true,PaddlesActive=true,Desktop=true,Facing=true,Left=new Float3(0,0,0),Right=new Float3(0,0,0),Chest=new Float3(0,0,0)};return true;}
        public string KillerFor(HealthTransition transition){return Attribution==null?null:Attribution.KillerFor(transition);}
    }
    sealed class Rig
    {
        public Environment E=new Environment();public Session S;public HealthService H;public Director D;int cmd;
        public CommandContext C(string actor){return new CommandContext("test",actor,"test:"+(++cmd),E.Tick,S.Epoch,true);}
        public Rig(string mode,int teams,int limit,int time,int rounds,int capacity,ScoreReceiptJournal journal=null)
        {
            S=new Session("test","content",new SessionRules("map",mode,"maphash",teams,64,true,false,true,true,1),E,E,E,E,E);
            D=new Director(S,new ModeRules(mode,teams,limit,time,rounds),"host",E,E,E,capacity,E,null,journal);
            H=Health();E.Attribution=new CommittedAttribution(H);
        }
        HealthService Health(){return new HealthService("test",S.Epoch,S.Rules.Teams,S.Rules.Mode=="respawn",E,E,E,60,1,0);}
        public string Join(string id,bool spectator,string token)
        {string result;Check(S.Join(C(id),new JoinRequest(Protocol.Name,1,"content",id,token,spectator),out result).Accepted,"join "+id);Check(D.ObserveMember(id).Accepted,"observe");return result;}
        public void Register(string id)
        {MemberSnapshot m=S.Get(id);Check(H.Register(C("host"),id,m.Team,m.Life==SessionLife.Spectator).Accepted,"register");}
        public void Deliver(){foreach(HealthTransition e in H.DrainTransitions()){Check(D.Deliver(e).Accepted,"deliver "+e.Kind);Check(D.Deliver(e).Accepted,"duplicate delivery");}}
        public void Load(params string[] ids)
        {
            E.InMatch=false;Check(D.BeginLoading().Accepted,"begin loading");H=Health();E.Attribution=new CommittedAttribution(H);
            foreach(string id in ids){Check(S.Loaded(C(id),"maphash").Accepted,"loaded");}
            Check(S.WorldReady(C("host"),"maphash").Accepted,"world ready");Check(S.Activate(C("host")).Accepted,"activate");
            foreach(string id in ids)if(S.Get(id).Life!=SessionLife.Spectator)Check(S.Spawn(C(id)).Accepted,"spawn session");
            foreach(string id in ids)Register(id);Deliver();E.InMatch=true;Check(D.StartLoadedRound().Accepted,"start round");
        }
        public void Loss(string id,bool revivable)
        {CommandContext hit=C("b");Check(H.ApplyLoss(hit,id,H.Read(id).Revision,100,0,revivable).Accepted,"damage");Check(E.Attribution.Capture(hit,hit.CommandId,id).Accepted,"capture committed attacker");Check(!E.Attribution.Capture(C("a"),hit.CommandId,id).Accepted,"cannot rebind attacker receipt");Deliver();}
    }
    static int Score(Director d,string id){foreach(ScoreRow row in d.Scores())if(row.Id==id)return row.Score;return -1;}

    static void Reconnect(string mode,bool downed)
    {
        var r=new Rig(mode,2,0,0,1,2);string token=r.Join("a",false,null);r.Join("b",false,null);r.Join("c",false,null);r.Join("d",false,null);r.Load("a","b","c","d");
        if(downed)r.Loss("a",true);
        else {var hit=r.C("b");Check(r.H.ApplyLoss(hit,"a",r.H.Read("a").Revision,23,0,false).Accepted,"injured");r.Deliver();}
        long life=r.D.LifeSequence("a"),gen=r.H.Read("a").LifeGeneration;float health=r.H.Read("a").Health;int team=r.H.Read("a").Team;
        Check(r.S.Disconnect(r.C("a"),false).Accepted,"disconnect session");Check(r.H.SetConnected(r.C("host"),"a",r.H.Read("a").Revision,false).Accepted,"disconnect health");r.Deliver();
        r.Join("new-a",false,token);Check(r.D.BindConnection("new-a").Accepted,"authenticated bind");Check(r.S.Loaded(r.C("new-a"),"maphash").Accepted,"reload");
        Check(r.H.SetConnected(r.C("host"),r.D.CanonicalActor("new-a"),r.H.Read("a").Revision,true).Accepted,"canonical reconnect health");r.Deliver();
        ModeActor a;Check(r.D.TryGet("new-a",out a)&&a.Id=="a"&&a.Team==team,"canonical roster alias");
        Check(r.H.Read("new-a")==null&&r.H.Read("a").Health==health&&r.H.Read("a").LifeGeneration==gen&&r.D.LifeSequence("a")==life,"no new health or life");
        Check(r.D.BindConnection("new-a").Accepted,"binding replay");Check(r.D.LifeSequence("new-a")==life,"alias life sequence");Check(!r.D.BindConnection("unknown").Accepted,"unknown rejected");
        Check(r.S.Disconnect(r.C("new-a"),false).Accepted,"disconnect new actor");Check(r.H.SetConnected(r.C("host"),"a",r.H.Read("a").Revision,false).Accepted,"disconnect again health");r.Deliver();
        r.Join("new-again",false,token);Check(r.D.BindConnection("new-again").Accepted,"second remap");Check(r.D.CanonicalActor("new-again")=="a","canonical survives remaps");
    }
    static void DurableReceipt()
    {
        var r=new Rig("respawn",0,0,0,1,1);r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");
        var hit=r.C("b");Check(r.H.ApplyLoss(hit,"a",r.H.Read("a").Revision,100,0,false).Accepted,"real committed death");Check(r.E.Attribution.Capture(hit,hit.CommandId,"a").Accepted,"capture");var e=r.H.DrainTransitions()[0];
        string path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Tests).Assembly.Location),"journal-"+Guid.NewGuid().ToString("N")+".json");
        var store=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(store.Load(false).Accepted,"load missing real store");var j=new ScoreReceiptJournal(store);
        Check(j.Retain(e,"b",1).Accepted,"retain committed uncredited receipt durably");
        var reopened=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(reopened.Load(false).Accepted,"disk reopen before credit");var recovered=new ScoreReceiptJournal(reopened);string payload;bool ack;
        Check(recovered.TryRead(ScoreReceiptJournal.Key(e),out payload,out ack)&&!ack&&payload.Length>0,"recover uncredited receipt");
        Check(recovered.Retain(e,"b",1).Accepted,"exact retained retry");Check(!recovered.Retain(e,"a",1).Accepted,"cannot replace attribution");
        Check(r.D.Deliver(e).Accepted&&r.D.Deliver(e).Accepted,"real score and lost ack replay");Check(Score(r.D,"b")==1,"exactly once score");
        Check(recovered.Acknowledge(e).Accepted,"durable acknowledge");
        var third=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(third.Load(false).Accepted,"reopen acknowledged");Check(new ScoreReceiptJournal(third).TryRead(ScoreReceiptJournal.Key(e),out payload,out ack)&&ack,"ack retains original receipt");
    }
    static void LateAndBudget(bool late)
    {
        string path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Tests).Assembly.Location),"late-"+Guid.NewGuid().ToString("N")+".json");
        var store=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(store.Load(false).Accepted,"late store load");var journal=new ScoreReceiptJournal(store);
        var r=new Rig("respawn",0,0,1,2,1,journal);r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");
        Check(r.D.UpdateLimits(r.C("host"),new LiveLimits(0,1)).Accepted,"fill limit budget");Check(!r.D.UpdateLimits(r.C("host"),new LiveLimits(0,1)).Accepted,"limit budget remains bounded");
        var hit=r.C("b");Check(r.H.ApplyLoss(hit,"a",r.H.Read("a").Revision,100,0,false).Accepted,"damage committed before clock");Check(r.E.Attribution.Capture(hit,hit.CommandId,"a").Accepted,"attribution");var e=r.H.DrainTransitions()[0];
        if(late){r.E.Now=1;Check(r.D.Heartbeat().Accepted,"clock before retained delivery");}
        using(var held=new System.IO.FileStream(path+".lock",System.IO.FileMode.OpenOrCreate,System.IO.FileAccess.ReadWrite,System.IO.FileShare.None))
        {Check(!r.D.Deliver(e).Accepted,"real Persistence family contention rejects publication");Check(Score(r.D,"b")==0&&r.D.LifeSequence("a")==1,"no score or new life on durable failure");}
        Check(r.D.Deliver(e).Accepted&&r.D.Deliver(e).Accepted,"delivery and lost ack");Check(Score(r.D,"b")== (late?0:1),"late remains uncredited; timely score independent from limit budget");
        Check(journal.Pending("test",r.S.Epoch).Length==(late?1:0),"durable uncredited index");
        var reopened=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(reopened.Load(false).Accepted,"reopen real receipt family");Check(new ScoreReceiptJournal(reopened).Pending("test",r.S.Epoch).Length==(late?1:0),"discover retained uncredited after reopen");
        if(!late){r.E.Now=1;Check(r.D.Heartbeat().Accepted,"clock after scoring");}
        Check(!r.D.IntegrationReadiness().Accepted,"missing real Session projection fails readiness");
        r.Load("a","b");Check(r.D.Epoch==2,"next round not blocked by retained receipt");r.E.Now=2;Check(r.D.Heartbeat().Accepted,"next round deadline");Check(r.D.AcknowledgeComplete().Accepted,"completion independent lane");
    }
    static int Main(){try{Reconnect("respawn",false);Reconnect("revive",true);DurableReceipt();LateAndBudget(false);LateAndBudget(true);Console.WriteLine("PASS "+assertions+" additional assertions; canonical identity and real Persistence disk reopen; Session down projection remains blocked");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
