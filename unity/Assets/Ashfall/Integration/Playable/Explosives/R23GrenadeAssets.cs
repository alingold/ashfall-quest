#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Explosives
{
    internal sealed class R23GrenadeAssets:IDisposable
    {
        [Serializable]sealed class Manifest{public Surface[] materials=null;public string[] parts=null;}
        [Serializable]sealed class Surface{public float[] color=null,emissionColor=null;public float metallic=0,roughness=1,emissionStrength=1,occlusionStrength=1;public bool doubleSided=false;public string alphaMode=null,baseColor=null,normal=null,metallicMap=null,occlusion=null,emission=null;}
        [Serializable]sealed class AudioManifest{public Cue[] cues=null;}[Serializable]sealed class Cue{public string action=null;public string[] files=null;}
        readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();readonly Dictionary<string,byte[]> files=new Dictionary<string,byte[]>();readonly Dictionary<string,AudioClip> audio=new Dictionary<string,AudioClip>();
        public string Failure {get;private set;}public bool Ready {get;private set;}
        static string Url(string name){string path=Application.streamingAssetsPath+"/PlayableR23/Explosives/"+name;return path.Contains("://")?path:new Uri(path).AbsoluteUri;}
        IEnumerator Read(string file)
        {if(files.ContainsKey(file))yield break;using(var req=UnityWebRequest.Get(Url(file))){req.timeout=30;yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Failure=file+": "+req.error;yield break;}files.Add(file,req.downloadHandler.data);}}
        public IEnumerator Load(Transform frag,Transform zone)
        {
            yield return Model("frag",frag);if(Failure!=null)yield break;yield return Model("zone",zone);if(Failure!=null)yield break;
            yield return Read("audio.json");if(Failure!=null)yield break;var m=JsonUtility.FromJson<AudioManifest>(System.Text.Encoding.UTF8.GetString(files["audio.json"]));
            foreach(var cue in m.cues)using(var req=UnityWebRequestMultimedia.GetAudioClip(Url(cue.files[0]),AudioType.WAV))
            {req.timeout=30;yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Failure=cue.action+": "+req.error;yield break;}var clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Decoded(req);clip.name=cue.action;resources.Add(clip);audio.Add(cue.action,clip);}
            files.Clear();Ready=true;
        }
        public void TrackArt(Transform root){foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))resources.Add(f.sharedMesh);foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))foreach(var m in renderer.sharedMaterials)if(!resources.Contains(m))resources.Add(m);}
        public AudioClip Clip(string action){AudioClip c;return audio.TryGetValue(action,out c)?Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(c):null;}
        IEnumerator Model(string name,Transform parent)
        {
            yield return Read(name+".json");yield return Read(name+".meshbin");if(Failure!=null)yield break;
            var m=JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(files[name+".json"]));var materials=new Material[m.materials.Length];
            for(int i=0;i<materials.Length;i++)
            {
                var s=m.materials[i];var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));resources.Add(mat);materials[i]=mat;mat.SetColor("_BaseColor",new Color(s.color[0],s.color[1],s.color[2],s.color[3]));mat.SetFloat("_Metallic",s.metallic);mat.SetFloat("_Smoothness",1-s.roughness);mat.SetFloat("_Cull",s.doubleSided?0:2);
                if(s.alphaMode=="BLEND"){mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;}
                mat.SetFloat("_OcclusionStrength",s.occlusionStrength);if(s.emissionColor!=null){mat.SetColor("_EmissionColor",new Color(s.emissionColor[0],s.emissionColor[1],s.emissionColor[2])*s.emissionStrength);mat.EnableKeyword("_EMISSION");}
                foreach(string field in new[]{"baseColor","normal","metallicMap","occlusion","emission"})
                {
                    string file=field=="baseColor"?s.baseColor:field=="normal"?s.normal:field=="metallicMap"?s.metallicMap:field=="emission"?s.emission:s.occlusion;if(String.IsNullOrEmpty(file))continue;
                    yield return Read(file);if(Failure!=null)yield break;var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,field!="baseColor"&&field!="emission");resources.Add(tex);if(!tex.LoadImage(files[file],true)){Failure=file+" invalid image";yield break;}
                    mat.SetTexture(field=="baseColor"?"_BaseMap":field=="normal"?"_BumpMap":field=="metallicMap"?"_MetallicGlossMap":field=="emission"?"_EmissionMap":"_OcclusionMap",tex);
                    if(field=="normal")mat.EnableKeyword("_NORMALMAP");if(field=="metallicMap"){mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Smoothness",1);}
                }
            }
            using(var r=new BinaryReader(new MemoryStream(files[name+".meshbin"])))
            {
                if(r.ReadInt32()!=0x5232334d)throw new IOException("source grenade mesh header");int count=r.ReadInt32();
                for(int i=0;i<count;i++)
                {
                    int nv=r.ReadInt32(),ni=r.ReadInt32(),mi=r.ReadInt32();var v=new Vector3[nv];var n=new Vector3[nv];var uv=new Vector2[nv];var ix=new int[ni];
                    for(int j=0;j<nv;j++){v[j]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());n[j]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[j]=new Vector2(r.ReadSingle(),r.ReadSingle());}for(int j=0;j<ni;j++)ix[j]=r.ReadInt32();
                    var mesh=new Mesh();resources.Add(mesh);mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=ix;mesh.RecalculateBounds();mesh.RecalculateTangents();
                    var go=new GameObject(m.parts[i]+"/"+i);go.layer=18;go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=materials[mi];
                }
            }
        }
        public void Dispose(){foreach(var o in resources)if(o!=null)UnityEngine.Object.Destroy(o);resources.Clear();files.Clear();audio.Clear();Ready=false;}
    }
}
#endif
