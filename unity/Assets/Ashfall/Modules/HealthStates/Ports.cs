using Ashfall.Core;

namespace Ashfall.Modules.HealthStates
{
    // These are trusted authority-side ports, never client-supplied validation flags.
    public interface IHealthWorld
    {
        double Now { get; }
        bool InMatch { get; }
        bool TeamChangesAllowed { get; }
        bool TryGetPose(string playerId, out RescuePose pose);
    }
    public struct RescuePose
    {
        public bool Tracked, Paused, PaddlesActive, Desktop, Facing;
        public Float3 Left, Right, Chest, LocalLeft, LocalRight;
    }
    // Combat evaluates armor/shield split, friendly fire, weapon and zone multipliers.
    // Revision binds its evaluated losses to exactly the health state it inspected.
    public interface IHealthMutationPort
    {
        RuleResult ApplyLoss(CommandContext context, string target, long revision,
            float healthLoss, float shieldLoss, bool revivable);
    }
    public interface ITransactionalHealthPort
    {
        DamageReceipt ApplyDamage(CommandContext context,string logicalId,string target,long revision,float healthLoss,float shieldLoss,bool revivable);
        DamageReceipt FindDamage(CommandContext context,string logicalId,string target);
        HealthTransition[] DrainTransitions();
    }
    public sealed class DamageReceipt
    {
        public readonly string LogicalId,TargetId;
        public readonly RuleResult Result;
        public readonly bool Retryable;
        public readonly HealthSnapshot Snapshot;
        internal DamageReceipt(string id,string target,RuleResult result,bool retryable,HealthSnapshot snapshot)
        {LogicalId=id;TargetId=target;Result=result;Retryable=retryable;Snapshot=snapshot;}
    }
    public sealed class HealthTransition
    {
        public readonly string SessionId,Kind;
        public readonly long AuthorityEpoch,Sequence,BeforeRevision;
        public readonly HealthSnapshot After;
        internal HealthTransition(string session,long epoch,long sequence,string kind,long before,HealthSnapshot after)
        {SessionId=session;AuthorityEpoch=epoch;Sequence=sequence;Kind=kind;BeforeRevision=before;After=after;}
    }
    public enum LifeState { Alive, Downed, Dead, Spectator }
    public sealed class HealthSnapshot
    {
        public readonly string Id;
        public readonly int Team, Deaths;
        public readonly long Revision;
        public readonly long LifeGeneration, ConnectionGeneration;
        public readonly LifeState Life;
        public readonly float Health, Shield;
        public readonly double DownedUntil, RespawnAt, ProtectionUntil, ReadyUntil;
        public readonly bool Connected;
        public readonly float Charge;
        internal HealthSnapshot(string id, int team, int deaths, long revision, LifeState life,
            float health, float shield, double downed, double respawn, double protection,
            double ready, bool connected, float charge, long lifeGeneration, long connectionGeneration)
        { Id=id; Team=team; Deaths=deaths; Revision=revision; Life=life; Health=health; Shield=shield;
          DownedUntil=downed; RespawnAt=respawn; ProtectionUntil=protection; ReadyUntil=ready; Connected=connected; Charge=charge; LifeGeneration=lifeGeneration; ConnectionGeneration=connectionGeneration; }
    }
}
