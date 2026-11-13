using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Core;

namespace Ashfall.Modules.Modes
{
    // net_session1000._die_r13/_check_end/_try_start. No health, spawn, transport or scene ownership.
    public sealed class RoundController
    {
        public const string ApiVersion = "Modes/2";
        private ModeRules rules;
        public ModeRules Rules { get { return rules; } }
        private CommandLedger lifecycle;
        private readonly IModeRoster roster;
        private readonly string director;
        private CommandLedger ledger;
        private readonly string session;
        private readonly IAuthorityGate gate;
        private readonly int journalCapacity;
        public long Epoch { get; private set; }
        private readonly Dictionary<string,int> totals=new Dictionary<string,int>(StringComparer.Ordinal);
        private readonly Dictionary<string,int> personal=new Dictionary<string,int>(StringComparer.Ordinal);
        private readonly Dictionary<string,long> deaths=new Dictionary<string,long>(StringComparer.Ordinal);
        private double started, lastTime;
        private long clockTickFloor=-1;
        public RoundStage Stage { get; private set; }
        public int RoundIndex { get; private set; }
        public string EndReason { get; private set; }
        public RoundController(ModeRules rules,IModeRoster roster,string director,string session,long epoch,IAuthorityGate gate,int journalCapacity)
        {
            if(rules==null||roster==null||String.IsNullOrEmpty(director)) throw new ArgumentException("rules/roster/director");
            this.rules=rules; this.roster=roster; this.director=director;
            this.session=session; this.gate=gate; this.journalCapacity=journalCapacity; Epoch=epoch;
            ledger=new CommandLedger(session,epoch,gate,journalCapacity);
            lifecycle=new CommandLedger(session,epoch,gate,2); Stage=RoundStage.Loading; EndReason="";
        }
        private RuleResult Check(CommandContext c,double now,bool control)
        {
            if(c.ActorId!=director) return RuleResult.Reject("director_only");
            if(Double.IsNaN(now)||Double.IsInfinity(now)||now<lastTime) return RuleResult.Reject("time");
            if(c.ActorId!=null && (control?ledger:lifecycle).Contains(c)) return RuleResult.Reject("replay");
            return (control?lifecycle:ledger).Check(c);
        }
        public RuleResult StartRound(CommandContext c,double now)
        {
            RuleResult r=Check(c,now,true); if(!r.Accepted) return r;
            if(Stage!=RoundStage.Loading||RoundIndex>=rules.Rounds) return RuleResult.Reject("stage");
            lifecycle.Commit(c); totals.Clear(); personal.Clear(); deaths.Clear();
            RoundIndex++; started=lastTime=now; Stage=RoundStage.Match; EndReason=""; return RuleResult.Accept();
        }
        public RuleResult RecordKnockdown(CommandContext c,Knockdown death,double now)
        {
            RuleResult r=Check(c,now,false); if(!r.Accepted) return r;
            if(Stage!=RoundStage.Match) return RuleResult.Reject("stage");
            ModeActor victim,killer; long previous;
            if(death.VictimId==null||death.LifeSequence<0||!roster.TryGet(death.VictimId,out victim)||victim.Spectator) return RuleResult.Reject("victim");
            if(deaths.TryGetValue(death.VictimId,out previous)&&death.LifeSequence<=previous) return RuleResult.Reject("duplicate_life");
            ledger.Commit(c); lastTime=now; deaths[death.VictimId]=death.LifeSequence;
            if(death.KillerId!=null && roster.TryGet(death.KillerId,out killer) && !killer.Spectator && !Allied(killer,victim,rules.Teams))
            { Increment(personal,killer.Id); Increment(totals,Key(killer)); }
            CheckEnd(now); return RuleResult.Accept();
        }
        // Trusted simulation clock: coordinator calls this directly, never routes client payloads here.
        // Clock evaluation has no command receipt and is idempotent at the same time.
        public RuleResult AdvanceClock(double now)
        {
            if(Double.IsNaN(now)||Double.IsInfinity(now)||now<lastTime) return RuleResult.Reject("time");
            if(Stage!=RoundStage.Match) return RuleResult.Reject("stage");
            lastTime=now; CheckEnd(now); return RuleResult.Accept();
        }
        // Compatibility adapter authenticates the director without admitting a gameplay command.
        public RuleResult Tick(CommandContext c,double now)
        {
            if(c.ActorId!=director||!c.IsAuthority||c.SessionId!=session||c.AuthorityEpoch!=Epoch||c.Tick<0||String.IsNullOrEmpty(c.CommandId)) return RuleResult.Reject("authority_or_epoch");
            RuleResult r=gate.Validate(c); if(!r.Accepted) return r;
            if(c.Tick<=clockTickFloor) return RuleResult.Reject("stale_clock_tick");
            r=AdvanceClock(now); if(r.Accepted) clockTickFloor=c.Tick; return r;
        }
        public RuleResult UpdateLimits(CommandContext c,LiveLimits limits,double now)
        {
            RuleResult r=Check(c,now,false); if(!r.Accepted) return r;
            if(Stage!=RoundStage.Match) return RuleResult.Reject("stage");
            ledger.Commit(c); rules=rules.WithLimits(limits.ScoreLimit,limits.TimeLimit);
            lastTime=now; CheckEnd(now); return RuleResult.Accept();
        }
        // Explicit results acknowledgement avoids the Quest early-results -> lobby branch
        // preempting configured later rounds. Coordinator performs world loading separately.
        public RuleResult AdvanceResults(CommandContext c,double now,long nextEpoch)
        {
            RuleResult r=Check(c,now,true); if(!r.Accepted) return r;
            if(Stage!=RoundStage.Results) return RuleResult.Reject("stage");
            if(RoundIndex<rules.Rounds && nextEpoch<=Epoch) return RuleResult.Reject("next_epoch");
            lifecycle.Commit(c); lastTime=now;
            Stage=RoundIndex<rules.Rounds ? RoundStage.Loading : RoundStage.Complete;
            if(Stage==RoundStage.Loading) { Epoch=nextEpoch; clockTickFloor=-1; ledger=new CommandLedger(session,Epoch,gate,journalCapacity); lifecycle=new CommandLedger(session,Epoch,gate,2); }
            return RuleResult.Accept();
        }
        private void CheckEnd(double now)
        {
            foreach(int score in totals.Values) if(rules.ScoreLimit>0&&score>=rules.ScoreLimit) { Finish("score_limit"); return; }
            if(rules.Mode=="revive")
            {
                HashSet<string> all=new HashSet<string>(),alive=new HashSet<string>();
                foreach(ModeActor a in roster.Snapshot()) if(!a.Spectator) { all.Add(Key(a)); if(a.Life=="alive") alive.Add(Key(a)); }
                if(all.Count>1&&alive.Count<=1) { Finish("last_side"); return; }
            }
            if(rules.TimeLimit>0 && now-started>=rules.TimeLimit) Finish("time_limit");
        }
        private void Finish(string reason) { Stage=RoundStage.Results; EndReason=reason; }
        private static void Increment(Dictionary<string,int> scores,string key) { int n; scores.TryGetValue(key,out n); scores[key]=n+1; }
        private string Key(ModeActor actor) { return rules.Teams>0 ? "team:"+actor.Team.ToString(CultureInfo.InvariantCulture) : actor.Id; }
        public static bool Allied(ModeActor a,ModeActor b,int teams) { return a.Id==b.Id || (teams>0&&a.Team==b.Team); }
        public int PersonalScore(string id) { int score; return personal.TryGetValue(id,out score) ? score : 0; }
        public ScoreRow[] Scores()
        {
            Dictionary<string,string> names=new Dictionary<string,string>(StringComparer.Ordinal);
            for(int t=0;t<rules.Teams;t++) names["team:"+t.ToString(CultureInfo.InvariantCulture)]="TEAM "+(t+1).ToString(CultureInfo.InvariantCulture);
            foreach(ModeActor actor in roster.Snapshot()) if(!actor.Spectator)
                names[Key(actor)]=rules.Teams>0?"TEAM "+(actor.Team+1).ToString(CultureInfo.InvariantCulture):actor.Name??actor.Id;
            List<ScoreRow> rows=new List<ScoreRow>();
            foreach(var pair in names) { int score; totals.TryGetValue(pair.Key,out score); rows.Add(new ScoreRow(pair.Key,pair.Value,score)); }
            rows.Sort(delegate(ScoreRow a,ScoreRow b) { int n=b.Score.CompareTo(a.Score); if(n!=0) return n; if(rules.Teams==0) { n=StringComparer.OrdinalIgnoreCase.Compare(a.Name,b.Name); if(n!=0) return n; } return StringComparer.Ordinal.Compare(a.Id,b.Id); });
            return rows.ToArray();
        }
        // Historical scorer totals are deliberately separate from current roster presentation.
        public ScoreRow[] CumulativeScores()
        {
            List<ScoreRow> rows=new List<ScoreRow>(); foreach(var pair in totals) rows.Add(new ScoreRow(pair.Key,pair.Value));
            rows.Sort(delegate(ScoreRow a,ScoreRow b) { return StringComparer.Ordinal.Compare(a.Id,b.Id); }); return rows.ToArray();
        }
        public double SecondsRemaining(double now)
        { if(Double.IsNaN(now)||Double.IsInfinity(now)) throw new ArgumentException("now"); return rules.TimeLimit==0 ? Double.PositiveInfinity : Math.Max(0,rules.TimeLimit-Math.Max(0,now-started)); }
    }
}
