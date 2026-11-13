#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    internal sealed class R23Visuals
    {
        [Serializable] public sealed class Manifest { public Surface[] materials=null;public string[] parts=null; }
        [Serializable] public sealed class Surface { public float[] color=null;public float metallic=0,roughness=1;public bool doubleSided=false;public string alphaMode=null,baseColor=null,normal=null,metallicMap=null,occlusion=null;public float[] emissionColor=null;public string emission=null;public bool specularWorkflow=false;public float[] specularColor=null;public float glossiness=1;public string specularGlossiness=null; }
        readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
        readonly Dictionary<string,byte[]> files=new Dictionary<string,byte[]>();
        readonly Dictionary<string,R23GloveArticulation> articulatedHands=new Dictionary<string,R23GloveArticulation>();
        static readonly R23SharedTextureCache<Texture2D> sharedTextures=new R23SharedTextureCache<Texture2D>(delegate(Texture2D texture){if(texture!=null)UnityEngine.Object.Destroy(texture);});
        readonly R23SharedTextureCache<Texture2D>.Owner textureOwner=sharedTextures.OpenOwner();bool disposed;
        readonly string directory;
        public R23Visuals() { directory=Application.streamingAssetsPath+"/PlayableR23/HandsWeapons/"; }
        IEnumerator Read(string file) {
            if(disposed)throw new ObjectDisposedException("R23Visuals");
            if(files.ContainsKey(file))yield break;
            string url=directory+file;if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            using(var req=UnityWebRequest.Get(url)) {
                yield return req.SendWebRequest();
                if(disposed)throw new ObjectDisposedException("R23Visuals");
                if(req.result!=UnityWebRequest.Result.Success)throw new IOException("R23 content "+file+": "+req.error);
                files[file]=req.downloadHandler.data;
            }
        }
        public IEnumerator Load(string name,Transform parent) {
            if(disposed)throw new ObjectDisposedException("R23Visuals");
            yield return Read(name+".json");yield return Read(name+".meshbin");
            var manifest=JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(files[name+".json"]));
            var mats=new Material[manifest.materials.Length];
            for(int i=0;i<mats.Length;i++) {
                if(disposed)throw new ObjectDisposedException("R23Visuals");
                var s=manifest.materials[i];var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)throw new InvalidOperationException("R23 requires URP Lit shader");
                var m=new Material(shader);resources.Add(m);mats[i]=m;
                m.SetColor("_BaseColor",new Color(s.color[0],s.color[1],s.color[2],s.color[3]));m.SetFloat("_Metallic",s.metallic);m.SetFloat("_Smoothness",1-s.roughness);m.SetFloat("_Cull",s.doubleSided?0:2);
                if(s.alphaMode=="BLEND"){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;m.SetShaderPassEnabled("ShadowCaster",false);}
                if(!string.IsNullOrEmpty(s.baseColor)){yield return ReadTexture(s.baseColor,false);m.SetTexture("_BaseMap",Texture(s.baseColor,false));}
                if(!string.IsNullOrEmpty(s.metallicMap)){yield return ReadTexture(s.metallicMap,true);m.SetTexture("_MetallicGlossMap",Texture(s.metallicMap,true));m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",1);}
                // glTF KHR_materials_pbrSpecularGlossiness: source RGB specular
                // is color data (sRGB image), alpha is linear gloss; do not alias
                // this image with a linear normal/metal map in the shared cache.
                if(s.specularWorkflow){
                    m.SetFloat("_WorkflowMode",0);m.EnableKeyword("_SPECULAR_SETUP");m.SetFloat("_Smoothness",s.glossiness);if(!string.IsNullOrEmpty(s.occlusion))m.EnableKeyword("_OCCLUSIONMAP");
                    if(s.specularColor!=null&&s.specularColor.Length==3){bool gamma=QualitySettings.activeColorSpace==ColorSpace.Gamma;m.SetVector("_SpecColor",new Vector4(Ashfall.Integration.Playable.Appearance.R23AppearanceColorDomain.ShaderRgb(s.specularColor[0],gamma),Ashfall.Integration.Playable.Appearance.R23AppearanceColorDomain.ShaderRgb(s.specularColor[1],gamma),Ashfall.Integration.Playable.Appearance.R23AppearanceColorDomain.ShaderRgb(s.specularColor[2],gamma),1));}
                    if(!string.IsNullOrEmpty(s.specularGlossiness)){yield return ReadTexture(s.specularGlossiness,false);m.SetTexture("_SpecGlossMap",Texture(s.specularGlossiness,false));m.EnableKeyword("_METALLICSPECGLOSSMAP");}
                }
                if(!string.IsNullOrEmpty(s.occlusion)){yield return ReadTexture(s.occlusion,true);m.SetTexture("_OcclusionMap",Texture(s.occlusion,true));}
                // glTF emission is a scene-linear color factor, separate from metal albedo.
                // Keep color images in the shared sRGB cache and data maps linear.
                if(s.emissionColor!=null&&s.emissionColor.Length==3&&(s.emissionColor[0]>0||s.emissionColor[1]>0||s.emissionColor[2]>0)){
                    bool gamma=QualitySettings.activeColorSpace==ColorSpace.Gamma;
                    m.SetVector("_EmissionColor",new Vector4(Ashfall.Integration.Playable.Appearance.R23AppearanceColorDomain.ShaderRgb(s.emissionColor[0],gamma),Ashfall.Integration.Playable.Appearance.R23AppearanceColorDomain.ShaderRgb(s.emissionColor[1],gamma),Ashfall.Integration.Playable.Appearance.R23AppearanceColorDomain.ShaderRgb(s.emissionColor[2],gamma),1));
                    if(!string.IsNullOrEmpty(s.emission)){yield return ReadTexture(s.emission,false);m.SetTexture("_EmissionMap",Texture(s.emission,false));}
                    m.EnableKeyword("_EMISSION");
                }
                if(!string.IsNullOrEmpty(s.normal)){yield return ReadTexture(s.normal,true);m.SetTexture("_BumpMap",Texture(s.normal,true));m.EnableKeyword("_NORMALMAP");}
            }
            using(var r=new BinaryReader(new MemoryStream(files[name+".meshbin"]))) {
                if(r.ReadInt32()!=0x5232334d)throw new IOException("R23 mesh header");int count=r.ReadInt32();
                for(int p=0;p<count;p++) {
                    if(disposed)throw new ObjectDisposedException("R23Visuals");
                    int nv=r.ReadInt32(),nt=r.ReadInt32(),mi=r.ReadInt32();var v=new Vector3[nv];var n=new Vector3[nv];var uv=new Vector2[nv];var indices=new int[nt];
                    for(int j=0;j<nv;j++){v[j]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());n[j]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[j]=new Vector2(r.ReadSingle(),r.ReadSingle());}
                    for(int j=0;j<nt;j++)indices[j]=r.ReadInt32();
                    var mesh=new Mesh();resources.Add(mesh);mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateBounds();mesh.RecalculateTangents();
                    var go=new GameObject(manifest.parts!=null&&p<manifest.parts.Length?manifest.parts[p]+"/"+p:name+"-"+p);go.layer=18;go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mats[mi];
                }
            }
            if(name=="left"||name=="right"){
                yield return Read(name+"-articulation.json");
                articulatedHands.Add(name,new R23GloveArticulation(System.Text.Encoding.UTF8.GetString(files[name+"-articulation.json"]),parent.GetComponentsInChildren<MeshFilter>(true)));
            }
        }
        public void PoseHand(int hand,string profile,bool sword,bool holding,float grip,float trigger,float dt){R23GloveArticulation articulation;if(!disposed&&articulatedHands.TryGetValue(hand==0?"left":"right",out articulation))articulation.Pose(profile,sword,holding,grip,trigger,dt);}
        string TextureKey(string file){return directory+file;}
        IEnumerator ReadTexture(string file,bool linear){if(!sharedTextures.Contains(TextureKey(file),linear))yield return Read(file);}
        Texture2D Texture(string file,bool linear) {return textureOwner.Acquire(TextureKey(file),linear,delegate{var t=new Texture2D(2,2,TextureFormat.RGBA32,false,linear);try{if(!t.LoadImage(files[file],true))throw new IOException(file);t.name="R23 shared "+file+(linear?" [linear]":" [sRGB]");return t;}catch{UnityEngine.Object.Destroy(t);throw;}});}
        public void ReleaseLoadedBytes() { files.Clear(); }
        public void Dispose() {if(disposed)return;disposed=true;articulatedHands.Clear();foreach(var r in resources)if(r!=null)UnityEngine.Object.Destroy(r);resources.Clear();files.Clear();textureOwner.Dispose();}
    }
}
#endif
