#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Ashfall.Integration.Playable.UI
{
    // Three owned 2D voices and three exact source WAVs; missing cues stay silent.
    public sealed class R23MenuAudio : MonoBehaviour
    {
        readonly AudioSource[] voices=new AudioSource[3];
        readonly AudioClip[] clips=new AudioClip[3];
        static readonly string[] Files={"isolated_menu_hover_00.wav","isolated_menu_click_00.wav","isolated_vr_initialize_00.wav"};
        bool initialized;
        float gain;
        public bool Loading {get;private set;}
        public string Failure {get;private set;}
        public int LoadedCueCount {get{int count=0;foreach(var clip in clips)if(clip!=null)count++;return count;}}
        public bool HasCue(R23MenuCue cue){return clips[(int)cue]!=null;}
        public void Initialize(float sfxVolume)
        {
            if(initialized)throw new InvalidOperationException("Menu audio already initialized");
            initialized=true;SetSfxVolume(sfxVolume);
            for(int i=0;i<voices.Length;i++)
            {
                var child=new GameObject("R23 menu source "+(R23MenuCue)i);child.transform.SetParent(transform,false);
                var voice=child.AddComponent<AudioSource>();voices[i]=voice;voice.playOnAwake=false;voice.loop=false;
                voice.spatialBlend=0;voice.dopplerLevel=0;voice.volume=gain;voice.priority=i==2?64:128;voice.ignoreListenerPause=true;
            }
            StartCoroutine(Load());
        }
        public void SetSfxVolume(float value)
        {gain=R23MenuAudioEdges.Gain(value);foreach(var voice in voices)if(voice!=null)voice.volume=gain;}
        static string Url(string file)
        {string path=Application.streamingAssetsPath+"/PlayableR23/MenuAudio/"+file;return path.Contains("://")?path:new Uri(path).AbsoluteUri;}
        IEnumerator Load()
        {
            Loading=true;
            for(int i=0;i<Files.Length;i++)
            {
                using(var request=UnityWebRequestMultimedia.GetAudioClip(Url(Files[i]),AudioType.WAV))
                {
                    request.timeout=10;yield return request.SendWebRequest();
                    if(request.result!=UnityWebRequest.Result.Success)
                    {Failure=Files[i]+": "+request.error;Debug.LogWarning("R23 source menu audio unavailable: "+Failure);continue;}
                    var clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Decoded(request);
                    if(clip==null){Failure=Files[i]+": no decoded clip";continue;}
                    clip.name=Files[i];clips[i]=clip;
                }
            }
            Loading=false;
        }
        public void Play(R23MenuCue cue)
        {
            int index=(int)cue;if(!initialized||!isActiveAndEnabled||clips[index]==null||gain<=0)return;
            var voice=voices[index];voice.Stop();voice.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(clips[index]);voice.Play();
        }
        public void Stop(){foreach(var voice in voices)if(voice!=null){voice.Stop();voice.clip=null;}}
        void OnDisable(){Stop();}
        void OnDestroy(){StopAllCoroutines();Stop();foreach(var clip in clips)if(clip!=null)Destroy(clip);}
    }
}
#endif
