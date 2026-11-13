#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.World.Destructibles
{
    // Exact archive_panel_r8 source cues; bounded positional voices, no listener.
    public sealed class R24MapImpactAudio:MonoBehaviour
    {
        readonly AudioClip[] clips=new AudioClip[3];readonly AudioSource[] voices=new AudioSource[8];int cursor;float volume=1;bool retired;
        public string Failure {get;private set;}
        public int LoadedClips {get{int n=0;foreach(var clip in clips)if(clip!=null)n++;return n;}}
        void Awake(){for(int i=0;i<voices.Length;i++){var go=new GameObject("R24 source map break voice "+i);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=5;source.maxDistance=35;source.volume=.7079458f*volume;voices[i]=source;}StartCoroutine(Load());}
        IEnumerator Load()
        {
            string[] names={"glass.wav","wood.wav","metal.wav"};for(int i=0;i<names.Length;i++)
            {
                string path=Application.streamingAssetsPath+"/PlayableR23/WorldAudio/"+names[i];if(!path.Contains("://"))path=new Uri(path).AbsoluteUri;
                using(var request=UnityWebRequestMultimedia.GetAudioClip(path,AudioType.WAV)){yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Failure=names[i]+": "+request.error;continue;}clips[i]=DownloadHandlerAudioClip.GetContent(request);}
            }
        }
        public void SetSfxVolume(float value){volume=float.IsNaN(value)?0:Mathf.Clamp01(value);foreach(var voice in voices)if(voice!=null)voice.volume=.7079458f*volume;}
        public void Play(R23ArchivePanelDefinition row,Vector3 point){if(retired||row==null)return;int index=row.sound=="res://assets/audio/sfx/r18/metal_grate.wav"?2:row.glass?0:1;if(clips[index]==null)return;var source=voices[cursor++%voices.Length];source.Stop();source.transform.position=point;source.clip=clips[index];source.Play();}
        public void Retire(){if(retired)return;retired=true;StopAllCoroutines();foreach(var source in voices)if(source!=null){source.Stop();Destroy(source.gameObject);}foreach(var clip in clips)if(clip!=null)Destroy(clip);}
        void OnDestroy(){Retire();}
    }
}
#endif
