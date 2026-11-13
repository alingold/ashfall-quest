#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Activities
{
    // field_builder.gd: eight positional voices at -10dB, lightning loop at -22dB.
    public sealed class R23ConstructionAudio:MonoBehaviour
    {
        [Serializable] sealed class Cue {public string kind=null,file=null;}
        [Serializable] sealed class Manifest {public Cue[] cues=null;}
        readonly Dictionary<string,List<AudioClip>> clips=new Dictionary<string,List<AudioClip>>();
        readonly Ashfall.Integration.Playable.HandsWeapons.R24ItemAudio buildBank=new Ashfall.Integration.Playable.HandsWeapons.R24ItemAudio();
        AudioSource buildVoice;
        readonly AudioSource[] voices=new AudioSource[8];AudioSource lightning;int cursor;float volume=1;
        public string LoadFailure {get;private set;}
        void Awake(){buildVoice=buildBank.Voice(transform,"Build tier cue",-10,48);StartCoroutine(LoadBuild());for(int i=0;i<voices.Length;i++)voices[i]=Voice("Source construction impact "+i,48,8,.31622777f);lightning=Voice("Source construction lightning",12,1,.07943282f);lightning.loop=true;StartCoroutine(Load());}
        AudioSource Voice(string title,float maximum,float minimum,float gain){var child=new GameObject(title);child.transform.SetParent(transform,false);var voice=child.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=1;voice.rolloffMode=AudioRolloffMode.Logarithmic;voice.maxDistance=maximum;voice.minDistance=minimum;voice.volume=gain*volume;return voice;}
        static string Url(string name){string path=Application.streamingAssetsPath+"/PlayableR23/Construction/Audio/"+name;return path.Contains("://")?path:new Uri(path).AbsoluteUri;}
        IEnumerator Load()
        {
            Manifest manifest;
            using(var request=UnityWebRequest.Get(Url("audio.json"))){yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){LoadFailure=request.error;yield break;}manifest=JsonUtility.FromJson<Manifest>(request.downloadHandler.text);}
            foreach(var cue in manifest.cues)using(var request=UnityWebRequestMultimedia.GetAudioClip(Url(cue.file),AudioType.WAV))
            {
                yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){LoadFailure=cue.file+": "+request.error;continue;}
                var clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Decoded(request);if(clip==null){LoadFailure=cue.file+": null clip";continue;}
                List<AudioClip> list;if(!clips.TryGetValue(cue.kind,out list)){list=new List<AudioClip>();clips.Add(cue.kind,list);}clip.name=cue.file;list.Add(clip);
            }
            List<AudioClip> loop;if(clips.TryGetValue("build_lightning",out loop)&&loop.Count>0)lightning.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(loop[0]);
        }
        IEnumerator LoadBuild(){foreach(string cue in new[]{"place","snap","dismantle","upgrade"})yield return buildBank.LoadOptional(cue,"Expansion/Audio/build/"+cue+".wav");}
        public void BuildCue(string cue,Vector3 point){if(cue=="place"){Play(point);return;}if(buildVoice==null)return;buildVoice.transform.position=point;buildBank.Play(buildVoice,cue);}
        public void SetSfxVolume(float value){buildBank.SetVolume(value);volume=float.IsNaN(value)?0:Mathf.Clamp01(value);foreach(var voice in voices)if(voice!=null)voice.volume=.31622777f*volume;if(lightning!=null)lightning.volume=.07943282f*volume;}
        public void Play(Vector3 point,bool breaking=false){List<AudioClip> list;if(!clips.TryGetValue(breaking?"build_break":"build_place",out list)||list.Count==0)return;var voice=voices[cursor];cursor=(cursor+1)%voices.Length;voice.Stop();voice.transform.position=point;voice.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(list[cursor%list.Count]);voice.Play();}
        public void SetLightning(Vector3 point,bool on){if(lightning==null)return;lightning.transform.position=point;if(on&&lightning.clip!=null){if(!lightning.isPlaying)lightning.Play();}else lightning.Stop();}
        public void Stop(){buildBank.Stop();foreach(var voice in voices)if(voice!=null)voice.Stop();if(lightning!=null)lightning.Stop();}
        void OnDisable(){Stop();}
        void OnDestroy(){StopAllCoroutines();buildBank.Dispose();Stop();foreach(var list in clips.Values)foreach(var clip in list)if(clip!=null)Destroy(clip);clips.Clear();}
    }
}
#endif
