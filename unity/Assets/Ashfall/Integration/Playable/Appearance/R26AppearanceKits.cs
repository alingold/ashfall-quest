#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Appearance
{
    // R26 (playtest 2026-11-15 ROOM-APPEARANCE): one decoded kit per outfit, shared by the
    // local body, the menu podium and every remote avatar wearing it. A live outfit change
    // in a room decodes each kit once per headset; PNGs decode on Unity's
    // DownloadHandlerTexture worker thread and mesh parsing is sliced across frames.
    public sealed partial class R23PlayerAppearance
    {
        const long KitSliceMilliseconds=4;const int RetainedKits=1;const float RetainKitSeconds=20;
        static readonly Dictionary<string,Kit> kits=new Dictionary<string,Kit>(StringComparer.Ordinal);
        static readonly HashSet<string> buildingKits=new HashSet<string>(StringComparer.Ordinal);
        static readonly List<Kit> retainedKits=new List<Kit>(RetainedKits+1);
        static int purgedFrame=-1;
        public static int CachedKitCount{get{return kits.Count;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetKits(){kits.Clear();buildingKits.Clear();retainedKits.Clear();purgedFrame=-1;}
        static Kit AcquireKit(string id)
        {
            Kit kit;if(!kits.TryGetValue(id,out kit))return null;
            retainedKits.Remove(kit);if(!kit.Alive){kits.Remove(id);return null;}
            kit.References++;return kit;
        }
        Kit BeginKit(string id){buildingKits.Add(id);return new Kit(id);}
        static Kit CompleteKit(Kit kit)
        {
            buildingKits.Remove(kit.Id);
            var existing=AcquireKit(kit.Id);if(existing!=null){kit.Dispose();return existing;}
            kits[kit.Id]=kit;kit.References=1;return kit;
        }
        void AbandonPending(){if(pendingKit==null)return;buildingKits.Remove(pendingKit.Id);pendingKit.Dispose();pendingKit=null;}
        // Deactivation stops coroutines: release a half-built kit (others may be waiting on it)
        // and restart the same request when this avatar is enabled again.
        string suspendedStyle;
        void SuspendLoad(){if(!Loading)return;suspendedStyle=RequestedStyleId;loadSerial++;CancelRead();AbandonPending();loading=null;Loading=false;}
        void ResumeLoad(){if(suspendedStyle==null||root==null)return;string id=suspendedStyle;suspendedStyle=null;RequestStyle(id);}
        // Last release keeps at most RetainedKits unused kits for RetainKitSeconds (browse back,
        // or a remote switching back), then frees textures/meshes; Quest memory stays bounded.
        static void ReleaseKit(Kit kit)
        {
            if(kit==null)return;kit.References--;if(kit.References>0)return;
            Kit cached;if(!kits.TryGetValue(kit.Id,out cached)||!ReferenceEquals(cached,kit)||!kit.Alive){if(ReferenceEquals(cached,kit))kits.Remove(kit.Id);kit.Dispose();return;}
            kit.ReleasedAt=Time.unscaledTime;retainedKits.Remove(kit);retainedKits.Add(kit);
            while(retainedKits.Count>RetainedKits)EvictKit(0);
        }
        static void EvictKit(int index)
        {var kit=retainedKits[index];retainedKits.RemoveAt(index);Kit cached;if(kits.TryGetValue(kit.Id,out cached)&&ReferenceEquals(cached,kit))kits.Remove(kit.Id);kit.Dispose();}
        static void PurgeKits()
        {
            if(retainedKits.Count==0||purgedFrame==Time.frameCount)return;purgedFrame=Time.frameCount;
            float now=Time.unscaledTime;for(int i=retainedKits.Count-1;i>=0;i--)if(now-retainedKits[i].ReleasedAt>RetainKitSeconds)EvictKit(i);
        }
        IEnumerator ReadTexture(string id,string file,bool linear,Dictionary<string,byte[]> files,int serial)
        {
            var kit=pendingKit;if(kit==null||kit.HasTexture(file))yield break;
            var parameters=DownloadedTextureParams.Default;parameters.mipmapChain=true;parameters.readable=false;parameters.linearColorSpace=linear;
            Texture2D texture=null;
            using(var request=UnityWebRequestTexture.GetTexture(StyleUrl(id,file),parameters))
            {
                pendingRead=request;request.timeout=30;yield return request.SendWebRequest();
                if(ReferenceEquals(pendingRead,request))pendingRead=null;
                if(serial!=loadSerial)yield break;
                if(request.result==UnityWebRequest.Result.Success)try{texture=DownloadHandlerTexture.GetContent(request);}catch(Exception){texture=null;}
            }
            if(texture!=null){kit.AddTexture(file,texture);yield break;}
            // Same bytes through the original main-thread decoder if the texture handler refuses a file.
            yield return Read(id,file,files,serial);
        }
        sealed class KitPart
        {
            public readonly string Label;public readonly Mesh Mesh;public readonly Material Material;
            public KitPart(string label,Mesh mesh,Material material){Label=label;Mesh=mesh;Material=material;}
        }
        sealed partial class Kit
        {
            public readonly string Id;public int References;public float ReleasedAt;
            public readonly List<KitPart> Parts=new List<KitPart>();public readonly List<string> AbsentParts=new List<string>();
            public Kit(string id){Id=id;}
            public bool Alive
            {get{if(Materials==null||Parts.Count==0)return false;foreach(var m in Materials)if(m==null)return false;foreach(var part in Parts)if(part.Mesh==null||part.Material==null)return false;return true;}}
            bool HasPart(string label){foreach(var part in Parts)if(part.Label==label)return true;return false;}
            public bool HasTexture(string file){return textures.ContainsKey(file);}
            public void AddTexture(string file,Texture2D value){resources.Add(value);FinishTexture(value,file);textures.Add(file,value);}
            static void FinishTexture(Texture2D value,string file){value.name=file;value.wrapMode=TextureWrapMode.Repeat;value.filterMode=FilterMode.Trilinear;value.anisoLevel=4;}
            internal static object ResponseFor(Manifest profile,int index){return SurfaceResponse(profile,index);}
        }
        // Per-avatar instance of a shared kit: transforms and renderers only (no decode, no mesh build).
        // Root/Profile/Materials stay fields and SurfaceResponse stays here: editor smokes read them by reflection.
        sealed class Asset:IDisposable
        {
            public GameObject Root;public readonly Kit Kit;bool released;
            public readonly Dictionary<string,Transform> Parts=new Dictionary<string,Transform>(StringComparer.Ordinal);
            public readonly Manifest Profile;public readonly Material GloveMaterial;public readonly Material[] Materials;
            static object SurfaceResponse(Manifest profile,int index){return Kit.ResponseFor(profile,index);}
            public Asset(Kit kit)
            {
                Kit=kit;Profile=kit.Profile;GloveMaterial=kit.GloveMaterial;Materials=kit.Materials;Root=new GameObject("R23 appearance "+kit.Profile.id);Root.SetActive(false);
                for(int p=0;p<kit.Parts.Count;p++)
                {
                    var source=kit.Parts[p];Transform group;bool exists=Parts.TryGetValue(source.Label,out group);
                    var part=new GameObject(exists?source.Label+" source surface "+p:source.Label);part.transform.SetParent(exists?group:Root.transform,false);
                    part.AddComponent<MeshFilter>().sharedMesh=source.Mesh;part.AddComponent<MeshRenderer>().sharedMaterial=source.Material;if(!exists)Parts.Add(source.Label,part.transform);
                }
                // Actual R23 apply_style empties missing variant pieces (Witch Doctor kits have no ChestPlate).
                foreach(string label in kit.AbsentParts){var empty=new GameObject(label+" (absent in source)");empty.transform.SetParent(Root.transform,false);Parts.Add(label,empty.transform);}
                Parts["AvatarHead"].gameObject.SetActive(false);
            }
            public void Dispose()
            {if(Root!=null){Root.SetActive(false);UnityEngine.Object.Destroy(Root);}Root=null;Parts.Clear();if(!released){released=true;ReleaseKit(Kit);}}
        }
    }
}
#endif
