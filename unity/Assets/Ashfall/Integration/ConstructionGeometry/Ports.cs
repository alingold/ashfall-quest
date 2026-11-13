using Ashfall.Core;

namespace Ashfall.Integration.ConstructionGeometry
{
    // Owned integration ports. No World implementation or engine API is implied.
    public enum SurfaceRole { Unknown, Structure, TerrainSurface, ConstructionTerrain, Decoration, Loot, Construction }
    public enum FloorBurialPolicy { LocalAllSamples025, RemoteAnySamples150 }
    public sealed class SourceTriangle
    {
        public readonly string SourceId, OwnerId, BodyId, SourceHash;
        public readonly SurfaceRole Role;
        public readonly int Layer;
        public readonly Float3 A, B, C;
        // Legacy records identify one physics body by OwnerId. Importers with multiple
        // bodies under a semantic owner must supply the explicit immutable BodyId.
        public SourceTriangle(string sourceId,string ownerId,string sourceHash,SurfaceRole role,int layer,Float3 a,Float3 b,Float3 c)
            : this(sourceId,ownerId,ownerId,sourceHash,role,layer,a,b,c) { }
        public SourceTriangle(string sourceId,string ownerId,string bodyId,string sourceHash,SurfaceRole role,int layer,Float3 a,Float3 b,Float3 c)
        { SourceId=sourceId;OwnerId=ownerId;BodyId=bodyId;SourceHash=sourceHash;Role=role;Layer=layer;A=a;B=b;C=c; }
    }
    public sealed class ProtectedBox
    {
        public readonly string Id;
        public readonly Float3 Center, HalfSize;
        public ProtectedBox(string id,Float3 center,Float3 halfSize){Id=id;Center=center;HalfSize=halfSize;}
    }
    public interface IConstructionOccupants
    {
        // Complete authoritative snapshot, including requester, all actors and objectives.
        // Return false when unavailable: missing safety data rejects placement.
        bool TrySnapshot(out ProtectedBox[] players,out Float3[] objectives);
    }
    public sealed class CollisionAllocation
    {
        public CollisionAllocation(int structure,int interaction,int player,int decoration)
        { if(structure!=8||interaction!=9||player!=10||decoration!=11)throw new System.ArgumentException("central_collision_allocation"); }
    }
}
