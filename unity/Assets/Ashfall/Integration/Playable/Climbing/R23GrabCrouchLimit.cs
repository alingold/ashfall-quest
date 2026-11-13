using System;
namespace Ashfall.Integration.Playable.Climbing
{
    // Limits artificial anchor pull only. Raw tracked head and hands are untouched.
    public static class R23GrabCrouchLimit
    {
        public const float MinimumEyeFraction = .5f;
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public static float DownwardMotion(float eyeY, float floorY, float standingEye, float requestedY)
        {
            if (!Finite(eyeY) || !Finite(floorY) || !Finite(standingEye) || !Finite(requestedY) || standingEye <= 0)
                throw new ArgumentOutOfRangeException("finite crouch frame");
            if (requestedY >= 0) return requestedY;
            // If physical crouching already put the eye below this height, block
            // further artificial descent without pushing the tracked view upward.
            float available = Math.Min(0, floorY + standingEye * MinimumEyeFraction - eyeY);
            return Math.Max(requestedY, available);
        }
        public static float KneeForward(float standingEye, float hipToFootDistance)
        {
            if (!Finite(standingEye) || !Finite(hipToFootDistance) || standingEye <= 0 || hipToFootDistance < 0)
                throw new ArgumentOutOfRangeException("finite crouch pose");
            float halfLeg = Math.Max(.1f, (standingEye * .49f - .07f) * .5f);
            return Math.Max(.12f, (float)Math.Sqrt(Math.Max(0, halfLeg * halfLeg - hipToFootDistance * hipToFootDistance * .25f)));
        }
    }
}
