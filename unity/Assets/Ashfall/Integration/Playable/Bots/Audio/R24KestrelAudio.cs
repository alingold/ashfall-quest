#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;
namespace Ashfall.Integration.Playable.Bots
{
 // Attach to the actual actor BODY root (not scaled/yawedvisual). Root owns
 // gameplay transitions and pause; an unavailable clip never blocks combat.
 public sealed class R24KestrelAudio:MonoBehaviour,IDisposable
 {
  R24KestrelClipBank bank;readonly R24KestrelAudioRules rules=new R24KestrelAudioRules();readonly AudioSource[] normal=new AudioSource[2],impact=new AudioSource[2];bool initialized,disposed;float volume=1,pitch,maximum;int normalCursor,impactCursor;
  public bool Ready {get{return initialized&&!disposed&&bank!=null&&bank.Ready;}}public string Failure{get{return bank==null?null:bank.Failure;}}public long Variation{get{return rules.Variation;}}public int PlaysAccepted{get;private set;}public string LastClip{get;private set;}
  public void Initialize(int variant,float engageRange){if(initialized||disposed)throw new InvalidOperationException("One source Kestrel audio owner");pitch=R24KestrelAudioRules.Pitch(variant);maximum=R24KestrelAudioRules.MaxDistance(engageRange);initialized=true;bank=R24KestrelClipBank.Acquire();var normalCurve=Curve(false);var impactCurve=Curve(true);for(int i=0;i<2;i++){normal[i]=Voice("Kestrel source -9dB channel/"+i,normalCurve);impact[i]=Voice("Kestrel source -12dB impact/"+i,impactCurve);}}
  AudioSource Voice(string label,AnimationCurve curve){var go=new GameObject(label);go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.up*1.2f;var voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=1;voice.dopplerLevel=0;voice.volume=volume;voice.pitch=pitch;voice.minDistance=0;voice.maxDistance=maximum;voice.rolloffMode=AudioRolloffMode.Custom;voice.SetCustomCurve(AudioSourceCurveType.CustomRolloff,curve);return voice;}
  AnimationCurve Curve(bool impactChannel){return CreateAttenuationCurve(9,maximum,impactChannel?-12:-9,3);}
  // Curve is normalized by DbToLinear(maxDb). For ambient negative maxDb,
  // source.volume=DbToLinear(maxDb)*UIbus; for Kestrel+3dB the OneShot scale
  // supplies that cap without changing any listener or project mixer setting.
  public static AnimationCurve CreateAttenuationCurve(float unit,float maxDistance,float volumeDb,float maxDb){var points=R24KestrelAudioRules.AttenuationCurvePoints(unit,maxDistance,volumeDb,maxDb);var keys=new Keyframe[points.Length];for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(points[i].Time,points[i].Gain,points[i].InTangent,points[i].OutTangent);return new AnimationCurve(keys);}
  public void SetSfxVolume(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentOutOfRangeException("value");volume=Mathf.Clamp01(value);foreach(var source in normal)if(source!=null)source.volume=volume;foreach(var source in impact)if(source!=null)source.volume=volume;}
  public bool Play(string kind,bool impact=false){if(!initialized||disposed)return false;var request=rules.Next(kind,impact);if(!Ready||!isActiveAndEnabled)return false;AudioClip clip=bank.Read(request.Kind,request.Index);if(clip==null)return false;var channel=impact?this.impact:normal;
   // Godot voice.stream assignment stops that logical player's old stream
   // before play(). Keep the source maxpolyphony2 allocation, not cue layering.
   foreach(var voice in channel)if(voice!=null){voice.Stop();voice.clip=null;}
   var selected=channel[impact?impactCursor++%2:normalCursor++%2];selected.clip=clip;selected.pitch=pitch;selected.volume=volume;
   // PlayOneShot permits the source's +3dB close-range cap. The normalized
   // rolloff already includes -9/-12; volume is ONLY the UI SFX bus multiplier.
   selected.PlayOneShot(clip,(float)R24KestrelAudioRules.SourceMaxLinear);LastClip=clip.name;PlaysAccepted++;return true;
  }
  public void Stop(){foreach(var voice in normal)if(voice!=null){voice.Stop();voice.clip=null;}foreach(var voice in impact)if(voice!=null){voice.Stop();voice.clip=null;}}
  public bool TryReadClip(string kind,int index,out AudioClip clip){clip=bank==null?null:bank.Read(kind,index);return clip!=null;}
  public void Dispose(){if(disposed)return;disposed=true;Stop();foreach(var voice in normal)if(voice!=null)Destroy(voice.gameObject);foreach(var voice in impact)if(voice!=null)Destroy(voice.gameObject);if(bank!=null){bank.Release();bank=null;}}
  void OnDisable(){Stop();}void OnDestroy(){Dispose();}
 }
}
#endif
