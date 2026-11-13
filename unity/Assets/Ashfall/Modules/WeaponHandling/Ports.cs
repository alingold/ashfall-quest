using Ashfall.Core;

namespace Ashfall.Modules.WeaponHandling
{
    // Integration maps inventory reserve by stable weapon ID. Transfers must be atomic;
    // return zero on refusal, never throw or transfer more than requested.
    public interface IAmmoReserve
    {
        int Available { get; }
        int Take(int requested);
        int Refund(int rounds);
    }
    public enum HandlingAction { Sample, Fire, ReloadStep, Eject }
    public enum ReloadPhase { Ready, NeedsMagazine, HeldMagazine, NeedsRack, NeedsBolt, CylinderOpen, CylinderLoaded }
    public enum WeaponMechanism { ServiceRifle, PrecisionRifle, MagnumRevolver }
    // Server supplies these poses/flags from validated input and its un-kicked weapon
    // frame. Unity +Z forward, SI meters; not positions from the recoiling mesh.
    public struct HandlingInput
    {
        public long Sequence;
        public float DeltaSeconds;
        public bool Enabled, PrimaryTracked, SupportTracked, Paused, Trigger, Grip;
        public bool PrimaryClimbing, SupportReserved, HeadObstructed, Desktop;
        // Validated dominant hand; revolver reload points mirror X for left dominance.
        public bool LeftHanded;
        // Corrected grip in player locomotion frame, never the kicked mesh. +X is player RIGHT.
        public bool HasPrimaryPose;
        public Float3 PrimaryPoint, PrimaryForward, PrimaryUp;
        public Float3 SupportLocal;
        public float ForegripDistance;
    }
    public struct HandlingSnapshot
    {
        public ReloadPhase Phase;
        public int Loaded, Seated;
        public bool Chambered, SpentChamber, Supported, ClaimsSupport, TriggerReleaseRequired;
        public float BoltTravel, RecoilDistance, RecoilPitch, RecoilYaw;
        public long Shots;
        public bool ReloadPending;
        public float CooldownSeconds, IdleSeconds;
        public int RecoilPattern;
    }
}
