#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using UnityEngine;using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    internal sealed class R24ItemAudio:IDisposable
    {
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();readonly List<AudioSource> voices=new List<AudioSource>();readonly Dictionary<AudioSource,float> gains=new Dictionary<AudioSource,float>();float volume=1;
        public IEnumerator Load(params string[] names){foreach(string name in names){string path=Application.streamingAssetsPath+"/PlayableR23/HandsWeapons/"+name+".wav";if(!path.Contains("://"))path=new Uri(path).AbsoluteUri;using(var req=UnityWebRequestMultimedia.GetAudioClip(path,AudioType.WAV)){yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success)throw new System.IO.IOException(name+": "+req.error);var clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Decoded(req);clip.name=name;clips.Add(name,clip);}}}
        public IEnumerator LoadOptional(string key,string relativePath)
        {
            string path=Application.streamingAssetsPath+"/PlayableR23/"+relativePath;
            if(!path.Contains("://"))path=new Uri(path).AbsoluteUri;
            using(var req=UnityWebRequestMultimedia.GetAudioClip(path,AudioType.WAV))
            {
                req.timeout=5;yield return req.SendWebRequest();
                if(req.result!=UnityWebRequest.Result.Success)yield break;
                AudioClip clip=null;
                try{clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Decoded(req);}catch(Exception){ }
                if(clip!=null){clip.name=key;clips[key]=clip;}
            }
        }
        public AudioSource Voice(Transform parent,string name,float db,float maxDistance,bool loop=false){var go=new GameObject(name);go.transform.SetParent(parent,false);var voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=1;voice.loop=loop;voice.rolloffMode=AudioRolloffMode.Logarithmic;voice.minDistance=1;voice.maxDistance=maxDistance;voices.Add(voice);gains[voice]=Mathf.Pow(10,db/20);voice.volume=gains[voice]*volume;return voice;}
        public void ReleaseVoice(AudioSource voice){if(voice==null)return;voices.Remove(voice);gains.Remove(voice);voice.Stop();UnityEngine.Object.Destroy(voice.gameObject);}
        public void SetVolume(float value){volume=Mathf.Clamp01(value);foreach(var pair in gains)if(pair.Key!=null)pair.Key.volume=pair.Value*volume;}
        public AudioClip Clip(string name){AudioClip value;return clips.TryGetValue(name,out value)?value:null;}
        public void Play(AudioSource voice,string name,float offset=0){if(voice==null)return;voice.Stop();voice.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(Clip(name));if(voice.clip==null)return;voice.time=Mathf.Clamp(offset,0,Mathf.Max(0,voice.clip.length-.001f));voice.Play();}
        public void Stop(){foreach(var v in voices)if(v!=null)v.Stop();}
        public void Dispose(){Stop();foreach(var v in voices)if(v!=null)UnityEngine.Object.Destroy(v.gameObject);foreach(var clip in clips.Values)UnityEngine.Object.Destroy(clip);voices.Clear();clips.Clear();gains.Clear();}
    }
}
#endif
