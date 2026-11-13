using System;

namespace Ashfall.Integration.Playable.Exclusion
{
    // Unity-free presentation policy for a held weapon whose head->muzzle segment
    // is blocked by the world (the same segment the firing rule already refuses).
    // It never touches the gameplay muzzle: the glue only offsets the visual model.
    public sealed class R25WeaponObstruction
    {
        // The visual muzzle stops this far in front of the blocking surface.
        public readonly float Margin = 0.03f;
        // Retractions longer than this would push the gun into the face: the
        // weapon is hidden ("ghosted") instead.
        public readonly float MaxRetract = 0.35f;
        // After the wall clears, the gun slides back out at MaxRetract per this
        // many seconds. Retracting INTO safety is always immediate.
        public readonly float ExtendSeconds = 0.12f;
        // Light haptic tick on the first blocked frame, at most once per interval.
        public readonly float HapticInterval = 0.4f;
        public readonly float HapticAmplitude = 0.12f;
        public readonly float HapticDuration = 0.02f;

        float retract;
        bool blocked, ghosted;
        float sinceHaptic = float.PositiveInfinity;

        public float Retract { get { return retract; } }
        public bool Blocked { get { return blocked; } }
        public bool Ghosted { get { return ghosted; } }

        public void Reset()
        {
            retract = 0;
            blocked = false;
            ghosted = false;
        }

        // segmentLength: head to visual muzzle. hitDistance: head to the first
        // blocking surface along that segment (ignored when !blockedNow).
        // haptic: true on the frame a new obstruction should tick the hand.
        public float Step(bool blockedNow, float segmentLength, float hitDistance, float seconds, out bool haptic)
        {
            haptic = false;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) seconds = 0;
            if (seconds > 0.25f) seconds = 0.25f;
            if (!Finite(segmentLength) || segmentLength < 0) segmentLength = 0;
            if (!Finite(hitDistance)) hitDistance = segmentLength;
            if (sinceHaptic < float.PositiveInfinity) sinceHaptic += seconds;
            float required = 0;
            if (blockedNow) required = Math.Max(0, segmentLength - Math.Max(0, hitDistance) + Margin);
            if (blockedNow && !blocked && sinceHaptic >= HapticInterval)
            {
                haptic = true;
                sinceHaptic = 0;
            }
            blocked = blockedNow;
            ghosted = blockedNow && required > MaxRetract;
            if (required >= retract) retract = required;
            else retract = Math.Max(required, retract - seconds * MaxRetract / ExtendSeconds);
            if (retract > MaxRetract) retract = MaxRetract; // Ghosted: the model is hidden anyway.
            return retract;
        }

        static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    // Hide latch for gloves/special weapons: hidden the moment they are blocked,
    // shown again only after HoldSeconds clear, so a hand resting on a wall plane
    // does not flicker.
    public sealed class R25ObstructionLatch
    {
        public readonly float HoldSeconds = 0.10f;
        bool hidden;
        float clearSeconds;

        public bool Hidden { get { return hidden; } }

        public void Reset()
        {
            hidden = false;
            clearSeconds = 0;
        }

        public bool Step(bool blockedNow, float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) seconds = 0;
            if (blockedNow)
            {
                hidden = true;
                clearSeconds = 0;
            }
            else if (hidden)
            {
                clearSeconds += seconds;
                if (clearSeconds >= HoldSeconds) hidden = false;
            }
            return hidden;
        }
    }
}
