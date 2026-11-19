#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        // Copies render nodes only, sharing the already loaded local meshes/materials.
        // No owner, collider, optic camera, animation, HUD or input component is cloned.
        internal Transform CreateRemoteWeaponArt(string profile,Transform parent,out float scale,out Vector3 bore)
        {
            int index=Array.IndexOf(weaponIds,profile);scale=1;bore=Vector3.zero;if(!Ready||index<0)return null;scale=modelScales[index];bore=sourceMuzzles[index]*scale;
            var source=weaponModels[index];if(R23RoundState.IsDualProfile(profile))source=source.GetChild(0);
            var result=new GameObject("Remote held / "+profile).transform;result.SetParent(parent,false);
            foreach(var view in source.GetComponentsInChildren<MeshRenderer>(true)){
                if(view.name.StartsWith("R23 feedback")||TransientWeaponArt(index,view.transform)||view.name=="Source live 2x SPR aperture")continue;
                var filter=view.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                Matrix4x4 pose=Matrix4x4.Scale(Vector3.one*scale)*source.worldToLocalMatrix*view.transform.localToWorldMatrix;
                var child=new GameObject(view.name).transform;child.SetParent(result,false);child.localPosition=pose.GetColumn(3);child.localRotation=pose.rotation;child.localScale=pose.lossyScale;child.gameObject.layer=18;
                child.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var copy=child.gameObject.AddComponent<MeshRenderer>();copy.sharedMaterials=view.sharedMaterials;copy.shadowCastingMode=ShadowCastingMode.Off;copy.receiveShadows=false;
            }
            return result;
        }
        internal Material RemoteTracerMaterial{get{return tracer==null?null:tracer.sharedMaterial;}}
        internal bool PlayRemoteShot(string profile,AudioSource source,string actor=null){return audioCues!=null&&audioCues.PlayRemoteShot(profile,source,sfxVolume,actor);}
        internal bool BindAwpCue(Action<byte> sink){if(audioCues==null)return false;audioCues.AwpCuePlayed=sink;return true;}
        internal bool PlayRemoteAwpCue(string actor,byte cue,AudioSource voice){return audioCues!=null&&audioCues.PlayRemoteAwpCue(actor,cue,voice,sfxVolume);}
    }
    internal sealed partial class R23Audio
    {
        // Borrow the loaded shot bank and use the caller's globally bounded spatial voice.
        readonly System.Collections.Generic.List<AudioSource> remoteAwpTails=new System.Collections.Generic.List<AudioSource>();
        internal bool PlayRemoteShot(string profile,AudioSource source,float volume,string actor)
        {
            AudioClip[] choices;if(!clips.TryGetValue(profile+"/shot",out choices)||choices.Length==0)return false;
            source.Stop();source.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(choices[variant++%choices.Length]);source.pitch=1;
            source.volume=(profile=="m60"||profile=="dt11"?.5623413f:profile=="awp"?.8912509f:.3548f)*volume;source.maxDistance=profile=="awp"?120:85;source.Play();
            if(profile=="awp"&&actor!=null&&!remoteAwpTails.Contains(source)){
                remoteAwpTails.RemoveAll(voice=>voice==null);remoteAwpTails.Add(source);
            }
            R26AwpAudio.Shot(source,profile=="awp",actor);return true;
        }
        internal bool PlayRemoteAwpCue(string actor,byte cue,AudioSource source,float volume)
        {
            AudioClip[] choices;if(!clips.TryGetValue("awp/"+R26AwpAudio.Action(cue),out choices)||choices.Length==0)return false;
            foreach(var tail in remoteAwpTails)if(tail!=null)R26AwpAudio.Duck(tail,actor,R26AwpAudio.Hold(cue));
            source.Stop();source.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(choices[variant++%choices.Length]);
            source.volume=R26AwpAudio.CueGain*volume;source.PlayDelayed(R26AwpAudio.CueDelay);return true;
        }
    }
}
#endif
