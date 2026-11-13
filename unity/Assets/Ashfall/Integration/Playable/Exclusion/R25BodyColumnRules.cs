using System;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.Exclusion
{
    // Unity-free geometry/slide/step policy for the walking body column: a capsule
    // under the head, from just above a stair step up to just below the eye.
    // It is used ONLY for thumbstick walk/run displacement while supported; the
    // glue performs the physics sweep and feeds hit distances/normals in here.
    // All vectors are world space with +Y up.
    public sealed class R25BodyColumnRules
    {
        // Capsule radius (m). Wider than the 0.12 m head dome so chest and hips meet
        // a railing before the head is over it; still fits any normal doorway.
        public readonly float Radius = 0.17f;
        // Anything at or below this height above the feet can be walked over/onto,
        // like a stair step or kerb. Taller obstacles block walking.
        public readonly float StepHeight = 0.45f;
        // The capsule top stays this far below the eye (the head dome's flat base).
        public readonly float HeadGap = 0.03f;
        // Walking stops this far before a contact.
        public readonly float Skin = 0.01f;
        // Walk steps shorter than this are ignored (no query, no step-up limit).
        public readonly float MinimumMotion = 0.0005f;
        // Sweeps per frame: the direct move plus slides along what it hit.
        public readonly int Passes = 3;
        // A hit whose horizontal normal is weaker than this (e.g. the top edge of a
        // box just above step height) blocks straight back along the motion.
        public readonly float MinimumHorizontalNormal = 0.2f;
        // Tolerance on the step-up limit.
        public readonly float StepTolerance = 0.005f;

        // Capsule sphere-centre heights for the current stance. False when the eye
        // is too low for a capsule between StepHeight and the eye (deep crouch):
        // then the rule is skipped and walking behaves exactly as before.
        public bool TryColumn(float feetY, float eyeY, out float bottomCenterY, out float topCenterY)
        {
            bottomCenterY = feetY + StepHeight + Radius;
            topCenterY = eyeY - HeadGap - Radius;
            if (float.IsNaN(feetY) || float.IsNaN(eyeY) || float.IsInfinity(feetY) || float.IsInfinity(eyeY)) return false;
            if (topCenterY < bottomCenterY) return false;
            return true;
        }

        // True when this walk step is long enough to be governed by the rule.
        public bool Walking(Float3 motion)
        {
            return motion.IsFinite && HorizontalLength(motion) >= MinimumMotion;
        }

        // Remove the part of a horizontal motion that would deepen an existing
        // overlap. away points out of the obstacle; only its horizontal part counts.
        public Float3 RemoveInto(Float3 motion, Float3 away)
        {
            Float3 n = HorizontalUnit(away);
            // A mostly vertical separation (uneven ground poking into the column
            // bottom) never blocks walking.
            if (Length(n) < 0.5f || HorizontalLength(away) < MinimumHorizontalNormal * Length(away)) return motion;
            float into = motion.X * n.X + motion.Z * n.Z;
            if (into >= 0) return motion;
            return new Float3(motion.X - into * n.X, motion.Y, motion.Z - into * n.Z);
        }

        // One swept contact. Returns the part of motion that may be taken now and
        // sets slide to the remainder projected along the obstacle (or zero).
        public Float3 Advance(Float3 motion, float hitDistance, Float3 hitNormal, out Float3 slide)
        {
            slide = new Float3();
            Float3 flat = new Float3(motion.X, 0, motion.Z);
            float length = Length(flat);
            if (length < 0.000001f || !motion.IsFinite) return new Float3();
            Float3 direction = new Float3(flat.X / length, 0, flat.Z / length);
            Float3 n = HorizontalUnit(hitNormal);
            if (Length(n) < 0.5f || HorizontalLength(hitNormal) < MinimumHorizontalNormal * Length(hitNormal))
                n = new Float3(-direction.X, 0, -direction.Z);
            float facing = direction.X * n.X + direction.Z * n.Z;
            if (facing >= 0) return flat; // Not moving into this surface.
            if (float.IsNaN(hitDistance) || float.IsInfinity(hitDistance)) hitDistance = 0;
            float allowed = Math.Max(0, Math.Min(length, hitDistance - Skin));
            Float3 moved = new Float3(direction.X * allowed, 0, direction.Z * allowed);
            Float3 rest = new Float3(flat.X - moved.X, 0, flat.Z - moved.Z);
            float into = rest.X * n.X + rest.Z * n.Z;
            if (into < 0) rest = new Float3(rest.X - into * n.X, 0, rest.Z - into * n.Z);
            slide = rest;
            return moved;
        }

        // Automatic support step-up while walking is limited to StepHeight.
        // Climbing, jumping, gliding and physical head motion never call this.
        public bool RefuseStep(bool walked, float rise)
        {
            if (!walked || float.IsNaN(rise)) return false;
            return rise > StepHeight + StepTolerance;
        }

        // R4 (playtest 3, owner rule): walking steps up onto anything shorter
        // than the chin and mantles onto it automatically. The chin sits this
        // far below the eye. Taller obstacles are stopped by the head sweep.
        public readonly float ChinBelowEye = 0.02f; // R10 (owner): walk-up height raised 10 cm (was 0.12 below the eye)
        public float WalkStepLimit(float eyeHeight)
        {
            if (float.IsNaN(eyeHeight) || float.IsInfinity(eyeHeight)) return StepHeight;
            return Math.Max(StepHeight, eyeHeight - ChinBelowEye);
        }
        public bool RefuseWalkStep(bool walked, float rise, float eyeHeight)
        {
            if (!walked || float.IsNaN(rise)) return false;
            return rise > WalkStepLimit(eyeHeight) + StepTolerance;
        }

        public static float HorizontalLength(Float3 v)
        {
            return (float)Math.Sqrt(v.X * v.X + v.Z * v.Z);
        }

        static Float3 HorizontalUnit(Float3 v)
        {
            float length = HorizontalLength(v);
            if (!v.IsFinite || length < 0.000001f) return new Float3();
            return new Float3(v.X / length, 0, v.Z / length);
        }

        static float Length(Float3 v)
        {
            return (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        }
    }
}
