#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Integration.Playable;
using UnityEngine;
using UnityEditor;
namespace Ashfall.Editor.Playable
{
    public sealed class R24TowerFamilySmoke
    {
        readonly bool classicPassages,classicSourceCuts,familyPassages;
        public R24TowerFamilySmoke(bool classicPassages=false,bool classicSourceCuts=false,bool familyPassages=false){this.classicPassages=classicPassages||classicSourceCuts;this.classicSourceCuts=classicSourceCuts;this.familyPassages=familyPassages;if(familyPassages&&this.classicPassages)throw new ArgumentException("Choose exactly one finite source owner profile");}
        static readonly string[] Slugs={"bigbabyjesuz-classic-kiki-80","classic-bw-187norvis","cont-cl-er-ssd-cjcoops","cont-cl-ssd-cjcoops","evolving-map-bigbox","statmaster-classic-soulsmight","statmaster-evolving-soulsmight"};
        [Serializable]sealed class MapResult{public string map="",error="";public bool passed,loaded;public int owners,allOwners,brokenAndReset,floorCases,passageApertureCases,sourceCutApertureCases,cutMeshes,cutColliders,cutOrientedMeshes,cutTriangles,actualMapRenderers,actualMapColliders;}
        [Serializable]sealed class Result{public bool success,nativeExecuted,headsetVerified=false;public string error="";public List<MapResult> maps=new List<MapResult>();}
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        public IEnumerator Start()
        {
            var result=new Result();R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-disabled scene required";Finish(result);yield break;}
            result.nativeExecuted=true;var rig=loop.Rig;bool manual=loop.ManualSmoke,external=loop.ExternalProbePump,enabled=rig.enabled,pump=loop.Gameplay.ExternalInputPump,persist=loop.Calibration.SuppressPersistence;
            Vector3 head=rig.Head.transform.localPosition;float deadline=Time.realtimeSinceStartup+360;
            try
            {
                loop.ManualSmoke=true;loop.ExternalProbePump=true;rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
                foreach(string slug in classicPassages?new[]{"classic-bigbox-vr"}:Slugs)
                {
                    var row=new MapResult{map="archive:"+slug};result.maps.Add(row);
                    try{Need(Time.realtimeSinceStartup<deadline,"360s family bound");loop.SelectMap(row.map,false);}catch(Exception e){row.error=e.ToString();}
                    float loadEnd=Mathf.Min(deadline,Time.realtimeSinceStartup+60);
                    while(row.error.Length==0&&!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<loadEnd)yield return null;
                    try
                    {
                        Need(row.error.Length==0&&loop.Ready&&loop.Map.MapId==row.map,"Actual source map load failed: "+loop.Failure+row.error);row.loaded=true;loop.StartPlaying();loop.WorldLoot.SetWorldEnabled(false);Physics.SyncTransforms();
                        var map=loop.Map;var definitions=map.DestructibleDefinitions;int expected=familyPassages?(slug=="evolving-map-bigbox"||slug=="statmaster-evolving-soulsmight"?319:120):classicSourceCuts?718:classicPassages?337:18;int first=familyPassages?18:0;int limit=first+expected;Need(definitions.Length>=limit&&(!(classicPassages||familyPassages)||definitions.Length==limit),"Exact complete source owner subset missing");row.owners=expected;row.allOwners=definitions.Length;var actualRenderers=map.GetComponentsInChildren<Renderer>(true);row.actualMapRenderers=actualRenderers.Length;row.actualMapColliders=map.GetComponentsInChildren<Collider>(true).Length;if(familyPassages){var shaders=new HashSet<Shader>();foreach(var renderer in actualRenderers)foreach(var material in renderer.sharedMaterials)if(material!=null&&material.shader!=null&&shaders.Add(material.shader))foreach(var message in ShaderUtil.GetShaderMessages(material.shader))Need(message.severity.ToString()!="Error",material.shader.name+": "+message.message);}
                        if(classicSourceCuts){row.cutMeshes=map.SourceCutMeshCount;row.cutColliders=map.SourceCutColliderCount;row.cutOrientedMeshes=map.SourceCutOrientedMeshCount;row.cutTriangles=map.SourceCutTriangleCount;Need(map.SourceCutApplied&&map.SourceCutOwnerCount==381&&row.cutMeshes>0&&row.cutColliders==row.cutMeshes&&row.cutTriangles==29289,"Actual source-cut assembly/collider creation incomplete");}
                        for(int i=first;i<limit;i++)
                        {
                            Need(Time.realtimeSinceStartup<deadline,"360s family bound");map.ResetDestructibles();Physics.SyncTransforms();var d=definitions[i];Vector3 at=map.transform.TransformPoint(new Vector3(d.probe[0],d.probe[1],d.probe[2])),normal=map.transform.TransformDirection(new Vector3(d.normal[0],d.normal[1],d.normal[2])).normalized;
                            RaycastHit nearest=new RaycastHit();float distance=float.PositiveInfinity;
                            foreach(var hit in Physics.RaycastAll(at+normal*.12f,-normal,.24f,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(map.transform)&&hit.distance<distance){nearest=hit;distance=hit.distance;}
                            string owner;bool broken;Need(nearest.collider!=null&&map.TryReadDestructible(nearest.collider,out owner,out broken)&&owner==d.id&&!broken,"Source tower probe ownership differs: "+d.id);
                            Need(map.TryDamageCollider(nearest.collider,1)&&map.BrokenDestructibleCount==1&&!nearest.collider.enabled,"Exact tower collider did not withdraw: "+d.id);
                            if((classicSourceCuts&&i>=337)||familyPassages)
                            {
                                // A disabled owned collider is insufficient if the
                                // old larger raw face still seals the source cut.
                                foreach(var hit in Physics.RaycastAll(at+normal*.12f,-normal,.24f,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                                    if(hit.collider.transform.IsChildOf(map.transform))Need(Mathf.Abs(Vector3.Dot(hit.point-at,normal))>.005f,"Static collision still seals source passage probe: "+d.id+" / "+hit.collider.name);
                                if(familyPassages)row.passageApertureCases++;else row.sourceCutApertureCases++;
                            }
                            foreach(var renderer in actualRenderers)if(renderer.gameObject.name.EndsWith(" tower owner "+i,StringComparison.Ordinal))Need(!renderer.gameObject.activeInHierarchy,"Source backing remained visible: "+d.id);
                            map.ResetDestructibles();Physics.SyncTransforms();Need(nearest.collider.enabled&&map.BrokenDestructibleCount==0,"Exact tower collider did not reset: "+d.id);row.brokenAndReset++;
                        }
                        // Hatches are intentionally open after damage. Check the actual
                        // authored lobby/game spawns after reset, never invent a floor.
                        if(familyPassages){foreach(bool game in new[]{false,true}){loop.ReturnToLobby();loop.StartPlaying();loop.WorldLoot.SetWorldEnabled(false);rig.Head.transform.localPosition=new Vector3(0,1.6f,0);rig.TrackingOrigin.localPosition=Vector3.zero;var spawn=game?map.GameSpawn:map.WarmupSpawn;rig.SetSpawn(spawn,spawn+Vector3.forward);loop.Calibration.NotifyRootPoseReset();rig.GameplayBlocked=false;Settle(loop);row.floorCases++;}}
                        else foreach(bool game in new[]{false,true})foreach(var offset in new[]{Vector2.zero,new Vector2(.35f,-.4f)})foreach(float eye in new[]{.7f,1.8f})
                        {
                            Need(Time.realtimeSinceStartup<deadline,"360s family bound");loop.ReturnToLobby();rig.Head.transform.localPosition=new Vector3(offset.x,eye,offset.y);rig.TrackingOrigin.localPosition=Vector3.zero;loop.UI.ShowReady();loop.UI.OpenSpawnChoice();Need(loop.UI.ConfirmMapSpawn(game),"Source spawn route refused");rig.GameplayBlocked=false;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);loop.Calibration.RequestRecalibration();for(int n=0;n<8;n++)loop.Calibration.Tick(true,true,false,.05f);
                            Settle(loop);row.floorCases++;loop.RestartRound();rig.GameplayBlocked=false;Settle(loop);row.floorCases++;
                        }
                        row.passed=row.brokenAndReset==expected&&row.floorCases==(familyPassages?2:16)&&(!familyPassages||row.passageApertureCases==expected)&&(!classicSourceCuts||row.sourceCutApertureCases==381);Debug.Log("R24 TOWER FAMILY "+JsonUtility.ToJson(row));
                    }
                    catch(Exception e){row.error=e.ToString();Debug.LogError("R24 TOWER FAMILY "+row.map+" "+row.error);}
                    finally{if(loop.Ready)loop.Map.ResetDestructibles();}
                    if(!loop.Ready)break;
                }
            }
            finally
            {
                if(loop.Ready){loop.Map.ResetDestructibles();loop.ReturnToLobby();}rig.Head.transform.localPosition=head;loop.Calibration.SuppressPersistence=persist;loop.Gameplay.ExternalInputPump=pump;rig.enabled=enabled;loop.ExternalProbePump=external;loop.ManualSmoke=manual;
            }
            result.success=result.maps.Count==(classicPassages?1:7);foreach(var row in result.maps)result.success&=row.passed&&row.error.Length==0;Finish(result);
        }
        static void Settle(R23PlayableLoop loop)
        {
            float start=loop.Rig.transform.position.y;for(int n=0;n<180;n++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);Physics.SyncTransforms();RaycastHit floor;
            Need(loop.Rig.HeadCollision.Support(loop.Rig.transform.position,loop.Rig.Head.transform.position,out floor),"Authored spawn has no actual body/head floor support");Need(start-loop.Rig.transform.position.y<.6f&&Mathf.Abs(loop.Rig.transform.position.y-floor.point.y)<.06f,"Source spawn falls away from its real floor");float y=loop.Rig.transform.position.y;for(int n=0;n<20;n++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);Need(Mathf.Abs(y-loop.Rig.transform.position.y)<.002f,"Actor keeps falling after settle");
        }
        static void Finish(Result result){R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));}
    }
}
#endif
