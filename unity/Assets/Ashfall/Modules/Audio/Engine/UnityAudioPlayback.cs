#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using Ashfall.Core;
using UnityEngine;

namespace Ashfall.Modules.Audio
{
    // Explicit runtime WAV import. Caller supplies a validated catalog, not arbitrary AudioClips.
    public sealed class UnityAudioPlayback : MonoBehaviour, IProfiledAudioPlayback
    {
        private sealed class Voice : IProfiledAudioVoice
        {
            internal AudioSource Source; internal string Bus; internal float Gain; internal bool Loop; internal CueProfile Profile;
            internal UnityAudioPlayback Owner;
            public void Set(float gain,float pitch,Float3 position){if(!Source)return;Gain=gain;Source.pitch=pitch;Source.transform.position=new Vector3(position.X,position.Y,position.Z);Owner.Apply(this);}
            public void SetProfile(CueProfile profile){if(!Source)return;Profile=profile;Owner.Configure(Source,profile);Owner.Apply(this);}
            public void Pause(bool paused){if(!Source)return;if(paused)Source.Pause();else Source.UnPause();}
            public void Stop(){if(!Source)return;Source.Stop();Destroy(Source.gameObject);Source=null;}
        }
        private readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        private readonly Dictionary<string,float> buses=new Dictionary<string,float>();
        private readonly List<Voice> voices=new List<Voice>();
        // Central bootstrap calls once on its final listener; no shared scene edits here.
        public static AudioPeakLimiter InstallMasterLimiter(AudioListener listener)
        {
            if(!listener)throw new ArgumentNullException("listener");
            var limiter=listener.GetComponent<AudioPeakLimiter>();
            return limiter?limiter:listener.gameObject.AddComponent<AudioPeakLimiter>();
        }
        IAudioVoice IAudioPlayback.Start(AudioAsset asset,bool spatial,float gain,float pitch,Float3 position)
        {return Play(asset,CueProfiles.For(asset.Route.Substring(asset.Route.LastIndexOf('/')+1),asset.Route.StartsWith("weapons/",StringComparison.Ordinal)),spatial,gain,pitch,position);}
        IAudioVoice IProfiledAudioPlayback.Start(AudioAsset asset,CueProfile profile,bool spatial,float gain,float pitch,Float3 position)
        {return Play(asset,profile,spatial,gain,pitch,position);}
        public IAudioVoice Play(AudioAsset asset,CueProfile profile,bool spatial,float gain,float pitch,Float3 position)
        {
            if(profile==null)throw new ArgumentNullException("profile");
            AudioClip clip;
            if(!clips.TryGetValue(asset.Hash,out clip))
            {
                byte[] bytes=File.ReadAllBytes(AudioPaths.Canonical(asset.Path));
                using(var sha=SHA256.Create())if(!string.Equals(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""),asset.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Audio changed after validation");
                PcmWave pcm=PcmWave.Decode(bytes);
                clip=AudioClip.Create(asset.Route,pcm.Frames,pcm.Channels,pcm.Rate,false);
                if(!clip.SetData(pcm.Samples,0)){Destroy(clip);throw new InvalidDataException("AudioClip import failed");}clips.Add(asset.Hash,clip);
            }
            var go=new GameObject("Audio "+asset.Route);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.clip=clip;source.loop=asset.Loop;
            source.spatialBlend=spatial?1:0;source.dopplerLevel=0;Configure(source,profile);
            go.AddComponent<AudioPeakLimiter>();
            var voice=new Voice{Source=source,Owner=this,Bus=asset.Bus,Gain=gain,Loop=asset.Loop,Profile=profile};
            voices.Add(voice);voice.Set(gain,pitch,position);source.Play();return voice;
        }
        private void Configure(AudioSource source,CueProfile profile)
        {
            source.minDistance=profile.UnitMeters;source.maxDistance=profile.MaxMeters;
            var keys=new Keyframe[65];for(int i=0;i<keys.Length;i++){float distance=profile.MaxMeters*i/(keys.Length-1);keys[i]=new Keyframe(distance/profile.MaxMeters,profile.Attenuation(distance));}
            source.rolloffMode=AudioRolloffMode.Custom;source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,new AnimationCurve(keys));
        }
        public void SetBus(string bus,float gain){buses[bus]=gain;foreach(var v in voices)Apply(v);}
        private void Apply(Voice v){if(!v.Source)return;float bus,master;buses.TryGetValue(v.Bus,out bus);buses.TryGetValue("Master",out master);v.Source.volume=AudioMix.Gain(v.Gain,v.Profile,bus,master,v.Bus=="Master");}
        public void StopTransients(){foreach(var v in voices)if(!v.Loop)v.Stop();}
        private void Update(){for(int i=voices.Count-1;i>=0;i--)if(!voices[i].Source||(!voices[i].Loop&&!voices[i].Source.isPlaying)){voices[i].Stop();voices.RemoveAt(i);}}
        private void OnDestroy(){foreach(var v in voices)v.Stop();voices.Clear();foreach(var clip in clips.Values)Destroy(clip);clips.Clear();}
    }
}
#endif
