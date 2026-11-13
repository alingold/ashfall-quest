using System;
namespace Ashfall.Modules.MapStreaming
{
    // Owned ports, declared before implementation. Pilot data has no production Build capability.
    public interface ITriangleSink { void Accept(string primitiveId, TriangleBatch batch); }
    public sealed class TriangleBatch
    {
        public readonly double[] Positions, Normals, Uv;
        public readonly int TriangleCount;
        public TriangleBatch(double[] positions, double[] normals, double[] uv, int triangles)
        { Positions=positions; Normals=normals; Uv=uv; TriangleCount=triangles; }
    }
}
