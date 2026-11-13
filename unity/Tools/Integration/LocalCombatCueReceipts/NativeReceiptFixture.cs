#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Integration.DesktopPlaytest;
using Ashfall.Integration.DesktopPlaytest.Combat;
using Ashfall.Integration.DesktopPlaytest.Audio;
using UnityEngine;
public static class NativeReceiptFixture
{
    static void Need(bool b,string name){if(!b)throw new Exception(name);}
    // Root-only native lane. Requires isolated scene with no listeners. COMPILE ONLY here.
    public static IEnumerator Run()
    {
        Need(UnityEngine.Object.FindObjectsByType<AudioListener>().Length==0,"isolated scene required");
        var root=new GameObject("receipt fixture");var cameraObject=new GameObject("view");
        var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.transform.position=new Vector3(0,0,4);
        var c=new DesktopPlaytestContext{PlayerRoot=root.transform,View=cameraObject.AddComponent<Camera>()};c.Targets.Add(target.GetComponent<Collider>());
        var weapons=root.AddComponent<DesktopWeapons>();weapons.Initialize(c);
        var audio=root.AddComponent<CommittedDesktopAudio>();audio.Initialize(c,weapons);
        Physics.SyncTransforms();audio.Pump(1);c.Hits=500;c.RemainingTargets=0;audio.Pump(2);Need(audio.CuesStarted==0,"mutable counters cannot mint cue");
        weapons.Sandbox.Step(.25f,true,false,false);
        Need(weapons.Sandbox.Hit("local-target/0",new Float3(),new Float3(0,0,3.5f)).Accepted,"actual physics combat damage");
        audio.Pump(3);Need(audio.CuesStarted==1,"committed cue");audio.Pump(4);Need(audio.CuesStarted==1,"no replay");
        audio.NotifyFocus(false);weapons.Sandbox.Step(.25f,false,false,false);weapons.Sandbox.Step(.25f,true,false,false);
        Need(weapons.Sandbox.Hit("local-target/0",new Float3(),new Float3(0,0,3.5f)).Accepted,"second commit");audio.Pump(5);audio.NotifyFocus(true);audio.Pump(6);Need(audio.CuesStarted==1,"hidden disposition");
        c.RequestReset();audio.Pump(7);Need(!audio.OwnsListener,"replacement retires old consumer");
        yield return null;
        Need(cameraObject.GetComponent<AudioListener>()==null,"owned listener destroyed");
        UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(cameraObject);UnityEngine.Object.Destroy(target);
        yield return null;
    }
}
#endif
