#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.World;
using Ashfall.Integration.Playable.World.Loot;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public sealed class R24ArchiveItemsSmoke
    {
        [Serializable]sealed class Group{public string name="",error="";public bool passed;public List<string> notes=new List<string>();}
        [Serializable]sealed class Result{public bool success,nativeExecuted,headsetVerified=false;public string error="";public List<Group> groups=new List<Group>();public List<Pose> poses=new List<Pose>();public List<string> shots=new List<string>();}
        [Serializable]sealed class Pose{public string id="",kind="",outcome="";public int index;public Vector3 sourcePosition,worldPosition,requestedSource;public bool visible;}
        [Serializable]sealed class Reference{public string resultSha256="";public Shot[] shots=null;}
        [Serializable]sealed class Shot{public string name="",mapId="";public float[] eyeMapLocal=null,targetMapLocal=null;}
        R23PlayableLoop loop;Result result;float deadline;string originalMap;bool manual,external,rigEnabled,persistence;Vector3 head,origin,left,right;Reference reference;
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}static Float3 F(float[] p){return new Float3(p[0],p[1],p[2]);}
        void Bound(){Need(Time.realtimeSinceStartup<deadline,"Archive item fixture exceeded300s active bound");}
        public IEnumerator Run()
        {
            result=new Result();float end=Time.realtimeSinceStartup+120;while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-disabled Root required";Finish();yield break;}
            originalMap=loop.Map.MapId;manual=loop.ManualSmoke;external=loop.ExternalProbePump;rigEnabled=loop.Rig.enabled;persistence=loop.Calibration.SuppressPersistence;head=loop.Rig.Head.transform.localPosition;origin=loop.Rig.TrackingOrigin.localPosition;left=loop.Rig.Left.localPosition;right=loop.Rig.Right.localPosition;
            result.nativeExecuted=true;deadline=Time.realtimeSinceStartup+300;loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;loop.Rig.TrackingOrigin.localPosition=Vector3.zero;loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);
            try
            {
                reference=JsonUtility.FromJson<Reference>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Tools/PlayableR23/WorldLoot/ArchiveItems/reference.json"))));
                foreach(string name in new[]{"ActualRangeRetainedSourceItems","ActualExploreAlphaSharedCachesAndScatter","ActualSourceMountedRedRacks","ActualSceneCleanup"})
                {
                    var g=new Group{name=name};result.groups.Add(g);IEnumerator work=name=="ActualRangeRetainedSourceItems"?Range(g):name=="ActualExploreAlphaSharedCachesAndScatter"?Alpha(g):name=="ActualSourceMountedRedRacks"?Racks(g):Cleanup(g);
                    var stack=new Stack<IEnumerator>();stack.Push(work);try{while(stack.Count>0){bool moved=false;object current=null;try{Bound();moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}catch(Exception e){g.error=e.ToString();Debug.LogError("R24 ARCHIVE ITEMS FAIL "+name+": "+e);break;}if(!moved){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}var nested=current as IEnumerator;if(nested!=null)stack.Push(nested);else yield return current;}g.passed=g.error.Length==0;}finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}
                }
            }
            finally
            {
                try{if(loop!=null&&loop.Ready)loop.ReturnToLobby();}catch(Exception e){result.error+="Cleanup: "+e;}
                if(loop!=null){loop.Rig.TrackingOrigin.localPosition=origin;loop.Rig.Head.transform.localPosition=head;loop.Rig.Left.localPosition=left;loop.Rig.Right.localPosition=right;loop.ManualSmoke=manual;loop.ExternalProbePump=external;loop.Rig.enabled=rigEnabled;loop.Calibration.SuppressPersistence=persistence;}Finish();
            }
        }
        IEnumerator Load(string map)
        {
            loop.ReturnToLobby();loop.SelectMap(map,true);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+80);while((!loop.Ready||loop.Map.MapId!=map)&&loop.Failure==null&&Time.realtimeSinceStartup<end){Bound();yield return null;}Need(loop.Ready&&loop.Map.MapId==map,"Actual map load failed: "+map+" / "+loop.Failure);loop.UI.ClearMatchSelection();loop.StartPlaying();Pump();Need(loop.ActualMatchRules==null,"Explore must not fabricate a match to prepare item placement");
        }
        IEnumerator Range(Group g)
        {
            yield return Load("range");float end=Time.realtimeSinceStartup+8;while(Time.realtimeSinceStartup<end){bool pending=false;foreach(var row in R24WorldPickupLayout.Ammo()){R24PickupSettleResult r;if(!loop.Supplies.TryReadSourceSettleResult(row.Id,out r)||r.Outcome==R24PickupSettleOutcome.Pending)pending=true;}if(!pending)break;Pump();yield return null;}
            foreach(var row in R24WorldPickupLayout.Ammo()){R24PickupSettleResult r;Need(loop.Supplies.TryReadSourceSettleResult(row.Id,out r)&&r.Outcome==R24PickupSettleOutcome.Settled,"Retained Range native settle failed: "+row.Id);Need(r.Position.Y>-.1f,"Do not reproduce reference inherited below-deck ammo defect");}
            Need(loop.Supplies.ArchiveSupplyIds.Length==0&&!loop.ArchiveWorldReady,"Range must retain its builtin layout rather than archive scatter");Capture("range");g.notes.Add("Range keeps native floor-settled base supplies; genuine source inherited below-deck defect deliberately not copied.");
        }
        IEnumerator Alpha(Group g)
        {
            yield return Load(R23MapCatalog.AlphaId);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+70);
            while(Time.realtimeSinceStartup<end){Pump();if(loop.ArchiveWorldReady&&!loop.WorldLoot.PreparationPending&&loop.WorldLoot.CacheSourceGeneration==loop.ArchiveWorldGeneration&&loop.ArchivePickups.AppliedGeneration==loop.ArchiveWorldGeneration)break;Need(loop.ArchiveWorldFailure==null&&loop.WorldLoot.Failure==null,"Source world/cache failure: "+loop.ArchiveWorldFailure+" / "+loop.WorldLoot.Failure);yield return null;}
            Need(loop.ArchiveWorldReady&&loop.ArchivePickups.AppliedMapId==loop.Map.MapId&&loop.WorldLoot.PreparedMapId==loop.Map.MapId&&!loop.WorldLoot.PreparationPending,"Explore shared native world/scatter/cache preparation incomplete");
            var loot=loop.ArchiveWorldPlan.Loot;Need(loot.Length>0&&loop.WorldLoot.ValidatedSites==loop.WorldLoot.InitialAcceptedSites+loop.WorldLoot.FallbackSites+loop.WorldLoot.SparseAddedSites,"Cache prefix/fallback/sparse source conservation failed");
            Need(loop.WorldLoot.CacheSourceGeneration==loop.ArchiveWorldGeneration&&loop.WorldLoot.SourceCacheSitePositions.Length==loop.WorldLoot.ValidatedSites,"Shared source context generation/frame differs");
            foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks())
            {
                Vector3 at;Quaternion rotation;bool held;bool found=row.IsSupply?loop.Supplies.TryReadWorldPose(row.Id,out at,out rotation,out held):loop.Consumables.TryReadWorldItem(row.Id,out at,out rotation,out held);Need(found&&!held,"Actual archive food/bag missing: "+row.Id);Float3 source;Need(loop.TryArchiveWorldToSource(at,out source),"Food source frame unavailable");var requested=loot[R24ArchivePickupLayout.FoodLootIndex(row.SourceIndex,loot.Length)];Need(Vector3.Distance(V(source),V(requested))<.0001f,"Food must stay at direct authored scatter: "+row.Id);result.poses.Add(new Pose{id=row.Id,kind=row.Kind,index=row.SourceIndex,sourcePosition=V(source),worldPosition=at,requestedSource=V(requested),outcome="ArchiveAssigned",visible=true});
            }
            end=Mathf.Min(deadline,Time.realtimeSinceStartup+12);while(Time.realtimeSinceStartup<end){bool pending=false;foreach(string id in loop.Supplies.ArchiveSupplyIds){R24PickupSettleResult r;if(!loop.Supplies.TryReadSourceSettleResult(id,out r)||r.Outcome==R24PickupSettleOutcome.Pending)pending=true;}if(!pending)break;Pump();yield return null;}
            Need(loop.Supplies.ArchiveSupplyIds.Length==72,"Exactly72 source archive supply records required");
            for(int k=0;k<72;k++)
            {
                string id=R24ArchivePickupLayout.AmmoId(loop.Map.MapId,k);R24PickupSettleResult r;Vector3 at;Quaternion rotation;bool collected,visible=false;Need(loop.Supplies.TryReadSourceSettleResult(id,out r)&&r.Outcome==R24PickupSettleOutcome.Settled,"Archive native supply settle failed: "+id+" / "+(r==null?"missing":r.Outcome.ToString()));Need(loop.Supplies.TryReadWorldPose(id,out at,out rotation,out collected)&&!collected&&loop.Supplies.TryReadWorldVisibility(id,out visible)&&visible,"Actual72 visible supply presentation missing");Float3 source;Need(loop.TryArchiveWorldToSource(at,out source),"Ammo source frame unavailable");var requested=loot[R24ArchivePickupLayout.AmmoLootIndex(k,loot.Length)];Need(Math.Abs(source.X-requested.X)<.0001f&&Math.Abs(source.Z-requested.Z)<.0001f,"Archive ammo must retain selected sourceXZ");result.poses.Add(new Pose{id=id,kind=R24ArchivePickupLayout.AmmoFamily(k),index=k,sourcePosition=V(source),worldPosition=at,requestedSource=V(requested),outcome=r.Outcome.ToString(),visible=visible});
            }
            foreach(var row in R24WorldPickupLayout.Ammo()){bool visible;Need(loop.Supplies.TryReadWorldVisibility(row.Id,out visible)&&!visible,"Legacy six ammo boxes remain visible in archive");}
            long generation=loop.ArchiveWorldGeneration;var original=loop.ArchiveWorldPlan;loop.UI.ToggleMenu();Pump();Need(loop.ArchiveWorldGeneration==generation&&System.Object.ReferenceEquals(loop.ArchiveWorldPlan,original),"Pause rebuilt actual world plan");loop.UI.ToggleMenu();Pump();Capture(loop.Map.MapId);
            g.notes.Add("Explore actual plan loot="+loot.Length+" cache initial="+loop.WorldLoot.InitialAcceptedSites+" fallback="+loop.WorldLoot.FallbackSites+" sparse="+loop.WorldLoot.SparseAddedSites+" floor/outside/hazard rejects="+loop.WorldLoot.InitialRejectedFloor+"/"+loop.WorldLoot.InitialRejectedOutside+"/"+loop.WorldLoot.InitialRejectedHazard+"; all12 direct +72 settled, base6 hidden; pause retains plan. Source finalAlpha reference242loot; exact per-item source pose comparison recorded separately.");
        }
        IEnumerator Racks(Group g)
        {
            yield return Load("archive:asylum-gods-pitdevil");float end=Mathf.Min(deadline,Time.realtimeSinceStartup+70);while(Time.realtimeSinceStartup<end){Pump();if(loop.ArchiveWorldReady&&loop.MountedRacks.PreparedMapId==loop.Map.MapId)break;Need(loop.ArchiveWorldFailure==null&&loop.MountedRacks.Failure==null,"Mounted source context failed");yield return null;}
            var owner=loop.MountedRacks;Need(owner.CandidateBoards==3,"Exact Asylum source three boards required");
            foreach(var board in owner.SourceBoards){Vector3 center;Need(loop.TryArchiveSourceToWorld(board.Center,out center),"Board source frame unavailable");loop.Rig.SetSpawn(center+Vector3.up*2,center+Vector3.forward);Pump();owner.ActivateNear();yield return null;}
            Need(owner.ValidatedBoards>0&&owner.WorldInstanceActionIds.Length==owner.ValidatedBoards*22,"No actual source board passed all22 native approach/LOS/floor checks: "+owner.LastRefusal);
            var before=owner.WorldInstanceActionIds;foreach(string id in before){R23WeaponInstanceInfo instance;Need(loop.Gameplay.TryReadWeaponInstance(id,out instance)&&instance.Stars==5,"Mounted firearm must be actual finite red-rarity owner");Need(Array.IndexOf(R24WeaponRackMath.Catalog,R23WeaponPickupRules.CatalogIndex(instance.ProfileId))>=0,"Non-tiered catalog on firearm board");}
            owner.SetWorldEnabled(false,false);foreach(var id in before)Need(!owner.AllowsWorldClaim(id)&&!loop.Gameplay.IsWorldWeaponVisible(id),"Disabled source rack remains claimable/visible");owner.SetWorldEnabled(true,true);owner.ActivateNear();Need(owner.WorldInstanceActionIds.Length==before.Length,"Visibility recreated finite rack instances");
            Need(owner.GetComponentsInChildren<Collider>(true).Length==0,"Rack hardware changed physics");g.notes.Add("Actual Asylum boards="+owner.CandidateBoards+" attempted="+owner.AttemptedBoards+" validated="+owner.ValidatedBoards+" rejected="+owner.RejectedBoards+" real22-per-board red firearm instances; disabled claims rejected; no hardware colliders. Other36 map board definitions source-only/unexercised.");
        }
        IEnumerator Cleanup(Group g){loop.ReturnToLobby();if(loop.Map.MapId!=originalMap){loop.SelectMap(originalMap,false);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+70);while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId==originalMap,"Original map restore failed");loop.ReturnToLobby();}Need(loop.Supplies.ArchiveSupplyIds.Length==0&&!loop.ArchiveWorldReady,"Old archive item/context survives actual map teardown");g.notes.Add("Original map/lobby restored; no alternate item, floor or authority owner.");}
        void Pump(){loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();Physics.SyncTransforms();}
        void Capture(string mapId)
        {
            foreach(var shot in reference.shots)if(shot.mapId==mapId)
            {
                Vector3 eye=Vector3.zero,target=Vector3.zero;if(mapId.StartsWith("archive:",StringComparison.Ordinal)){Need(loop.TryArchiveSourceToWorld(F(shot.eyeMapLocal),out eye)&&loop.TryArchiveSourceToWorld(F(shot.targetMapLocal),out target),"Capture source frame");}else{var origin=new Vector3(0,0,-600);eye=loop.Map.transform.TransformPoint(origin+new Vector3(shot.eyeMapLocal[0],shot.eyeMapLocal[1],-shot.eyeMapLocal[2]));target=loop.Map.transform.TransformPoint(origin+new Vector3(shot.targetMapLocal[0],shot.targetMapLocal[1],-shot.targetMapLocal[2]));}
                var go=new GameObject("Exact source pickup camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(loop.Rig.Head);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.fieldOfView=58;camera.aspect=1.5f;go.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,Vector3.up));var render=RenderTexture.GetTemporary(1200,800,24);var active=RenderTexture.active;Texture2D pixels=null;
                try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();string file=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),shot.name+".png");File.WriteAllBytes(file,pixels.EncodeToPNG());result.shots.Add(file);}finally{camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(render);if(pixels!=null)UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(go);}
            }
        }
        void Finish(){result.success=result.nativeExecuted&&result.error.Length==0&&result.groups.Count==4;foreach(var g in result.groups)result.success&=g.passed;R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));}
    }
}
#endif
