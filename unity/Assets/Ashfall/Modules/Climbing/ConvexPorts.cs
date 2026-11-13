using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    // Candidate only. Central binding must use the locomotion collider basis, NOT HMD pitch.
    // World offset is already transformed; it is added once, never rotated again.
    public struct ConvexHeadPose
    {
        public Float3 WorldOffset;
        public float Qx, Qy, Qz, Qw;
    }
    public interface IConvexHeadSource { bool TryRead(out ConvexHeadPose pose); }
    public struct ConvexHeadQuery
    {
        public SourceHeadConvex Shape;
        public ConvexHeadPose Pose;
        public Float3 Origin, Motion;
        public int Mask;
        // A retry may resolve numerical touching contacts ONLY when separating.
        // It must still test all geometry continuously, with unchanged shape/masks/exclusions.
        public bool Recovery;
    }
    // Revision covers geometry, filtering and collider identity. Changes during a solve fail closed.
    // Penetrating means true geometric penetration, not merely touching a contact margin.
    // Backend excludes actor hierarchy, ignores triggers, refuses overflow/unavailable queries.
    // false never means clear. Cast distance is swept translation length in SI metres.
    public interface IConvexClimbScene
    {
        long Revision { get; }
        bool Penetrating(ConvexHeadQuery query, out bool penetrating);
        bool Cast(ConvexHeadQuery query, out bool hit, out ClimbQueryHit nearest);
        bool Ray(ClimbQuery query, out bool hit, out ClimbQueryHit nearest);
        bool Grab(ClimbQuery query, out bool hit, out ClimbQueryHit nearest);
    }
    public struct ConvexSweepResult
    {
        public Float3 Displacement;
        public bool Complete, Available;
        public int Casts;
    }
}
