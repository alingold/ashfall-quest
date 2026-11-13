using System;
using Ashfall.Core;
using Ashfall.Modules.Player;

public static class PlayerTests
{
    private static int checks;
    private static long tick;
    private static InputFrame Frame(float x, float y)
    { tick++; return new InputFrame { Tick = tick, Sequence = tick, Tracked = true, MoveX = x, MoveY = y }; }
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); checks++; }
    private static void Near(float actual, float expected, float tolerance, string name)
    { Check(Math.Abs(actual - expected) <= tolerance, name + " actual=" + actual + " expected=" + expected); }
    public static int Main()
    {
        try { Movement(); Turning(); Sprint(); InputSafety(); Ownership(); Console.WriteLine("PASS " + checks + " behavioral checks"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Movement()
    {
        // Independent source dimensional calculation, not outdated source comments.
        float speed = (float)(6.5 * .6096 * 48 / 41.5);
        var tuning = PlayerTuning.SkinRuntime();
        var sim = new Locomotion(tuning);
        var ground = new MotionEnvironment { Grounded = true };
        MotionStep step = sim.Step(Frame(1, 1), ground, .02f);
        Near((float)Math.Sqrt(step.Velocity.X * step.Velocity.X + step.Velocity.Z * step.Velocity.Z), speed, .0001f, "diagonal speed capped");
        step = sim.Step(Frame(0, 0), ground, .02f);
        Near(step.Velocity.X, 0, 0, "ground stops immediately");
        step = sim.Step(Frame(.14f, 0), ground, .02f);
        Near(step.Velocity.X, 0, 0, "movement deadzone");
        ground.HeadingRadians = (float)Math.PI / 2;
        step = sim.Step(Frame(0, 1), ground, .02f);
        Near(step.Velocity.X, speed, .0001f, "head-relative forward");
        Near(step.Velocity.Z, 0, .0001f, "reflected Unity forward");
        var jump = Frame(0, 0); jump.Jump = true;
        step = sim.Step(jump, ground, .02f);
        Near(step.Velocity.Y, 6, 0, "source jump impulse"); Check(step.Jumped, "grounded jump");
        step = sim.Step(Frame(0, 0), new MotionEnvironment(), .1f);
        Near(step.Velocity.Y, 4.38f, .0001f, "air gravity");
        jump = Frame(0, 0); jump.Jump = true;
        Check(!sim.Step(jump, new MotionEnvironment(), .02f).Jumped, "no air jump");
        Check(!sim.Step(jump = Next(jump), ground, .02f).Jumped, "held jump cannot bunny hop");
        sim.Step(Frame(0, 0), ground, .02f);
        ground.CeilingBlocked = true;
        jump = Frame(0, 0); jump.Jump = true;
        Check(!sim.Step(jump, ground, .02f).Jumped, "ceiling blocks jump");
        sim.SetVelocity(new Float3(4, -10, 0));
        step = sim.Step(Frame(0, 0), new MotionEnvironment(), .1f);
        Near(step.Velocity.X, 3.92f, .0001f, "free fall drag");
        sim.SetVelocity(new Float3());
        step = sim.Step(Frame(0, 1), new MotionEnvironment(), .1f);
        Near(step.Velocity.Z, .6f, .0001f, "air steering acceleration bound");
        float distance = 0;
        sim = new Locomotion(tuning);
        ground = new MotionEnvironment { Grounded = true };
        for (int i = 0; i < 50; i++) distance += sim.Step(Frame(0, 1), ground, .02f).Displacement.Z;
        Near(distance, speed, .0001f, "one second walking distance");
        sim = new Locomotion(new PlayerTuning());
        Near(sim.Step(Frame(0, 1), ground, .02f).Velocity.Z, speed * 2.5f, .0001f, "R22 tuning retained");
        var stale = Frame(0, 0); sim.Step(stale, ground, .02f);
        bool threw = false; try { sim.Step(stale, ground, .02f); } catch (ArgumentException) { threw = true; }
        Check(threw, "duplicate simulation rejected");
        tuning.WalkMultiplier = 99;
        Near(new Locomotion(tuning).Step(Frame(0, 1), ground, .02f).Velocity.Z, speed * 3, .0001f, "skin multiplier migration clamp");
        threw = false;
        try { sim.Step(Frame(0, 0), ground, .5f); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "long frame rejected before movement");
    }
    private static InputFrame Next(InputFrame input) { tick++; input.Tick = input.Sequence = tick; return input; }
    private static void Turning()
    {
        var tuning = PlayerTuning.SkinRuntime(); tuning.Turning = TurnStyle.Snap;
        var sim = new Locomotion(tuning); var world = new MotionEnvironment { Grounded = true };
        var f = Frame(0, 0); f.TurnX = 1;
        Near(sim.Step(f, world, .02f).YawRadians, (float)Math.PI / 6, .0001f, "right snap reflected");
        for (int i = 0; i < 20; i++) Near(sim.Step(Next(f), world, .02f).YawRadians, 0, 0, "held snap only once");
        sim.Step(Frame(0, 0), world, .02f);
        Check(sim.Step(Next(f), world, .02f).YawRadians > 0, "neutral rearms snap");
        tuning.Turning = TurnStyle.AutoSnap;
        sim = new Locomotion(tuning);
        int snaps = 0; for (int i = 0; i < 8; i++) if (sim.Step(Next(f), world, .1f).YawRadians > 0) snaps++;
        Check(snaps == 3, "auto snap repeat cadence");
        tuning.Turning = TurnStyle.Smooth;
        sim = new Locomotion(tuning); f.TurnX = .65f;
        Near(sim.Step(Next(f), world, .1f).YawRadians, (float)Math.PI * .1f, .0001f, "smooth analog half rate");
        f.TurnX = .4f; f.TurnY = -1;
        Near(sim.Step(Next(f), world, .1f).YawRadians, 0, 0, "manual eject reserves turn");
        tuning.SmoothEaseIn = true; f.TurnX = 1; f.TurnY = 0;
        sim = new Locomotion(tuning);
        float eased = sim.Step(Next(f), world, .02f).YawRadians;
        Check(eased > 0 && eased < (float)Math.PI * 2 * .02f, "smooth ease starts gradually");
        Near(sim.Step(Frame(0, 0), world, .02f).YawRadians, 0, 0, "neutral stops without ease out");
    }
    private static void Sprint()
    {
        var sim = new Locomotion(PlayerTuning.SkinRuntime());
        var world = new MotionEnvironment { IsXr = true, Grounded = true };
        var f = Frame(0, 1); f.Sprint = true;
        float run = sim.Step(f, world, .1f).Velocity.Z;
        Near(run, PlayerTuning.WalkSpeed * 7.5f / 6.5f, .0001f, "first click run");
        sim.Step(Frame(0, 1), world, .1f);
        f = Next(f); float fast = sim.Step(f, world, .1f).Velocity.Z;
        Check(fast > run && sim.SprintTier == 2, "second click fast");
        for (int i = 0; i < 65; i++) sim.Step(Next(f), world, .1f);
        Check(sim.SprintTier == 0, "stamina exhausts without held-click restart");
        sim.Step(Frame(0, 0), world, .1f);
        for (int i = 0; i < 40; i++) sim.Step(Frame(0, 0), world, .1f);
        Near(sim.SprintStamina, 6, 0, "stamina recovers");
        sim.Step(Next(f), world, .1f);
        var stop = Next(f); stop.MoveY = 0; sim.Step(stop, world, .1f);
        sim.Step(Next(f), world, .1f);
        Check(sim.SprintTier == 0, "stop cancels tier and retains click latch");
        sim.Step(Frame(0, 1), world, .02f); sim.Step(Next(f), world, .02f);
        world.SprintBlocked = true;
        Near(sim.Step(Next(f), world, .02f).Velocity.Z, PlayerTuning.WalkSpeed, .0001f, "arbitration suspends speed");
        Check(sim.SprintTier == 1, "arbitration preserves tier");
        world.SprintBlocked = false;
        Check(sim.Step(Next(f), world, .02f).Velocity.Z > PlayerTuning.WalkSpeed && sim.SprintTier == 1, "unblock held click does not advance tier");
        var desktop = new Locomotion(PlayerTuning.SkinRuntime()); world.IsXr = false;
        Near(desktop.Step(Next(f), world, .1f).Velocity.Z, PlayerTuning.WalkSpeed, .0001f, "source desktop has no XR sprint");
    }
    private static void InputSafety()
    {
        var input = new PlayerInput(); long t = 0;
        var sample = new DeviceSample { IsXr = true, Focused = true, HeadTracked = true,
            Left = new HandSample { Tracked = true }, Right = new HandSample { Tracked = true } };
        for (int i = 0; i < 4; i++) input.Read(++t, .02f, sample);
        sample.MoveY = 1; sample.Left.StickY = 1; sample.Left.Trigger = 1;
        Check(input.Read(++t, .02f, sample).TriggerLeft, "armed hand action");
        sample.HeadTracked = false;
        var frame = input.Read(++t, .02f, sample);
        Check(!frame.Tracked && !frame.TriggerLeft && frame.MoveY == 0, "tracking loss disarms");
        sample.HeadTracked = true;
        for (int i = 0; i < 10; i++) Check(!input.Read(++t, .02f, sample).TriggerLeft, "held recovery stays disarmed");
        sample.MoveY = 0; sample.Left.StickY = 0; sample.Left.Trigger = 0;
        for (int i = 0; i < 3; i++) input.Read(++t, .02f, sample);
        Check(!input.LeftReady, "60 ms neutral insufficient");
        input.Read(++t, .02f, sample); Check(input.LeftReady, "80 ms neutral rearms");
        sample.Paused = true; sample.HeadPosition = new Float3(.2f, 1.7f, .1f);
        frame = input.Read(++t, .02f, sample);
        Check(frame.Tracked && frame.Paused && frame.HeadPosition.Y == 1.7f && !input.LeftReady, "pause preserves tracking but disarms");
        sample.Paused = false;
        for (int i = 0; i < 4; i++) input.Read(++t, .02f, sample);
        sample.Left.Tracked = false; sample.Left.Trigger = 1; sample.Right.Trigger = 1;
        frame = input.Read(++t, .02f, sample);
        Check(!frame.TriggerLeft && frame.TriggerRight, "independent hand loss");
        sample.Focused = false; Check(!input.Read(++t, .02f, sample).Tracked, "focus loss disarms");
        var bindings = new PlayerInput();
        var bound = new DeviceSample { IsXr = true, Focused = true, HeadTracked = true, Jump = true,
            Left = new HandSample { Tracked = true }, Right = new HandSample { Tracked = true } };
        for (int i = 0; i < 5; i++) Check(!bindings.Read(++t, .02f, bound).Jump, "bound held action blocks neutral without physical primary flag");
        var desktop = new PlayerInput(); sample = new DeviceSample { Focused = true, Jump = true, MoveY = 1 };
        for (int i = 0; i < 6; i++) Check(!desktop.Read(++t, .02f, sample).Jump, "desktop held startup blocked");
        sample.Jump = false; sample.MoveY = 0;
        for (int i = 0; i < 4; i++) desktop.Read(++t, .02f, sample);
        sample.MoveY = float.NaN; frame = desktop.Read(++t, .02f, sample);
        Check(frame.MoveY == 0, "invalid axes sanitized");
        desktop.Disarm(); sample.MoveY = 1;
        Check(desktop.Read(++t, .02f, sample).MoveY == 0, "lifecycle disarm requires neutral");
    }
    private static void Ownership()
    {
        var sim = new Locomotion(PlayerTuning.SkinRuntime());
        var f = Frame(0, 1); f.Paused = true;
        var result = sim.Step(f, new MotionEnvironment { Grounded = true }, .02f);
        Check(!result.OwnsMotion && !result.InteractionsAllowed && result.Displacement.Z == 0, "pause no root motion");
        result = sim.Step(Frame(0, 1), new MotionEnvironment { ExternalMotionOwner = true }, .02f);
        Check(!result.OwnsMotion, "external flight/climb owner excludes walk");
        sim.SetVelocity(new Float3(3, 8, 0));
        result = sim.Step(Frame(0, 0), new MotionEnvironment(), .1f);
        Near(result.Velocity.Y, 6.38f, .0001f, "handoff momentum preserved");
        f = Frame(0, 1); f.Tracked = false;
        result = sim.Step(f, new MotionEnvironment(), .02f);
        Near(sim.Velocity.Y, 0, 0, "tracking loss clears momentum");
        Check(!result.OwnsMotion && !result.InteractionsAllowed, "tracking loss denies motion and interaction");
        sim.SetVelocity(new Float3(1, 2, 3)); sim.ResetMotion();
        Check(sim.Velocity.X == 0 && sim.Velocity.Y == 0 && sim.Velocity.Z == 0, "lifecycle reset clears momentum");
    }
}
