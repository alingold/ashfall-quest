using System;
using Ashfall.Core;

namespace Ashfall.Modules.Player
{
    // Stateful safety boundary shared by desktop and Input System/OpenXR adapters.
    // Raw poses are tracking-local meters, never locomotion/collision authority.
    public sealed class PlayerInput
    {
        public readonly HandActionOwnership Ownership = new HandActionOwnership();
        private DominantHand previousDominant;
        private SprintBinding previousSprint;
        private bool ownershipInitialized;
        private bool modeInitialized, previousXr;
        private long sequence, lastTick = -1;
        private float leftNeutral, rightNeutral, desktopNeutral;
        private bool leftReady, rightReady, desktopReady;
        public bool DesktopReady { get { return desktopReady; } }
        public bool LeftReady { get { return leftReady; } }
        public bool RightReady { get { return rightReady; } }
        public void Disarm()
        {
            leftReady = rightReady = desktopReady = false;
            leftNeutral = rightNeutral = desktopNeutral = 0;
        }
        public InputFrame Read(long tick, float dt, DeviceSample sample)
        {
            if (tick <= lastTick) throw new ArgumentOutOfRangeException("tick", "Input ticks must increase");
            if (!Finite(dt) || dt <= 0 || dt > .1f) throw new ArgumentOutOfRangeException("dt");
            if (!Enum.IsDefined(typeof(DominantHand), Ownership.Dominant) || !Enum.IsDefined(typeof(SprintBinding), Ownership.Sprint))
                throw new ArgumentException("hand ownership");
            if (ownershipInitialized && (Ownership.Dominant != previousDominant || Ownership.Sprint != previousSprint)) Disarm();
            if (modeInitialized && previousXr != sample.IsXr) Disarm();
            previousXr=sample.IsXr;modeInitialized=true;
            previousDominant = Ownership.Dominant; previousSprint = Ownership.Sprint; ownershipInitialized = true;
            lastTick = tick;
            bool tracked = sample.Focused && (!sample.IsXr || (sample.HeadTracked && sample.HeadPosition.IsFinite));
            InputFrame frame = new InputFrame { Tick = tick, Sequence = ++sequence,
                Tracked = tracked, Paused = sample.Paused,
                HeadPosition = Safe(sample.HeadPosition), LeftHandPosition = Safe(sample.Left.Position),
                RightHandPosition = Safe(sample.Right.Position), Menu = sample.Left.Menu || sample.Right.Menu };
            if (!tracked || sample.Paused)
            {
                Disarm();
                return frame;
            }
            if (sample.IsXr)
            {
                HandSample left = sample.Left, right = sample.Right;
                bool leftWeapon = Ownership.Dominant == DominantHand.Left;
                if (leftWeapon) { right.Primary |= sample.Jump; left.Secondary |= sample.Reload || sample.Ping; }
                else { left.Primary |= sample.Jump; right.Secondary |= sample.Reload || sample.Ping; }
                left.Click |= sample.Sprint;
                if (Math.Abs(sample.MoveX) > Math.Abs(left.StickX)) left.StickX = sample.MoveX;
                if (Math.Abs(sample.MoveY) > Math.Abs(left.StickY)) left.StickY = sample.MoveY;
                if (Math.Abs(sample.TurnX) > Math.Abs(right.StickX)) right.StickX = sample.TurnX;
                if (Math.Abs(sample.TurnY) > Math.Abs(right.StickY)) right.StickY = sample.TurnY;
                Rearm(left, dt, ref leftReady, ref leftNeutral);
                Rearm(right, dt, ref rightReady, ref rightNeutral);
                if (leftReady)
                {
                    frame.MoveX = Axis(sample.MoveX); frame.MoveY = Axis(sample.MoveY);
                    frame.GripLeft = sample.Left.Grip > .65f;
                    frame.TriggerLeft = sample.Left.Trigger > .5f;
                    frame.Sprint = sample.Sprint;
                }
                if (rightReady)
                {
                    frame.TurnX = Axis(sample.TurnX); frame.TurnY = Axis(sample.TurnY);
                    frame.GripRight = sample.Right.Grip > .65f;
                    frame.TriggerRight = sample.Right.Trigger > .5f;
                    
                }
                frame.Jump = sample.Jump && (leftWeapon ? rightReady : leftReady);
                frame.Reload = sample.Reload && (leftWeapon ? leftReady : rightReady);
                frame.Ping = sample.Ping && (leftWeapon ? leftReady : rightReady);
            }
            else
            {
                if (!Finite(sample.Left.Grip) || !Finite(sample.Right.Grip) ||
                    !Finite(sample.Left.Trigger) || !Finite(sample.Right.Trigger))
                { Disarm(); return frame; }
                bool neutral = Math.Abs(Axis(sample.MoveX)) < .25f && Math.Abs(Axis(sample.MoveY)) < .25f &&
                    Math.Abs(Axis(sample.TurnX)) < .25f && !sample.Jump && !sample.Sprint && !sample.Reload && !sample.Ping &&
                    sample.Left.Grip < .2f && sample.Right.Grip < .2f && sample.Left.Trigger < .2f && sample.Right.Trigger < .2f;
                desktopNeutral = neutral ? desktopNeutral + dt : 0;
                if (desktopNeutral >= .08f) desktopReady = true;
                if (desktopReady)
                {
                    frame.MoveX = Axis(sample.MoveX); frame.MoveY = Axis(sample.MoveY);
                    frame.TurnX = Axis(sample.TurnX); frame.TurnY = Axis(sample.TurnY);
                    frame.Jump = sample.Jump; frame.Sprint = sample.Sprint;
                    frame.Reload = sample.Reload; frame.Ping = sample.Ping;
                    frame.GripLeft = sample.Left.Grip > .65f; frame.GripRight = sample.Right.Grip > .65f;
                    frame.TriggerLeft = sample.Left.Trigger > .5f; frame.TriggerRight = sample.Right.Trigger > .5f;
                }
            }
            return frame;
        }
        private static void Rearm(HandSample hand, float dt, ref bool ready, ref float time)
        {
            if (!hand.Tracked || !hand.Position.IsFinite || !Finite(hand.Grip) || !Finite(hand.Trigger) || !Finite(hand.StickX) || !Finite(hand.StickY))
            { ready = false; time = 0; return; }
            if (ready) return;
            bool neutral = hand.Grip < .2f && hand.Trigger < .2f &&
                hand.StickX * hand.StickX + hand.StickY * hand.StickY < .0625f &&
                !hand.Primary && !hand.Secondary && !hand.Click && !hand.Menu;
            time = neutral ? time + dt : 0;
            ready = time >= .08f;
        }
        private static Float3 Safe(Float3 v) { return v.IsFinite ? v : new Float3(); }
        private static float Axis(float v) { return Finite(v) ? Math.Max(-1, Math.Min(1, v)) : 0; }
        internal static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
