using Ashfall.Core;
namespace Ashfall.Integration.CombatPublication
{
    // Historical proposal, still not implemented with these live-object signatures.
    // Actual cold-only factories are HealthService.TryRestoreCheckpoint and
    // HealthPublicationLane.TryRestoreCold. Retirement awaits coordinator quiescence.
    public interface IHealthCheckpointProposal
    {
        RuleResult RestoreSessionCheckpoint(CommandContext host,string checkpointSha256,byte[] checkpoint);
        RuleResult PrepareEpochRetirement(CommandContext host,long expectedLastSequence,long acknowledgedThrough,out byte[] checkpoint);
    }
}
