#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Source WAV routes, including explicitly attributed original procedural cue bakes.
    internal sealed class R23Audio : IDisposable
    {
        [Serializable] sealed class Manifest { public Cue[] cues=null; }
        [Serializable] sealed class Cue { public string weapon=null,action=null;public string[] files=null; }
        readonly Dictionary<string,AudioClip[]> clips=new Dictionary<string,AudioClip[]>();
        readonly List<AudioClip> resources=new List<AudioClip>();
        readonly List<GameObject> voices=new List<GameObject>();
        readonly Dictionary<AudioSource,float> sourceGains=new Dictionary<AudioSource,float>();
        float sfxVolume=1;
        readonly AudioSource[] shots=new AudioSource[6];
        readonly AudioSource mechanical,feedback;
        int cursor,variant;
        public R23Audio(Transform weapon,Transform muzzle) {
            for(int i=0;i<shots.Length;i++)shots[i]=Voice(muzzle,"R23 source shot voice "+i,85,2,.3548f,true);
            mechanical=Voice(weapon,"R23 source reload voice",14,1,.1995f,true);
            feedback=Voice(weapon,"R23 source hit feedback",14,1,.1995f,false);
        }
        AudioSource Voice(Transform parent,string name,float maximum,float minimum,float volume,bool spatial) {
            var go=new GameObject(name);go.transform.SetParent(parent,false);voices.Add(go);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=spatial?1:0;source.rolloffMode=AudioRolloffMode.Logarithmic;source.maxDistance=maximum;source.minDistance=minimum;sourceGains.Add(source,volume);source.volume=volume*sfxVolume;return source;
        }
        public void SetSfxVolume(float value) { sfxVolume=Mathf.Clamp01(value);foreach(var pair in sourceGains)if(pair.Key!=null)pair.Key.volume=pair.Value*sfxVolume; }
        static string Url(string name) { string path=Application.streamingAssetsPath+"/PlayableR23/HandsWeapons/"+name;return path.Contains("://")?path:new Uri(path).AbsoluteUri; }
        public IEnumerator Load(string profile=null) {
            Manifest manifest;
            using(var request=UnityWebRequest.Get(Url("audio.json"))) {
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){Debug.LogWarning("R23 source audio manifest unavailable: "+request.error);yield break;}
                manifest=JsonUtility.FromJson<Manifest>(request.downloadHandler.text);
            }
            foreach(var cue in manifest.cues) {
                if(profile!=null&&!string.IsNullOrEmpty(cue.weapon)&&cue.weapon!=profile)continue;
                var loaded=new List<AudioClip>();
                foreach(var file in cue.files)using(var request=UnityWebRequestMultimedia.GetAudioClip(Url(file),AudioType.WAV)) {
                    yield return request.SendWebRequest();
                    if(request.result!=UnityWebRequest.Result.Success){Debug.LogWarning("R23 source audio "+file+": "+request.error);continue;}
                    var clip=DownloadHandlerAudioClip.GetContent(request);clip.name=file;loaded.Add(clip);resources.Add(clip);
                }
                clips.Add(cue.weapon+"/"+cue.action,loaded.ToArray());
            }
        }
        public void Play(string weapon,string action,bool shot) {
            if(string.IsNullOrEmpty(action))return;
            AudioClip[] choices;if(!clips.TryGetValue(weapon+"/"+action,out choices)&&!clips.TryGetValue("/"+action,out choices))return;
            if(choices.Length==0)return;var source=shot?shots[cursor++%shots.Length]:(action=="hit"||action=="hurt"?feedback:mechanical);
            float gain=shot?(weapon=="m60"?.5623413f:weapon=="awp"?.8912509f:.3548f):(weapon=="m60"||weapon=="awp"?.31622777f:.1995f);sourceGains[source]=gain;source.volume=gain*sfxVolume;if(shot)source.maxDistance=weapon=="awp"?120:85;source.Stop();source.clip=choices[variant++%choices.Length];source.Play();
        }
        public void Stop() { foreach(var voice in voices)if(voice!=null){var source=voice.GetComponent<AudioSource>();source.Stop();source.clip=null;} }
        public void Dispose() { Stop();foreach(var voice in voices)if(voice!=null)UnityEngine.Object.Destroy(voice);foreach(var clip in resources)UnityEngine.Object.Destroy(clip);voices.Clear();resources.Clear();clips.Clear();sourceGains.Clear(); }
    }
}
#endif
