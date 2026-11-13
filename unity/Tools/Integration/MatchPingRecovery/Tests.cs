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
        public bool TryResolve(string s,long e,string c,out string id){id=c.StartsWith("new-")?c.Substring(4):c;return s=="test";}
        public double Now {get;set;} public long Tick {get {return (long)(Now*100)+1;}}
        public bool InMatch {get;set;} public bool TeamChangesAllowed {get {return !InMatch;}}
        public bool DenyScore=false; int token; public string Killer="b"; public CommittedAttribution Attribution;
        public RuleResult Validate(CommandContext c){return DenyScore&&c.ActorId=="host"&&c.CommandId.StartsWith("matchdirector:")?RuleResult.Reject("score-held"):(c.IsAuthority?RuleResult.Accept():RuleResult.Reject("authority"));}
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
        public bool Bind=true;public Environment E=new Environment();public Session S;public HealthService H;public Director D;int cmd;
        public CommandContext C(string actor){return new CommandContext("test",actor,"test:"+(++cmd),E.Tick,S.Epoch,true);}
        public Rig(string mode,int teams,int limit,int time,int rounds,int capacity,ScoreReceiptJournal journal=null,bool project=false)
        {
            S=new Session("test","content",new SessionRules("map",mode,"maphash",teams,64,true,false,true,true,1),E,E,E,E,E);
            H=Health();E.Attribution=new CommittedAttribution(H);
            D=new Director(S,new ModeRules(mode,teams,limit,time,rounds),"host",E,E,E.Attribution,capacity,E,project?new SessionHealthProjection(S,E,"host"):null,journal);
            
        }
        public HealthService Health(){return new HealthService("test",1,S.Rules.Teams,S.Rules.Mode=="respawn",E,E,E,60,1,0);}
        public string Join(string id,bool spectator,string token)
        {string result;Check(S.Join(C(id),new JoinRequest(Protocol.Name,1,"content",id,token,spectator),out result).Accepted,"join "+id);Check(D.ObserveMember(id).Accepted,"observe");return result;}
        public void Register(string id)
        {MemberSnapshot m=S.Get(id);Check(H.Register(C("host"),id,m.Team,m.Life==SessionLife.Spectator).Accepted,"register");}
        public void Deliver(){foreach(HealthTransition e in H.DrainTransitions()){Check(D.Deliver(e).Accepted,"deliver "+e.Kind);Check(D.Deliver(e).Accepted,"duplicate delivery");}}
        public void Load(params string[] ids)
        {
            E.InMatch=false;Check(D.BeginLoading().Accepted,"begin loading");
            if(Bind)Check(D.BindProducer(H,C("host"),new string('a',64)).Accepted,"bind real producer");
            foreach(string id in ids){Check(S.Loaded(C(id),"maphash").Accepted,"loaded");}
            Check(S.WorldReady(C("host"),"maphash").Accepted,"world ready");Check(S.Activate(C("host")).Accepted,"activate");
            foreach(string id in ids)if(S.Get(id).Life!=SessionLife.Spectator)Check(S.Spawn(C(id)).Accepted,"spawn session");
            foreach(string id in ids)Register(id);Deliver();E.InMatch=true;Check(D.StartLoadedRound().Accepted,"start round");
        }
        public void Loss(string id,bool revivable)
        {CommandContext hit=C("b");Check(H.ApplyLoss(hit,id,H.Read(id).Revision,100,0,revivable).Accepted,"damage");Check(E.Attribution.Capture(hit,hit.CommandId,id).Accepted,"capture committed attacker");Check(!E.Attribution.Capture(C("a"),hit.CommandId,id).Accepted,"cannot rebind attacker receipt");Deliver();}
    }
    static int Score(Director d,string id){foreach(ScoreRow row in d.Scores())if(row.Id==id)return row.Score;return -1;}


    static string PathFor(string name){return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Tests).Assembly.Location),name+"-"+Guid.NewGuid().ToString("N")+".json");}
    static HealthTransition Kill(Rig r){var hit=r.C("b");Check(r.H.ApplyLoss(hit,"a",r.H.Read("a").Revision,100,0,false).Accepted,"actual health death");Check(r.E.Attribution.Capture(hit,hit.CommandId,"a").Accepted,"actual attribution");return r.H.DrainTransitions()[0];}
    sealed class FailSecondWrite : Ashfall.Modules.Persistence.IAtomicSettingsFiles
    {
        readonly Ashfall.Modules.Persistence.PhysicalSettingsFiles files=new Ashfall.Modules.Persistence.PhysicalSettingsFiles();int writes;
        public bool Exists(string p){return files.Exists(p);}public IDisposable AcquireFamily(string p){return files.AcquireFamily(p);}public byte[] Read(string p,int max){return files.Read(p,max);}
        public void CreateDurable(string p,byte[] b){if(++writes==2)throw new System.IO.IOException("review-ack-write-failed");files.CreateDurable(p,b);}
        public void Commit(string p,string d,string b,bool replacing){files.Commit(p,d,b,replacing);}
    }
    static void Refuse(Action action,string label){bool refused=false;try{action();}catch(System.IO.InvalidDataException){refused=true;}catch(System.IO.IOException){refused=true;}catch(ArgumentException){refused=true;}Check(refused,label);}
    static Ashfall.RecoveryContracts.CheckpointBinding AB(){return new Ashfall.RecoveryContracts.CheckpointBinding("build","catalog","test","match",1,0,0,100);}


    static Director.QuarantinedPingArchive RestorePair(byte[] bytes,Environment e,HealthService h,Ashfall.RecoveryContracts.CheckpointBinding binding=null)
    {return Director.RestorePartialPingArchive(bytes,Ashfall.RecoveryContracts.RecoveryWire.Hash(bytes),binding??AB(),new ModeRules("respawn",0,10,100,2),"host",8,e,e,e,e,e,e,e,h,null);}
    static void ReadPair(string folder)
    {
        var e=new Environment();byte[] hb=System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,"health"));HealthService h;
        Check(HealthService.TryRestoreCheckpoint(new CommandContext("test","host","restore",9999,1,true),new string('a',64),Ashfall.RecoveryContracts.RecoveryWire.Hash(hb),hb,0,true,e,e,e,out h,60,1,0),"cold Health owner");
        byte[] bytes=System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,"pair"));var x=RestorePair(bytes,e,h);
        Check(x.HistoricalLife("a").Life==LifeState.Alive&&h.Read("a").Life==LifeState.Dead,"original historical life retained");
        int score=0;foreach(var row in x.Scores())if(row.Id=="b")score=row.Score;
        Check(score==(folder.EndsWith("after")?1:0)&&x.PendingScore>0,"score cut retained without replay");
        Check(x.PhysicalPending("test",1)==1,"actual physical pending");
        Check(x.Visible("b","map",1,0).Length==1,"cold private ping state");
        var point=new Float3(1,2,3);var normal=new Float3(0,1,0);
        Check(!x.ProbePing(new CommandContext("test","b","ping-original",1,1,true),RoundStage.Match,point,normal,1,-1,0).Accepted,"replay retained");
        Check(x.ProbePing(new CommandContext("test","b","fresh",2,1,true),RoundStage.Match,point,normal,1,-1,0).Reason=="cooldown","cooldown retained");
        Check(x.ProbePing(new CommandContext("test","b","fresh",2,1,true),RoundStage.Match,point,normal,1,-1,6).Accepted,"quarantined owner retry after cooldown");
        Check(x.AdoptionBlocker.Contains("physical-source-binding"),"adoption still refused");
    }

    static void Pressure()
    {
        var r=new Rig("respawn",0,10,100,2,8);r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");
        var owner=r.D.CreateRecoveryPingOwner(0,8192);var point=new Float3(0,0,0);var normal=new Float3(0,1,0);
        for(int i=0;i<8192;i++)Check(owner.Place(new CommandContext("test","b","pressure"+i,i,1,true),point,normal,0,-1).Accepted,"maximum actual private history "+i);
        Check(owner.Place(new CommandContext("test","b","overflow",8192,1,true),point,normal,0,-1).Reason=="journal_full","capacity never evicts replay");
        string path=PathFor("pressure");var store=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(store.Load(false).Accepted,"empty actual physical family");byte[] family=ReceiptFamilyArchive.Capture(path,new Ashfall.Modules.Persistence.PhysicalSettingsFiles());
        byte[] bytes=owner.CapturePartialArchive(AB(),family);var x=RestorePair(bytes,r.E,r.H);
        Check(!x.ProbePing(new CommandContext("test","b","pressure0",0,1,true),RoundStage.Match,point,normal,0,-1,0).Accepted,"oldest replay retained at capacity");
        Check(x.ProbePing(new CommandContext("test","b","overflow",8192,1,true),RoundStage.Match,point,normal,0,-1,0).Reason=="journal_full","cold saturation retained");
        foreach(var b in new[]{new Ashfall.RecoveryContracts.CheckpointBinding("build","catalog","test","match",1,1,0,100),new Ashfall.RecoveryContracts.CheckpointBinding("build","catalog","test","match",1,0,1,100),new Ashfall.RecoveryContracts.CheckpointBinding("build","catalog","test","match",1,0,0,101)})Refuse(()=>RestorePair(bytes,r.E,r.H,b),"mixed owner generation binding");
        byte[] trailing=new byte[bytes.Length+1];Array.Copy(bytes,trailing,bytes.Length);Refuse(()=>RestorePair(trailing,r.E,r.H),"trailing composite");
        using(var m=new System.IO.MemoryStream((byte[])bytes.Clone()))using(var rd=new System.IO.BinaryReader(m)){
            Ashfall.RecoveryContracts.RecoveryWire.Text(rd);AB().Check(rd);long time=m.Position;
            foreach(double value in new[]{double.NaN,double.PositiveInfinity,-1,1}){
                byte[] bad=(byte[])bytes.Clone();Array.Copy(BitConverter.GetBytes(value),0,bad,time,8);Refuse(()=>RestorePair(bad,r.E,r.H),"forged composite logical time");
            }
        }
        Console.WriteLine("PRESSURE imageBytes="+bytes.Length+" commands=8192");
    }
    static int Main(string[] args)
    {
        try {
            if(args.Length>0){ReadPair(args[0]);Console.WriteLine("PAIR COLD PASS "+assertions);return 0;}
            foreach(bool after in new[]{false,true}){
                string path=PathFor("pair-receipt");var store=new Ashfall.Modules.Persistence.AtomicSettingsStore(path,after?(Ashfall.Modules.Persistence.IAtomicSettingsFiles)new FailSecondWrite():new Ashfall.Modules.Persistence.PhysicalSettingsFiles());Check(store.Load(false).Accepted,"store");
                var r=new Rig("respawn",0,10,100,2,8,new ScoreReceiptJournal(store));r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");
                var ping=r.D.CreateRecoveryPingOwner(5,8192);Check(ping.Place(new CommandContext("test","b","ping-original",1,1,true),new Float3(1,2,3),new Float3(0,1,0),1,-1).Accepted,"new versioned owner ping");
                var hit=r.C("b");Check(r.H.ApplyLoss(hit,"a",r.H.Read("a").Revision,100,0,false).Accepted,"actual death");Check(r.E.Attribution.Capture(hit,hit.CommandId,"a").Accepted,"actual killer");r.E.DenyScore=!after;Check(!r.D.DrainProducer().Accepted,"actual deferred/ACK cut");
                byte[] family=ReceiptFamilyArchive.Capture(path,new Ashfall.Modules.Persistence.PhysicalSettingsFiles());byte[] bytes=ping.CapturePartialArchive(AB(),family);
                string folder=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Tests).Assembly.Location),after?"pair-after":"pair-before");System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"pair"),bytes);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"health"),r.H.CreateCheckpoint(r.C("host"),new string('a',64)));
                ReadPair(folder);ReadPair(folder);
                using(var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(typeof(Tests).Assembly.Location,"\""+folder+"\""){UseShellExecute=false,RedirectStandardOutput=true})){Console.Write(child.StandardOutput.ReadToEnd());child.WaitForExit();Check(child.ExitCode==0,"cold subprocess");}
                for(int n=0;n<bytes.Length;n++){byte[] bad=new byte[n];Array.Copy(bytes,bad,n);Refuse(()=>RestorePair(bad,r.E,r.H),"truncated envelope "+n);}
                Refuse(()=>RestorePair(bytes,r.E,r.H,new Ashfall.RecoveryContracts.CheckpointBinding("other","catalog","test","match",1,0,0,100)),"source mismatch");
                Refuse(()=>RestorePair(bytes,r.E,r.H,new Ashfall.RecoveryContracts.CheckpointBinding("build","catalog","test","match",2,0,0,100)),"epoch mismatch");
                Refuse(()=>ping.CapturePartialArchive(AB(),new byte[4*1024*1024+1]),"maximum family bound");
                r.E.Now=1;Check(r.D.Heartbeat().Accepted,"advance director");Refuse(()=>ping.CapturePartialArchive(AB(),family),"partial owner clocks reject rather than trust capture time");
                r.E.DenyScore=false;Check(r.D.DrainProducer().Accepted&&Score(r.D,"b")==1&&r.S.Get("a").Deaths==1,"original retry never duplicates credited score");
            }
            Pressure();
            bool gate=false;try{Director.RequireCompleteRecovery();}catch(InvalidOperationException){gate=true;}Check(gate,"full proof unavailable");
            Console.WriteLine("PAIR PASS "+assertions);return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}

