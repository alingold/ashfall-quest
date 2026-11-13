#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Player;
using Ashfall.Modules.Avatar;
using Ashfall.Integration.AvatarPose;
using UnityEngine;
// Coordinator-only staged fixture. Configure actual pilot bytes/ground/rig/motor and Session.
// No Unity execution was performed by this leaf. No tracking/art/production attestation.
public static class AvatarLifecycleR8NativeTests
{
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static bool Hidden(AvatarView view){foreach(var r in view.GetComponentsInChildren<Renderer>(true))if(r.enabled&&r.gameObject.activeInHierarchy)return false;return true;}
    // Root must arrange no competing movement proposal during this coroutine.
    // The supplied proposal must pass the real approved motor/geometry; no fake success adapter.
    public static void CheckPartSuppression(AvatarView view)
    {
        var frame=new System.Collections.Generic.Dictionary<string,Matrix4x4>();
        foreach(var label in AvatarAssembler.RequiredLabels())frame.Add(label,Matrix4x4.identity);
        view.Present(frame,true,true,new[]{"LUpperArm","RForearm"});
        foreach(var t in view.GetComponentsInChildren<Transform>(true))
            if(frame.ContainsKey(t.name))Require(t.gameObject.activeSelf==(t.name!="AvatarHead"&&t.name!="LUpperArm"&&t.name!="RForearm"),"per-part visibility "+t.name);
        view.Present(frame,true,false,new string[0]);
        foreach(var t in view.GetComponentsInChildren<Transform>(true))if(frame.ContainsKey(t.name))Require(t.gameObject.activeSelf,"suppression does not latch "+t.name);
    }
    public static IEnumerator RunScheduler(UnityPlayerDriver driver,AvatarView view,NativePoseBinding.Provider provider,RootMotionProposal approved)
    {
        Require(driver.Motor is UnityExclusivePlayerMotor,"actual native motor required");
        var actual=driver.Motor;var owner=new GameObject("AvatarSchedulerR8Fixture");
        var scheduler=owner.AddComponent<NativePoseScheduler>();
        try {
            scheduler.Install(driver,provider);
            Require(!System.Object.ReferenceEquals(driver.Motor,actual),"installed observer wraps actual motor");
            var head=driver.Head.localToWorldMatrix;var left=driver.LeftHand.localToWorldMatrix;var right=driver.RightHand.localToWorldMatrix;
            var result=driver.Motor.Apply(approved,.016f);
            Require(result.Complete&&result.Velocity.IsFinite,"actual approved motor completed");
            var postRoot=driver.transform.localToWorldMatrix;
            Require(Hidden(view),"observer hides until post-motor LateUpdate");
            yield return null;
            Require(!Hidden(view),"actual motor result scheduled presentation");
            Require(driver.transform.localToWorldMatrix==postRoot&&driver.Head.localToWorldMatrix==head&&
                driver.LeftHand.localToWorldMatrix==left&&driver.RightHand.localToWorldMatrix==right,"presentation has no authoritative transform writes (zero-displacement proposal required)");
            driver.enabled=false;Require(Hidden(view),"scheduler/source teardown hides without Pump");
            Require(System.Object.ReferenceEquals(driver.Motor,actual),"teardown restores exact actual motor");
        } finally {scheduler.Dispose();UnityEngine.Object.DestroyImmediate(owner);}
    }
    public static IEnumerator Run(UnityPlayerDriver driver,AvatarView view,Session session,string contentHash,string actor,string reconnect,
        Func<string,NativePoseBinding.Provider> fresh,Func<string,CommandContext> context)
    {
        Require(!driver.UseXr,"synthetic desktop fixture only");
        NativePoseBinding.Provider old=null,replacement=null;
        try {
            old=fresh(actor);yield return null;
            Require(old.Pump(.016f,true)&&!Hidden(view),"initial full visible pilot");
            object source=driver.PoseSourceIdentity;
            driver.Paused=true;
            Require(System.Object.ReferenceEquals(source,driver.PoseSourceIdentity),"pause must not rotate physical source");
            yield return null;Require(old.Pump(.016f,true),"paused physical presentation");
            driver.Paused=false;
            // Deliberately no Pump and no yield between disable/re-enable and visibility assertion.
            driver.enabled=false;
            Require(Hidden(view),"disable must synchronously hide without Pump");
            driver.enabled=true;
            Require(Hidden(view)&&!old.RefreshLifetime(),"missed disable/re-enable old provider terminal");
            // Capture the obsolete invocation list, then replace without invalidating the driver.
            old=fresh(actor);
            var stale=(Action)typeof(UnityPlayerDriver).GetField("PoseSourceInvalidated",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(driver);
            replacement=fresh(actor);yield return null;Require(replacement.Pump(.016f,true),"replacement after re-enable");
            stale();Require(!Hidden(view),"captured obsolete source callbacks cannot hide replacement");
            old.Dispose();Require(!Hidden(view),"old callback/disposal cannot hide replacement");
            old=replacement;replacement=null;
            // Same-actor disconnect/reconnect occurs entirely between presentation samples.
            Require(session.Disconnect(context(actor),false).Accepted,"disconnect");string next;
            Require(session.Join(context(actor),new JoinRequest(Protocol.Name,1,contentHash,actor,reconnect,false),out next).Accepted,"same actor reconnect");
            Require(!old.Pump(.016f,true)&&Hidden(view),"same actor old provider rejected before any frame reuse");
            replacement=fresh(actor);yield return null;Require(replacement.Pump(.016f,true),"fresh same actor binding");
            old.Dispose();Require(!Hidden(view),"old reconnect disposal lacks replacement authority");
            old=replacement;replacement=null;
            Require(session.Disconnect(context(actor),false).Accepted,"disconnect before remap");
            string remapped=actor+"-remap";
            Require(session.Join(context(remapped),new JoinRequest(Protocol.Name,1,contentHash,remapped,reconnect,false),out next).Accepted,"remapped reconnect");
            Require(!old.RefreshLifetime()&&Hidden(view),"membership hook hides synchronously without Pump");
            replacement=fresh(remapped);yield return null;Require(replacement.Pump(.016f,true),"remapped replacement");
            old.Dispose();Require(!Hidden(view),"stale remap disposal cannot hide current");
            source=driver.PoseSourceIdentity;driver.UseXr=true;
            Require(!System.Object.ReferenceEquals(source,driver.PoseSourceIdentity)&&Hidden(view),"source switch synchronous invalidation");
            driver.UseXr=false;
            Require(!replacement.RefreshLifetime(),"source switch back cannot resurrect");
        } finally {if(old!=null)old.Dispose();if(replacement!=null)replacement.Dispose();driver.Paused=false;driver.UseXr=false;}
    }
}
#endif
