#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public sealed class R24TrainingSourceSmoke
    {
        [Serializable]sealed class Group{public string name="",error="",notes="";public bool passed;}
        [Serializable]sealed class Receipt{public bool success,nativeExecuted,headsetVerified=false;public string error="",sparseJson="";public List<Group> groups=new List<Group>();public List<string> shots=new List<string>();}
        [Serializable]sealed class Cameras{public Shot[] shots=null;}
        [Serializable]sealed class Shot{public string name="",mapId="";public float[] eyeMapLocal=null,targetMapLocal=null;}
        readonly bool fixesOnly,captureOnly;public R24TrainingSourceSmoke(bool actualFixesOnly=false,bool actualCaptureOnly=false){fixesOnly=actualFixesOnly;captureOnly=actualCaptureOnly;}
        R23PlayableLoop loop;Receipt result;float deadline;string savedMap;bool manual,external,rig,persistence;Vector3 savedOrigin,savedHead,savedLeft,savedRight;Quaternion savedLeftQ,savedRightQ,savedHeadQ;
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        void Bound(){Need(Time.realtimeSinceStartup<deadline,"Source training/sparse fixture exceeded360s active bound");}
        public IEnumerator Run()
        {
            result=new Receipt();float end=Time.realtimeSinceStartup+120;while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-disabled Root required";Finish();yield break;}
            savedMap=loop.Map.MapId;manual=loop.ManualSmoke;external=loop.ExternalProbePump;rig=loop.Rig.enabled;persistence=loop.Calibration.SuppressPersistence;savedOrigin=loop.Rig.TrackingOrigin.localPosition;savedHead=loop.Rig.Head.transform.localPosition;savedHeadQ=loop.Rig.Head.transform.localRotation;savedLeft=loop.Rig.Left.localPosition;savedRight=loop.Rig.Right.localPosition;savedLeftQ=loop.Rig.Left.localRotation;savedRightQ=loop.Rig.Right.localRotation;
            result.nativeExecuted=true;deadline=Time.realtimeSinceStartup+360;loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;loop.Rig.TrackingOrigin.localPosition=Vector3.zero;loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Head.transform.localRotation=Quaternion.identity;
            try
            {
                foreach(string name in captureOnly?new[]{"ActualSourceRangeTrainingLayout","ActualRangeMatchedCapture","ActualSceneCleanup"}:fixesOnly?new[]{"ActualSourceRangeTrainingLayout","ActualRangeMatchedCapture","ActualSceneCleanup"}:new[]{"ActualSourceRangeTrainingLayout","ActualRangeMatchedCapture","ActualPositiveSparseArchive","ActualSceneCleanup"})
                {
                    var g=new Group{name=name};result.groups.Add(g);IEnumerator work=name=="ActualSourceRangeTrainingLayout"?Layout(g):name=="ActualRangeMatchedCapture"?Capture(g):name=="ActualPositiveSparseArchive"?Sparse(g):Cleanup(g);
                    var stack=new Stack<IEnumerator>();stack.Push(work);try{while(stack.Count>0){bool moved=false;object current=null;try{Bound();moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}catch(Exception e){g.error=e.ToString();Debug.LogError("R24 SOURCE TRAINING FAIL "+name+": "+e);break;}if(!moved){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}var nested=current as IEnumerator;if(nested!=null)stack.Push(nested);else yield return current;}g.passed=g.error.Length==0;}finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}
                }
            }
            finally
            {
                try{if(loop!=null&&loop.Ready){loop.StowPaddles();loop.ReturnToLobby();}}catch(Exception e){result.error+="Cleanup: "+e;}
                if(loop!=null){loop.Rig.TrackingOrigin.localPosition=savedOrigin;loop.Rig.Head.transform.SetLocalPositionAndRotation(savedHead,savedHeadQ);loop.Rig.Left.SetLocalPositionAndRotation(savedLeft,savedLeftQ);loop.Rig.Right.SetLocalPositionAndRotation(savedRight,savedRightQ);loop.ManualSmoke=manual;loop.ExternalProbePump=external;loop.Rig.enabled=rig;loop.Calibration.SuppressPersistence=persistence;}Finish();
            }
        }
        IEnumerator Layout(Group g)
        {
            loop.ReturnToLobby();loop.SelectMap("range",true);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+70);while((!loop.Ready||loop.Map.MapId!="range")&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range failed to load");loop.StartPlaying();Pump();
            Need(!loop.MatchActive&&loop.Gameplay.VisiblePracticeTargetCount==0&&loop.GetComponentInChildren<R24RescueTrainingDummy>(true)==null,"Warm-up contains legacy training props");
            Need(loop.RangeLane!=null,"The independent sandbox range is absent");
            g.notes="Range entry stays in warm-up with the sandbox lane and without legacy training props.";
        }
        IEnumerator Capture(Group g)
        {
            loop.StowPaddles();loop.UI.SetPlaying();loop.Rig.SetSpawn(loop.Map.GameSpawn,loop.Map.GameSpawn+Vector3.forward);loop.Calibration.NotifyRootPoseReset();loop.Gameplay.ResetPracticeTargets();Pump();
            // Observe the same final native placement stage as the Godot
            // reference; do not capture an earlier pending physics callback.
            float end=Mathf.Min(deadline,Time.realtimeSinceStartup+8);bool settled=false;
            while(Time.realtimeSinceStartup<end){settled=true;foreach(var row in Ashfall.Integration.Playable.Consumables.WorldPickupLayout.R24WorldPickupLayout.FoodsAndBackpacks()){Ashfall.Integration.Playable.Consumables.WorldPickupLayout.R24PickupSettleResult r;bool found=row.IsSupply?loop.Supplies.TryReadSourceSettleResult(row.Id,out r):loop.Consumables.TryReadSourceSettleResult(row.Id,out r);if(!found||r.Outcome!=Ashfall.Integration.Playable.Consumables.WorldPickupLayout.R24PickupSettleOutcome.Settled)settled=false;}if(settled)break;Pump();yield return null;}Need(settled,"Actual Range food/bag callbacks not settled before source final-stage camera");
            end=Mathf.Min(deadline,Time.realtimeSinceStartup+4);bool allVisible=false;
            while(Time.realtimeSinceStartup<end){allVisible=loop.WorldLoot.WorldInstanceActionIds.Length==26&&loop.WorldLoot.WorldGrenadeInstanceIds.Length==2;foreach(string id in loop.WorldLoot.WorldInstanceActionIds)allVisible&=loop.Gameplay.IsWorldWeaponVisible(id);foreach(string id in loop.WorldLoot.WorldGrenadeInstanceIds)allVisible&=loop.Explosives.IsWorldItemVisible(id);if(allVisible)break;Pump();yield return null;}Need(allVisible,"Actual Range world art stream incomplete at final source camera stage");yield return null;
            var source=JsonUtility.FromJson<Cameras>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Tools/PlayableR23/WorldLoot/ArchiveItems/reference.json"))));int count=0;
            foreach(var shot in source.shots)if(shot.mapId=="range")
            {
                Vector3 origin=new Vector3(0,0,-600),eye=origin+new Vector3(shot.eyeMapLocal[0],shot.eyeMapLocal[1],-shot.eyeMapLocal[2]),target=origin+new Vector3(shot.targetMapLocal[0],shot.targetMapLocal[1],-shot.targetMapLocal[2]);var go=new GameObject("Source matched training camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(loop.Rig.Head);camera.enabled=false;camera.cullingMask=(1<<0)|(1<<18);camera.stereoTargetEye=StereoTargetEyeMask.None;camera.fieldOfView=58;camera.aspect=1.5f;camera.ResetProjectionMatrix();go.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,Vector3.up));var texture=RenderTexture.GetTemporary(1200,800,24);var active=RenderTexture.active;Texture2D pixels=null;
                try{camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();string path=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),shot.name+".png");File.WriteAllBytes(path,pixels.EncodeToPNG());result.shots.Add(path);count++;}finally{camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(texture);if(pixels!=null)UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(go);}
            }
            Need(count==2,"Two actual source-camera Range observations required");g.notes="Actual source Range rack/food cameras1200x800/FOV58, one reflection/origin600; no global radiometry claim.";yield break;
        }
        IEnumerator Sparse(Group g)
        {
            var helper=new R24SparseWorldSmoke();yield return helper.Run(loop);result.sparseJson=helper.Json;Need(helper.Passed,"Actual positive sparse-map helper failed; see independent sparseJson");g.notes="Actual sparse helper result retained independently: "+helper.Json;
        }
        IEnumerator Cleanup(Group g){loop.StowPaddles();loop.ReturnToLobby();if(loop.Map.MapId!=savedMap){loop.SelectMap(savedMap,false);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+70);while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId==savedMap,"Original map cleanup failed");loop.ReturnToLobby();}Need(loop.Gameplay.VisiblePracticeTargetCount==0&&loop.GetComponentInChildren<R24RescueTrainingDummy>(true)==null,"Training art stays visible in lobby");g.notes="Source map/lobby restored, no synthetic Health ally or collision geometry.";}
        void Pump(){loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();Physics.SyncTransforms();}
        void Finish(){result.success=result.nativeExecuted&&result.error.Length==0&&result.groups.Count==(captureOnly||fixesOnly?3:4);foreach(var g in result.groups)result.success&=g.passed;R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));}
    }
}
#endif
