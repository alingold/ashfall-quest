#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Calibration;
using Ashfall.Integration.Playable.Input;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Explicit bounded native fixture route; no screenshots, XR or device acceptance.
    public sealed class R23HumanBugSmoke
    {
        [Serializable]sealed class Group
        {public string name="",error="";public bool passed;public List<string> observations=new List<string>();}
        [Serializable]sealed class Result
        {public bool success,headsetVerified=false;public string startedUtc="",finishedUtc="",error="";public List<Group> groups=new List<Group>();}
        static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static Group Add(Result result,string name)
        {var group=new Group{name=name};result.groups.Add(group);Debug.Log("R23 HUMAN BUG GROUP BEGIN "+name);return group;}
        static void Observe(Group group,string message){group.observations.Add(message);Debug.Log("R23 HUMAN BUG "+group.name+": "+message);}
        static void Fail(Group group,Exception error){group.error=error.ToString();Debug.LogError("R23 HUMAN BUG GROUP FAIL "+group.name+": "+error);}
        static void Sync(Result result,string name,Action body)
        {var group=Add(result,name);try{body();group.passed=true;Debug.Log("R23 HUMAN BUG GROUP PASS "+name);}catch(Exception error){Fail(group,error);}}
        // SmokeControl's Editor pump is a flat iterator. Explicitly flatten actual
        // production coroutines, including nested wheel capture callbacks if added.
        static IEnumerator Drive(Group group,IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            try
            {
                while(stack.Count>0&&group.error.Length==0)
                {
                    bool moved=false;object next=null;
                    try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                    catch(Exception error){Fail(group,error);}
                    if(group.error.Length>0)break;
                    if(!moved){var done=stack.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}
                    var child=next as IEnumerator;if(child!=null){stack.Push(child);continue;}
                    var operation=next as AsyncOperation;if(operation!=null)while(!operation.isDone)yield return null;
                    yield return null;
                }
                group.passed=group.error.Length==0;
                if(group.passed)Debug.Log("R23 HUMAN BUG GROUP PASS "+group.name);
            }
            finally{while(stack.Count>0){var disposable=stack.Pop() as IDisposable;if(disposable!=null)disposable.Dispose();}}
        }
        public IEnumerator Start()
        {
            var result=new Result{startedUtc=DateTime.UtcNow.ToString("o")};R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<end)
            {loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready)
            {result.error=loop==null?"Playable loop missing":loop.Failure??"Initial source load timeout";Finish(result);yield break;}
            loop.ExternalProbePump=true;loop.ManualSmoke=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;
            Sync(result,"MotionFix",delegate{R23MotionFixSmoke.Run(loop);});
            Sync(result,"CurrentPostureCalibration",delegate{RunCalibration();});
            var appearance=Add(result,"ActualAppearanceArrowPreview");IEnumerator job=null;
            try{loop.ReturnToLobby();job=loop.UI.CheckAppearancePreviewChange(loop.Preview,delegate(string message){Observe(appearance,message);},30);}
            catch(Exception error){Fail(appearance,error);}
            if(job!=null){var drive=Drive(appearance,job);while(drive.MoveNext())yield return null;}
            var wheel=Add(result,"SourceWeaponWheelUI");job=null;
            try{job=R23WeaponWheelFixture.Run(loop.Rig.Head.transform,delegate(string message){Observe(wheel,message);},null);}
            catch(Exception error){Fail(wheel,error);}
            if(job!=null){var drive=Drive(wheel,job);while(drive.MoveNext())yield return null;}
            Finish(result);
        }
        static void Finish(Result result)
        {
            result.success=result.error.Length==0&&result.groups.Count==4;
            foreach(var group in result.groups)result.success&=group.passed&&group.error.Length==0;
            result.finishedUtc=DateTime.UtcNow.ToString("o");string json=JsonUtility.ToJson(result,true);
            Debug.Log("R23 HUMAN BUG TERMINAL "+json);R23SmokeControl.Finish(result.success,json);
        }
        static void RunCalibration()
        {
            foreach(float height in new[]{.65f,1.15f,2.4f})
            {
                var owner=new GameObject("HumanBug isolated current-posture calibration "+height);R23VRRig rig=null;
                try
                {
                    Vector3 site=new Vector3(6000,400,6000);rig=owner.AddComponent<R23VRRig>();rig.Initialize(site,site+new Vector3(.6f,0,.8f));rig.enabled=false;rig.Head.enabled=false;
                    foreach(var listener in rig.Head.GetComponents<AudioListener>())listener.enabled=false;
                    rig.TrackingOrigin.localPosition=new Vector3(.27f,.17f,-.41f);
                    rig.Head.transform.localPosition=new Vector3(.63f,height,-.32f);
                    rig.Left.localPosition=new Vector3(-.36f,height-.3f,.18f);rig.Right.localPosition=new Vector3(.28f,height-.25f,-.11f);
                    Vector3 rawHead=rig.Head.transform.localPosition,rawLeft=rig.Left.localPosition,rawRight=rig.Right.localPosition,root=rig.transform.position,origin=rig.TrackingOrigin.localPosition;
                    Quaternion rotation=rig.transform.rotation,rawRotation=rig.Head.transform.localRotation;int events=0;
                    var calibration=owner.AddComponent<R23VerticalCalibration>();calibration.SuppressPersistence=true;calibration.HeadCollision=rig.HeadCollision;
                    calibration.CalibrationChanged+=delegate{events++;};calibration.Initialize(rig.TrackingOrigin,rig.Head,rig.Body);Physics.SyncTransforms();
                    for(int i=0;i<8;i++)calibration.Tick(true,true,false,.05f);
                    Need(calibration.Calibrated&&!calibration.Pending&&!calibration.CeilingLimited,"Boot failed at raw height "+height+": "+calibration.Status);
                    Need(Mathf.Abs(rig.Head.transform.position.y-root.y-1.7088f)<.002f,"Native boot did not normalize seated/tall eye at "+height);
                    Need(Mathf.Abs(calibration.Receipt.TrackedStandingEye-height)<.001f,"Boot receipt used a different raw posture");
                    Need(rig.transform.position==root&&rig.transform.rotation==rotation,"Calibration moved/rotated actual root");
                    Need(rig.TrackingOrigin.localPosition.x==origin.x&&rig.TrackingOrigin.localPosition.z==origin.z,"Calibration rewrote nonzero origin X/Z");
                    Need(rig.Head.transform.localPosition==rawHead&&rig.Head.transform.localRotation==rawRotation&&rig.Left.localPosition==rawLeft&&rig.Right.localPosition==rawRight,"Calibration overwrote raw tracked pose");
                    calibration.RequestRecalibration();rawHead.y=height+.1f;rig.Head.transform.localPosition=rawHead;
                    for(int i=0;i<8;i++)calibration.Tick(true,true,false,.05f);
                    Need(calibration.Calibrated&&!calibration.Pending&&events==2,"Manual current-posture calibration did not emit exactly one fresh result");
                    Need(Mathf.Abs(calibration.Receipt.TrackedStandingEye-rawHead.y)<.001f&&Mathf.Abs(rig.Head.transform.position.y-root.y-1.7088f)<.002f,"Manual calibration reused boot baseline");
                    Need(rig.transform.position==root&&rig.Head.transform.localPosition==rawHead&&rig.Left.localPosition==rawLeft&&rig.Right.localPosition==rawRight,"Manual calibration overwrote actual root/raw poses");
                    Need(rig.TrackingOrigin.localPosition.x==origin.x&&rig.TrackingOrigin.localPosition.z==origin.z,"Manual calibration rewrote origin X/Z");
                    Debug.Log("R23 HUMAN BUG CALIBRATION PASS raw="+height+" manualRaw="+rawHead.y+" rootRelativeEye="+(rig.Head.transform.position.y-root.y)+" originXZ="+origin.x+","+origin.z);
                }
                finally{owner.SetActive(false);UnityEngine.Object.Destroy(owner);Physics.SyncTransforms();}
            }
        }
    }
}
#endif
