using Ashfall.Core;
namespace Ashfall.Modules.Player
{
    // Central desktop presentation writer implements this; Player never owns a camera.
    public interface IDesktopViewPort { void SetPitchRadians(float pitch); }
    public enum DominantHand { Right, Left }
    public enum SprintBinding { LeftClick, LeftGrip }
    // Physical movement/sprint remain LEFT and turn RIGHT in both source profiles.
    // Dominance changes support jump and weapon actions, not locomotion stick roles.
    public sealed class HandActionOwnership
    {
        public DominantHand Dominant = DominantHand.Right;
        public SprintBinding Sprint = SprintBinding.LeftClick;
    }
    public struct DesktopLookSample
    {
        public bool Focused, Paused, Enabled, CapturePressed, EscapePressed, GameplayReady;
        public bool InitializationLook, InputRequested;
        public float DeltaX, DeltaY;
    }
    public struct DesktopLookStep
    {
        public bool Captured, CaptureChanged;
        public float YawRadians, PitchRadians;
    }
    public struct RigMeasurements
    {
        public float Radius, Height, SkinWidth;
        public Float3 Center, RootUp, TrackingUp;
        // Measured each frame, not cached standing calibration.
        public Float3 CurrentHeadPosition, LeftHandPosition, RightHandPosition;
        public bool HierarchyValid, UnitScales, ExclusivePoseWriter, IsXr;
    }
}
