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
            D=new Director(S,new ModeRules(mode,teams,limit,time,rounds),"host",E,E,E,capacity,E,project?new SessionHealthProjection(S,E,"host"):null,journal);
            H=Health();E.Attribution=new CommittedAttribution(H);
        }
        public HealthService Health(){return new HealthService("test",S.Epoch,S.Rules.Teams,S.Rules.Mode=="respawn",E,E,E,60,1,0);}
        public string Join(string id,bool spectator,string token)
        {string result;Check(S.Join(C(id),new JoinRequest(Protocol.Name,1,"content",id,token,spectator),out result).Accepted,"join "+id);Check(D.ObserveMember(id).Accepted,"observe");return result;}
        public void Register(string id)
        {MemberSnapshot m=S.Get(id);Check(H.Register(C("host"),id,m.Team,m.Life==SessionLife.Spectator).Accepted,"register");}
        public void Deliver(){foreach(HealthTransition e in H.DrainTransitions()){Check(D.Deliver(e).Accepted,"deliver "+e.Kind);Check(D.Deliver(e).Accepted,"duplicate delivery");}}
        public void Load(params string[] ids)
        {
            E.InMatch=false;Check(D.BeginLoading().Accepted,"begin loading");H=Health();E.Attribution=new CommittedAttribution(H);
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
    sealed class Facts : Ashfall.Modules.Combat.ICombatEvidence
    {
        public Ashfall.Modules.Combat.HostHit Hit;
        public bool TryResolve(CommandContext c,string id,out Ashfall.Modules.Combat.HitEvidence e)
        {e=new Ashfall.Modules.Combat.HitEvidence{Identity=Hit,ActorInputReady=true,BaseDamage=200,DamageMultiplier=1,HeadRatio=1,MaximumRange=10};return true;}
    }
    static void CloseCase(bool cold,bool rollover)
    {
        string path=PathFor("r9-close");var scoreStore=new Ashfall.Modules.Persistence.AtomicSettingsStore(PathFor("score"));Check(scoreStore.Load(false).Accepted,"score store");
        var r=new Rig("respawn",0,0,1,rollover?2:1,8,new ScoreReceiptJournal(scoreStore),true);r.Bind=false;
        r.Join("a",false,null);r.Join("b",false,null);r.Join("c",false,null);r.Load("a","b","c");
        var f=new Facts();var bridge=new Ashfall.Integration.CombatHealthBridge(r.H,r.E,f,r.E,r.E,"test",1);
        var lane=new Ashfall.Integration.CombatPublication.HealthPublicationLane(r.H,bridge,new Ashfall.Modules.Persistence.AtomicSettingsStore(path),"test",1,"host",65,new string('a',64));
        Check(r.D.BindProducer(r.H,r.C("host"),new string('a',64),lane).Accepted,"bind durable producer");
        var weapon=bridge.Equip(r.C("host"),"r9-gun","b","rifle",1,r.E.Tick);
        var ev=bridge.PublishEvent(r.C("host"),1,r.E.Tick+100,Ashfall.Modules.Combat.DamageSource.CurrentActorIntent,Ashfall.Modules.Combat.DamageKind.Bullet,weapon,r.E.Tick);
        f.Hit=bridge.PublishHit(r.C("host"),"first",ev,"body","a",0,r.E.Tick);var delivery=r.C("b");
        Check(lane.Combat(r.C("host"),delivery,"first",r.E.Tick,false).Accepted,"actual first lethal Combat publication");
        Check(r.E.Attribution.CapturePublication(r.C("host"),f.Hit,bridge.Apply(delivery,"first",r.E.Tick,false)).Accepted,"first attribution");
        Check(r.D.DrainProducer().Accepted,"actual score then durable ACK");
        r.E.Now=1;Check(r.D.Heartbeat().Accepted,"Results");
        var complete=rollover?r.D.BeginLoading():r.D.AcknowledgeComplete();
        Check(!complete.Accepted&&complete.Reason=="root-producer-quiescence-required"&&r.D.Stage==RoundStage.Results&&r.S.Epoch==1,"global teardown refuses absent registry coverage");
        Check(lane.ProducerAdmissionClosed,"completion request persists routed tombstone");
        Ashfall.Integration.CombatPublication.HealthPublicationLane.ProducerAdmissionClose closure;
        Check(lane.CloseProducerAdmission(r.C("host"),2,new string('a',64),out closure).Accepted&&closure.Checkpoint!=null,"typed idempotent closure receipt");
        Check(lane.CloseProducerAdmission(r.C("host"),3,new string('a',64),out closure).Reason=="producer-close-conflict","cannot rebind tombstone");
        Check(lane.Damage(r.C("host"),"late-routed","c",r.H.Read("c").Revision,100,0,false).Reason=="producer-admission-closed","retained routed reference rejects late death");
        if(cold)
        {
            var receipt=lane.SuccessfulCheckpoint;string fork=PathFor("cold");System.IO.File.Copy(path,fork);
            HealthService h;Ashfall.Integration.CombatHealthBridge b;Ashfall.Integration.CombatPublication.HealthPublicationLane l;
            Check(Ashfall.Integration.CombatPublication.HealthPublicationLane.TryRestoreCold(r.C("host"),new string('a',64),receipt.Digest,receipt.HealthSequence,new Ashfall.Modules.Persistence.AtomicSettingsStore(fork),0,true,r.E,r.E,r.E,f,out h,out b,out l,60,1,0,65,receipt.Generation),"cold coherent restore");
            Check(l.ProducerAdmissionClosed&&l.Damage(r.C("host"),"cold-late","c",h.Read("c").Revision,100,0,false).Reason=="producer-admission-closed","cold retained tombstone rejects late death");
            Check(l.Offer(r.C("host")).Length==0&&l.CheckGlobalRetirement(r.C("host"),receipt.HealthSequence).Reason=="root-producer-quiescence-required","cold ACK prefix retained without false retirement");
            string processFork=PathFor("subprocess-copy");System.IO.File.Copy(fork,processFork);
            var start=new System.Diagnostics.ProcessStartInfo(typeof(Tests).Assembly.Location,"cold \""+processFork+"\" "+l.CheckpointDigest+" "+l.SuccessfulCheckpoint.HealthSequence+" "+l.CheckpointGeneration);start.UseShellExecute=false;start.RedirectStandardOutput=true;
            using(var process=System.Diagnostics.Process.Start(start)){string output=process.StandardOutput.ReadToEnd();process.WaitForExit();Console.Write(output);Check(process.ExitCode==0,"real cold subprocess");}
            var coldWeapon=b.Equip(r.C("host"),"cold-direct-gun","b","rifle",1,r.E.Tick);
            var coldEvent=b.PublishEvent(r.C("host"),2,r.E.Tick+100,Ashfall.Modules.Combat.DamageSource.CurrentActorIntent,Ashfall.Modules.Combat.DamageKind.Bullet,coldWeapon,r.E.Tick);
            f.Hit=b.PublishHit(r.C("host"),"cold-direct-hit",coldEvent,"body","c",0,r.E.Tick);
            Check(b.Apply(r.C("b"),"cold-direct-hit",r.E.Tick,false).Result.Accepted,"cold direct reference remains unregistered, not falsely retired");
            Check(l.Offer(r.C("host")).Length==1&&h.Read("c").Deaths==1&&!l.CheckGlobalRetirement(r.C("host"),receipt.HealthSequence).Accepted,"cold bypass durably retained with no global success");
            Console.WriteLine("COLD DIRECT: actual Health death=1 journal pending=1; no cold Match restoration claimed");
        }
        // Same actual references captured before close; legacy factories remain unregistered.
        var heldHealth=r.H;var heldBridge=bridge;
        ev=heldBridge.PublishEvent(r.C("host"),2,r.E.Tick+100,Ashfall.Modules.Combat.DamageSource.CurrentActorIntent,Ashfall.Modules.Combat.DamageKind.Bullet,weapon,r.E.Tick);
        f.Hit=heldBridge.PublishHit(r.C("host"),"direct-late",ev,"body","c",0,r.E.Tick);delivery=r.C("b");
        var actual=heldBridge.Apply(delivery,"direct-late",r.E.Tick,false);Check(actual.Result.Accepted,"legacy direct reference bypass remains explicit");
        Check(r.E.Attribution.CapturePublication(r.C("host"),f.Hit,actual).Accepted,"late genuine attribution");
        Check(lane.Offer(r.C("host")).Length==1,"routing captures direct bypass into actual durable journal");
        Check(r.D.AcknowledgeComplete().Reason=="producer-undelivered","late actual prefix blocks completion");
        Check(r.D.DrainProducer().Accepted&&r.D.DrainProducer().Accepted,"Results drain and retry acknowledge late event");
        Check(Score(r.D,"b")==2&&r.S.Get("a").Deaths==1&&r.S.Get("c").Deaths==1&&heldHealth.Read("c").Deaths==1,"actual Match Session Health totals agree");
        Check(lane.Offer(r.C("host")).Length==0&&(rollover?r.D.BeginLoading():r.D.AcknowledgeComplete()).Reason=="root-producer-quiescence-required","ACK complete but global retirement still blocked");
        Console.WriteLine("LIVE/COLD="+cold+" rollover="+rollover+" stage="+r.D.Stage+" epoch="+r.S.Epoch+" score="+Score(r.D,"b")+" sessionDeaths=1/1 healthDeaths=1/1 journal=0 global=BLOCKED");
    }
    static int Cold(string[] args)
    {
        var e=new Environment{Now=1};var f=new Facts();var c=new CommandContext("test","host","cold-process",e.Tick,1,true);
        HealthService h;Ashfall.Integration.CombatHealthBridge b;Ashfall.Integration.CombatPublication.HealthPublicationLane l;
        Check(Ashfall.Integration.CombatPublication.HealthPublicationLane.TryRestoreCold(c,new string('a',64),args[2],long.Parse(args[3]),new Ashfall.Modules.Persistence.AtomicSettingsStore(args[1]),0,true,e,e,e,f,out h,out b,out l,60,1,0,65,long.Parse(args[4])),"subprocess restore");
        Check(l.ProducerAdmissionClosed&&l.Damage(c,"cold-late","c",h.Read("c").Revision,100,0,false).Reason=="producer-admission-closed"&&l.Offer(c).Length==0,"subprocess closed and ACKed");
        Console.WriteLine("COLD SUBPROCESS: persisted routed admission closed; ACK preserved; global coverage OPEN");return 0;
    }
    static void Reservation()
    {
        var r=new Rig("respawn",0,0,1,1,8);r.Bind=false;r.Join("a",false,null);r.Load("a");
        var f=new Facts();var bridge=new Ashfall.Integration.CombatHealthBridge(r.H,r.E,f,r.E,r.E,"test",1);
        var lane=new Ashfall.Integration.CombatPublication.HealthPublicationLane(r.H,bridge,new Ashfall.Modules.Persistence.AtomicSettingsStore(PathFor("reservation")),"test",1,"host",65,new string('a',64));
        var state=r.H.Read("a");HealthService.PreparedGain gain;
        Check(r.H.PrepareGain(r.C("a"),"held-token","a",state.Revision,state.LifeGeneration,state.ConnectionGeneration,1,0,out gain)&&gain.Validate(),"retain actual validated owner token");
        Ashfall.Integration.CombatPublication.HealthPublicationLane.ProducerAdmissionClose close;
        Check(lane.CloseProducerAdmission(r.C("host"),2,new string('a',64),out close).Reason=="producer-close-reservation"&&close==null,"no durable closure receipt while owner token outstanding");
        gain.CommitValidated();gain.CommitValidated();gain.Abort();
        Check(lane.CloseProducerAdmission(r.C("host"),2,new string('a',64),out close).Accepted&&close!=null,"retry closure after actual token disposition");
        Check(!lane.CheckGlobalRetirement(r.C("host"),r.H.CheckpointSequence).Accepted,"never certify untracked legacy token inventory");
    }
    static void FinalCuts(bool afterScore)
    {
        string path=PathFor(afterScore?"final-ack-cut":"final-io-cut");
        var store=afterScore?new Ashfall.Modules.Persistence.AtomicSettingsStore(path,new FailSecondWrite()):new Ashfall.Modules.Persistence.AtomicSettingsStore(path);
        Check(store.Load(false).Accepted,"physical load");var journal=new ScoreReceiptJournal(store);
        var r=new Rig("respawn",0,0,1,1,8,journal,true);r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");var e=Kill(r);
        r.E.DenyScore=false;Check(r.D.RetainCommittedScore(e).Accepted,"retain before cut");
        if(afterScore)Check(!r.D.Deliver(e).Accepted&&Score(r.D,"b")==1&&r.S.Get("a").Deaths==1,"post score/projection physical ACK cut");
        else using(new Ashfall.Modules.Persistence.PhysicalSettingsFiles().AcquireFamily(path))
            Check(!r.D.Deliver(e).Accepted&&Score(r.D,"b")==0&&r.S.Get("a").Deaths==0,"pre score physical IO cut");
        r.E.Now=1;Check(r.D.Heartbeat().Accepted,"Results");
        for(int i=0;i<3;i++){Check(r.D.AcknowledgeComplete().Reason=="pending-score","repeated final guard");Check(r.D.BeginLoading().Reason=="pending-score","repeated load guard");}
        Check(r.D.Deliver(e).Accepted&&r.D.Deliver(e).Accepted,"same event settles");Check(Score(r.D,"b")==1&&r.S.Get("a").Deaths==1,"score/death once");
        var reopen=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(reopen.Load(false).Accepted&&new ScoreReceiptJournal(reopen).Pending("test",1).Length==0,"durable ACK before completion");
        Check(r.D.AcknowledgeComplete().Reason=="root-producer-quiescence-required","settled prefix still requires retirement");Check(!r.D.AcknowledgeComplete().Accepted&&!r.D.BeginLoading().Accepted&&r.S.Epoch==1,"repeat cannot advance epoch");
        r.E.Now=.5;Check(r.D.Heartbeat().Reason=="server-clock","regressed clock rejects");r.E.Now=2;Check(r.D.Heartbeat().Accepted&&r.S.Epoch==1,"monotonic clock recovers");
    }
    static void OrphanJournal(bool final)
    {
        string path=PathFor("orphan");var store=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(store.Load(false).Accepted,"orphan physical load");var j=new ScoreReceiptJournal(store);
        var r=new Rig("respawn",0,0,1,final?1:2,8,j);r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");var e=Kill(r);
        Check(j.Retain(e,"b",1,-1).Accepted,"actual committed loss journaled outside Director");r.E.Now=1;Check(r.D.Heartbeat().Accepted,"orphan Results");
        Check(r.D.AcknowledgeComplete().Reason=="pending-score","journal-only final guard");
        if(final){Check(r.D.Deliver(e).Accepted&&Score(r.D,"b")==1&&r.D.AcknowledgeComplete().Reason=="root-producer-quiescence-required","journal-only final recovers without false retirement");return;}
        var load=r.D.BeginLoading();var retry=r.D.Deliver(e);
        Console.WriteLine("ORPHAN load="+load.Accepted+" epoch="+r.S.Epoch+" retry="+retry.Accepted+":"+retry.Reason+" oldPending="+j.Pending("test",1).Length);
        Check(!load.Accepted&&load.Reason=="pending-score"&&r.S.Epoch==1&&retry.Accepted&&j.Pending("test",1).Length==0,"old epoch retained and genuine event ACKed");Check(Score(r.D,"b")==1&&r.S.Get("a").Deaths==1&&r.D.BeginLoading().Reason=="root-producer-quiescence-required"&&r.S.Epoch==1,"credited exactly once; rollover waits for real retirement");
    }
    static void UnseenSecond()
    {
        var r=new Rig("respawn",0,0,1,1,8);r.Join("a",false,null);r.Join("b",false,null);r.Join("c",false,null);r.Load("a","b","c");var first=Kill(r);
        var hit=r.C("b");Check(r.H.ApplyLoss(hit,"c",r.H.Read("c").Revision,100,0,false).Accepted,"second real death");Check(r.E.Attribution.Capture(hit,hit.CommandId,"c").Accepted,"second attribution");var second=r.H.DrainTransitions()[0];
        Check(r.D.RetainCommittedScore(first).Accepted,"first retained");Check(r.D.RetainCommittedScore(second).Reason=="pending-score","single outstanding event refuses second");
        Check(r.D.Deliver(first).Accepted,"first settles");r.E.Now=1;Check(r.D.Heartbeat().Accepted,"two deaths Results");var ack=r.D.AcknowledgeComplete();var retry=r.D.Deliver(second);
        Console.WriteLine("UNSEEN ack="+ack.Accepted+" stage="+r.D.Stage+" second="+retry.Accepted+":"+retry.Reason+" score="+Score(r.D,"b"));
        Check(!ack.Accepted&&ack.Reason=="producer-undelivered"&&retry.Accepted&&Score(r.D,"b")==2&&r.S.Get("c").Deaths==1,"unseen prefix blocks completion then second loss reconciles");Check(r.D.AcknowledgeComplete().Reason=="root-producer-quiescence-required","prefix settled without retirement");
    }
    static void EligibilityRemap()
    {
        var r=new Rig("respawn",2,0,1,1,8);r.Join("a",false,null);string token=r.Join("b",false,null);r.Load("a","b");var e=Kill(r);Check(r.D.RetainCommittedScore(e).Accepted,"retain original team/life/killer");
        Check(r.S.Disconnect(r.C("b"),false).Accepted,"real killer disconnect");r.Join("new-b",false,token);Check(r.S.Loaded(r.C("new-b"),"maphash").Accepted,"real remap loaded");Check(r.D.BindConnection("new-b").Accepted,"bind canonical killer");
        r.E.Now=1;Check(r.D.Heartbeat().Accepted,"remap Results");Check(r.D.Deliver(e).Accepted&&r.D.Deliver(e).Accepted&&Score(r.D,"team:1")==1&&r.S.Get("a").Deaths==1,"original killer team/life retained once across remap");
    }
    static void SessionCAS()
    {
        var r=new Rig("respawn",0,0,1,2,8);r.Join("a",false,null);r.Load("a");r.E.Now=1;Check(r.D.Heartbeat().Accepted,"CAS Results");
        long n=r.S.LifecycleSequence+1;var c=new CommandContext("test","host","sessions-lifecycle/"+n,n,r.S.Epoch,true);Session.PreparedMatchRollover a,b;
        Check(r.S.PrepareMatchRollover(c,6,out a).Accepted&&r.S.Epoch==1,"CAS prepare pure");Check(r.S.PrepareMatchRollover(c,6,out b).Accepted,"second candidate prepare");
        Check(r.S.ValidateMatchRollover(a).Accepted&&r.S.ValidateMatchRollover(b).Accepted,"both validate frozen old state");r.S.CommitMatchRolloverValidated(a);
        Check(!r.S.ValidateMatchRollover(b).Accepted&&r.S.Epoch==2,"actual epoch publication invalidates other token");r.S.AbortMatchRollover(b);r.S.AbortMatchRollover(b);r.S.CommitMatchRolloverValidated(a);r.S.AbortMatchRollover(a);
        Check(r.S.Epoch==2&&r.S.Stage==SessionStage.Loading,"committed token retry and abort preserve epoch");
    }
    
    sealed class CutFiles : Ashfall.Modules.Persistence.IAtomicSettingsFiles
    {
        readonly Ashfall.Modules.Persistence.PhysicalSettingsFiles actual=new Ashfall.Modules.Persistence.PhysicalSettingsFiles();
        public bool Cut;public bool After;
        public bool Exists(string p){return actual.Exists(p);}public IDisposable AcquireFamily(string p){return actual.AcquireFamily(p);}public byte[] Read(string p,int max){return actual.Read(p,max);}
        public void CreateDurable(string p,byte[] bytes){actual.CreateDurable(p,bytes);}
        public void Commit(string p,string d,string b,bool replacing){if(Cut&&!After)throw new System.IO.IOException("before atomic replace");actual.Commit(p,d,b,replacing);if(Cut)throw new System.IO.IOException("after atomic replace");}
    }
    static void PhysicalCut(bool after)
    {
        string path=PathFor("close-cut");var r=new Rig("respawn",0,0,1,1,8);r.Bind=false;r.Join("a",false,null);r.Load("a");
        var f=new Facts();var b=new Ashfall.Integration.CombatHealthBridge(r.H,r.E,f,r.E,r.E,"test",1);var files=new CutFiles{After=after};
        var lane=new Ashfall.Integration.CombatPublication.HealthPublicationLane(r.H,b,new Ashfall.Modules.Persistence.AtomicSettingsStore(path,files),"test",1,"host",65,new string('a',64));
        var prior=lane.SuccessfulCheckpoint;files.Cut=true;
        Ashfall.Integration.CombatPublication.HealthPublicationLane.ProducerAdmissionClose close;
        Check(!lane.CloseProducerAdmission(r.C("host"),2,new string('a',64),out close).Accepted&&close==null&&lane.ProducerAdmissionClosed&&lane.RecoveryRequired,"failed atomic closure exposes no receipt and freezes live lane");
        Check(!lane.Damage(r.C("host"),"after-failure","a",r.H.Read("a").Revision,100,0,false).Accepted,"failed close never resumes live writes");
        var physical=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(physical.Load(false).Accepted,"actual physical checkpoint remains valid");string digest;Check(physical.TryRead("combat-checkpoint-sha/2",out digest),"read physical test receipt");
        HealthService h;Ashfall.Integration.CombatHealthBridge restored;Ashfall.Integration.CombatPublication.HealthPublicationLane cold;
        // Synthetic trusted receipt for this physical test only. Production root must provide
        // authenticated freshness; reading a digest from the image is not authentication.
        Check(Ashfall.Integration.CombatPublication.HealthPublicationLane.TryRestoreCold(r.C("host"),new string('a',64),digest,prior.HealthSequence,physical,0,true,r.E,r.E,r.E,f,out h,out restored,out cold,60,1,0,65),"cut reopens entire committed image");
        Check(cold.ProducerAdmissionClosed==after,"atomic image is wholly before or wholly after tombstone");
        Check(cold.CloseProducerAdmission(r.C("host"),2,new string('a',64),out close).Accepted,"cold retry establishes persisted closure");
        Check(cold.CloseProducerAdmission(r.C("host"),2,new string('a',64),out close).Accepted&&cold.Offer(r.C("host")).Length==0,"cold closure retry idempotent and no fabricated transition");
        Check(!cold.CheckGlobalRetirement(r.C("host"),h.CheckpointSequence).Accepted,"atomic cut never grants global authority");
        Console.WriteLine("PHYSICAL CUT after="+after+" cold closed="+cold.ProducerAdmissionClosed+" sequence="+h.CheckpointSequence+" global=BLOCKED");
    }

    static void ResultsPressure()
    {
        var r=new Rig("respawn",0,0,1,1,1);string[] ids=new string[17];ids[0]="b";
        for(int i=1;i<17;i++)ids[i]="v"+i;
        foreach(string id in ids)r.Join(id,false,null);r.Load(ids);
        var events=new HealthTransition[16];
        for(int i=0;i<16;i++){var hit=r.C("b");string target=ids[i+1];Check(r.H.ApplyLoss(hit,target,r.H.Read(target).Revision,100,0,false).Accepted,"pressure actual death");Check(r.E.Attribution.Capture(hit,hit.CommandId,target).Accepted,"pressure attribution");events[i]=r.H.DrainTransitions()[0];}
        r.E.Now=1;Check(r.D.Heartbeat().Accepted,"pressure Results");
        for(int i=0;i<16;i++)
        {
            Check(r.D.AcknowledgeComplete().Reason=="producer-undelivered","pending prefix blocks under Results pressure");
            Check(r.D.Deliver(events[i]).Accepted&&r.D.Deliver(events[i]).Accepted,"late Results retry exactly once");
            Check(Score(r.D,"b")==i+1&&r.S.Get(ids[i+1]).Deaths==1,"actual pressure totals");
        }
        Check(r.D.AcknowledgeComplete().Reason=="root-producer-quiescence-required"&&Score(r.D,"b")==16,"all Results deaths settled before completion");
    }


    static int Main(string[] args){try{if(args.Length>0)return Cold(args);FinalCuts(false);FinalCuts(true);OrphanJournal(false);OrphanJournal(true);UnseenSecond();EligibilityRemap();SessionCAS();CloseCase(false,false);CloseCase(true,false);CloseCase(false,true);Reservation();ResultsPressure();PhysicalCut(false);PhysicalCut(true);Console.WriteLine("PASS "+assertions+" R9 assertions");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}

