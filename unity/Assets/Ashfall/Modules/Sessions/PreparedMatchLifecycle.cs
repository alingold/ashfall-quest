using System;
using System.Collections.Generic;
using System.Text;
using Ashfall.Core;
namespace Ashfall.Modules.Sessions
{
    public sealed partial class Session
    {
        // Exact value stamp supplements admissionRevision for other partial publishers.
        // Allocate only during prepare/validate; publication uses the prepared dictionaries.
        private string MatchStamp()
        {
            var b=new StringBuilder();
            foreach(Member p in players.Values) {
                b.Append(p.Id.Length).Append(':').Append(p.Id).Append('|').Append(p.CanonicalId).Append('|').Append(p.Name).Append('|').Append(p.Token).Append('|');
                b.Append(p.Team).Append('|').Append(p.Revision).Append('|').Append(p.Deaths).Append('|').Append(p.Loaded).Append('|').Append(p.Waiting).Append('|').Append(p.HasPosition).Append('|').Append(p.Spectator).Append('|').Append(p.Life).Append('|').Append(p.TeamLayout).Append('|');
                b.Append(p.RespawnAt.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(p.Position.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(p.Position.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(p.Position.Z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            }
            // All standard mutations also replace admissionRevision; projected-life-only changes
            // are covered by exact immutable projection references below.
            return b.ToString();
        }
        public sealed class PreparedMatchRollover
        {
            internal Session Owner;internal CommandContext Context;internal object Revision,NextRevision;
            internal SessionRules Rules;internal SessionStage Stage;internal long Epoch,Lifecycle,Host;
            internal double Time,LastTime,Expiry;internal string Stamp;
            internal Dictionary<string,Member> Players;internal Dictionary<string,Reservation> Reservations;
            internal LifeProjection[] Projections;internal int ProjectionCount;
            internal bool Validated,Aborted,Committed;
            internal PreparedMatchRollover(){}
        }
        public RuleResult PrepareMatchRollover(CommandContext c,double expiry,out PreparedMatchRollover p)
        {
            p=null;double now=clock.Now;
            if(!c.IsAuthority||c.SessionId!=Id||c.AuthorityEpoch!=Epoch||c.Tick<0||c.CommandId!="sessions-lifecycle/"+c.Tick.ToString(System.Globalization.CultureInfo.InvariantCulture)||c.Tick<=lifecycleTick)return RuleResult.Reject("lifecycle-context");
            RuleResult r=gate.Validate(c);if(!r.Accepted)return r;if(!management.CanManage(c))return RuleResult.Reject("manager-required");
            if((Stage!=SessionStage.Results&&Stage!=SessionStage.Lobby)||Epoch>=Int64.MaxValue-1||Double.IsNaN(now)||Double.IsInfinity(now)||now<lastTime||Double.IsNaN(expiry)||Double.IsInfinity(expiry)||expiry<=now||expiry-now>5)return RuleResult.Reject("rollover");
            var next=new Dictionary<string,Member>(StringComparer.Ordinal);
            foreach(var pair in players){Member old=pair.Value;var m=new Member {ConnectionIdentity=old.ConnectionIdentity,Id=old.Id,CanonicalId=old.CanonicalId,Name=old.Name,Token=old.Token,Team=old.Team,Spectator=old.Spectator,TeamLayout=old.TeamLayout};ResetRound(m);next.Add(pair.Key,m);}
            var saved=new Dictionary<string,Reservation>(StringComparer.Ordinal);foreach(var pair in reservations)if(pair.Value.Until>now)saved.Add(pair.Key,pair.Value);
            p=new PreparedMatchRollover {Owner=this,Context=c,Revision=admissionRevision,NextRevision=new object(),Rules=Rules,Stage=Stage,Epoch=Epoch,Lifecycle=lifecycleTick,Host=hostReceipt,Time=now,LastTime=lastTime,Expiry=expiry,Stamp=MatchStamp(),Players=next,Reservations=saved,Projections=(LifeProjection[])lifeProjections.Clone(),ProjectionCount=projectionCount};
            checkpointReservations.Add(p);return RuleResult.Accept();
        }
        public RuleResult ValidateMatchRollover(PreparedMatchRollover p)
        {
            if(p==null||p.Owner!=this||p.Aborted||p.Committed||p.Revision!=admissionRevision||p.Epoch!=Epoch||p.Stage!=Stage||!ReferenceEquals(p.Rules,Rules)||p.Lifecycle!=lifecycleTick||p.Host!=hostReceipt||p.LastTime!=lastTime||clock.Now!=p.Time||p.Time>=p.Expiry||p.Stamp!=MatchStamp()||p.ProjectionCount!=projectionCount)return RuleResult.Reject("stale-rollover");
            for(int i=0;i<projectionCount;i++)if(!ReferenceEquals(p.Projections[i],lifeProjections[i]))return RuleResult.Reject("stale-projection");
            RuleResult r=gate.Validate(p.Context);if(!r.Accepted)return r;if(!management.CanManage(p.Context))return RuleResult.Reject("manager-required");p.Validated=true;return RuleResult.Accept();
        }
        public void CommitMatchRolloverValidated(PreparedMatchRollover p)
        {
            if(p.Committed)return;
            if(p.Owner!=this||!p.Validated||p.Aborted)throw new InvalidOperationException("unvalidated rollover");
            players=p.Players;reservations=p.Reservations;Epoch=p.Epoch+1;Stage=SessionStage.Loading;worldReady=false;LoadingDeadline=p.Time+LoadingTimeoutSeconds;LastLoadingRecovery=String.Empty;loadingTarget=SessionStage.Match;
            commands.Clear();ticks.Clear();lastTime=p.Time;lifecycleTick=p.Context.Tick;admissionRevision=p.NextRevision;p.Committed=true;checkpointReservations.Remove(p);
        }
        public void AbortMatchRollover(PreparedMatchRollover p){if(p!=null&&p.Owner==this&&!p.Committed){p.Aborted=true;checkpointReservations.Remove(p);}}
    }
}
