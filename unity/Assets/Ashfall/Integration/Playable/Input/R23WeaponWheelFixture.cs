#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
namespace Ashfall.Integration.Playable.Input
{
    public static class R23WeaponWheelFixture
    {
        // Callable from the root's existing native Smoke coroutine. Dedicated fixture
        // callback records requests; it never calls inventory or changes tracked poses.
        public static IEnumerator Run(Transform head,Action<string> report=null,Func<string,IEnumerator> capture=null)
        {
            string selected=null;int accepted=0,backpack=0;
            var go=new GameObject("Source wheel isolated native fixture");var wheel=go.AddComponent<R23WeaponWheel>();
            var pose=new GameObject("Untracked fixture view snapshot").transform;pose.SetParent(go.transform,false);pose.SetPositionAndRotation(head.position,head.rotation);head=pose;var before=head.position;var rotation=head.rotation;
            try{
                wheel.Initialize(head,delegate(string id,int hand){selected=id;accepted++;return true;},delegate{backpack++;});
                if(!wheel.Ready)throw new InvalidOperationException(wheel.Failure);
                wheel.SetSlots(new R23WeaponWheelSlot("fixture/mk18","MK18","mk18"),new R23WeaponWheelSlot("fixture/akm","AKM","akm"),new R23WeaponWheelSlot("fixture/spr","SPR","spr"));
                var ray=new Ray(head.position,head.forward);wheel.Step(.02f,false,true,ray,Vector3.zero);wheel.Step(.11f,true,true,ray,Vector3.zero);yield return null;
                if(!wheel.Active||!wheel.FixtureVisible)throw new InvalidOperationException("Native wheel failed to open");
                ray=new Ray(head.position,(wheel.FixtureSectorPoint(2)-head.position).normalized);wheel.Step(.02f,true,true,ray,Vector3.zero);yield return null;
                if(wheel.InteriorTextCount!=0||wheel.StatusText!="MK18 \u2605")throw new InvalidOperationException("Native wheel source name/stars or interior text mismatch");
                if(capture!=null)yield return capture("source-wheel-owned-mk18");
                if(!wheel.Step(.02f,false,true,ray,Vector3.zero)||selected!="fixture/mk18"||accepted!=1||wheel.Active)throw new InvalidOperationException("Native owned slot release failed");
                wheel.Step(.11f,true,true,new Ray(head.position,head.forward),Vector3.zero);ray=new Ray(head.position,(wheel.FixtureSectorPoint(4)-head.position).normalized);wheel.Step(.02f,true,true,ray,Vector3.zero);wheel.Step(.02f,false,true,ray,Vector3.zero);
                if(backpack!=1||wheel.Active)throw new InvalidOperationException("Native nonpausing backpack request failed");
                wheel.Step(.11f,true,true,new Ray(head.position,head.forward),Vector3.zero);wheel.Step(.02f,true,false,ray,Vector3.zero);wheel.Step(.02f,false,true,ray,Vector3.zero);
                if(wheel.Active||accepted!=1||head.position!=before||head.rotation!=rotation)throw new InvalidOperationException("Wheel lifecycle changed a tracked pose or accepted stale input");
                if(report!=null)report("PASS native wheel source geometry/icons, release equip, backpack and focus cleanup; tracked head unchanged");
            }finally{UnityEngine.Object.Destroy(go);}
        }
    }
}
#endif
