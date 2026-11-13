using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    // EXPERIMENTAL BOX QUARANTINE. No box substitution approval has been issued.
    // Owned legacy experiment ports; Approved is only caller fixture data, not an approval receipt.
    // No implicit replacement of the source convex goggles: unapproved shape => fail closed.
    public struct HeadBox
    {
        public Float3 CenterOffset, HalfExtents;
        public float Qx, Qy, Qz, Qw;
        public bool Approved;
    }
    public interface IHeadBoxSource { bool TryRead(out HeadBox shape); }
    public struct CollisionLayers
    {
        public int StructureMask, SupportMask, HeadMask;
        public bool Valid { get { return StructureMask==(1<<8) && (HeadMask&(1<<8))!=0 && (HeadMask&~((1<<8)|(1<<9)))==0 && (SupportMask&(1<<8))!=0 && (SupportMask&~((1<<8)|(1<<9)))==0; } }
    }
    public struct ClimbQuery
    {
        public Float3 Origin, Motion;
        public HeadBox Shape;
        public float Radius;
        public int Mask;
    }
    public struct ClimbQueryHit
    {
        public SurfaceHit Surface;
        public float Distance;
    }
    // Backend excludes the supplied actor hierarchy, ignores triggers, rejects saturated buffers.
    // false means unavailable/overflow; it must never be treated as a clear scene.
    public interface IClimbQueryScene
    {
        bool Ray(ClimbQuery query, out bool hit, out ClimbQueryHit nearest);
        bool Grab(ClimbQuery query, out bool hit, out ClimbQueryHit nearest);
        bool Overlap(ClimbQuery query, out bool blocked);
        bool Cast(ClimbQuery query, out bool hit, out ClimbQueryHit nearest);
    }
}
