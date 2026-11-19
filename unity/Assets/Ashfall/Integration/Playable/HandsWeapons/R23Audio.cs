#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Source WAV routes, including explicitly attributed original procedural cue bakes.
    internal sealed partial class R23Audio : IDisposable
    {
        [Serializable] sealed class Manifest { public Cue[] cues=null; }
        [Serializable] sealed class Cue { public string weapon=null,action=null;public string[] files=null; }
        readonly Dictionary<string,AudioClip[]> clips=new Dictionary<string,AudioClip[]>();
        readonly List<AudioClip> resources=new List<AudioClip>();
        readonly List<GameObject> voices=new List<GameObject>();
        readonly Dictionary<AudioSource,float> sourceGains=new Dictionary<AudioSource,float>();
        float sfxVolume=1;
        internal Action<byte> AwpCuePlayed;
        readonly AudioSource[] shots=new AudioSource[16]; // 16 so reverb tails (to ~1.9 s) fade out before a voice is reused in automatic fire
        readonly AudioSource mechanical,feedback,pickup;readonly AudioSource[] playerHurt;readonly R24PlayerHurtCueRules hurtSlots=new R24PlayerHurtCueRules();
        int cursor,variant;readonly System.Random dt11MechanicalPitch=new System.Random();
        public R23Audio(Transform weapon,Transform muzzle,Transform actualPlayerAudioRoot=null) {
            for(int i=0;i<shots.Length;i++)shots[i]=Voice(muzzle,"R23 source shot voice "+i,85,2,.3548f,true);
            pickup=Voice(actualPlayerAudioRoot==null?weapon:actualPlayerAudioRoot,"R28 weapon pickup",14,1,.39810717f,false);
            mechanical=Voice(weapon,"R23 source reload voice",14,1,.1995f,true);
            feedback=Voice(weapon,"R23 source hit feedback",14,1,.1995f,false);
            playerHurt=new AudioSource[actualPlayerAudioRoot==null?0:R24PlayerHurtCueRules.VoiceCount];for(int i=0;i<playerHurt.Length;i++)playerHurt[i]=Voice(actualPlayerAudioRoot,"Actor source hurt cue "+i,14,1,R24PlayerHurtCueRules.SourceGain,false);
        }
        AudioSource Voice(Transform parent,string name,float maximum,float minimum,float volume,bool spatial) {
            var go=new GameObject(name);go.transform.SetParent(parent,false);voices.Add(go);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=spatial?1:0;source.rolloffMode=AudioRolloffMode.Logarithmic;source.maxDistance=maximum;source.minDistance=minimum;sourceGains.Add(source,volume);source.volume=volume*sfxVolume;return source;
        }
        public void SetSfxVolume(float value) { sfxVolume=Mathf.Clamp01(value);foreach(var pair in sourceGains)if(pair.Key!=null)R26AwpAudio.SetVolume(pair.Key,pair.Value*sfxVolume); }
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
                    var clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Decoded(request);clip.name=file;loaded.Add(clip);resources.Add(clip);
                }
                clips.Add(cue.weapon+"/"+cue.action,loaded.ToArray());
            }
            yield return LoadPickup();
        }
        // Reuse the existing swap bank and its retired counterpart. Load even when
        // a weapon-specific profile is requested; pickup is common to every gun.
        IEnumerator LoadPickup() {
            using(var request=UnityWebRequestMultimedia.GetAudioClip(Ashfall.Integration.Playable.UI.R25LegacyPack.Url("Consumables/Audio/weapon_swap_ui0992.wav"),AudioType.WAV)) {
                request.timeout=30;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){Debug.LogWarning("R28 pickup audio: "+request.error);yield break;}
                var clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Decoded(request);
                if(clip==null)yield break;clip.name="weapon_swap_ui0992.wav";resources.Add(clip);clips["/pickup"]=new[]{clip};
            }
        }
        public void Play(string weapon,string action,bool shot) {
            if(string.IsNullOrEmpty(action))return;
            AudioClip[] choices;if(!clips.TryGetValue(weapon+"/"+action,out choices)&&!clips.TryGetValue("/"+action,out choices))return;
            if(choices.Length==0)return;bool hurt=!shot&&action=="hurt"&&playerHurt.Length>0;var source=shot?shots[cursor++%shots.Length]:action=="pickup"?pickup:hurt?playerHurt[hurtSlots.NextSlot()]:(action=="hit"||action=="hurt"?feedback:mechanical);
            int awpCue=!shot&&weapon=="awp"?R26AwpAudio.Cue(action):-1;
            float gain=awpCue>=0?R26AwpAudio.CueGain:action=="pickup"?.39810717f:hurt?R24PlayerHurtCueRules.SourceGain:shot?(weapon=="m60"||weapon=="dt11"?.5623413f:weapon=="awp"?.8912509f:.3548f):(weapon=="m60"||weapon=="awp"||weapon=="dt11"?.31622777f:.1995f);sourceGains[source]=gain;source.volume=gain*sfxVolume;source.pitch=weapon=="dt11"&&!shot&&!hurt&&action!="hit"?.985f+(float)dt11MechanicalPitch.NextDouble()*.03f:1;if(shot)source.maxDistance=weapon=="awp"?120:85;source.Stop();source.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(choices[variant++%choices.Length]);if(awpCue>=0)source.PlayDelayed(R26AwpAudio.CueDelay);else source.Play();
            if(shot)R26AwpAudio.Shot(source,weapon=="awp");
            if(awpCue>=0){foreach(var voice in shots)R26AwpAudio.Duck(voice,null,R26AwpAudio.Hold(awpCue));if(AwpCuePlayed!=null)AwpCuePlayed((byte)awpCue);}
        }
        public void StopWeapon(){foreach(var source in shots)StopVoice(source);StopVoice(mechanical);StopVoice(feedback);StopVoice(pickup);}
        static void StopVoice(AudioSource source){if(source!=null){source.Stop();source.clip=null;}}
        public void Stop() { hurtSlots.Reset();foreach(var voice in voices)if(voice!=null){var source=voice.GetComponent<AudioSource>();source.Stop();source.clip=null;} }
        public void Dispose() { Stop();foreach(var voice in voices)if(voice!=null)UnityEngine.Object.Destroy(voice);foreach(var clip in resources)UnityEngine.Object.Destroy(clip);voices.Clear();resources.Clear();clips.Clear();sourceGains.Clear(); }
    }
}
#endif
