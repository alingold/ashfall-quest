namespace Ashfall.Integration.AudioSettings
{
    public enum AudioPublicationState { Rejected, LivePending, Synchronized, Conflict }
    // Immutable receipt: Accepted means the entire snapshot reached the live boundary.
    // Committed never means rollback is possible. SnapshotId identifies exact raw values.
    public sealed class AudioPublicationReceipt
    {
        public readonly AudioPublicationState State;
        public readonly bool Committed;
        public readonly string SnapshotId, FailedBus, Reason;
        public bool Accepted { get { return State == AudioPublicationState.Synchronized; } }
        public bool LivePending { get { return State == AudioPublicationState.LivePending || State == AudioPublicationState.Conflict; } }
        internal AudioPublicationReceipt(AudioPublicationState state,bool committed,string id,string bus,string reason)
        { State=state;Committed=committed;SnapshotId=id;FailedBus=bus;Reason=reason; }
    }
    // Single owner/main thread. A pending snapshot blocks new edits and Load.
    // RetryPublication replays all five assignments idempotently, never commits.
    public interface IAudioSettingsBinding
    {
        Ashfall.Core.RuleResult Load();
        Ashfall.Core.RuleResult SaveBus(string bus,float linearGain);
        AudioPublicationReceipt LoadPublication();
        AudioPublicationReceipt PublishBus(string bus,float linearGain);
        AudioPublicationReceipt RetryPublication();
        AudioPublicationReceipt LastReceipt { get; }
    }
}
