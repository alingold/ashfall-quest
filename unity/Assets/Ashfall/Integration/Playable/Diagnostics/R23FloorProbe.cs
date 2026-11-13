#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.World;
namespace Ashfall.Integration.Playable.Diagnostics
{
    public sealed class R23FloorProbe
    {
        [Serializable]sealed class Sample
        {
            public string map,entry;public Vector3 rawHead,logicalStart,actualFeetStart,headStart,bodyCenter,logicalEnd,headEnd;
            public bool restart,bodyEnabled,detectCollisions,layersIgnore,floorAtLogical,floorAtHead,fell,settledSupport;public float height,radius,slopeLimit,rootFloorY,headFloorY,initialDrop,settledFloorY,settledClearance,lastStepVertical;
        }
        [Serializable]sealed class Result {public bool probeComplete;public bool collisionMeshesReadable=true;public int collisionMeshChecks,expectedCases;public string error="";public List<Sample> cases=new List<Sample>();}
        static bool Floor(R23PlayableLoop loop,Vector3 from,out float y)
        {
            float nearest=float.PositiveInfinity;y=float.NaN;
            foreach(var h in Physics.RaycastAll(from+Vector3.up*.015f,Vector3.down,200,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(h.collider.gameObject.layer!=R23PlayableMap.BallisticOnlyLayer&&h.collider.transform.IsChildOf(loop.Map.transform)&&h.normal.y>=Ashfall.Integration.Playable.Climbing.R23HeadCollision.FloorNormal&&h.distance<nearest){nearest=h.distance;y=h.point.y;}
            return !float.IsPositiveInfinity(nearest);
        }
        Sample Measure(R23PlayableLoop loop,string map,bool training,bool restart)
        {
            var rig=loop.Rig;rig.SmokeXrMove(Vector2.zero,.02f);Physics.SyncTransforms();var body=rig.Body;
            var row=new Sample{map=map,entry=training?"training":"explore",restart=restart,rawHead=rig.Head.transform.localPosition,logicalStart=rig.transform.position,headStart=rig.Head.transform.position,actualFeetStart=rig.transform.TransformPoint(body.center)-Vector3.up*body.height*.5f,bodyCenter=body.center,height=body.height,radius=body.radius,slopeLimit=body.slopeLimit,bodyEnabled=body.enabled,detectCollisions=body.detectCollisions,layersIgnore=Physics.GetIgnoreLayerCollision(rig.gameObject.layer,loop.Map.gameObject.layer)};
            row.floorAtLogical=Floor(loop,new Vector3(row.logicalStart.x,row.headStart.y,row.logicalStart.z),out row.rootFloorY);row.floorAtHead=Floor(loop,row.headStart,out row.headFloorY);
            float previousY=rig.transform.position.y;
            for(int i=0;i<200;i++){previousY=rig.transform.position.y;rig.SmokeXrMove(Vector2.zero,.02f);}
            row.logicalEnd=rig.transform.position;row.headEnd=rig.Head.transform.position;row.initialDrop=row.logicalStart.y-row.logicalEnd.y;row.lastStepVertical=row.logicalEnd.y-previousY;
            row.settledSupport=Floor(loop,row.headEnd,out row.settledFloorY);row.settledClearance=row.logicalEnd.y-row.settledFloorY;
            // Actual Valley spawn is one metre above ground. A source-authored
            // drop is valid only when the real actor lands and remains supported.
            row.fell=!row.settledSupport||row.settledClearance<-.05f||Mathf.Abs(row.lastStepVertical)>.002f||row.settledClearance>.1f;
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"FLOOR PROBE {0} {1} restart={2} raw={3} start={4} actualfeet={5} end={6} rootfloor={7} headfloor={8} fell={9}",map,row.entry,restart,row.rawHead,row.logicalStart,row.actualFeetStart,row.logicalEnd,row.rootFloorY,row.headFloorY,row.fell);return row;
        }
        public IEnumerator Start(Action<bool,string> finish)
        {
            var result=new Result{expectedCases=R23MapCatalog.AvailableMaps.Length*40};R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready){result.error=loop==null?"No loop":loop.Failure??"Load timeout";finish(false,JsonUtility.ToJson(result,true));yield break;}
            loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;loop.UI.SetPlaying();loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);
            foreach(var choice in R23MapCatalog.AvailableMaps)foreach(bool training in new[]{false,true})
            {
                loop.SelectMap(choice.Id,training);end=Time.realtimeSinceStartup+90;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
                try
                {
                    if(!loop.Ready)throw new Exception(loop.Failure??"Map timeout");
                    foreach(var collider in loop.Map.GetComponentsInChildren<MeshCollider>(true)){result.collisionMeshChecks++;result.collisionMeshesReadable&=collider.sharedMesh!=null&&collider.sharedMesh.isReadable;}
                    foreach(var offset in new[]{Vector2.zero,new Vector2(1.2f,.8f),new Vector2(-1.4f,-.6f),new Vector2(3,-2),new Vector2(6,-4)})foreach(float rawEye in new[]{1.55f,1.9f})
                    {
                        var rig=loop.Rig;
                        rig.Head.transform.localPosition=new Vector3(offset.x,rawEye,offset.y);rig.TrackingOrigin.localPosition=Vector3.zero;loop.StartPlaying();
                        loop.Activities.ResetRound();loop.Climbing.ResetMotion();loop.Calibration.NotifyRootPoseReset();loop.Calibration.RequestRecalibration();for(int i=0;i<8;i++)loop.Calibration.Tick(true,true,false,.05f);
                        result.cases.Add(Measure(loop,choice.Id,training,false));loop.RestartRound();result.cases.Add(Measure(loop,choice.Id,training,true));
                    }
                }
                catch(Exception e){result.error=e.ToString();break;}
            }
            result.probeComplete=result.error.Length==0;bool passed=result.probeComplete&&result.collisionMeshesReadable&&result.cases.Count==result.expectedCases;foreach(var row in result.cases)passed&=!row.fell&&row.floorAtHead;finish(passed,JsonUtility.ToJson(result,true));
        }
    }
}
#endif
