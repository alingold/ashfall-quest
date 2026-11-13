#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Modules.MapStreaming;
using Ashfall.Modules.World;
using UnityEngine;

namespace Ashfall.Integration.WorldRuntime
{
    // Bounded partial full-map geometry only. No collider, semantic registry, material inference,
    // approval conversion or gameplay activation capability. Stream source remains file-locked/read-only.
    public sealed class QuarantinedStreamGeometry : ITriangleSink,IDisposable
    {
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly GameObject root;
        readonly int budget;
        bool disposed;
        public int TriangleCount {get;private set;}
        public int MeshCount {get {return meshes.Count;}}
        public string Disposition {get {return "pilot-quarantine-incomplete-appearance-and-semantics";}}
        QuarantinedStreamGeometry(int triangleBudget)
        {if(triangleBudget<1||triangleBudget>100000)throw new ArgumentOutOfRangeException("triangleBudget");budget=triangleBudget;root=new GameObject("QuarantinedStreamGeometry");root.SetActive(false);}
        public static QuarantinedStreamGeometry Stage(MapStream stream,QuarantinedWorld world,int triangleBudget)
        {
            var sink=new QuarantinedStreamGeometry(triangleBudget);
            try {WorldMapAdapter.StreamQuarantined(stream,world,sink,256);return sink;}
            catch {sink.Dispose();throw;}
        }
        static float Convert(double d)
        {if(double.IsNaN(d)||double.IsInfinity(d)||Math.Abs(d)>1000000)throw new InvalidOperationException("Quarantine geometry domain");return (float)d;}
        public void Accept(string primitiveId,TriangleBatch batch)
        {
            if(disposed)throw new ObjectDisposedException("QuarantinedStreamGeometry");
            if(String.IsNullOrWhiteSpace(primitiveId)||batch==null||batch.TriangleCount<1||batch.TriangleCount>256||batch.Positions==null||batch.Positions.Length!=batch.TriangleCount*9)throw new InvalidOperationException("Invalid stream batch");
            // Keep streaming source validation complete while retaining only a bounded triangle prefix.
            int triangles=Math.Min(batch.TriangleCount,budget-TriangleCount);if(triangles<=0||meshes.Count>=4096)return;
            var vertices=new Vector3[triangles*3];var indices=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++){vertices[i]=new Vector3(Convert(batch.Positions[3*i]),Convert(batch.Positions[3*i+1]),Convert(batch.Positions[3*i+2]));indices[i]=i;}
            var mesh=new Mesh();meshes.Add(mesh);mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateBounds();
            if(batch.Normals!=null){if(batch.Normals.Length!=batch.Positions.Length)throw new InvalidOperationException("Invalid normals");var normals=new Vector3[vertices.Length];for(int i=0;i<normals.Length;i++)normals[i]=new Vector3(Convert(batch.Normals[3*i]),Convert(batch.Normals[3*i+1]),Convert(batch.Normals[3*i+2]));mesh.normals=normals;}
            if(batch.Uv!=null){if(batch.Uv.Length!=batch.TriangleCount*6)throw new InvalidOperationException("Invalid UV");var uv=new Vector2[vertices.Length];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(Convert(batch.Uv[2*i]),Convert(batch.Uv[2*i+1]));mesh.uv=uv;}
            var go=new GameObject(primitiveId);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            // Deliberately no renderer: appearance dependency attestation is absent from TriangleBatch.
            TriangleCount+=triangles;
        }
        static void Release(UnityEngine.Object o){if(!o)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
        public void Dispose(){if(disposed)return;disposed=true;if(root)root.SetActive(false);Release(root);foreach(var mesh in meshes)Release(mesh);meshes.Clear();}
    }
}
#endif
