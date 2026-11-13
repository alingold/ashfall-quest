using System;
using System.IO;
using Ashfall.Integration.MapNative;
using Ashfall.Modules.MapStreaming;
#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
#endif
namespace Ashfall.Tools.MapNative
{
    public static class AppearanceFixture
    {
        static void Need(bool b,string why){if(!b)throw new Exception(why);}
        public static void Cpu(string pngPath)
        {
            var raw=File.ReadAllBytes(pngPath);var receipt=new VerifiedImageSlice(Path.GetFullPath(pngPath),raw.Length,MapStream.Hash(raw),0,raw.Length);
            int w,h;Need(BoundedPng.Inspect(receipt.Read(raw.Length),4096,out w,out h)==16&&w==2&&h==2,"real PNG bound");
            Reject(()=>receipt.Read(raw.Length-1));var bad=(byte[])raw.Clone();bad[bad.Length-1]^=1;Reject(()=>BoundedPng.Inspect(bad,4096,out w,out h));
            Reject(()=>new VerifiedImageSlice(Path.GetFullPath(pngPath),raw.Length,new string('0',64),0,raw.Length).Read(raw.Length));
            Reject(()=>BoundedPng.Inspect(raw,1,out w,out h));var life=new PreviewLifetime();int calls=0;life.Revoked+=()=>calls++;life.Dispose();life.Dispose();Need(calls==1&&!life.Live,"terminal synchronous revoke");Reject(life.Check);
            Console.WriteLine("PASS bounded PNG/hash/encoded and decoded admission/CRC/lifetime");
        }
        static void Reject(Action a){bool rejected=false;try{a();}catch{rejected=true;}Need(rejected,"expected rejection");}
#if UNITY_6000_0_OR_NEWER
        // ROOT ONLY: real native API assertions, never run by leaf. URP package/version and
        // main-thread identity are root deployment preconditions. This synthetic fixture is
        // deliberately distinct from full Alpha/indoor geometry and appearance acceptance.
        public static string RunNative(string pngPath,Shader urp176Lit)
        {
            var raw=File.ReadAllBytes(pngPath);var manifest=System.Text.Encoding.UTF8.GetBytes("{\"references\":{\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorFactor\":[0.2,0.4,0.6,1],\"baseColorTexture\":{\"index\":0},\"metallicFactor\":0.25,\"roughnessFactor\":0.75}}],\"textures\":[{\"source\":0,\"sampler\":0}],\"images\":[{\"uri\":\"fixture.png\"}],\"samplers\":[{\"magFilter\":9729,\"minFilter\":9729,\"wrapS\":10497,\"wrapT\":10497}]}}");
            var images=new Dictionary<int,VerifiedImageSlice>{{0,new VerifiedImageSlice(Path.GetFullPath(pngPath),raw.Length,MapStream.Hash(raw),0,raw.Length)}};
            using(var resolver=new UrpSourceAppearance(manifest,urp176Lit,images,1,16)) {
                var binding=new PrimitiveBinding("fixture/material0",0,manifest,MapStream.Hash(manifest));
                var material=resolver.Resolve(binding);Need(resolver.MaterialCount==1&&resolver.TextureCount==1&&resolver.DecodedBytes==16,"actual dependency census");
                var batch=new PackedBatch(binding,new TriangleBatch(new double[]{0,0,0,1,0,0,0,1,0},new double[]{0,0,0,0,0,1,0,0,1},new double[]{0,0,1,0,0,1},1));
                using(var store=new NativeGeometryStore(resolver,1,1,108)) {
                    store.Add(batch);store.Validate();Need(store.MeshCount==1&&store.MaterialCloneCount==1&&store.PackedBytes==108,"native census");
                    using(var life=new PreviewLifetime()){store.ActivatePreview(life);Need(store.PreviewActive,"explicit activation");store.Validate();life.Dispose();Need(!store.PreviewActive&&store.MeshCount==0&&store.PackedBytes==0,"immediate revocation census");}
                }
                using(var failed=new NativeGeometryStore(resolver,1,1,107)){Reject(()=>failed.Add(batch));Need(failed.MeshCount==0&&failed.MaterialCloneCount==0&&failed.PackedBytes==0,"failed batch rollback");}
                Need(material&&resolver.Matches(binding,material,material),"borrowed dependency survives store disposal");
                using(var tampered=new NativeGeometryStore(resolver)) {
                    tampered.Add(batch);var root=(GameObject)typeof(NativeGeometryStore).GetField("root",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(tampered);
                    var renderer=root.GetComponentInChildren<MeshRenderer>(true);var mesh=root.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                    Need(renderer.sharedMaterials.Length==1&&renderer.sharedMaterial.GetTexture("_BaseMap")==material.GetTexture("_BaseMap"),"actual texture material slot");
                    Need(mesh.uv[1].Equals(new Vector2(1,0))&&mesh.normals[0].Equals(Vector3.zero),"actual UV and zero normal");
                    renderer.sharedMaterials=new[]{renderer.sharedMaterial,renderer.sharedMaterial};Reject(tampered.Validate);Need(tampered.MeshCount==0,"extra slot terminal rollback");
                }
                using(var tampered=new NativeGeometryStore(resolver)) {
                    tampered.Add(batch);var root=(GameObject)typeof(NativeGeometryStore).GetField("root",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(tampered);
                    var mesh=root.GetComponentInChildren<MeshFilter>(true).sharedMesh;var uv=mesh.uv;uv[0]=new Vector2(.25f,.75f);mesh.uv=uv;Reject(tampered.Validate);Need(tampered.MeshCount==0,"UV tamper terminal rollback");
                }
                using(var revoked=new NativeGeometryStore(resolver)){revoked.Add(batch);resolver.Dispose();Reject(revoked.Validate);Need(revoked.MeshCount==0,"source appearance lifetime revocation");}
            }
            return "native PNG/material-slot/UV/zero-normal/activation/revoke/rollback assertions passed";
        }
#endif
    }
}
