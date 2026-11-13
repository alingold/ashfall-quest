#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.UI
{
    // Single optional-pack owner. Android StreamingAssets is read through web requests.
    public sealed class R25LegacyPack : MonoBehaviour
    {
        [Serializable] public sealed class MapRow { public string id="",title="",relativeFolder=""; }
        [Serializable] public sealed class SkinRow { public string id="",label=""; public int sourceIndex=0,originalIndex=0; }
        [Serializable] sealed class Manifest
        { public int version=0,mapCount=0,skinCount=0,audioCount=0; public string owner=""; public MapRow[] maps=new MapRow[0]; public SkinRow[] skins=new SkinRow[0]; public string[] audio=new string[0],files=new string[0]; }
        static Manifest manifest;
        static R25LegacyPack instance;
        static R23UiSettings settings=new R23UiSettings();
        static readonly Dictionary<string,Texture2D> thumbnails=new Dictionary<string,Texture2D>();
        public static string Responses="",Finishes="";
        public static bool Ready {get;private set;}
        public static bool Present {get{return manifest!=null;}}
        public static MapRow[] Maps {get{return Present?manifest.maps:new MapRow[0];}}
        public static SkinRow[] Skins {get{return Present?manifest.skins:new SkinRow[0];}}
        public static R25LegacyPolicy Policy {get{return new R25LegacyPolicy(Present,Maps.Length,Skins.Length,Present?manifest.audio.Length:0);}}
        public static bool MapsEnabled {get{return Policy.Maps(settings);}}
        public static bool SkinsEnabled {get{return Policy.Skins(settings);}}
        public static bool SoundsEnabled {get{return Policy.Sounds(settings);}}
        public static void Apply(R23UiSettings value){settings=value;}
        public static bool IsMap(string id){foreach(var row in Maps)if(row.id==id)return true;return false;}
        public static bool IsSkin(string id){foreach(var row in Skins)if(row.id==id)return true;return false;}
        public static bool SourceArtFile(string file){return Present&&Array.IndexOf(manifest.files,"SourceArt/"+file)>=0;}
        public static string Url(string relative){string path=Application.streamingAssetsPath+"/PlayableR23/"+relative;return path.Contains("://")?path:new Uri(path).AbsoluteUri;}
        public static Texture2D Thumbnail(string key){Texture2D t;return thumbnails.TryGetValue(key,out t)?t:null;}
        public static void Begin(GameObject owner,R23UiSettings value)
        {settings=value;if(instance==null)instance=owner.AddComponent<R25LegacyPack>();}
        IEnumerator Start()
        {
            using(var request=UnityWebRequest.Get(Url("Legacy/manifest.json")))
            {
                request.timeout=15;yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success)
                {
                    Manifest data=null;try{data=JsonUtility.FromJson<Manifest>(request.downloadHandler.text);}catch(ArgumentException){}
                    if(data!=null&&data.version==1&&data.owner=="ashfall.legacy_pack/1"&&data.maps!=null&&data.skins!=null&&data.audio!=null&&data.files!=null&&data.maps.Length<=44&&data.skins.Length<=112&&data.audio.Length<=97)manifest=data;
                }
            }
            if(Present)
            {
                foreach(string file in new[]{"R23AppearanceMaterialResponses.json","R23AppearanceFinishes.json"})
                    using(var request=UnityWebRequest.Get(Url("Legacy/Appearance/"+file)))
                    {yield return request.SendWebRequest();if(request.result==UnityWebRequest.Result.Success){if(file.Contains("Responses"))Responses=request.downloadHandler.text;else Finishes=request.downloadHandler.text;}}
                foreach(string file in manifest.files)
                {
                    if(!file.Contains("/Thumbnails/")||!(file.EndsWith(".jpg")||file.EndsWith(".png")))continue;
                    using(var request=UnityWebRequest.Get(Url("Legacy/"+file)))
                    {yield return request.SendWebRequest();if(request.result==UnityWebRequest.Result.Success){var image=new Texture2D(2,2,TextureFormat.RGBA32,false);if(image.LoadImage(request.downloadHandler.data))thumbnails[System.IO.Path.GetFileNameWithoutExtension(file)]=image;else Destroy(image);}}
                }
                foreach(string file in manifest.audio)
                    using(var request=UnityWebRequestMultimedia.GetAudioClip(Url("Legacy/Audio/"+file),AudioType.WAV))
                    {yield return request.SendWebRequest();if(request.result==UnityWebRequest.Result.Success)legacyAudio[file]=DownloadHandlerAudioClip.GetContent(request);}
            }
            Ready=true;
        }
        static readonly Dictionary<AudioClip,string> originals=new Dictionary<AudioClip,string>();
        static readonly Dictionary<string,AudioClip> legacyAudio=new Dictionary<string,AudioClip>();
        public static AudioClip Decoded(UnityWebRequest request)
        {
            var clip=DownloadHandlerAudioClip.GetContent(request);
            string url=Uri.UnescapeDataString(request.url);int at=url.IndexOf("/PlayableR23/",StringComparison.Ordinal);
            if(clip!=null&&at>=0)originals[clip]=url.Substring(at+13);
            return clip;
        }
        public static AudioClip Resolve(AudioClip clip)
        {
            if(clip==null||!SoundsEnabled)return clip;
            string file;AudioClip legacy;
            return originals.TryGetValue(clip,out file)&&legacyAudio.TryGetValue(file,out legacy)?legacy:clip;
        }
        bool lastSounds;
        void Update()
        {
            // Already-running loops switch as well as newly triggered one-shots.
            if(!Ready||lastSounds==SoundsEnabled)return;lastSounds=SoundsEnabled;
            foreach(var voice in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                var clip=voice.clip;if(clip==null)continue;AudioClip original=clip;
                foreach(var row in originals){AudioClip old;if(legacyAudio.TryGetValue(row.Value,out old)&&old==clip){original=row.Key;break;}}
                var wanted=Resolve(original);if(wanted==clip)continue;
                bool playing=voice.isPlaying;voice.Stop();voice.clip=wanted;if(playing)voice.Play();
            }
        }
        void OnDestroy()
        {
            if(instance!=this)return;instance=null;manifest=null;Ready=false;
            foreach(var t in thumbnails.Values)if(t!=null)Destroy(t);thumbnails.Clear();
            foreach(var c in legacyAudio.Values)if(c!=null)Destroy(c);legacyAudio.Clear();originals.Clear();Responses=Finishes="";
        }
    }
}
#endif
