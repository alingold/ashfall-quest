#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.World;
namespace Ashfall.Editor.Playable
{
    // Independent native observations; no XR/human acceptance is inferred.
    public sealed class R23SourceMotionSmoke
    {
        [Serializable]sealed class Result{public bool headHull,constructionClimbGlide,constructionDamage,carriedCoverDamage,authoredPads,range;public bool headsetVerified=false;public string error="";}
        static void Check(Result result,string group,Action body)
        {try{body();}catch(Exception error){result.error+=group+": "+error+"\n";Debug.LogException(error);}}
        public IEnumerator Start()
        {
            var result=new Result();R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready){result.error=loop==null?"Loop absent":loop.Failure??"Initial source load timed out";R23SmokeControl.Finish(false,JsonUtility.ToJson(result,true));yield break;}
            loop.ExternalProbePump=true;loop.ManualSmoke=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;
            loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.TrackingOrigin.localPosition=Vector3.zero;
            loop.SelectMap(R23MapCatalog.RangeId,true);end=Time.realtimeSinceStartup+90;
            while(!loop.Ready&&Time.realtimeSinceStartup<end&&loop.Failure==null)yield return null;
            Check(result,"Range",delegate{
                if(!loop.Ready||loop.Map.MapId!=R23MapCatalog.RangeId)throw new Exception(loop.Failure??"Actual Range load timed out");
                loop.StartPlaying();loop.Rig.GameplayBlocked=false;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);
                if(loop.Map.GetComponentsInChildren<BoxCollider>(true).Length!=4||loop.JumpPads.PadCount!=0||!loop.JumpPads.Ready)throw new Exception("Source Range box/pad census differs");
                Physics.SyncTransforms();RaycastHit hit;bool floor=false;
                if(Physics.Raycast(loop.Rig.Head.transform.position,Vector3.down,out hit,4,Physics.AllLayers,QueryTriggerInteraction.Ignore))Debug.Log("R23 Range unfiltered downward first hit: "+hit.collider.name+" "+hit.point);
                foreach(var candidate in Physics.RaycastAll(loop.Rig.Head.transform.position,Vector3.down,4,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(candidate.collider.transform.IsChildOf(loop.Map.transform)&&Mathf.Abs(candidate.point.y)<.001f&&candidate.normal.y>.9f)floor=true;
                if(!floor)throw new Exception("Source Range spawn has no actual native floor (actor-excluded query)");
                float before=loop.Rig.transform.position.y;for(int i=0;i<200;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
                if(loop.Rig.transform.position.y<before-.5f)throw new Exception("Source Range actual root fell through floor");
                R23SmokeControl.Capture(loop.Rig.Head,"range-native");result.range=true;
            });
            if(loop.Ready)
            {
                Check(result,"Construction damage",delegate{R23ConstructionDamageSmoke.Run();result.constructionDamage=true;});
                Check(result,"Carried cover damage",delegate{R23CarriedBuildDamageSmoke.Run(loop);result.carriedCoverDamage=true;});
                Check(result,"Capsule movement/climb/glide",delegate{loop.Rig.GameplayBlocked=false;R23ActivitySmoke.Run(loop);result.constructionClimbGlide=true;});
                Check(result,"Opt-in source head hull",delegate{R23HeadHullSmoke.Run(loop);result.headHull=true;});
                loop.SelectMap(R23MapCatalog.AlphaId,true);end=Time.realtimeSinceStartup+90;
                while((!loop.Ready||!loop.JumpPads.Ready)&&Time.realtimeSinceStartup<end&&loop.Failure==null)yield return null;
                Check(result,"ALFA pads",delegate{
                    if(!loop.Ready||!loop.JumpPads.Ready||loop.Map.MapId!=R23MapCatalog.AlphaId)throw new Exception(loop.Failure??"Actual ALFA pad load timed out");
                    loop.StartPlaying();loop.Rig.GameplayBlocked=false;R23JumpPadSmoke.Run(loop);result.authoredPads=true;
                });
            }
            R23SmokeControl.Finish(result.error.Length==0,JsonUtility.ToJson(result,true));
        }
    }
}
#endif
