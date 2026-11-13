#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
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
        static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        void Bound(){Need(Time.realtimeSinceStartup<deadline,"Source training/sparse fixture exceeded360s active bound");}
        public IEnumerator Run()
        {
            result=new Receipt();float end=Time.realtimeSinceStartup+120;while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-disabled Root required";Finish();yield break;}
            savedMap=loop.Map.MapId;manual=loop.ManualSmoke;external=loop.ExternalProbePump;rig=loop.Rig.enabled;persistence=loop.Calibration.SuppressPersistence;savedOrigin=loop.Rig.TrackingOrigin.localPosition;savedHead=loop.Rig.Head.transform.localPosition;savedHeadQ=loop.Rig.Head.transform.localRotation;savedLeft=loop.Rig.Left.localPosition;savedRight=loop.Rig.Right.localPosition;savedLeftQ=loop.Rig.Left.localRotation;savedRightQ=loop.Rig.Right.localRotation;
            result.nativeExecuted=true;deadline=Time.realtimeSinceStartup+360;loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;loop.Rig.TrackingOrigin.localPosition=Vector3.zero;loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Head.transform.localRotation=Quaternion.identity;
            try
            {
                foreach(string name in captureOnly?new[]{"ActualSourceRangeTrainingLayout","ActualRangeMatchedCapture","ActualSceneCleanup"}:fixesOnly?new[]{"ActualSourceRangeTrainingLayout","ActualPhysicalPaddlesRescue","ActualRangeMatchedCapture","ActualSceneCleanup"}:new[]{"ActualSourceRangeTrainingLayout","ActualIndependentPracticeReset","ActualPhysicalPaddlesRescue","ActualRangeMatchedCapture","ActualPositiveSparseArchive","ActualSceneCleanup"})
                {
                    var g=new Group{name=name};result.groups.Add(g);IEnumerator work=name=="ActualSourceRangeTrainingLayout"?Layout(g):name=="ActualIndependentPracticeReset"?Practice(g):name=="ActualPhysicalPaddlesRescue"?Rescue(g):name=="ActualRangeMatchedCapture"?Capture(g):name=="ActualPositiveSparseArchive"?Sparse(g):Cleanup(g);
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
            Need(loop.Gameplay.PracticeTargetCount==1&&loop.Gameplay.VisiblePracticeTargetCount==1,"Production must have one authored practice dummy, not the former three-headset row");
            var expected=loop.Map.GameSpawn+new Vector3(-3,-1,6);Need(Vector3.Distance(loop.Gameplay.PracticeAimPoint(0,false),expected+Vector3.up*1.14f)<.0001f,"Practice dummy isn't at exact reflected checkpoint offset");
            Need(loop.RescueTraining!=null&&Vector3.Distance(loop.RescueTraining.WorldPosition,loop.Map.GameSpawn+new Vector3(-4,.45f,3))<.0001f&&loop.RescueTraining.Downed&&loop.RescueTraining.Health==0&&loop.RescueTraining.ColliderCount==0,"Actual solo rescue geometry/state/placement differs from source");
            R24ReviveTarget target;Need(loop.RescueTraining.TryReadTarget(out target),"Source casualty not actually eligible: visible="+loop.RescueTraining.WorldVisible+" downed="+loop.RescueTraining.Downed+" health="+loop.RescueTraining.Health+" rootFrame="+Time.frameCount);Need(Vector3.Distance(V(target.LeftPad),loop.RescueTraining.WorldPosition+new Vector3(-.13f,.61f,-.13f))<.0001f&&Vector3.Distance(V(target.RightPad),loop.RescueTraining.WorldPosition+new Vector3(.13f,.61f,-.13f))<.0001f,"Actual source contacts reflected twice/wrong basis");
            var before=loop.Gameplay.PracticeAimPoint(0,false);loop.Rig.Head.transform.localRotation=Quaternion.Euler(0,117,0);loop.StartPlaying();Pump();Need(Vector3.Distance(before,loop.Gameplay.PracticeAimPoint(0,false))<.0001f,"Head yaw moved source training fixture");loop.Rig.Head.transform.localRotation=Quaternion.identity;g.notes="One practice dummy at checkpoint+(-3,-1,+6), exact rescue body at(-4,+.45,+3) with reflected two source pads/no colliders; head yaw independent.";
        }
        IEnumerator Practice(Group g)
        {
            Need(loop.Gameplay.ApplyTargetDamage("target/0",25,loop.Rig.Head.transform.position),"Actual practice target damage refused");Need(loop.Gameplay.ReadPracticeTarget(0).Shield==75,"Real shield damage did not apply");loop.Gameplay.HolsterCurrent();loop.UI.ToggleMenu();
            for(int i=0;i<5;i++)loop.Gameplay.TickSourcePracticeTargets(.25f);Need(loop.Gameplay.ReadPracticeTarget(0).Shield==75,"Dummy reset before source1.5seconds");loop.Gameplay.TickSourcePracticeTargets(.25f);Need(loop.Gameplay.ReadPracticeTarget(0).Shield==100&&loop.Gameplay.ReadPracticeTarget(0).Health==100,"Source dummy reset clock incorrectly depends on held weapon/menu/focus/player");loop.UI.ToggleMenu();Pump();g.notes="Real Health target took25 shield, stayed damaged at1.25s, reset at1.5s while holstered/menu open; no gun-time double count.";yield break;
        }
        IEnumerator Rescue(Group g)
        {
            var dummy=loop.RescueTraining;Need(dummy!=null&&dummy.Downed,"Actual source casualty unavailable");loop.Rig.SetSpawn(dummy.WorldPosition+new Vector3(0,-.45f,-.7f),dummy.WorldPosition+Vector3.up*1.5f);loop.Calibration.NotifyRootPoseReset();Physics.SyncTransforms();
            ItemStep(.02f);Need(loop.EquipPaddles(),"Actual Root refused reusable paddles with free tracked hands");
            Vector3 basePoint=loop.Rig.transform.position+new Vector3(0,1.15f,-.45f);for(int i=0;i<125&&!loop.Paddles.State.Ready;i++){float y=(i%20<=10?i%20:20-i%20)*.025f;SetPaddle(0,basePoint+new Vector3(-.20f,y,0),Vector3.right);SetPaddle(1,basePoint+new Vector3(.20f,y,0),Vector3.left);ItemStep(.05f);}
            Need(loop.Paddles.State.Ready&&dummy.Downed,"Actual hand motion did not charge source paddles independently of target contact");R24ReviveTarget target;Need(dummy.TryReadTarget(out target),"Actual downed source target disappeared before contact");int before=dummy.ReviveCount,discharges=loop.Paddles.State.SuccessfulRevives;
            // Native approach tests the source outward .4m allowance, not just
            // coincident contacts (which would hide a double-Z reflection).
            SetPaddle(0,V(target.LeftPad)+Vector3.back*.4f,Vector3.right);SetPaddle(1,V(target.RightPad)+Vector3.back*.4f,Vector3.left);ItemStep(.02f);
            Need(!dummy.Downed&&dummy.Health==50&&dummy.ReviveCount==before+1&&loop.Paddles.State.SuccessfulRevives==discharges+1&&loop.Paddles.DischargeVisible,"Real Root completed source rescue/HP50/discharge exactly once");ItemStep(.02f);Need(dummy.ReviveCount==before+1,"Held contact completed twice");
            loop.StowPaddles();for(int i=0;i<47;i++)dummy.Tick(.25f,true);Need(!dummy.Downed&&dummy.ResetRemaining>0,"Source casualty reset before12seconds");dummy.Tick(.25f,true);Need(dummy.Downed&&dummy.Health==0&&dummy.ReviveCount==before+1,"Source casualty did not reset after12s even with ordinary menu pause");
            dummy.SetWorldVisible(false);Need(!dummy.TryReadTarget(out target)&&!dummy.TryRevive(R24RescueTrainingDummyState.TargetId),"Hidden source casualty remains eligible");dummy.SetWorldVisible(true);g.notes="Charged actual tracked paddle poses through Root; source front.4m contacts completed once toHP50+native discharge;12s reset retained count; hidden casualty refused.";yield break;
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
        IEnumerator Cleanup(Group g){loop.StowPaddles();loop.ReturnToLobby();if(loop.Map.MapId!=savedMap){loop.SelectMap(savedMap,false);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+70);while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId==savedMap,"Original map cleanup failed");loop.ReturnToLobby();}Need(loop.Gameplay.VisiblePracticeTargetCount==0&&!loop.RescueTraining.WorldVisible,"Training art stays visible in lobby");g.notes="Source map/lobby restored, no synthetic Health ally or collision geometry.";}
        void Pump(){loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();Physics.SyncTransforms();}
        void SetPaddle(int hand,Vector3 origin,Vector3 normal){var tracked=hand==0?loop.Rig.Left:loop.Rig.Right;tracked.SetPositionAndRotation(origin-normal*.10f,Quaternion.LookRotation(normal,Vector3.up)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation));}
        void ItemStep(float seconds){var left=new HandSample{Tracked=true};var right=new HandSample{Tracked=true};loop.Rig.SmokeTrackedHands(left,right);loop.StepItemInput(seconds,left,right,true,false);Physics.SyncTransforms();}
        void Finish(){result.success=result.nativeExecuted&&result.error.Length==0&&result.groups.Count==(captureOnly?3:fixesOnly?4:6);foreach(var g in result.groups)result.success&=g.passed;R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));}
    }
}
#endif
