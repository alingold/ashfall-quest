using System;
namespace Ashfall.Modules.Player
{
    // Mouse deltas are pixels/frame, never multiplied by dt. Unity yaw reverses
    // Godot's sign under Z reflection. Pitch is a central presentation request.
    public sealed class DesktopLook
    {
        private bool captured;
        private float pitch;
        public void InitializeCapture() { captured=true; }
        public void Release() { captured = false; }
        public DesktopLookStep Read(DesktopLookSample s)
        {
            bool before = captured;
            float yaw = 0;
            if (!s.Focused || s.Paused || !s.Enabled || s.EscapePressed ||
                !PlayerInput.Finite(s.DeltaX) || !PlayerInput.Finite(s.DeltaY)) captured = false;
            else if (!captured) { if (s.CapturePressed) captured = true; }
            else if (s.GameplayReady || (s.InitializationLook && s.InputRequested))
            {
                // Bounded event avoids platform overflow and unchecked multi-turn sweeps.
                yaw = (float)Math.Max(-Math.PI, Math.Min(Math.PI, (double)s.DeltaX * .002));
                pitch = (float)Math.Max(-1.45, Math.Min(1.45, (double)pitch - (double)s.DeltaY * .002));
            }
            return new DesktopLookStep { Captured = captured, CaptureChanged = before != captured,
                YawRadians = yaw, PitchRadians = pitch };
        }
    }
    public static class HandActionAdapter
    {
        // Raw physical samples; never merges weapon actions into movement's hand.
        public static DeviceSample Bind(DeviceSample s, HandActionOwnership ownership)
        {
            if (ownership == null || !Enum.IsDefined(typeof(DominantHand), ownership.Dominant) ||
                !Enum.IsDefined(typeof(SprintBinding), ownership.Sprint)) throw new ArgumentException("hand ownership");
            if (!s.IsXr) return s;
            s.MoveX = s.Left.StickX; s.MoveY = s.Left.StickY;
            s.TurnX = s.Right.StickX; s.TurnY = s.Right.StickY;
            HandSample support = ownership.Dominant == DominantHand.Left ? s.Right : s.Left;
            HandSample weapon = ownership.Dominant == DominantHand.Left ? s.Left : s.Right;
            s.Jump = support.Primary;
            s.Sprint = ownership.Sprint == SprintBinding.LeftGrip ? s.Left.Grip > .65f : s.Left.Click;
            s.Reload = weapon.Secondary;
            return s;
        }
    }
    public static class PlayerRigSafety
    {
        // Numerical/hierarchy boundary only; this performs no geometry or XR proof.
        private static bool Upright(Ashfall.Core.Float3 up)
        { return up.IsFinite && up.Y > .99999f && Math.Abs(up.X) < .0045f && Math.Abs(up.Z) < .0045f &&
            Math.Abs((double)up.X * up.X + (double)up.Y * up.Y + (double)up.Z * up.Z - 1) < .00002; }
        public static bool Valid(RigMeasurements m)
        {
            return m.HierarchyValid && m.UnitScales && (!m.IsXr || m.ExclusivePoseWriter) &&
                PlayerInput.Finite(m.Radius) && m.Radius > 0 && PlayerInput.Finite(m.Height) && m.Height >= 2 * m.Radius &&
                PlayerInput.Finite(m.SkinWidth) && m.SkinWidth >= 0 && m.SkinWidth < m.Radius && m.Center.IsFinite &&
                m.CurrentHeadPosition.IsFinite && (!m.IsXr || (m.LeftHandPosition.IsFinite && m.RightHandPosition.IsFinite)) &&
                Upright(m.RootUp) && Upright(m.TrackingUp);
        }
    }
}
