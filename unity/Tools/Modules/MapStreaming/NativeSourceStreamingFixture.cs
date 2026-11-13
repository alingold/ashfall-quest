using System;
using System.IO;
using Ashfall.Modules.MapStreaming;
namespace Ashfall.Tools.MapStreaming
{
    // Callable by the coordinator's serialized native lane. Does not launch an engine,
    // create GameObjects, grant content approval, retain batches or publish active geometry.
    public static class NativeSourceStreamingFixture
    {
        sealed class Census : ITriangleSink
        {
            internal long Triangles,ZeroNormals;internal int Calls,Maximum;
            public void Accept(string id,TriangleBatch b)
            {
                if(String.IsNullOrEmpty(id)||b.TriangleCount<1||b.TriangleCount>256)throw new InvalidDataException("batch identity/budget");
                Calls++;Triangles+=b.TriangleCount;Maximum=Math.Max(Maximum,b.TriangleCount);
                for(int i=0;i<b.Positions.Length;i++)if(Double.IsNaN(b.Positions[i])||Double.IsInfinity(b.Positions[i]))throw new InvalidDataException("position finite");
                if(b.Normals!=null)for(int i=0;i<b.Normals.Length;i+=3) {
                    double x=b.Normals[i],y=b.Normals[i+1],z=b.Normals[i+2];
                    if(x==0&&y==0&&z==0){ZeroNormals++;continue;}
                    double length=x*x+y*y+z*z;if(Double.IsNaN(length)||Math.Abs(length-1)>1e-12)throw new InvalidDataException("normal finite/unit");
                }
            }
        }
        public static string Run(string maprefPath,long expectedTriangles,long expectedZeroNormals)
        {
            byte[] bytes=File.ReadAllBytes(maprefPath);var sink=new Census();
            using(var stream=MapStream.OpenPilot(bytes,MapStream.Hash(bytes))) {
                if(stream.SourceCacheBytes!=65536||stream.ApprovalDisposition!="pilot-quarantine")throw new InvalidDataException("cache/quarantine");
                stream.StreamAll(sink,256);
                if(sink.Triangles!=expectedTriangles||sink.ZeroNormals!=expectedZeroNormals)throw new InvalidDataException("source census");
            }
            return "pilot-only triangles="+sink.Triangles+" zeroNormals="+sink.ZeroNormals+" callbacks="+sink.Calls+" maximum="+sink.Maximum;
        }
    }
}
