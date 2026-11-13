using System;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.MatchRounds
{
    public sealed partial class RoundController
    {
        private readonly System.Collections.Generic.HashSet<object> checkpointReservations=new System.Collections.Generic.HashSet<object>();
        // Host-only capability, minted while the original loss transition is retained.
        // Caller serializes commit-time roster/attribution capture with damage publication.
        public sealed class CommittedEvent
        {
            internal readonly RoundController Owner;
            internal readonly ModeActor Victim, Killer;
            internal readonly bool HasKiller;
            internal readonly long Epoch, Life, Sequence, Revision, Generation, Connection;
            internal readonly int Round;
            internal CommittedEvent(RoundController owner,HealthTransition e,long life,ModeActor v,ModeActor k,bool has)
            { Owner=owner;Epoch=e.AuthorityEpoch;Round=owner.RoundIndex;Life=life;Sequence=e.Sequence;Revision=e.After.Revision;Generation=e.After.LifeGeneration;Connection=e.After.ConnectionGeneration;Victim=v;Killer=k;HasKiller=has; }
        }
        public RuleResult CaptureCommittedEvent(HealthTransition e,long life,string killer,out CommittedEvent receipt)
        {
            receipt=null;ModeActor v,k=default(ModeActor);
            if(e==null||e.After==null||e.SessionId!=session||e.AuthorityEpoch!=Epoch||life<1||
                (Stage!=RoundStage.Match&&Stage!=RoundStage.Results)||!roster.TryGet(e.After.Id,out v)||v.Spectator||
                (e.After.Life!=LifeState.Downed&&e.After.Life!=LifeState.Dead))return RuleResult.Reject("committed-event");
            bool has=killer!=null&&roster.TryGet(killer,out k)&&!k.Spectator;
            receipt=new CommittedEvent(this,e,life,v,k,has);return RuleResult.Accept();
        }
        // Writes the SAME totals/personal/deaths used by ordinary source scoring.
        public RuleResult ReconcileCommittedEvent(CommandContext c,CommittedEvent e,double now)
        {
            if(e==null||e.Owner!=this||e.Epoch!=Epoch||e.Round!=RoundIndex||
                (Stage!=RoundStage.Match&&Stage!=RoundStage.Results))return RuleResult.Reject("event-scope");
            RuleResult r=Check(c,now,false);if(!r.Accepted)return r;
            long previous;
            if(deaths.TryGetValue(e.Victim.Id,out previous)&&e.Life<=previous)return RuleResult.Reject("duplicate_life");
            bool credit=e.HasKiller&&!Allied(e.Killer,e.Victim,rules.Teams);
            int value;
            if(credit&&((personal.TryGetValue(e.Killer.Id,out value)&&value==Int32.MaxValue)||(totals.TryGetValue(Key(e.Killer),out value)&&value==Int32.MaxValue)))return RuleResult.Reject("score-overflow");
            ledger.Commit(c);mutation++;lastTime=now;deaths[e.Victim.Id]=e.Life;
            if(credit){Increment(personal,e.Killer.Id);Increment(totals,Key(e.Killer));}
            if(Stage==RoundStage.Match)CheckEnd(now);
            return RuleResult.Accept();
        }
        public sealed class PreparedRollover
        {
            internal readonly RoundController Owner;internal readonly CommandContext Context;
            internal readonly long Revision,OldEpoch,NextEpoch;internal readonly double Time,Expiry;
            internal readonly CommandLedger Ledger,Lifecycle;
            internal bool Validated,Aborted,Committed;
            internal PreparedRollover(RoundController o,CommandContext c,double now,double expiry,long next)
            {Owner=o;Context=c;Revision=o.mutation;OldEpoch=o.Epoch;NextEpoch=next;Time=now;Expiry=expiry;Ledger=new CommandLedger(o.session,next,o.gate,o.journalCapacity);Lifecycle=new CommandLedger(o.session,next,o.gate,2);}
        }
        public RuleResult PrepareRollover(CommandContext c,double now,double expiry,long nextEpoch,out PreparedRollover p)
        {
            p=null;RuleResult r=Check(c,now,true);if(!r.Accepted)return r;
            if(Stage!=RoundStage.Results||RoundIndex>=rules.Rounds||Epoch==Int64.MaxValue||nextEpoch!=Epoch+1||mutation==Int64.MaxValue||Double.IsNaN(expiry)||Double.IsInfinity(expiry)||expiry<=now||expiry-now>5)return RuleResult.Reject("rollover");
            p=new PreparedRollover(this,c,now,expiry,nextEpoch);checkpointReservations.Add(p);return RuleResult.Accept();
        }
        public RuleResult ValidateRollover(PreparedRollover p,double now)
        {
            if(p==null||p.Owner!=this||p.Aborted||p.Committed||p.Revision!=mutation||p.OldEpoch!=Epoch||Stage!=RoundStage.Results||now!=p.Time||now>=p.Expiry)return RuleResult.Reject("stale-rollover");
            RuleResult r=Check(p.Context,now,true);p.Validated=r.Accepted;return r;
        }
        // Privileged no-fail publication, immediately after validate-all on frozen host lane.
        public void CommitRolloverValidated(PreparedRollover p)
        {
            if(p.Committed)return;
            if(p.Owner!=this||!p.Validated||p.Aborted)throw new InvalidOperationException("unvalidated rollover");
            Epoch=p.NextEpoch;Stage=RoundStage.Loading;clockTickFloor=-1;lastTime=p.Time;ledger=p.Ledger;lifecycle=p.Lifecycle;mutation++;p.Committed=true;checkpointReservations.Remove(p);
        }
        public void AbortRollover(PreparedRollover p){if(p!=null&&p.Owner==this&&!p.Committed){p.Aborted=true;checkpointReservations.Remove(p);}}
    }
}
