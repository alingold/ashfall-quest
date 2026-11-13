#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Ashfall.Integration.MapNative;
using Ashfall.Modules.MapStreaming;
namespace Ashfall.Tools.MapNative
{
    // ROOT ONLY. Every setter is followed by a getter proving effective native state changed.
    public static class IntegrityFixtureR3
    {
        static void Need(bool b,string why){if(!b)throw new Exception("R3: "+why);}
        static GameObject Root(NativeGeometryStore s){return (GameObject)typeof(NativeGeometryStore).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);}
        static void Release(UnityEngine.Object o){if(!o)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
        public static System.Collections.IEnumerator RunPlayMode(Shader lit)
        {
            Need(Application.isPlaying,"PlayMode required");
            byte[] manifest=System.Text.Encoding.UTF8.GetBytes("{\"references\":{\"materials\":[{\"pbrMetallicRoughness\":{\"metallicFactor\":0,\"roughnessFactor\":1}}]}}");
            using(var resolver=new UrpSourceAppearance(manifest,lit,new Dictionary<int,VerifiedImageSlice>(),1,16)){
                for(int active=0;active<2;active++){
                    var binding=new PrimitiveBinding("lifetime",0,manifest,MapStream.Hash(manifest));var borrowed=resolver.Resolve(binding);
                    var store=new NativeGeometryStore(resolver);var life=new PreviewLifetime();
                    try{
                        store.Add(new PackedBatch(binding,new TriangleBatch(new double[]{0,0,0,1,0,0,0,1,0},null,null,1)));
                        var root=Root(store);var renderer=root.GetComponentInChildren<MeshRenderer>(true);var mesh=root.GetComponentInChildren<MeshFilter>(true).sharedMesh;var clone=renderer.sharedMaterial;
                        if(active==1){store.ActivatePreview(life);life.Dispose();}else store.Dispose();
                        Need(!root.activeSelf&&!renderer.enabled&&!store.PreviewActive&&store.MeshCount==0,"immediate active/never-active withdrawal");
                        yield return null;yield return null;
                        Need(!root&&!renderer&&!mesh&&!clone&&borrowed,"deferred owned destruction and borrowed material survival");
                    }finally{store.Dispose();life.Dispose();}
                }
            }
        }
        public static string Run(Shader lit)
        {
            byte[] manifest=System.Text.Encoding.UTF8.GetBytes("{\"references\":{\"materials\":[{\"pbrMetallicRoughness\":{\"metallicFactor\":0,\"roughnessFactor\":1}}]}}");
            int cases=0;
            for(int active=0;active<2;active++)for(int test=0;test<25;test++)
            using(var resolver=new UrpSourceAppearance(manifest,lit,new Dictionary<int,VerifiedImageSlice>(),1,16))
            using(var store=new NativeGeometryStore(resolver))using(var life=new PreviewLifetime()){
                var binding=new PrimitiveBinding("R3/synthetic",0,manifest,MapStream.Hash(manifest));
                var batch=new PackedBatch(binding,new TriangleBatch(new double[]{0,0,0,1,0,0,0,1,0},new double[]{0,0,0,0,0,1,0,0,1},new double[]{0,0,1,0,0,1},1));
                store.Add(batch);store.Validate();if(active==1)store.ActivatePreview(life);
                var root=Root(store);var renderer=root.GetComponentInChildren<MeshRenderer>(true);var filter=root.GetComponentInChildren<MeshFilter>(true);var mesh=filter.sharedMesh;var original=resolver.Resolve(binding);var clone=renderer.sharedMaterial;
                Mesh foreign=null;Material foreignMaterial=null;
                try{
                    switch(test){
                        case 0:renderer.localBounds=new Bounds(Vector3.one*1000,Vector3.one);Need(renderer.localBounds.center.x==1000,"local culling bounds getter");break;
                        case 1:foreign=new Mesh();foreign.vertices=new[]{Vector3.one*500,Vector3.one*501,Vector3.one*502};renderer.additionalVertexStreams=foreign;Need(renderer.additionalVertexStreams==foreign&&foreign.vertices[0].x==500,"effective alternate stream");break;
                        case 2:clone.SetShaderPassEnabled("ForwardLit",false);Need(!clone.GetShaderPassEnabled("ForwardLit"),"pass getter");break;
                        case 3:case 4:case 5:case 6:
                            string property=new[]{"_SrcBlend","_DstBlend","_SrcBlendAlpha","_DstBlendAlpha"}[test-3];float value=clone.GetFloat(property)==0?1:0;clone.SetFloat(property,value);Need(clone.GetFloat(property)==value,"blend getter");break;
                        case 7:renderer.forceRenderingOff=true;Need(renderer.forceRenderingOff,"force rendering getter");break;
                        case 8:renderer.enabled=active==0;Need(renderer.enabled==(active==0),"enabled getter");break;
                        case 9:renderer.renderingLayerMask^=2;Need(renderer.renderingLayerMask!=1,"rendering layer getter");break;
                        case 10:foreign=UnityEngine.Object.Instantiate(mesh);filter.sharedMesh=foreign;Need(filter.sharedMesh==foreign&&filter.sharedMesh!=mesh,"filter identity getter");break;
                        case 11:var v=mesh.vertices;v[0].x=17;mesh.vertices=v;Need(mesh.vertices[0].x==17,"vertices getter");break;
                        case 12:mesh.triangles=new[]{2,1,0};Need(mesh.GetIndices(0)[0]==2,"indices getter");break;
                        case 13:mesh.subMeshCount=2;Need(mesh.subMeshCount==2,"submesh getter");break;
                        case 14:mesh.tangents=new[]{Vector4.one,Vector4.one,Vector4.one};Need(mesh.tangents.Length==3,"tangent getter");break;
                        case 15:var n=mesh.normals;n[0]=Vector3.one;mesh.normals=n;Need(mesh.normals[0]==Vector3.one,"zero normal preserved before tamper");break;
                        case 16:var uv=mesh.uv;uv[0]=Vector2.one;mesh.uv=uv;Need(mesh.uv[0]==Vector2.one,"UV getter");break;
                        case 17:foreignMaterial=new Material(clone);renderer.sharedMaterial=foreignMaterial;Need(renderer.sharedMaterial==foreignMaterial,"material identity getter");break;
                        case 18:original.SetFloat("_Cull",0);Need(original.GetFloat("_Cull")==0,"borrowed cull getter");break;
                        case 19:root.SetActive(active==0);Need(root.activeSelf==(active==0),"root lifecycle getter");break;
                        case 20:renderer.gameObject.SetActive(false);Need(!renderer.gameObject.activeSelf,"node lifecycle getter");break;
                        case 21:renderer.bounds=new Bounds(Vector3.one*1000,Vector3.one);Need(renderer.bounds.center.x==1000,"world culling getter");break;
                        case 22:var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",Color.red);renderer.SetPropertyBlock(block);Need(renderer.HasPropertyBlock(),"property block getter");break;
                        case 23:clone.shader=Shader.Find("Hidden/InternalErrorShader");Need(clone.shader&&clone.shader!=lit,"shader identity getter");break;
                        case 24:renderer.sharedMaterials=new[]{clone,clone};Need(renderer.sharedMaterials.Length==2,"material slots getter");break;
                    }
                    bool rejected=false;try{store.Validate();}catch(InvalidOperationException){rejected=true;}
                    Need(rejected,"tamper accepted case "+test+" active="+active);
                    Need(!store.PreviewActive&&store.MeshCount==0&&store.MaterialCloneCount==0&&store.PackedBytes==0,"terminal census");
                    Need(!root||!root.activeSelf,"synchronous root withdrawal");Need(!renderer||!renderer.enabled,"synchronous renderer withdrawal");Need(original,"borrowed material survives");Need(!foreign||foreign.vertexCount==3,"borrowed stream/filter mesh survives");cases++;
                }finally{store.Dispose();Release(foreign);Release(foreignMaterial);}
            }
            // Retained input arrays cannot rewrite the store's expected geometry.
            using(var resolver=new UrpSourceAppearance(manifest,lit,new Dictionary<int,VerifiedImageSlice>(),1,16))using(var store=new NativeGeometryStore(resolver)){
                var binding=new PrimitiveBinding("alias",0,manifest,MapStream.Hash(manifest));var batch=new PackedBatch(binding,new TriangleBatch(new double[]{0,0,0,1,0,0,0,1,0},null,null,1));store.Add(batch);batch.Positions[0]=13;store.Validate();Need(Root(store).GetComponentInChildren<MeshFilter>(true).sharedMesh.vertices[0].x==0,"caller alias cannot redefine retained geometry");
            }
            return "R3 synthetic native PASS: "+cases+" getter-proven active/inactive tampers plus retained array isolation; no map/render approval";
        }
    }
}
#endif
