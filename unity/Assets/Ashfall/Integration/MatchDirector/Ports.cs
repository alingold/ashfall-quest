using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.MatchDirector
{
    // Host-only composition ports. Neither implementation may read client time/life IDs.
    public interface IMatchClock { double Now { get; } long Tick { get; } }
    // Resolve from committed damage receipt/event revision, never latest attacker state.
    // Null means environmental/unattributed damage. Must be pure and non-reentrant.
    public interface IKnockdownAttribution { string KillerFor(HealthTransition transition); }
    public interface ILogicalPlayerIdentity
    { bool TryResolve(string session,long epoch,string connectionActor,out string canonicalActor); }
    // Requested shared adapter: must mutate real Session state, not a parallel life table.
    public interface ISessionHealthProjection
    { Ashfall.Core.RuleResult Project(string session,long epoch,string connectionActor,HealthSnapshot state); }
}
