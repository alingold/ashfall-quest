#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.World;
using UnityEditor;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public sealed class R24LowerGrilleSmoke
    {
        static readonly string[] Slugs={"alfa-aglae-souza","classic-bigbox-vr","bigbabyjesuz-classic-kiki-80","classic-bw-187norvis","cont-cl-er-ssd-cjcoops","cont-cl-ssd-cjcoops","evolving-map-bigbox","statmaster-classic-soulsmight","statmaster-evolving-soulsmight"};
        [Serializable]sealed class Row{public string map="",error="";public bool passed,loaded;public int allOwners,firstNewOwner,breakResetCases,pairApertures,floorCases,renderers,colliders,shaders,retainedCutOwners;}
        [Serializable]sealed class Result{public string fixture="R24LowerGrilleSmoke-v1",error="";public bool success,nativeExecuted,headsetVerified=false;public List<Row> maps=new List<Row>();}
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        static Vector3 V(float[] value){return new Vector3(value[0],value[1],value[2]);}
        static Collider First(R23PlayableMap map,int index)
        {
            var d=map.DestructibleDefinitions[index];Vector3 at=map.transform.TransformPoint(V(d.probe)),normal=map.transform.TransformDirection(V(d.normal)).normalized;
            RaycastHit nearest=new RaycastHit();float distance=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(at+normal*.12f,-normal,.24f,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(map.transform)&&hit.distance<distance){nearest=hit;distance=hit.distance;}
            string owner;bool broken;Need(nearest.collider!=null&&map.TryReadDestructible(nearest.collider,out owner,out broken)&&owner==d.id&&!broken,"Actual FIRST map collider owner differs: "+d.id);return nearest.collider;
        }
        static bool OwnedAncestor(Transform node,Transform map,int owner)
        {string suffix=" tower owner "+owner;for(var at=node;at!=null&&at!=map;at=at.parent)if(at.name.EndsWith(suffix,StringComparison.Ordinal))return true;return false;}
        static void Floor(R23PlayableLoop loop,bool game)
        {
            loop.ReturnToLobby();loop.StartPlaying();loop.WorldLoot.SetWorldEnabled(false);var rig=loop.Rig;var map=loop.Map;
            rig.Head.transform.localPosition=new Vector3(0,1.6f,0);rig.TrackingOrigin.localPosition=Vector3.zero;var spawn=game?map.GameSpawn:map.WarmupSpawn;
            rig.SetSpawn(spawn,spawn+Vector3.forward);loop.Calibration.NotifyRootPoseReset();rig.GameplayBlocked=false;float initial=rig.transform.position.y;
            for(int i=0;i<180;i++)rig.SmokeXrMove(Vector2.zero,.02f);Physics.SyncTransforms();RaycastHit support;
            Need(rig.HeadCollision.Support(rig.transform.position,rig.Head.transform.position,out support),"Authored source spawn support missing");
            Need(initial-rig.transform.position.y<.6f&&Mathf.Abs(rig.transform.position.y-support.point.y)<.06f,"Authored source spawn falls through source geometry");
            float y=rig.transform.position.y;for(int i=0;i<20;i++)rig.SmokeXrMove(Vector2.zero,.02f);Need(Mathf.Abs(rig.transform.position.y-y)<.002f,"Authored source spawn keeps falling");
        }
        public IEnumerator Start()
        {
            Debug.Log("R24 LOWER GRILLES ENTRY");var result=new Result();R23PlayableLoop loop=null;float startup=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<startup){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual XR-off ready scene required";R23SmokeControl.Finish(false,JsonUtility.ToJson(result,true));yield break;}
            result.nativeExecuted=true;var rig=loop.Rig;bool manual=loop.ManualSmoke,external=loop.ExternalProbePump,rigEnabled=rig.enabled,pump=loop.Gameplay.ExternalInputPump,persistence=loop.Calibration.SuppressPersistence;
            Vector3 head=rig.Head.transform.localPosition,origin=rig.TrackingOrigin.localPosition;float deadline=Time.realtimeSinceStartup+360;
            try
            {
                loop.ManualSmoke=true;loop.ExternalProbePump=true;rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
                foreach(string slug in Slugs)
                {
                    var row=new Row{map="archive:"+slug};result.maps.Add(row);
                    try{Need(Time.realtimeSinceStartup<deadline,"360s lower grille bound");loop.SelectMap(row.map,false);}catch(Exception e){row.error=e.ToString();}
                    float end=Mathf.Min(deadline,Time.realtimeSinceStartup+60);while(row.error.Length==0&&!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
                    try
                    {
                        Need(row.error.Length==0&&loop.Ready&&loop.Map.MapId==row.map,"Actual map load failed: "+loop.Failure+row.error);row.loaded=true;loop.StartPlaying();loop.WorldLoot.SetWorldEnabled(false);Physics.SyncTransforms();
                        var map=loop.Map;var defs=map.DestructibleDefinitions;int old=slug=="alfa-aglae-souza"?948:slug=="classic-bigbox-vr"?718:(slug=="evolving-map-bigbox"||slug=="statmaster-evolving-soulsmight")?337:138;
                        Need(defs.Length==old+10,"Exact retained+new owner count differs");row.allOwners=defs.Length;row.firstNewOwner=old;
                        if(slug=="classic-bigbox-vr"){Need(map.SourceCutApplied&&map.SourceCutOwnerCount==381,"Protected Classic source-cut owners lost");row.retainedCutOwners=map.SourceCutOwnerCount;}
                        var renderers=map.GetComponentsInChildren<Renderer>(true);var colliders=map.GetComponentsInChildren<Collider>(true);row.renderers=renderers.Length;row.colliders=colliders.Length;var shaders=new HashSet<Shader>();
                        foreach(var renderer in renderers)foreach(var material in renderer.sharedMaterials)if(material!=null&&material.shader!=null&&shaders.Add(material.shader))foreach(var message in ShaderUtil.GetShaderMessages(material.shader))Need(message.severity.ToString()!="Error",material.shader.name+": "+message.message);row.shaders=shaders.Count;
                        for(int index=old;index<old+10;index++)
                        {
                            Need(Time.realtimeSinceStartup<deadline,"360s lower grille bound");map.ResetDestructibles();Physics.SyncTransforms();
                            string expected=slug+"/lower_tower_grille/core_"+((index-old)/2).ToString("00")+((index-old)%2==0?"_inner":"_outer");Need(defs[index].id==expected,"Appended grille ID/order differs");
                            var first=First(map,index);Need(map.TryDamageCollider(first,1)&&map.BrokenDestructibleCount==1&&!first.enabled,"Actual grille damage/withdrawal refused: "+expected);
                            int views=0;foreach(var renderer in renderers)if(OwnedAncestor(renderer.transform,map.transform,index)){views++;Need(!renderer.gameObject.activeInHierarchy,"Owned grille render part stayed visible: "+expected);}Need(views>0,"No actual owned grille renderer");
                            int owned=0;foreach(var collider in colliders){string id;bool broken;if(map.TryReadDestructible(collider,out id,out broken)&&id==expected){owned++;Need(broken&&!collider.enabled&&!collider.gameObject.activeInHierarchy,"Owned grille collision part stayed live: "+expected);}}Need(owned>0,"No actual owned grille collider");
                            map.ResetDestructibles();Physics.SyncTransforms();Need(first.enabled&&first.gameObject.activeInHierarchy&&map.BrokenDestructibleCount==0,"Actual grille reset failed");row.breakResetCases++;
                        }
                        for(int pair=0;pair<5;pair++)
                        {
                            map.ResetDestructibles();Physics.SyncTransforms();int inner=old+pair*2,outer=inner+1;var a=First(map,inner);var b=First(map,outer);
                            Need(map.TryDamageCollider(a,1)&&map.TryDamageCollider(b,1)&&map.BrokenDestructibleCount==2,"Both exact source grille owners did not withdraw");
                            Vector3 p=V(defs[inner].center),q=V(defs[outer].center),axis=q-p;axis.y=0;float length=axis.magnitude;Need(length>3.5f&&length<4.5f,"Source pair gap differs");axis/=length;
                            Need(Vector3.Distance(map.transform.lossyScale,Vector3.one)<.0001f,"Declared native aperture requires unit source map scale");
                            var center=map.transform.TransformPoint((p+q)*.5f);var rotation=Quaternion.LookRotation(map.transform.TransformDirection(axis),map.transform.up);
                            foreach(var obstacle in Physics.OverlapBox(center,new Vector3(.8f,2.25f,length*.5f+.15f),rotation,Physics.AllLayers,QueryTriggerInteraction.Ignore))Need(!obstacle.transform.IsChildOf(map.transform),"Cooked map collision remains in declared paired grille prism: "+obstacle.name);
                            row.pairApertures++;map.ResetDestructibles();Physics.SyncTransforms();Need(a.enabled&&b.enabled&&map.BrokenDestructibleCount==0,"Paired grille reset failed");
                        }
                        Floor(loop,false);row.floorCases++;Floor(loop,true);row.floorCases++;
                        row.passed=row.breakResetCases==10&&row.pairApertures==5&&row.floorCases==2;Debug.Log("R24 LOWER GRILLES "+JsonUtility.ToJson(row));
                    }
                    catch(Exception e){row.error=e.ToString();Debug.LogError("R24 LOWER GRILLES "+row.map+" "+row.error);}
                    finally{if(loop.Ready)loop.Map.ResetDestructibles();}
                    if(!loop.Ready)break;
                }
            }
            finally
            {if(loop.Ready){loop.Map.ResetDestructibles();loop.ReturnToLobby();}rig.Head.transform.localPosition=head;rig.TrackingOrigin.localPosition=origin;loop.Calibration.SuppressPersistence=persistence;loop.Gameplay.ExternalInputPump=pump;rig.enabled=rigEnabled;loop.ExternalProbePump=external;loop.ManualSmoke=manual;}
            result.success=result.maps.Count==9;foreach(var row in result.maps)result.success&=row.passed&&row.error.Length==0;R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));
        }
    }
}
#endif
