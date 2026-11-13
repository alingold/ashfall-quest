#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
namespace Ashfall.Integration.Playable.Gallery
{
    // Real Magnum body/cylinder/rounds and private audio, never a rifle substitute.
    internal sealed class R23MagnumGalleryModel:IDisposable
    {
        internal readonly Transform Root;
        readonly GameObject owner;
        readonly R23MagnumView view;
        readonly R23MagnumRuntime dock=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(),"lobby-static/magnum");
        R23Audio audio;float volume=1;readonly R23WeaponFeedbackState feedback=new R23WeaponFeedbackState("magnum");
        public bool Ready {get;private set;}
        public bool Visible {get{return Ready&&Root!=null&&Root.gameObject.activeInHierarchy&&view.Model!=null&&view.Model.gameObject.activeInHierarchy;}}
        internal Transform HandArtModel {get{return view.Model;}}public Transform Muzzle {get{return view.Muzzle;}}
        public R23MagnumGalleryModel(Transform gallery)
        {
            Root=new GameObject("R23 gallery MAGNUM").transform;Root.SetParent(gallery,false);Root.gameObject.SetActive(false);
            // Asset owner stays active while the render root is docked/hidden.
            owner=new GameObject("Private Magnum gallery asset owner");owner.transform.SetParent(gallery,false);view=owner.AddComponent<R23MagnumView>();
        }
        public IEnumerator Load(float sfx)
        {
            volume=sfx;view.Initialize(Root);
            while(view.Loading)yield return null;
            if(!view.Ready)throw new InvalidOperationException(view.Failure??"Source Magnum gallery view failed");
            audio=new R23Audio(Root,Muzzle);audio.SetSfxVolume(volume);yield return audio.Load();Ready=true;Hide();
        }
        public void Dock(Transform gallery,Vector3 position,Quaternion rotation)
        {if(!Ready)return;Root.SetParent(gallery,true);Root.SetPositionAndRotation(position,rotation);Root.localScale=Vector3.one;Root.gameObject.SetActive(true);feedback.Reset();RefreshTrackedPresentation();view.Present(dock,true,false,1,null);if(audio!=null)audio.Stop();}
        public void Attach(Transform hand)
        {Root.SetParent(hand,false);Root.localPosition=Vector3.zero;Root.localRotation=R23TrackedGripPose.LocalRotation;Root.localScale=Vector3.one;Root.gameObject.SetActive(true);}
        public Vector3 SupportLocal(Vector3 point){return Root.InverseTransformPoint(point)/R23MagnumRuntime.ModelScale;}
        public float ForegripDistance(Vector3 point){return Vector3.Distance(point,Root.TransformPoint(new Vector3(0,-.025f,.36f)*R23MagnumRuntime.ModelScale));}
        public void AdvanceFeedback(float seconds){feedback.Advance(seconds);RefreshTrackedPresentation();}
        public void RefreshTrackedPresentation(){if(!Ready)return;var q=Root.rotation;var neutral=new R23ShotPose(F(Root.position),F(q*Vector3.right),F(q*Vector3.up),F(q*Vector3.forward));R23ShotPose model,shot;R23WeaponPoseMath.Resolve(neutral,R23MagnumRuntime.SourceMuzzle,R23MagnumRuntime.ModelScale,feedback.Distance,feedback.PitchRadians,feedback.YawRadians,true,out model,out shot);view.Model.SetPositionAndRotation(V(model.Origin),Quaternion.LookRotation(V(model.Forward),V(model.Up)));}
        public void Present(R23MagnumRuntime runtime,bool focused,int primary,Transform support)
        {if(Ready){feedback.AcceptedShot(runtime.Snapshot.Shots,runtime.Snapshot.Supported);RefreshTrackedPresentation();view.Present(runtime,true,focused,primary,support);}}
        static Float3 F(Vector3 value){return new Float3(value.x,value.y,value.z);}static Vector3 V(Float3 value){return new Vector3(value.X,value.Y,value.Z);}
        public void Cue(string action,bool shot=false){if(audio!=null)audio.Play("magnum",action,shot);}
        public void SetVolume(float value){volume=value;if(audio!=null)audio.SetSfxVolume(value);}
        public void Hide(){if(audio!=null)audio.Stop();if(Root!=null)Root.gameObject.SetActive(false);}
        public void Dispose(){Hide();dock.Retire();if(audio!=null)audio.Dispose();if(owner!=null)UnityEngine.Object.Destroy(owner);if(Root!=null)UnityEngine.Object.Destroy(Root.gameObject);}
    }
}
#endif
