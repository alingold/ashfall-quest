using Ashfall.Core;

namespace Ashfall.Modules.Climbing
{
    public enum QueryKind { Structure, Support }
    public struct SurfaceHit
    {
        public Float3 Point, Normal;
        public bool ArchiveGeometry, AuthoredClimbable;
    }
    public struct MotionResult
    {
        public Float3 Displacement, Velocity;
        public MotionResult(Float3 displacement, Float3 velocity) { Displacement = displacement; Velocity = velocity; }
    }
    // Queries are read-only, world-space Unity-axis SI, excluding the actor.
    // Structure maps source mask 1; Support maps source mask 5|16 via central layer mapping.
    // Sweep calculates a safe displacement/slide velocity without mutating the scene.
    // It uses the centrally owned live goggle/head collider, never a substituted torso capsule.
    // HeadMotionClear also rejects initial overlap. SweepHead must handle upward floor-normal
    // recovery contacts (source uses up to four retries), while preserving wall/ceiling blocking.
    public interface IClimbCollision
    {
        bool TryGrab(Float3 point, float radius, out SurfaceHit hit);
        bool Raycast(Float3 from, Float3 to, QueryKind kind, out SurfaceHit hit);
        bool HeadMotionClear(Float3 head, Float3 motion);
        MotionResult SweepHead(Float3 head, Float3 motion, float seconds, bool slide);
    }
    public struct ClimbOptions
    {
        public bool Enabled, LeftTracked, RightTracked, LeftReserved, RightReserved;
        public Float3 LeftContact, RightContact;
        // Standing calibration (.55..2.2) is retained independently of live mantle eligibility.
        public float FlingPower, StandingEyeHeight, FloorNormalY, GrabCooldown;
        public static ClimbOptions Default
        {
            get { return new ClimbOptions { Enabled = true, LeftTracked = true, RightTracked = true,
                FlingPower = 1, StandingEyeHeight = 1.75f, FloorNormalY = .70710678f }; }
        }
    }
    public struct ClimbStep
    {
        public bool Accepted, Climbing, Mantling, Launch, Released, MantleBlocked, OwnsMotion;
        public Float3 RootDisplacement, Velocity;
        public string Reason;
    }
}
