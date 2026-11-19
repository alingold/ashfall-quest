#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace Ashfall.Integration.Playable.HandsWeapons
{
    // AWP only. The short envelope follows each source across overlapping shot tails.
    internal sealed class R26AwpAudio:MonoBehaviour
    {
        internal const float CueGain=1,CueRange=18,CueDelay=.025f;
        const float DuckGain=.12589254f,Attack=.02f,Release=.10f;
        static readonly string[] Actions={"bolt","mag_eject","pouch","mag_insert"};
        AudioSource voice;string actor;float nominal,level=1;double duckUntil;bool awpShot;
        internal static int Cue(string action){return System.Array.IndexOf(Actions,action);}
        internal static string Action(int cue){return Actions[cue];}
        internal static float Hold(int cue){return CueDelay+(cue==1?.60f:.26f);} // Legacy mag eject has a late latch.
        internal static void Shot(AudioSource voice,bool awp,string actor=null)
        {
            R26AwpAudio tail;
            if(!voice.TryGetComponent(out tail)){
                if(!awp)return;
                tail=voice.gameObject.AddComponent<R26AwpAudio>();tail.voice=voice;
            }
            tail.awpShot=awp;tail.actor=actor;tail.nominal=voice.volume;tail.level=1;tail.duckUntil=0;tail.enabled=false;
        }
        internal static void Duck(AudioSource voice,string actor,float seconds)
        {
            R26AwpAudio tail;
            if(!voice.isPlaying||!voice.TryGetComponent(out tail)||!tail.awpShot||tail.actor!=actor)return;
            tail.duckUntil=System.Math.Max(tail.duckUntil,Time.unscaledTimeAsDouble+seconds);tail.enabled=true;
        }
        internal static void SetVolume(AudioSource voice,float gain)
        {
            R26AwpAudio tail;
            if(voice.TryGetComponent(out tail)){tail.nominal=gain;gain*=tail.level;}
            voice.volume=gain;
        }
        void Update()
        {
            if(!voice.isPlaying){level=1;voice.volume=nominal;enabled=false;return;}
            bool held=Time.unscaledTimeAsDouble<duckUntil;
            level=Mathf.MoveTowards(level,held?DuckGain:1,Time.unscaledDeltaTime*(1-DuckGain)/(held?Attack:Release));
            voice.volume=nominal*level;
            if(!held&&level>=1)enabled=false;
        }
    }
}
#endif
