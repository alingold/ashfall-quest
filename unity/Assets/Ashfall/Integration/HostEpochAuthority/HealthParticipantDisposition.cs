using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.HostEpochAuthority
{
    public sealed class HealthParticipantKey
    {
        internal HealthParticipantKey() {}
    }
    // Immutable observation of the actual registered owner. This is not global retirement,
    // durable producer adoption, or authority to construct a replacement epoch.
    public sealed class HealthParticipantDisposition
    {
        public readonly HealthParticipantKey Key;
        public readonly EpochBinding Binding;
        public readonly long RegistryRevision;
        public readonly HealthEpochAccounting Current, BeforeClose, AfterClose;
        internal HealthParticipantDisposition(HealthParticipantKey key,EpochBinding binding,long revision,
            HealthEpochAccounting current,HealthEpochAccounting before,HealthEpochAccounting after)
        {Key=key;Binding=binding;RegistryRevision=revision;Current=current;BeforeClose=before;AfterClose=after;}
    }
}
