using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.MatchDirector;
namespace Ashfall.Integration.LifecycleTransactions
{
    // Privileged host adapter. Historical event payload wakes current projection;
    // scoring still consumes the immutable historical event in Director itself.
    public sealed class MatchDirectorProjection : ISessionHealthProjection
    {
        private readonly Coordinator coordinator;private readonly Session session;private readonly HealthService health;
        private readonly Func<CommandContext> context;private readonly Func<double> expiry;
        public MatchDirectorProjection(Coordinator c,Session s,HealthService h,Func<CommandContext> hostContext,Func<double> expires)
        { if(c==null||s==null||h==null||hostContext==null||expires==null)throw new ArgumentNullException();coordinator=c;session=s;health=h;context=hostContext;expiry=expires; }
        public RuleResult Project(string id,long epoch,string connection,HealthSnapshot state)
        {
            return coordinator.Serialize(delegate {
                MemberSnapshot m=session.Get(connection);HealthSnapshot current=m==null?null:health.Read(m.CanonicalActorId);
                if(id!=session.Id||epoch!=session.Epoch||state==null||current==null||state.Id!=current.Id||state.Team!=current.Team||state.Revision>current.Revision||state.LifeGeneration>current.LifeGeneration||state.ConnectionGeneration>current.ConnectionGeneration)return RuleResult.Reject("projection-identity");
                return coordinator.ProjectCurrent(context(),connection,expiry())?RuleResult.Accept():RuleResult.Reject("projection-not-ready");
            });
        }
    }
}
