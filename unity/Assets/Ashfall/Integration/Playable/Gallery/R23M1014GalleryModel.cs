#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
namespace Ashfall.Integration.Playable.Gallery
{
    // Reuse the actual production source presenter. Active asset host is separate
    // from its hidden/docked render root so coroutines can always finish loading.
    internal sealed class R23M1014GalleryModel:IDisposable
    {
        internal readonly Transform Root;readonly GameObject assetHost;readonly R23M1014View view;
        R23Audio audio;float volume=1;
        public bool Ready {get;private set;}public bool Visible {get{return Ready&&Root.gameObject.activeInHierarchy&&view.Model!=null&&view.Model.gameObject.activeInHierarchy;}}
        internal Transform HandArtModel {get{return view.Model;}}public Transform Muzzle {get{return view.Muzzle;}}
        public R23M1014GalleryModel(Transform gallery)
        {
            Root=new GameObject("R23 gallery M1014").transform;Root.SetParent(gallery,false);Root.gameObject.SetActive(false);
            assetHost=new GameObject("Private M1014 gallery asset host");assetHost.transform.SetParent(gallery,false);view=assetHost.AddComponent<R23M1014View>();
        }
        public IEnumerator Load(float sfxVolume)
        {
            volume=sfxVolume;view.Initialize(Root);while(!view.Ready){if(!string.IsNullOrEmpty(view.Failure))throw new InvalidOperationException(view.Failure);yield return null;}
            view.Model.gameObject.AddComponent<R23ReflexOptic>().Initialize(1,Muzzle,.139f);
            audio=new R23Audio(Root,Muzzle);audio.SetSfxVolume(volume);yield return audio.Load();Ready=true;Hide();
        }
        public void Dock(Transform gallery,Vector3 point,Quaternion rotation)
        {if(!Ready)return;Root.SetParent(gallery,true);Root.SetPositionAndRotation(point,rotation);Root.localScale=Vector3.one;Root.gameObject.SetActive(true);view.Suspend();view.Present(new HandlingSnapshot{Phase=ReloadPhase.Ready,Loaded=6,Seated=5,Chambered=true},480,false,true,0);if(audio!=null)audio.Stop();}
        public void Attach(Transform hand){Root.SetParent(hand,false);Root.localPosition=Vector3.zero;Root.localRotation=R23TrackedGripPose.LocalRotation;Root.localScale=Vector3.one;Root.gameObject.SetActive(true);}
        public Vector3 SupportLocal(Vector3 point){return Root.InverseTransformPoint(point);}
        public float ForegripDistance(Vector3 point){var p=R23M1014Definition.Foregrip;return Vector3.Distance(point,Root.TransformPoint(new Vector3(p.X,p.Y,p.Z)));}
        public R23M1014Pose FiringPose(){return view.FiringPose();}
        public void Present(R23M1014Owner owner,bool focused,int hand,float seconds){if(Ready)view.Present(owner.Snapshot,owner.Reserve,hand==0,focused,seconds);}
        public void Cue(string action,bool shot=false){if(audio!=null)audio.Play("m1014",action,shot);}
        public void SetVolume(float value){volume=value;if(audio!=null)audio.SetSfxVolume(value);}
        public void Hide(){if(audio!=null)audio.Stop();view.Suspend();if(Root!=null)Root.gameObject.SetActive(false);}
        public void Dispose(){Hide();if(audio!=null)audio.Dispose();if(assetHost!=null)UnityEngine.Object.Destroy(assetHost);if(Root!=null)UnityEngine.Object.Destroy(Root.gameObject);}
    }
}
#endif
