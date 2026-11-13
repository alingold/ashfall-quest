using System;
#if UNITY_6000_0_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
#endif
namespace Ashfall.Integration.Playable.World
{
    // Godot Image::FORMAT_RGBE9995 is enum 16, four bytes/pixel, no alpha.
    // Its entire representable RGB domain expands exactly into IEEE binary16.
    public static class R23ValleyHdrCodec
    {
        static ushort Half(uint mantissa,int exponent)
        {
            if(mantissa==0)return 0;
            int leading=0;for(uint value=mantissa;value>1;value>>=1)leading++;
            int halfExponent=exponent+leading-9;
            if(halfExponent<=0)return (ushort)(mantissa<<exponent);
            return (ushort)(((uint)halfExponent<<10)|((mantissa-(1u<<leading))<<(10-leading)));
        }
        public static void ExpandRange(byte[] packed,byte[] rgbaHalf,int firstPixel,int pixelCount)
        {
            if(packed==null||rgbaHalf==null||packed.Length%4!=0||rgbaHalf.Length!=(long)packed.Length*2||firstPixel<0||pixelCount<0||(long)firstPixel+pixelCount>packed.Length/4)throw new ArgumentException("RGBE9995 expansion bounds");
            int end=firstPixel+pixelCount;
            for(int i=firstPixel;i<end;i++){
                int p=i*4,d=i*8;uint value=(uint)packed[p]|((uint)packed[p+1]<<8)|((uint)packed[p+2]<<16)|((uint)packed[p+3]<<24);int exponent=(int)(value>>27);
                for(int c=0;c<3;c++){ushort half=Half((value>>(9*c))&511,exponent);rgbaHalf[d+c*2]=(byte)half;rgbaHalf[d+c*2+1]=(byte)(half>>8);}
                rgbaHalf[d+6]=0;rgbaHalf[d+7]=0x3c;
            }
        }
    }
#if UNITY_6000_0_OR_NEWER
    /// <summary>Map-owned original Valley HDR sky, lights and fog; hidden loading changes no globals.</summary>
    public sealed class R23ValleyEnvironment:MonoBehaviour
    {
        public bool Ready {get;private set;}
        public bool Loading {get;private set;}
        public string Failure {get;private set;}
        public bool ActiveEnvironment {get{return applied;}}
        public bool SourceWorldLightsHidden{get{return sourceWorldHidden;}}
        public int ActiveSourceLightCount{get{if(lightRoot==null)return 0;int count=0;foreach(var light in lightRoot.GetComponentsInChildren<Light>(true))if(light.enabled&&light.gameObject.activeInHierarchy)count++;return count;}}
        public int SourceToneMode {get{return manifest==null?-1:manifest.toneMode;}}
        public float SourceExposure {get{return manifest==null?1:manifest.exposure;}}
        public string RenderingLimits {get{return manifest==null?null:manifest.limits;}}
        Manifest manifest;string folder;bool wanted,applied,sourceWorldHidden;Coroutine pending;UnityWebRequest request;byte[] response;
        GameObject lightRoot;Light sun;Material sky;Texture2D panorama;Snapshot previous;
        readonly List<LightState> directionals=new List<LightState>();
        [Serializable] sealed class Manifest
        {
            public int version=0,width=0,height=0,sourceFormat=0,toneMode=0,ambientSource=0,fogMode=0;
            public string mapId="",sourceEncoding="",skyFile="",skySha256="",limits="";
            public float skyEnergy=1,backgroundEnergy=1,exposure=1,ambientEnergy=1,ambientSkyContribution=1,fogEnergy=1,fogDensity=0,fogSkyAffect=0;
            public bool fogEnabled=false;public float[] ambientColor=null,fogColor=null;
            public Sample[] ambientSamples=null;public LightRow[] lights=null;
        }
        [Serializable] sealed class Sample {public float[] direction=null,radiance=null;public float weight=0;}
        [Serializable] sealed class LightRow
        {
            public string name="",sourceClass="";public float[] position=null,forward=null,up=null,color=null;
            public float energy=1,indirectEnergy=1,range=1,shadowBias=0,shadowNormalBias=0;
            public bool visible=true,shadows=false;public long cullMask=-1;
        }
        struct LightState {public Light Light;public bool Enabled;}
        sealed class Snapshot
        {
            public Material Sky;public Light Sun;public AmbientMode AmbientMode;public Color Ambient,SkyColor,Equator,Ground,FogColor;
            public float AmbientIntensity,FogDensity,FogStart,FogEnd;public bool Fog;public FogMode FogMode;public SphericalHarmonicsL2 Probe;
            public Snapshot(){Sky=RenderSettings.skybox;Sun=RenderSettings.sun;AmbientMode=RenderSettings.ambientMode;Ambient=RenderSettings.ambientLight;SkyColor=RenderSettings.ambientSkyColor;Equator=RenderSettings.ambientEquatorColor;Ground=RenderSettings.ambientGroundColor;AmbientIntensity=RenderSettings.ambientIntensity;Probe=RenderSettings.ambientProbe;Fog=RenderSettings.fog;FogMode=RenderSettings.fogMode;FogColor=RenderSettings.fogColor;FogDensity=RenderSettings.fogDensity;FogStart=RenderSettings.fogStartDistance;FogEnd=RenderSettings.fogEndDistance;}
            public void Restore(){RenderSettings.skybox=Sky;RenderSettings.sun=Sun;RenderSettings.ambientMode=AmbientMode;RenderSettings.ambientLight=Ambient;RenderSettings.ambientSkyColor=SkyColor;RenderSettings.ambientEquatorColor=Equator;RenderSettings.ambientGroundColor=Ground;RenderSettings.ambientIntensity=AmbientIntensity;RenderSettings.ambientProbe=Probe;RenderSettings.fog=Fog;RenderSettings.fogMode=FogMode;RenderSettings.fogColor=FogColor;RenderSettings.fogDensity=FogDensity;RenderSettings.fogStartDistance=FogStart;RenderSettings.fogEndDistance=FogEnd;}
        }
        // Accept a map's absolute packaged folder/URL, or e.g. "Maps/valley" relative
        // to StreamingAssets/PlayableR23. Initializing never enables the environment.
        public void Initialize(string mapFolder,bool actualSourceWorldHidden=false)
        {
            DisposeEnvironment();Failure=null;sourceWorldHidden=actualSourceWorldHidden;
            if(string.IsNullOrEmpty(mapFolder)){Failure="Valley environment folder required";return;}
            folder=mapFolder.TrimEnd('/','\\')+"/";
            if(!folder.Contains("://")&&!Path.IsPathRooted(folder))folder=Application.streamingAssetsPath.TrimEnd('/','\\')+"/PlayableR23/"+folder;
            Loading=true;pending=StartCoroutine(LoadSafely());
        }
        IEnumerator Fetch(string file)
        {
            string url=folder+file;if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            request=UnityWebRequest.Get(url);request.timeout=120;yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success)throw new IOException("Valley environment "+file+": "+request.error);
            response=request.downloadHandler.data;request.Dispose();request=null;
        }
        IEnumerator Load()
        {
            yield return Fetch("environment.json");manifest=JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(response));response=null;
            if(manifest==null||manifest.version!=1||manifest.mapId!="valley"||manifest.sourceFormat!=16||manifest.sourceEncoding!="RGBE9995"||manifest.width!=2048||manifest.height!=1024||manifest.skyFile!="sky.rgb9e5"||manifest.lights==null||manifest.lights.Length!=14||manifest.ambientSource!=3||manifest.ambientSamples==null||manifest.ambientSamples.Length!=2048)throw new IOException("Unsupported original Valley environment manifest");
            yield return Fetch(manifest.skyFile);byte[] raw=response;response=null;
            if(raw.Length!=manifest.width*manifest.height*4)throw new IOException("Valley RGBE9995 byte length");
            using(var hash=SHA256.Create()){string actual=BitConverter.ToString(hash.ComputeHash(raw)).Replace("-","").ToLowerInvariant();if(actual!=manifest.skySha256)throw new IOException("Valley panorama source hash");}
            bool packed=SystemInfo.SupportsTextureFormat(TextureFormat.RGB9e5Float);
            if(!packed){if(!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf))throw new IOException("Valley requires a supported HDR texture format");var half=new byte[raw.Length*2];for(int first=0;first<raw.Length/4;first+=32768){R23ValleyHdrCodec.ExpandRange(raw,half,first,Math.Min(32768,raw.Length/4-first));yield return null;}raw=half;}
            panorama=new Texture2D(manifest.width,manifest.height,packed?TextureFormat.RGB9e5Float:TextureFormat.RGBAHalf,true,true);panorama.name="Original Valley linear HDR panorama";panorama.wrapModeU=TextureWrapMode.Repeat;panorama.wrapModeV=TextureWrapMode.Clamp;panorama.filterMode=FilterMode.Trilinear;
            panorama.SetPixelData<byte>(raw,0);panorama.Apply(true,true);raw=null;yield return null;
            var shader=Resources.Load<Shader>("R23ValleySky");if(shader==null)throw new IOException("R23ValleySky resource missing");
            sky=new Material(shader);sky.name="Original R23 Valley panorama";sky.SetTexture("_Panorama",panorama);sky.SetFloat("_Energy",manifest.skyEnergy*manifest.backgroundEnergy*manifest.exposure);sky.SetColor("_FogColor",ColorValue(manifest.fogColor)*manifest.fogEnergy);sky.SetFloat("_SkyFog",manifest.fogEnabled?manifest.fogSkyAffect:0);
            lightRoot=new GameObject("Original Valley environment lights");lightRoot.SetActive(false);lightRoot.transform.SetParent(transform,false);
            foreach(var row in manifest.lights){var go=new GameObject(row.name);go.transform.SetParent(lightRoot.transform,false);go.transform.localPosition=Vector(row.position);go.transform.localRotation=Quaternion.LookRotation(Vector(row.forward),Vector(row.up));var light=go.AddComponent<Light>();light.type=row.sourceClass=="DirectionalLight3D"?LightType.Directional:LightType.Point;light.color=ColorValue(row.color);light.intensity=row.energy;light.bounceIntensity=row.indirectEnergy;light.cullingMask=unchecked((int)row.cullMask);light.shadows=row.shadows?LightShadows.Hard:LightShadows.None;light.shadowBias=row.shadowBias;light.shadowNormalBias=row.shadowNormalBias;light.enabled=row.visible;if(light.type==LightType.Point){light.range=row.range;light.renderMode=LightRenderMode.ForcePixel;}else sun=light;}
            Ready=true;Loading=false;pending=null;if(wanted&&isActiveAndEnabled)Apply();
        }
        IEnumerator LoadSafely()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Load());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception error){Failure=error.ToString();Loading=false;Ready=false;pending=null;ReleaseResources();yield break;}if(!next){var done=stack.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;}
        }
        public void SetActiveEnvironment(bool active){wanted=active;if(!active)Restore();else if(Ready&&isActiveAndEnabled&&!applied)Apply();}
        void Apply()
        {
            if(applied||!Ready)return;previous=new Snapshot();directionals.Clear();
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))if(light.type==LightType.Directional&&!light.transform.IsChildOf(lightRoot.transform)){directionals.Add(new LightState{Light=light,Enabled=light.enabled});light.enabled=false;}
            applied=true;RenderSettings.skybox=sky;RenderSettings.sun=sourceWorldHidden?null:sun;
            // Source ambientSource=SKY. The original color is retained, but at sky
            // contribution1 the actual sky radiance supplies diffuse illumination.
            RenderSettings.ambientMode=AmbientMode.Custom;RenderSettings.ambientLight=ColorValue(manifest.ambientColor)*manifest.ambientEnergy;RenderSettings.ambientIntensity=1;
            var probe=new SphericalHarmonicsL2();foreach(var sample in manifest.ambientSamples)probe.AddDirectionalLight(transform.TransformDirection(Vector(sample.direction)),ColorValue(sample.radiance),sample.weight*manifest.skyEnergy*manifest.ambientEnergy);RenderSettings.ambientProbe=probe;
            RenderSettings.fog=manifest.fogEnabled;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=manifest.fogDensity;RenderSettings.fogColor=ColorValue(manifest.fogColor)*manifest.fogEnergy;
            // sandbox_map_091 hides the original world. Its global environment
            // remains selected, while the world's Node3D lights are invisible.
            // Reuse the actual HDR/ambient/fog without adding another sun.
            sky.SetMatrix("_WorldToSky",Matrix4x4.Rotate(Quaternion.Inverse(transform.rotation)));lightRoot.SetActive(!sourceWorldHidden);
        }
        void Restore()
        {
            if(lightRoot!=null)lightRoot.SetActive(false);if(!applied)return;applied=false;
            if(previous!=null)previous.Restore();previous=null;foreach(var state in directionals)if(state.Light!=null)state.Light.enabled=state.Enabled;directionals.Clear();
        }
        void ReleaseResources(){Restore();if(request!=null){request.Abort();request.Dispose();request=null;}response=null;if(lightRoot!=null)Destroy(lightRoot);if(sky!=null)Destroy(sky);if(panorama!=null)Destroy(panorama);lightRoot=null;sky=null;panorama=null;sun=null;}
        public void DisposeEnvironment(){wanted=false;if(pending!=null){StopCoroutine(pending);pending=null;}ReleaseResources();manifest=null;Ready=false;Loading=false;}
        static Vector3 Vector(float[] value){if(value==null||value.Length!=3)throw new IOException("Valley vector");return new Vector3(value[0],value[1],value[2]);}
        static Color ColorValue(float[] value){if(value==null||value.Length<3)throw new IOException("Valley color");return new Color(value[0],value[1],value[2],value.Length>3?value[3]:1);}
        void OnDisable(){wanted=false;Restore();}
        void OnDestroy(){DisposeEnvironment();}
    }
#endif
}
