using Ashfall.Core;
namespace Ashfall.Modules.Player
{
    public enum ForwardReference { Head, Hand }
    public interface IPlayerSettingsPort { ForwardReference ForwardReference { get; } float GroundSpeedGain(float sourceGain); }
    public struct SprintContacts { public bool Climbing, Mantling, HasGrab, LeftReserved, LeftGripReserved, ProviderBlocksSprint, LeftSurfaceContact; }
    public interface IPlayerInteractionPort { SprintContacts ReadContacts(); }
    public struct PlayerLifecycleState
    {
        public bool Enabled, Focused, Paused, RigValid, Xr, SessionFocused, Present;
        public string Session, Actor; public long Epoch;
    }
    // Actual convex representation and revision belong to the geometry owner. No box fallback.
    public interface IApprovedPlayerGeometry
    {
        long Revision { get; }
        bool Approved { get; }
        RuleResult HeadSweep(Float3 head, Float3 displacement);
        RuleResult RootSweep(Float3 root, Float3 displacement);
        RuleResult HeightSupportObstruction(Float3 root, Float3 head);
        RuleResult PivotClearance(RootMotionProposal proposal);
    }
    public struct PlayerMotorResult { public Float3 AppliedDisplacement, AppliedPhysicalTranslation, AppliedPivotCorrection, Velocity; public bool HasPhaseFeedback, Complete, Grounded, CeilingBlocked; }
    public interface IExclusivePlayerMotor
    { PlayerMotorResult Apply(RootMotionProposal proposal, float seconds); }
    public interface IXrPlayerLifecycle { event System.Action Invalidated; bool SessionFocused { get; } bool Present { get; } }
}
