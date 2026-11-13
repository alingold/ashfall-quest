#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Ashfall.Integration.MapNative;
using Ashfall.Modules.MapStreaming;
using Ashfall.Modules.World;
namespace Ashfall.Tools.MapNative
{
    // Coordinator calls only under engine_lane.py. Resolver is explicit and externally supplied;
    // this method neither creates material approvals nor activates the quarantined hierarchy.
    public static class NativeFixture
    {
        sealed class Lease : IQuarantineLease {internal bool Valid=true;public bool Matches(SourceProvenance source){return Valid;}}
        static GameObject Root(NativeGeometryStore store){return (GameObject)typeof(NativeGeometryStore).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(store);}
        // Root-only full native replay with actual supplied conversion. No fallback resolver
        // is manufactured for unsupported real-map appearance dependencies.
        public static string RunFullPreview(string manifestPath,string worldPath,ISourceMaterialResolver resolver,long expectedTriangles,int expectedBatches,long expectedZeroNormals)
        {
            byte[] manifest=File.ReadAllBytes(manifestPath),payload=File.ReadAllBytes(worldPath);var world=QuarantinedWorld.LoadPilot(payload,WorldLoader.Hash(payload));
            using(var stream=MapStream.OpenPilot(manifest,MapStream.Hash(manifest))) {
                var store=new NativeGeometryStore(resolver);
                using(var geometry=FullGeometry.Stage(stream,world,manifest,store,new Lease(),2000000,20000,536870912))
                using(var life=new PreviewLifetime()) {
                    geometry.Validate();long zero=0,corners=0,uv=0;
                    foreach(var filter in Root(store).GetComponentsInChildren<MeshFilter>(true)) {
                        var mesh=filter.sharedMesh;corners+=mesh.vertexCount;uv+=mesh.uv.Length;
                        foreach(var normal in mesh.normals)if(normal.Equals(Vector3.zero))zero++;
                    }
                    if(geometry.Triangles!=expectedTriangles||store.MeshCount!=expectedBatches||zero!=expectedZeroNormals||corners!=3*expectedTriangles||uv!=corners)throw new Exception("full native triangles/batches/UV/zero-normal census");
                    geometry.ActivatePreview(life);geometry.Validate();life.Dispose();
                    if(geometry.RetainedBytes!=0||store.PreviewActive||store.MeshCount!=0||store.MaterialCloneCount!=0||store.PackedBytes!=0)throw new Exception("full preview immediate revoke census");
                    return "full native geometry preview/revoke passed; triangles="+expectedTriangles+" zeroNormals="+zero;
                }
            }
        }
        public static IEnumerator RunPlayModeLifetime(string manifestPath,string worldPath,ISourceMaterialResolver resolver)
        {
            if(!Application.isPlaying)throw new Exception("actual PlayMode required");
            byte[] manifest=File.ReadAllBytes(manifestPath),payload=File.ReadAllBytes(worldPath);var world=QuarantinedWorld.LoadPilot(payload,WorldLoader.Hash(payload));
            using(var stream=MapStream.OpenPilot(manifest,MapStream.Hash(manifest))) {
                var store=new NativeGeometryStore(resolver);var geometry=FullGeometry.Stage(stream,world,manifest,store,new Lease(),2000000,20000,536870912);
                var root=Root(store);var filters=root.GetComponentsInChildren<MeshFilter>(true);var meshes=new Mesh[filters.Length];for(int i=0;i<filters.Length;i++)meshes[i]=filters[i].sharedMesh;
                var renderers=root.GetComponentsInChildren<MeshRenderer>(true);var materials=new Material[renderers.Length];for(int i=0;i<renderers.Length;i++)materials[i]=renderers[i].sharedMaterial;
                geometry.Dispose();if(root.activeSelf)throw new Exception("immediate withdrawal");
                foreach(var r in renderers)if(r.enabled)throw new Exception("renderer withdrawal");
                yield return null;
                if(root)throw new Exception("deferred root destruction");foreach(var m in meshes)if(m)throw new Exception("deferred mesh destruction");foreach(var m in materials)if(m)throw new Exception("deferred material destruction");
            }
        }
        public static string RunTamper(string manifestPath,string worldPath,ISourceMaterialResolver resolver)
        {
            byte[] manifest=File.ReadAllBytes(manifestPath),payload=File.ReadAllBytes(worldPath);var world=QuarantinedWorld.LoadPilot(payload,WorldLoader.Hash(payload));
            using(var stream=MapStream.OpenPilot(manifest,MapStream.Hash(manifest))) {
                var store=new NativeGeometryStore(resolver);using(var geometry=FullGeometry.Stage(stream,world,manifest,store,new Lease(),2000000,20000,536870912)) {
                    var root=Root(store);root.transform.GetChild(0).localPosition=new Vector3(.001f,0,0);bool rejected=false;
                    try{geometry.Validate();}catch(InvalidOperationException){rejected=true;}
                    if(!rejected||store.MeshCount!=0)throw new Exception("tampered hierarchy retained");
                    return "native transform tamper rejects and disposes";
                }
            }
        }
        public static string Run(string manifestPath,string worldPath,ISourceMaterialResolver resolver,long expectedTriangles)
        {
            var manifest=File.ReadAllBytes(manifestPath);var payload=File.ReadAllBytes(worldPath);var world=QuarantinedWorld.LoadPilot(payload,WorldLoader.Hash(payload));
            var lease=new Lease();var store=new NativeGeometryStore(resolver);
            using(var stream=MapStream.OpenPilot(manifest,MapStream.Hash(manifest)))
            using(var geometry=FullGeometry.Stage(stream,world,manifest,store,lease,2000000,20000,536870912)) {
                geometry.Validate();if(geometry.Triangles!=expectedTriangles||store.MeshCount!=geometry.Batches)throw new Exception("native full coverage");
                lease.Valid=false;bool rejected=false;try{geometry.Validate();}catch(InvalidOperationException){rejected=true;}
                if(!rejected||store.MeshCount!=0||geometry.RetainedBytes!=0)throw new Exception("native revocation cleanup");
                geometry.Dispose();return "quarantined full geometry staged/validated/revoked; triangles="+expectedTriangles;
            }
        }
    }
}
#endif
