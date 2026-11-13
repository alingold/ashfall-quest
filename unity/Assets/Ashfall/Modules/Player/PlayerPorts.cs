using Ashfall.Core;

namespace Ashfall.Modules.Player
{
    // Immutable settings capability. Replacement through Locomotion.Settings
    // invalidates prepared motion; implementations must be pure on the host lane.
    public interface IPlayerSprintSettingsPort : IPlayerSettingsPort
    {
        float StaminaSeconds { get; }
        float RecoverySeconds { get; }
    }
    // Owned integration ports. The coordinator supplies authority and the exclusive
    // motion owner; this module does not validate network commands or author damage.
    public interface IPlayerMotionPort
    {
        bool Grounded { get; }
        bool CeilingBlocked { get; }
        void Move(Float3 displacement);
    }
    // Query only: must not mutate root. Coordinator implements actual collision,
    // head sweep, height/support and external-owner arbitration. Missing XR port fails closed.
    public interface IRootCollisionPreflight
    {
        RuleResult Validate(RootMotionProposal proposal);
    }
    public struct RootMotionProposal
    {
        public Float3 RootPosition, HeadPosition, Translation, PivotCorrection;
        public float YawRadians;
        public bool OwnsTranslation;
    }
    public enum TurnStyle { Smooth, Snap, AutoSnap }
    [System.Serializable]
    public sealed class PlayerTuning
    {
        // measurement_calibration.gd FACTOR is essential: source comments predate it.
        public const float WalkSpeed = 6.5f * .6096f * (48f / 41.5f);
        public float WalkMultiplier = 2.5f;
        public readonly float MaximumWalkMultiplier;
        public PlayerTuning() : this(5f, 2.5f) { }
        private PlayerTuning(float maximum, float initial)
        { MaximumWalkMultiplier = maximum; WalkMultiplier = initial; }
        public float SnapRadians = (float)(System.Math.PI / 6);
        public float SmoothRadiansPerSecond = (float)(System.Math.PI * 2);
        public bool SmoothAnalog = true, SmoothEaseIn;
        public TurnStyle Turning = TurnStyle.Smooth;
        public static PlayerTuning SkinRuntime()
        { return new PlayerTuning(3f, 1f); }
    }
    public struct MotionEnvironment
    {
        public bool Grounded, CeilingBlocked, IsXr, ExternalMotionOwner, SprintBlocked, TurningBlocked;
        // PRE-turn world heading in Unity radians. Step adds its yaw exactly once.
        public float HeadingRadians;
        public float DesktopYawRadians;
    }
    public struct MotionStep
    {
        public Float3 Velocity, Displacement;
        public float YawRadians;
        public bool InteractionsAllowed, Jumped, OwnsMotion, OwnsYaw;
    }
    public struct HandSample
    {
        public bool Tracked, Primary, Secondary, Click, Menu;
        public float Grip, Trigger, StickX, StickY;
        public Float3 Position;
    }
    public struct DeviceSample
    {
        public bool IsXr, Focused, HeadTracked, Paused, Jump, Sprint, Reload, Ping;
        public Float3 HeadPosition;
        public HandSample Left, Right;
        // Desktop values, or pre-normalized XR bindings (positive Y is forward).
        public float MoveX, MoveY, TurnX, TurnY;
    }
}
