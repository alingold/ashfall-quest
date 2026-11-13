#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Consumables
{
    // Caller supplies accepted events. This component never polls or mutates gameplay.
    public sealed class R23ConsumableAudio:MonoBehaviour
    {
        [Serializable] sealed class Manifest {public Cue[] cues=null;}
        [Serializable] sealed class Cue {public string kind=null,action=null;public string[] files=null;}
        readonly Dictionary<string,AudioClip[]> cues=new Dictionary<string,AudioClip[]>();
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly R23ConsumableAudioPolicy policy=new R23ConsumableAudioPolicy();
        readonly AudioSource[] ui=new AudioSource[2];AudioSource physical,shake;Coroutine loading;
        Transform actor;float sfx=1;int uiCursor;bool enabledByHost,shakePaused,initialized;
        public bool Ready {get;private set;}public string Failure {get;private set;}
        public int LoadedClipCount {get{return clips.Count;}}
        public int PlayedCueCount {get;private set;}public int ShakeStartCount {get;private set;}
        public bool ShakePaused {get{return shakePaused;}}public float ShakeSecondsLeft {get{return policy.ShakeSecondsLeft;}}
        public string LastAction {get;private set;}public string LastKind {get;private set;}
        public void Initialize(Transform actorRoot)
        {
            if(initialized)throw new InvalidOperationException("Consumable audio already initialized");
            if(actorRoot==null)throw new ArgumentNullException("actorRoot");initialized=true;actor=actorRoot;
            for(int i=0;i<ui.Length;i++)ui[i]=Voice("R23 consumable UI "+i,false,12,R23ConsumableAudioPolicy.UiGain);
            physical=Voice("R23 consumable action",true,12,R23ConsumableAudioPolicy.PhysicalGain);
            shake=Voice("R23 consumable shake",true,8,R23ConsumableAudioPolicy.PhysicalGain);
            loading=StartCoroutine(Load());
        }
        AudioSource Voice(string name,bool spatial,float maximum,float gain)
        {
            // Source action audio keeps its event world position; walking the actor must not drag it.
            var go=new GameObject(name);go.transform.position=actor.position;var voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;
            voice.loop=false;voice.spatialBlend=spatial?1:0;voice.dopplerLevel=0;voice.rolloffMode=AudioRolloffMode.Logarithmic;
            voice.minDistance=1;voice.maxDistance=maximum;voice.volume=gain*sfx;return voice;
        }
        static bool Finite(Vector3 p){return !float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);}
        bool Allowed {get{return initialized&&enabledByHost&&isActiveAndEnabled;}}
        public void SetEnabled(bool value){enabledByHost=value;if(!value)StopVoices();}
        public void SetSfxVolume(float value)
        {
            sfx=float.IsNaN(value)?0:Mathf.Clamp01(value);
            foreach(var voice in ui)if(voice!=null)voice.volume=R23ConsumableAudioPolicy.UiGain*sfx;
            if(physical!=null)physical.volume=R23ConsumableAudioPolicy.PhysicalGain*sfx;if(shake!=null)shake.volume=R23ConsumableAudioPolicy.PhysicalGain*sfx;
        }
        // Tick before this frame's samples, matching source _process. Pass the live held-prop center.
        public void Tick(float seconds,Vector3 heldWorldPosition,bool enabled)
        {
            SetEnabled(enabled);if(!Allowed)return;policy.Tick(seconds);
            if(!Finite(heldWorldPosition)){StopShake();return;}shake.transform.position=heldWorldPosition;
            if(!policy.ShakeActive&&!shakePaused&&shake.isPlaying){shake.Pause();shakePaused=true;}
        }
        public bool Notify(R23ConsumableAudioEvent value,string kind,Vector3 worldPosition)
        {
            if(!Allowed||!Finite(worldPosition))return false;R23ConsumableCue cue;if(!policy.TryCue(value,kind,out cue))return false;
            if(cue.StopShake)StopShake();LastAction=cue.Action;LastKind=kind;
            var clip=Find(cue.Kind,cue.Action,cue.Variant);if(clip==null)return false;
            var voice=cue.Spatial?physical:ui[uiCursor];if(!cue.Spatial)uiCursor=(uiCursor+1)%ui.Length;
            voice.Stop();voice.transform.position=worldPosition;voice.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(clip);voice.Play();PlayedCueCount++;return true;
        }
        // Call ONLY in a real shaker preparation phase after the actual motion/input qualifies.
        public bool ShakeMotion(Vector3 heldWorldPosition)
        {
            if(!Allowed||!Finite(heldWorldPosition))return false;policy.ShakeMotion();return PlayShake(heldWorldPosition);
        }
        public bool SampleShake(Vector3 actorLocalHand,Vector3 heldWorldPosition,float seconds,bool preparingShaker)
        {
            if(!Allowed||!Finite(heldWorldPosition))return false;
            if(!policy.SampleShake(new Ashfall.Core.Float3(actorLocalHand.x,actorLocalHand.y,actorLocalHand.z),seconds,preparingShaker))return false;
            return PlayShake(heldWorldPosition);
        }
        bool PlayShake(Vector3 position)
        {
            shake.transform.position=position;if(shakePaused){shake.UnPause();shakePaused=false;}
            if(shake.isPlaying)return true;var clip=Find("shaker","consume_shake",0);if(clip==null)return false;
            shake.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(clip);shake.Play();ShakeStartCount++;return true;
        }
        AudioClip Find(string kind,string action,int variant)
        {AudioClip[] list;return cues.TryGetValue(kind+"/"+action,out list)&&list.Length>0?list[variant%list.Length]:null;}
        static string Url(string file)
        {string path=Application.streamingAssetsPath+"/PlayableR23/Consumables/Audio/"+file;return path.Contains("://")?path:new Uri(path).AbsoluteUri;}
        IEnumerator Load()
        {
            Manifest manifest;using(var request=UnityWebRequest.Get(Url("audio.json")))
            {request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Fail(request.error);yield break;}manifest=JsonUtility.FromJson<Manifest>(request.downloadHandler.text);}
            if(manifest==null||manifest.cues==null){Fail("Invalid consumable audio manifest");yield break;}
            foreach(var cue in manifest.cues)
            {
                var loaded=new List<AudioClip>();foreach(var file in cue.files)
                {
                    AudioClip clip;if(!clips.TryGetValue(file,out clip))using(var request=UnityWebRequestMultimedia.GetAudioClip(Url(file),AudioType.WAV))
                    {request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Fail(file+": "+request.error);continue;}clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Decoded(request);if(clip==null){Fail(file+": null decoded clip");continue;}clip.name=file;clips.Add(file,clip);}
                    loaded.Add(clip);
                }
                cues[cue.kind+"/"+cue.action]=loaded.ToArray();
            }
            Ready=Failure==null;loading=null;
        }
        void Fail(string message){Failure=message;Debug.LogWarning("R23 consumable audio: "+message);}
        void StopShake(){policy.StopShake();if(shake!=null){shake.Stop();shake.clip=null;}shakePaused=false;}
        void StopVoices(){StopShake();if(physical!=null){physical.Stop();physical.clip=null;}foreach(var voice in ui)if(voice!=null){voice.Stop();voice.clip=null;}}
        public void Suspend(){enabledByHost=false;StopVoices();}
        public void ResetRound(){Suspend();policy.Reset();PlayedCueCount=ShakeStartCount=uiCursor=0;LastAction=LastKind=null;}
        void OnDisable(){Suspend();}
        void OnApplicationPause(bool paused){if(paused)Suspend();}
        // Host passes true OpenXR session focus; desktop mirror focus is not an authority.
        void OnDestroy()
        {
            if(loading!=null)StopCoroutine(loading);StopVoices();foreach(var voice in ui)if(voice!=null)Destroy(voice.gameObject);
            if(physical!=null)Destroy(physical.gameObject);if(shake!=null)Destroy(shake.gameObject);
            foreach(var clip in clips.Values)if(clip!=null)Destroy(clip);clips.Clear();cues.Clear();Ready=false;
        }
    }
}
#endif
