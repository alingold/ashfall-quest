using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using RoundController = Ashfall.Integration.MatchRounds.RoundController;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.MatchDirector
{
    // All methods execute on the trusted serialized server lane. Never expose this object to RPC dispatch.
    // The host owns transition fan-out: Deliver is fed once from its retained Health outbox.
    public sealed partial class Director : IModeRoster
    {
        private sealed class Life { public HealthSnapshot State; public long Sequence; }
        private readonly Dictionary<string,Life> lives=new Dictionary<string,Life>(StringComparer.Ordinal);
        private readonly HashSet<string> members=new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,string> connections=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly Dictionary<string,ModeActor> retainedActors=new Dictionary<string,ModeActor>(StringComparer.Ordinal);
        private readonly ILogicalPlayerIdentity identity;
        private ISessionHealthProjection projection;
        private ScoreReceiptJournal journal;
        private HealthProducerDrain producer;
        // Bind the actual epoch producer before accepting final completion or rollover.
        // Never replace a producer within an epoch, even when it appears empty.
        public RuleResult BindProducer(HealthService health,CommandContext host,string sourceBinding)
        {return BindProducer(health,host,sourceBinding,null);}
        public RuleResult BindProducer(HealthService health,CommandContext host,string sourceBinding,Ashfall.Integration.CombatPublication.HealthPublicationLane publication)
        {
            if(health==null||host.SessionId!=session.Id||host.AuthorityEpoch!=Epoch||!host.IsAuthority)return RuleResult.Reject("producer-scope");
            if(publication!=null&&!publication.IsBoundTo(health,session.Id,Epoch,sourceBinding))return RuleResult.Reject("producer-binding");
            if(producer!=null)return RuleResult.Reject("producer-already-bound");
            producer=new HealthProducerDrain(health,host,sourceBinding,publication);return RuleResult.Accept();
        }
        public RuleResult DrainProducer()
        {return producer==null?RuleResult.Reject("producer-drain-required"):producer.Drain(this);}
        private RuleResult Settled()
        {
            if(HasPendingScore())return RuleResult.Reject("pending-score");
            return producer==null?RuleResult.Reject("producer-drain-required"):producer.Check(session.Id,Epoch,eventSequence);
        }
        private bool HasPendingScore()
        {return pendingScoreEvent!=0||retainedScore!=null||retainedTransition!=null||(journal!=null&&journal.Pending(session.Id,Epoch).Length!=0);}
        private bool scoring;
        private RoundController.CommittedEvent retainedScore;
        private string retainedKiller;
        private int retainedTeam;
        private HealthTransition retainedTransition;
        private int limitAdmissions;
        // Pinned HealthService accepts at most 8192 damage receipts per epoch.
        private const int ScoreAdmissions=8192;
        private readonly Session session;
        private readonly IMatchClock clock;
        private readonly IAuthorityGate gate;
        private IKnockdownAttribution attribution;
        private readonly string principal;
        private readonly ModeRules initialRules;
        private readonly int capacity;
        private RoundController round;
        private PingService pings;
        private long command, eventSequence, scoredEvent, pendingScoreEvent;
        private double lastTime; private long lastTick=-1;
        public string Fault { get; private set; }
        public RoundStage Stage { get { return round==null?RoundStage.Lobby:round.Stage; } }
        public long Epoch { get { return session.Epoch; } }
        public int RoundIndex { get { return round==null?0:round.RoundIndex; } }
        public string EndReason { get { return round==null?"":round.EndReason; } }
        public RuleResult IntegrationReadiness()
        {if(identity==null)return RuleResult.Reject("identity-port-required");if(projection==null)return RuleResult.Reject("session-projection-api-required");if(journal==null)return RuleResult.Reject("durable-receipts-required");if(producer==null)return RuleResult.Reject("producer-drain-required");return RuleResult.Accept();}
        public Director(Session session,ModeRules rules,string principal,IMatchClock clock,IAuthorityGate gate,IKnockdownAttribution attribution,int capacity)
            :this(session,rules,principal,clock,gate,attribution,capacity,null,null,null) { }
        public Director(Session session,ModeRules rules,string principal,IMatchClock clock,IAuthorityGate gate,IKnockdownAttribution attribution,int capacity,ILogicalPlayerIdentity identity,ISessionHealthProjection projection,ScoreReceiptJournal journal)
        {
            if(session==null||rules==null||String.IsNullOrEmpty(principal)||clock==null||gate==null||attribution==null||capacity<1)throw new ArgumentException("dependencies");
            if(session.Rules.Mode!=rules.Mode||session.Rules.Teams!=rules.Teams)throw new ArgumentException("rules mismatch");
            this.session=session;initialRules=rules;this.principal=principal;this.clock=clock;this.gate=gate;this.attribution=attribution;this.capacity=capacity;
            if(capacity>Int32.MaxValue-ScoreAdmissions)throw new ArgumentException("capacity");
            this.identity=identity;this.projection=projection;this.journal=journal;
            Fault="";ResetPings();
        }
        private RuleResult Time()
        {
            double now=clock.Now; long tick=clock.Tick;
            if(Double.IsNaN(now)||Double.IsInfinity(now)||now<0||now<lastTime||tick<0||tick<lastTick)return RuleResult.Reject("server-clock");
            lastTime=now;lastTick=tick;return RuleResult.Accept();
        }
        private RuleResult SessionControl(LifecycleAction action)
        {
            if(session.LifecycleSequence==Int64.MaxValue)return RuleResult.Reject("lifecycle-exhausted");
            long sequence=session.LifecycleSequence+1;
            var c=new CommandContext(session.Id,principal,"sessions-lifecycle/"+sequence.ToString(CultureInfo.InvariantCulture),sequence,session.Epoch,true);
            return session.Lifecycle(c,action,session.Rules,session.Rules.MapHash,true);
        }
        private CommandContext Context(string actor,long epoch)
        { if(command==Int64.MaxValue)throw new InvalidOperationException("director command exhausted");return new CommandContext(session.Id,actor,"matchdirector:"+(++command).ToString(CultureInfo.InvariantCulture),lastTick,epoch,true); }
        private void ResetPings() { pings=new PingService(this,gate,session.Id,session.Epoch,session.Rules.MapId,session.Rules.Teams,0,capacity); }
        // Host calls after successful join/reconnect. Presence always comes from Session.Get.
        public RuleResult ObserveMember(string id)
        { if(id==null||session.Get(id)==null)return RuleResult.Reject("member");if(!members.Contains(id)&&members.Count>=256)return RuleResult.Reject("roster-capacity");members.Add(id);return RuleResult.Accept(); }
        public string CanonicalActor(string connection)
        {foreach(var p in connections)if(p.Value==connection)return p.Key;return connection;}
        private string Connection(string canonical)
        {string c;return connections.TryGetValue(canonical,out c)?c:canonical;}
        public RuleResult BindConnection(string connection)
        {
            string canonical;
            if(identity==null||!identity.TryResolve(session.Id,Epoch,connection,out canonical)||String.IsNullOrEmpty(canonical))return RuleResult.Reject("identity-proof-required");
            MemberSnapshot m=session.Get(connection);Life l;
            if(m==null||!lives.TryGetValue(canonical,out l)||m.Team!=l.State.Team)return RuleResult.Reject("identity-binding");
            if(connection!=canonical&&lives.ContainsKey(connection))return RuleResult.Reject("identity-health-conflict");
            string old=Connection(canonical);
            if(old!=connection&&session.Get(old)!=null)return RuleResult.Reject("identity-active");
            foreach(var p in connections)if(p.Key!=canonical&&p.Value==connection)return RuleResult.Reject("identity-conflict");
            connections[canonical]=connection;members.Remove(connection);members.Add(canonical);return RuleResult.Accept();
        }
        public bool TryGet(string id,out ModeActor actor)
        {
            id=CanonicalActor(id);
            if(scoring&&retainedActors.TryGetValue(id,out actor))return true;
            actor=default(ModeActor);MemberSnapshot m=session.Get(Connection(id));Life life;
            if(m==null||!members.Contains(id)||!lives.TryGetValue(id,out life)||!life.State.Connected)return false;
            bool spectator=m.Life==SessionLife.Spectator||m.WaitingRound||life.State.Life==LifeState.Spectator;
            actor=new ModeActor(id,m.Name,m.Team,life.State.Life.ToString().ToLowerInvariant(),m.Loaded,spectator);return true;
        }
        public ModeActor[] Snapshot()
        { var list=new List<ModeActor>();foreach(string id in members){ModeActor a;if(TryGet(id,out a))list.Add(a);}list.Sort(delegate(ModeActor a,ModeActor b){return StringComparer.Ordinal.Compare(a.Id,b.Id);});return list.ToArray(); }
        public long LifeSequence(string id) { Life life;return lives.TryGetValue(CanonicalActor(id),out life)?life.Sequence:0; }
        // Call on the host loss-publication lane, before membership/team can change.
        // Retains eligibility even if durable IO or score authorization subsequently fails.
        public RuleResult RetainCommittedScore(HealthTransition e)
        {
            if(retainedTransition!=null)return Object.ReferenceEquals(retainedTransition,e)?RuleResult.Accept():RuleResult.Reject("pending-score");
            Life life;
            if(e==null||e.After==null||round==null||e.SessionId!=session.Id||e.AuthorityEpoch!=Epoch||e.Sequence!=eventSequence+1||!lives.TryGetValue(e.After.Id,out life)||life.State.Revision!=e.BeforeRevision||life.State.Life!=LifeState.Alive)return RuleResult.Reject("event-scope");
            retainedKiller=CanonicalActor(attribution.KillerFor(e));
            ModeActor scorer;retainedTeam=retainedActors.TryGetValue(retainedKiller??"",out scorer)?scorer.Team:-1;
            RuleResult r;scoring=true;
            try {r=round.CaptureCommittedEvent(e,life.Sequence,retainedKiller,out retainedScore);}
            finally {scoring=false;}
            if(r.Accepted){retainedTransition=e;pendingScoreEvent=e.Sequence;}return r;
        }
        // Events cannot be constructed through Health's public API. A retained host outbox must
        // retry the same event on rejection and acknowledge only Accepted. No raw client payload.
        public RuleResult Deliver(HealthTransition e)
        {
            RuleResult r=Time();if(!r.Accepted)return r;
            if(e==null||e.SessionId!=session.Id||e.AuthorityEpoch!=session.Epoch||e.After==null)return RuleResult.Reject("event-scope");
            if(e.Sequence<=eventSequence)return RuleResult.Accept();
            if(e.Sequence!=eventSequence+1)return RuleResult.Reject("event-gap");

            HealthSnapshot next=e.After;Life life;
            bool known=lives.TryGetValue(next.Id,out life);
            if((!known&&(e.Kind!="registered"||e.BeforeRevision!=0))||(known&&life.State.Revision!=e.BeforeRevision))return RuleResult.Reject("revision-gap");
            if(!known&&lives.Count>=256)return RuleResult.Reject("life-capacity");
            MemberSnapshot bound=session.Get(Connection(next.Id));
            if(!known&&(bound==null||next.Team!=bound.Team||(next.Life==LifeState.Alive&&(bound.Life!=SessionLife.Alive||!bound.HasPosition))))return RuleResult.Reject("initial-spawn-not-bound");
            bool wasAlive=known&&life.State.Life==LifeState.Alive;
            bool becomesAlive=next.Life==LifeState.Alive&&(!known||!wasAlive);
            if(becomesAlive&&known&&e.Kind=="revive"&&projection!=null)
            {r=projection.Project(session.Id,Epoch,Connection(next.Id),next);if(!r.Accepted)return r;bound=session.Get(Connection(next.Id));}
            if(becomesAlive&&(bound==null||bound.Team!=next.Team||bound.Life!=SessionLife.Alive||!bound.HasPosition))return RuleResult.Reject("alive-session-binding");
            if(becomesAlive&&e.Kind!="registered"&&e.Kind!="spawn"&&e.Kind!="revive")return RuleResult.Reject("unbound-life");
            bool knockdown=known&&wasAlive&&(next.Life==LifeState.Downed||next.Life==LifeState.Dead)&&round!=null;
            if(e.Sequence!=scoredEvent&&knockdown)
            {
                if(retainedScore==null)
                {
                    r=RetainCommittedScore(e);
                    if(!r.Accepted)return r;
                }
                if(!Object.ReferenceEquals(retainedTransition,e))return RuleResult.Reject("retained-event-identity");
                pendingScoreEvent=e.Sequence;
                if(journal!=null){r=journal.Retain(e,retainedKiller,life.Sequence,retainedTeam);if(!r.Accepted)return r;}
                r=round.ReconcileCommittedEvent(Context(principal,round.Epoch),retainedScore,lastTime);
                if(!r.Accepted){Fault="score:"+r.Reason;return r;}
                scoredEvent=e.Sequence;
            }
            // Final death is mirrored through Sessions' existing public elimination API.
            // A downed actor stays Session.Alive: Sessions/2 has no down/revive representation.
            if(projection!=null)
            {r=projection.Project(session.Id,Epoch,Connection(next.Id),next);if(!r.Accepted)return r;}
            MemberSnapshot m=session.Get(Connection(next.Id));
            if(projection==null&&next.Life==LifeState.Dead&&m!=null&&m.Life==SessionLife.Alive&&session.Stage==SessionStage.Results)
            {r=new SessionHealthProjection(session,clock,principal).Project(session.Id,Epoch,Connection(next.Id),next);if(!r.Accepted)return r;}
            if(next.Life==LifeState.Dead&&m!=null&&m.Life==SessionLife.Alive&&session.Stage==SessionStage.Match)
            { r=session.Eliminate(Context(Connection(next.Id),session.Epoch));if(!r.Accepted){Fault="eliminate:"+r.Reason;return r;} }
            if(journal!=null&&scoredEvent==e.Sequence){r=journal.Acknowledge(e);if(!r.Accepted)return r;}
            if(!known){life=new Life();lives.Add(next.Id,life);}
            if(becomesAlive){if(life.Sequence==Int64.MaxValue)throw new InvalidOperationException("life exhausted");life.Sequence++;}
            life.State=next;
            ModeActor retained;
            if(TryGet(next.Id,out retained))retainedActors[next.Id]=retained;
            eventSequence=e.Sequence;pendingScoreEvent=0;retainedScore=null;retainedTransition=null;Fault="";return RuleResult.Accept();
        }
        public RuleResult BeginLoading()
        {return producer==null?BeginLoadingSerialized():producer.Serialize(BeginLoadingSerialized);}
        private RuleResult BeginLoadingSerialized()
        {
            RuleResult r=Time();if(!r.Accepted)return r;
            if(HasPendingScore())return RuleResult.Reject("pending-score");
            if(round!=null){r=Settled();if(!r.Accepted)return r;r=producer.Retirement(session.Id,Epoch,eventSequence);if(!r.Accepted)return r;}
            if(round!=null&&round.Stage!=RoundStage.Results)return RuleResult.Reject("stage");
            if(round!=null&&round.RoundIndex>=round.Rules.Rounds)return RuleResult.Reject("complete-use-acknowledge");
            if(session.LifecycleSequence==Int64.MaxValue||session.Epoch>=Int64.MaxValue-1)return RuleResult.Reject("lifecycle-exhausted");
            var c=new CommandContext(session.Id,principal,"sessions-lifecycle/"+(session.LifecycleSequence+1).ToString(CultureInfo.InvariantCulture),session.LifecycleSequence+1,session.Epoch,true);
            Session.PreparedMatchRollover preparedSession;
            r=session.PrepareMatchRollover(c,lastTime+5,out preparedSession);if(!r.Accepted)return r;
            RoundController.PreparedRollover preparedRound=null;
            RoundController nextRound=round;
            if(round==null)nextRound=new RoundController(initialRules,this,principal,session.Id,session.Epoch+1,gate,capacity+ScoreAdmissions);
            else {r=round.PrepareRollover(Context(principal,round.Epoch),lastTime,lastTime+5,session.Epoch+1,out preparedRound);if(!r.Accepted){session.AbortMatchRollover(preparedSession);return r;}}
            var nextPings=new PingService(this,gate,session.Id,session.Epoch+1,session.Rules.MapId,session.Rules.Teams,0,capacity);
            r=round==null?RuleResult.Accept():Settled();
            if(r.Accepted)r=session.ValidateMatchRollover(preparedSession);
            if(r.Accepted&&preparedRound!=null)r=round.ValidateRollover(preparedRound,lastTime);
            if(!r.Accepted){session.AbortMatchRollover(preparedSession);if(preparedRound!=null)round.AbortRollover(preparedRound);return r;}
            // Trusted frozen lane: no callbacks, IO or allocations from here to publication end.
            session.CommitMatchRolloverValidated(preparedSession);
            if(preparedRound!=null)round.CommitRolloverValidated(preparedRound);
            round=nextRound;pings=nextPings;producer=null;
            lives.Clear();retainedActors.Clear();limitAdmissions=0;eventSequence=0;scoredEvent=0;return RuleResult.Accept();
        }
        // Sessions owner performs WorldReady/Loaded/Activate first; no scene ownership here.
        public RuleResult StartLoadedRound()
        {
            RuleResult r=Time();if(!r.Accepted)return r;
            if(round==null||session.Stage!=SessionStage.Match||session.Epoch!=round.Epoch)return RuleResult.Reject("session-not-active");
            foreach(string id in members){MemberSnapshot m=session.Get(Connection(id));Life l;if(m!=null&&(!m.Loaded||!lives.TryGetValue(id,out l)))return RuleResult.Reject("health-not-bound");}
            return round.StartRound(Context(principal,round.Epoch),lastTime);
        }
        public RuleResult Heartbeat()
        {
            RuleResult r=Time();if(!r.Accepted)return r;
            if(round==null)return RuleResult.Accept();
            if(round.Stage==RoundStage.Match){r=round.AdvanceClock(lastTime);if(!r.Accepted)return r;}
            if(round.Stage==RoundStage.Results&&session.Stage==SessionStage.Match)return SessionControl(LifecycleAction.EnterResults);
            return RuleResult.Accept();
        }
        public RuleResult AcknowledgeComplete()
        {return producer==null?CompleteSerialized():producer.Serialize(CompleteSerialized);}
        private RuleResult CompleteSerialized()
        { RuleResult r=Time();if(!r.Accepted)return r;r=Settled();if(!r.Accepted)return r;if(round==null||round.RoundIndex!=round.Rules.Rounds)return RuleResult.Reject("stage");r=producer.Retirement(session.Id,Epoch,eventSequence);if(!r.Accepted)return r;return round.AdvanceResults(Context(principal,round.Epoch),lastTime,round.Epoch); }
        public RuleResult UpdateLimits(CommandContext authenticatedDirector,LiveLimits limits)
        { RuleResult r=Time();if(!r.Accepted)return r;if(round==null)return RuleResult.Reject("stage");if(limitAdmissions>=capacity)return RuleResult.Reject("journal_full");r=round.UpdateLimits(authenticatedDirector,limits,lastTime);if(r.Accepted)limitAdmissions++;return r; }
        public RuleResult Ping(CommandContext authenticatedActor,Float3 point,Float3 normal,int color,int catalog)
        { RuleResult r=Time();if(!r.Accepted)return r;RoundStage stage=round==null?(session.Stage==SessionStage.Lobby?RoundStage.Lobby:RoundStage.Loading):round.Stage;return pings.Place(authenticatedActor,stage,point,normal,color,catalog,lastTime); }
        public PingRecord[] VisiblePings(string viewer) { return pings.Visible(viewer,session.Rules.MapId,session.Epoch,clock.Now); }
        public ScoreRow[] Scores() { return round==null?new ScoreRow[0]:round.Scores(); }
    }
}






