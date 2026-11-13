#if !UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
public static class DesktopPawnNeutralChecks
{
    static int checks;
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
    sealed class Speed : IPlayerSettingsPort
    {
        public ForwardReference ForwardReference { get { return ForwardReference.Head; } }
        public float GroundSpeedGain(float gain) { return gain * (9.5f / 6.5f); }
    }
    public static int Main()
    {
        var input = new PlayerInput();
        DeviceSample sample = new DeviceSample { Focused = true };
        for (int i = 1; i <= 4; i++) input.Read(i, .02f, sample);
        Check(input.DesktopReady, "neutral arms real input");
        sample.MoveY = 1; sample.Jump = true;
        var frame = input.Read(5, .02f, sample);
        Check(frame.MoveY == 1 && frame.Jump && frame.Tracked, "armed desktop input");
        sample.Focused = false; frame = input.Read(6, .02f, sample);
        Check(!frame.Tracked && !input.DesktopReady && !frame.Jump, "focus disarms");
        sample.Focused = true;
        for (int i = 7; i < 20; i++) frame = input.Read(i, .02f, sample);
        Check(!input.DesktopReady && frame.MoveY == 0, "held movement cannot rearm");
        sample = new DeviceSample { Focused = true };
        for (int i = 20; i < 25; i++) input.Read(i, .02f, sample);
        Check(input.DesktopReady, "released controls rearm");
        var look = new DesktopLook(); look.InitializeCapture();
        var rotation = look.Read(new DesktopLookSample { Focused = true, Enabled = true, GameplayReady = true, DeltaX = 100, DeltaY = 100 });
        Check(Math.Abs(rotation.YawRadians - .2f) < .0001f && rotation.PitchRadians < 0, "source mouse sensitivity and pitch");
        rotation = look.Read(new DesktopLookSample { Focused = false, Enabled = true });
        Check(!rotation.Captured && rotation.CaptureChanged, "look focus loss");
        var motor = new Locomotion(PlayerTuning.SkinRuntime());
        var world = new MotionEnvironment { Grounded = true };
        var step = motor.Step(new InputFrame { Tick = 1, Sequence = 1, Tracked = true, MoveY = 1, Jump = true }, world, .02f);
        Check(step.Jumped && step.Velocity.Y == 6, "real desktop jump");
        float walk = step.Velocity.Z;
        motor.Settings = new Speed();
        step = motor.Step(new InputFrame { Tick = 2, Sequence = 2, Tracked = true, MoveY = 1 }, world, .02f);
        Check(step.Velocity.Z > walk * 1.4f, "existing settings port desktop speed gain");
        world.CeilingBlocked = true; world.Grounded = false;
        step = motor.Step(new InputFrame { Tick = 3, Sequence = 3, Tracked = true }, world, .02f);
        Check(step.Velocity.Y <= 0, "ceiling clips upward motor state");
        var feedback = PlayerMotorFeedback.Resolve(new RootMotionProposal { Translation = new Float3(0, .1f, .2f), OwnsTranslation = true }, new Float3(0, .02f, 0), .02f, false, true);
        Check(feedback.Velocity.Y == 0 && feedback.Velocity.Z == 0 && !feedback.Complete, "actual collision feedback");
        motor.SetVelocity(feedback.Velocity); Check(motor.Velocity.Y == 0 && motor.Velocity.Z == 0, "feedback accepted by actual motor");
        motor.CancelMotion(); Check(motor.Velocity.X == 0 && motor.Velocity.Y == 0 && motor.Velocity.Z == 0, "pause cancellation");
        motor.ResetMotion(); Check(motor.SprintStamina == motor.StaminaMaximum, "explicit synthetic reset");
        Console.WriteLine("PASS " + checks + " neutral assertions; no native collision or runtime claim"); return 0;
    }
}
#endif
