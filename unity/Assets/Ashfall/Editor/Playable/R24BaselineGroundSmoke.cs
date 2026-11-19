#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Input;
using Ashfall.Integration.Playable.World;
using Ashfall.Integration.Playable.World.Loot;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Explosives;
using Ashfall.Integration.Playable.Consumables;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Genuine Root/providers, native loaded map floors and actual finite owners.
    // This fixture creates only two temporary reference cameras, never gameplay content.
    public sealed class R24BaselineGroundSmoke
    {
        [Serializable] sealed class Group { public string name="",error=""; public bool passed; public List<string> notes=new List<string>(); }
        [Serializable] sealed class Pose { public string map="",id="",outcome=""; public int catalog,grid,instance,layout; public Vector3 requested,accepted; public float meshBottom,floor; }
        [Serializable] sealed class Receipt { public bool success,nativeExecuted,headsetVerified=false; public string error=""; public List<Group> groups=new List<Group>(); public List<Pose> poses=new List<Pose>(); public List<string> shots=new List<string>(); }
        [Serializable] sealed class Reference { public string resultSha256=""; public Shot[] shots=null; }
        [Serializable] sealed class Shot { public string name="",mapId=""; public float[] eyeMapLocal=null,targetMapLocal=null; }
        public bool AutoFinish=true;
        public bool Passed {get;private set;}
        public string Json {get;private set;}
        R23PlayableLoop loop; Receipt result; Saved saved; float deadline; Inventory initial; bool collected;
        static readonly int[] Catalog={0,1,2,3,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,23,24,25,26,27,28,29};
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        void Bound(){Need(Time.realtimeSinceStartup<deadline,"Baseline ground fixture exceeded360s total bound");}
        void Note(Group g,string text){g.notes.Add(text);Debug.Log("R24 BASELINE "+g.name+": "+text);}
        public IEnumerator Run()
        {
            result=new Receipt();deadline=Time.realtimeSinceStartup+360;float readyEnd=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<readyEnd){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-off Root required: "+(loop==null?"missing Root":loop.Failure);Finish();yield break;}
            saved=new Saved(loop);result.nativeExecuted=true;
            try
            {
                loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
                loop.Rig.TrackingOrigin.localPosition=Vector3.zero;loop.Rig.Head.transform.SetLocalPositionAndRotation(new Vector3(0,1.7088f,0),Quaternion.identity);
                loop.Rig.Left.SetLocalPositionAndRotation(new Vector3(-.24f,1.16f,.24f),Quaternion.identity);loop.Rig.Right.SetLocalPositionAndRotation(new Vector3(.24f,1.16f,.24f),Quaternion.identity);
                foreach(string name in new[]{"RangeOneGroundKitAndActualCarriedOwners","RangeMenuResumeAndTwoSourceViews","AlphaSortedBaselineSeparateFromCaches","AsylumOneMountedWeaponStreaming","RangeActualRootPickupFiniteOwner","ActualSceneCleanup"})
                {
                    var g=new Group{name=name};result.groups.Add(g);
                    IEnumerator work=name=="RangeOneGroundKitAndActualCarriedOwners"?Range(g):name=="RangeMenuResumeAndTwoSourceViews"?MenuAndViews(g):name=="AlphaSortedBaselineSeparateFromCaches"?Alpha(g):name=="AsylumOneMountedWeaponStreaming"?Mounted(g):name=="RangeActualRootPickupFiniteOwner"?Pickup(g):Cleanup(g);
                    var run=Drive(g,work);while(run.MoveNext())yield return run.Current;
                }
            }
            finally
            {
                try{if(loop!=null&&loop.Ready)loop.ReturnToLobby();}catch(Exception e){result.error+="Lobby cleanup: "+e;}
                saved.Restore(loop);Finish();
            }
        }
        IEnumerator Drive(Group g,IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            try{while(stack.Count>0){bool more=false;object value=null;try{Bound();Need(g.name=="ActualSceneCleanup"||Time.realtimeSinceStartup<deadline-45,"Reserved final45s for actual map/lobby cleanup");more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){g.error=e.ToString();Debug.LogError("R24 BASELINE FAIL "+g.name+": "+e);break;}if(!more){var done=stack.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;}g.passed=g.error.Length==0;}
            finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}
        }
        IEnumerator Load(string id)
        {
            loop.ReturnToLobby();loop.SelectMap(id,true);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);
            while((!loop.Ready||loop.Map.MapId!=id)&&loop.Failure==null&&Time.realtimeSinceStartup<end){Bound();yield return null;}
            Need(loop.Ready&&loop.Map.MapId==id,"Actual map load failed: "+id+" / "+loop.Failure);loop.UI.ClearMatchSelection();loop.StartPlaying();Neutral();Need(loop.ActualMatchRules==null,"Baseline warm-up must leave the match inactive");
            end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);
            while(Time.realtimeSinceStartup<end){Pump();if(loop.WorldLoot.BaselineSpawnedCount==28&&(!id.StartsWith("archive:",StringComparison.Ordinal)||loop.ArchiveWorldReady&&!loop.WorldLoot.PreparationPending&&loop.WorldLoot.CacheSourceGeneration==loop.ArchiveWorldGeneration))break;Need(loop.WorldLoot.Failure==null&&loop.ArchiveWorldFailure==null,"Actual source preparation: "+loop.WorldLoot.Failure+" / "+loop.ArchiveWorldFailure);yield return null;}
            Need(loop.WorldLoot.BaselineSpawnedCount==28,"Exactly one28-item baseline kit required: "+loop.WorldLoot.BaselineSpawnedCount);
        }
        IEnumerator Range(Group g)
        {
            initial=new Inventory(loop);initial.RequireSourceStart(loop);yield return Load("range");initial.AssertUnchanged(loop);CheckKit(false);
            Need(loop.Gameplay.SourceGroundLayoutManaged&&loop.Gameplay.LegacyStarterWorldCount==0&&loop.Gameplay.LegacyStarterWorldActionIds.Length==0,"Legacy near-head starter pile remains");
            Same(loop.Gameplay.WorldWeaponActionIds,loop.WorldLoot.BaselineWeaponActionIds,"Range contains extra/legacy ground weapon IDs");Need(loop.WorldLoot.CacheWeaponActionIds.Length==0&&loop.WorldLoot.CacheGrenadeInstanceIds.Length==0,"Range cache IDs must not alias baseline");
            R23GrenadeItemState legacy;for(int i=0;i<3;i++)Need(!loop.Explosives.TryReadItem("r23-practice-grenade/"+i,out legacy),"Obsolete practice grenade duplicates baseline");
            yield return WaitRangeVisuals();CheckRangeSettled();CheckEnvironment();initial.AssertUnchanged(loop);
            Note(g,"Exactly26 independent source weapons +FRAG/ZONE; original carried CX4/PX4/M1014 loaded25/16/6 and actual family reserves unchanged. No legacy world IDs. All28 real visible mesh bottoms settled against native source floors.");
        }
        void CheckKit(bool archive)
        {
            var rows=loop.WorldLoot.BaselineKit;Need(rows.Length==28&&loop.WorldLoot.BaselineWeaponActionIds.Length==26&&loop.WorldLoot.BaselineGrenadeInstanceIds.Length==2,"Baseline catalog cardinality differs");var ids=new HashSet<string>();var catalogs=new HashSet<int>();var expected=new List<int>();foreach(int c in Catalog)if(!archive||c!=0&&c!=2&&c!=9)expected.Add(c);if(archive)expected.AddRange(new[]{0,2,9});
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];int grid=Array.IndexOf(Catalog,row.Catalog);Need(row.LayoutIndex==i&&row.Catalog==expected[i]&&row.GridIndex==grid&&ids.Add(row.ActionId)&&catalogs.Add(row.Catalog),"Source catalog/grid/sorted order/unique ID differs at "+i);
                int instance=row.Catalog==0?30:row.Catalog==2?31:row.Catalog==9?32:row.Catalog;Need(row.SourceInstanceIndex==instance,"Original owned0/2/9 copies must retain source instance30/31/32");
                Vector3 requested=Vector3.zero;if(archive){var loot=loop.ArchiveWorldPlan.Loot;Need(loot.Length>0&&loop.TryArchiveSourceToWorld(loot[(i*17)%loot.Length],out requested),"Actual shared archive source frame unavailable");}else requested=loop.Map.Spawn+new Vector3(-4.3f+(grid%8)*1.4f,.3f,-.6f+(grid/8)*1.4f);
                Need(Vector3.Distance(row.RequestedPosition,requested)<.002f,"Requested source slot/scatter differs: "+row.ActionId);
                if(row.Catalog==26||row.Catalog==28){R23GrenadeItemState state;Need(loop.Explosives.TryReadItem(row.ActionId,out state)&&state.Kind==(row.Catalog==26?"frag":"zone")&&!state.Owned&&!state.Spent&&state.Stars==1,"Actual baseline grenade state differs");}
                else{R23WeaponInstanceInfo state;Need(loop.Gameplay.TryReadWeaponInstance(row.ActionId,out state)&&state.Stars==1&&R23WeaponPickupRules.CatalogIndex(state.ProfileId)==row.Catalog&&Array.IndexOf(loop.Gameplay.OwnedWeaponActionIds,row.ActionId)<0,"Baseline substituted/owned duplicate firearm");}
            }
            Need(catalogs.Count==28,"Baseline omits active source catalogs");
        }
        IEnumerator WaitRangeVisuals()
        {
            float end=Mathf.Min(deadline,Time.realtimeSinceStartup+15);bool ready=false;
            while(Time.realtimeSinceStartup<end){Pump();ready=true;foreach(var row in loop.WorldLoot.BaselineKit){if(row.Catalog==26||row.Catalog==28){var prop=GrenadeVisual(row.ActionId,row.Catalog==26?"frag":"zone");Vector3 settled;if(prop==null||!loop.Explosives.IsWorldItemVisible(row.ActionId)||!R24WorldPickupSettler.TryReadSettledPosition(prop,loop.Rig.transform,out settled)||Vector3.Distance(settled,prop.position)>.003f)ready=false;}else{R24PickupSettleOutcome outcome;Vector3 at;if(!loop.Gameplay.TryReadWorldWeaponSettle(row.ActionId,out outcome,out at)||outcome!=R24PickupSettleOutcome.Settled||!loop.Gameplay.IsWorldWeaponVisible(row.ActionId)||WeaponVisual(row.ActionId)==null)ready=false;}}foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks()){R24PickupSettleResult r;bool found=row.IsSupply?loop.Supplies.TryReadSourceSettleResult(row.Id,out r):loop.Consumables.TryReadSourceSettleResult(row.Id,out r);if(!found||r.Outcome!=R24PickupSettleOutcome.Settled)ready=false;}foreach(var row in R24WorldPickupLayout.Ammo()){R24PickupSettleResult r;if(!loop.Supplies.TryReadSourceSettleResult(row.Id,out r)||r.Outcome!=R24PickupSettleOutcome.Settled)ready=false;}if(ready)break;yield return null;}
            Need(ready,"Actual28 ground and18 checkpoint visuals/native deferred settlement not ready");yield return new WaitForFixedUpdate();Physics.SyncTransforms();
        }
        void CheckRangeSettled()
        {
            foreach(var row in loop.WorldLoot.BaselineKit)
            {
                Transform prop;Vector3 at;string outcome;if(row.Catalog==26||row.Catalog==28){Quaternion rotation;bool owned;Need(loop.Explosives.TryReadWorldItem(row.ActionId,out at,out rotation,out owned)&&!owned,"Grenade accepted world pose missing");prop=GrenadeVisual(row.ActionId,row.Catalog==26?"frag":"zone");outcome="NativeMeshBottom";}else{R24PickupSettleOutcome settled;Need(loop.Gameplay.TryReadWorldWeaponSettle(row.ActionId,out settled,out at)&&settled==R24PickupSettleOutcome.Settled,"Weapon deferred settlement failed");prop=WeaponVisual(row.ActionId);outcome=settled.ToString();}
                Need(prop!=null&&prop.gameObject.activeInHierarchy&&Vector3.Distance(at,prop.position)<.002f,"Actual state/visible prop position differs");float bottom;Need(R24WorldPickupSettler.TryReadLowestVisibleMesh(prop,out bottom),"Actual source mesh missing: "+row.ActionId);var floor=Floor(at+Vector3.up*R24WorldPickupLayout.RayUp,R24WorldPickupLayout.RayLength);Need(Mathf.Abs(bottom-floor.point.y-R24WorldPickupLayout.HoverHeight)<.005f,"Source mesh-bottom hover differs: "+row.ActionId+" bottom="+bottom+" floor="+floor.point.y);
                Need(Mathf.Abs(at.x-row.RequestedPosition.x)<.002f&&Mathf.Abs(at.z-row.RequestedPosition.z)<.002f,"Settlement moved authored grid XZ");result.poses.Add(new Pose{map=loop.Map.MapId,id=row.ActionId,catalog=row.Catalog,grid=row.GridIndex,instance=row.SourceInstanceIndex,layout=row.LayoutIndex,requested=row.RequestedPosition,accepted=at,outcome=outcome,meshBottom=bottom,floor=floor.point.y});
            }
        }
        void CheckEnvironment(){var env=loop.Map.GetComponent<R23ValleyEnvironment>();Need(env!=null&&env.Ready&&env.ActiveEnvironment&&env.SourceWorldLightsHidden&&env.ActiveSourceLightCount==0,"Range actual Valley HDR environment/hidden source world-light policy not active");Need(RenderSettings.skybox!=null&&RenderSettings.skybox.HasProperty("_Panorama")&&RenderSettings.skybox.GetTexture("_Panorama")!=null,"Actual source HDR panorama not bound");}
        IEnumerator MenuAndViews(Group g)
        {
            if(loop.Map.MapId!="range"||loop.WorldLoot.BaselineSpawnedCount!=28)yield return Load("range");Need(initial!=null,"Original carried snapshot unavailable");initial.AssertUnchanged(loop);var before=loop.WorldLoot.BaselineKit;var ammo=new Inventory(loop);int capacity=loop.InventoryCapacity;loop.UI.ToggleMenu();Pump();Need(loop.UI.BlocksGameplay,"Actual ordinary menu did not block gameplay");foreach(var row in before)Need(row.Catalog==26||row.Catalog==28?!loop.Explosives.IsWorldItemVisible(row.ActionId):!loop.Gameplay.IsWorldWeaponVisible(row.ActionId),"Menu leaves ground item visible/claimable");loop.UI.ToggleMenu();Pump();yield return WaitRangeVisuals();ammo.AssertUnchanged(loop);Need(loop.InventoryCapacity==capacity,"Menu granted backpack capacity");CheckKit(false);SameIds(before,loop.WorldLoot.BaselineKit);CheckRangeSettled();CheckEnvironment();CaptureRange();Need(result.shots.Count==2,"Exactly two genuine Range reference views required");Note(g,"Actual menu hides/resumes same28 IDs and unchanged finite ammo/custody/capacity. Two1200x800 FOV58 source cameras, world0|weapon18 only; no HUD19. HDR binding checked, no radiometric/Filmic claim.");
        }
        IEnumerator Alpha(Group g)
        {
            yield return Load(R23MapCatalog.AlphaId);Need(initial!=null,"Original carried snapshot unavailable");initial.AssertUnchanged(loop);CheckKit(true);var w=loop.WorldLoot;Need(w.CacheSourceGeneration==loop.ArchiveWorldGeneration&&loop.ArchiveWorldMapId==loop.Map.MapId,"Baseline/cache did not use same actual source plan generation");
            var baseline=new HashSet<string>();foreach(var row in w.BaselineKit)baseline.Add(row.ActionId);foreach(string id in w.CacheWeaponActionIds)Need(!baseline.Contains(id),"Cache firearm aliases baseline");foreach(string id in w.CacheGrenadeInstanceIds)Need(!baseline.Contains(id),"Cache grenade aliases baseline");Same(w.WorldInstanceActionIds,w.CacheWeaponActionIds,"Archive legacy alias must remain cache-only");Same(w.WorldGrenadeInstanceIds,w.CacheGrenadeInstanceIds,"Archive grenade alias must remain cache-only");Need(w.SpawnedCount==w.CacheWeaponActionIds.Length+w.CacheGrenadeInstanceIds.Length&&w.ValidatedSites>0,"Archive cache counter includes baseline/missing source sites");
            foreach(var row in w.BaselineKit)result.poses.Add(new Pose{map=loop.Map.MapId,id=row.ActionId,catalog=row.Catalog,grid=row.GridIndex,instance=row.SourceInstanceIndex,layout=row.LayoutIndex,requested=row.RequestedPosition,outcome="ExactSharedSourceScatter"});Note(g,"Actual native source loot_points="+loop.ArchiveWorldPlan.Loot.Length+" generation="+loop.ArchiveWorldGeneration+"; baseline28 source-sorted17 permutation, copies0/2/9 at30/31/32 tail. Separate cache weapons="+w.CacheWeaponActionIds.Length+",grenades="+w.CacheGrenadeInstanceIds.Length+". No eager-art/native-appearance acceptance for remote archive pickups.");
        }
        IEnumerator Mounted(Group g)
        {
            yield return Load("archive:asylum-gods-pitdevil");float end=Mathf.Min(deadline,Time.realtimeSinceStartup+30);while(Time.realtimeSinceStartup<end){Pump();if(loop.MountedRacks.PreparedMapId==loop.Map.MapId)break;Need(loop.MountedRacks.Failure==null,"Mounted source data failed: "+loop.MountedRacks.Failure);yield return null;}var racks=loop.MountedRacks;Need(racks.PreparedMapId==loop.Map.MapId&&racks.CandidateBoards==3,"Exact source Asylum board data unavailable");
            foreach(var board in racks.SourceBoards){Vector3 center;Need(loop.TryArchiveSourceToWorld(board.Center,out center),"Actual mounted board frame unavailable");loop.Rig.SetSpawn(center+Vector3.up*2,center+Vector3.forward);loop.Calibration.NotifyRootPoseReset();Pump();racks.ActivateNear();if(racks.WorldInstanceActionIds.Length>0)break;yield return null;}
            Need(racks.WorldInstanceActionIds.Length>=22,"No actual source Asylum board passed native floor/LOS: "+racks.LastRefusal);string id=racks.WorldInstanceActionIds[0];Vector3 centerAt,at;Quaternion rotation;string instance;Need(loop.Gameplay.TryReadWorldWeapon(id,out instance,out centerAt,out at,out rotation),"Actual mounted instance absent");R24PickupSettleOutcome state;Vector3 accepted;Need(loop.Gameplay.IsWorldWeaponWallMounted(id)&&loop.Gameplay.TryReadWorldWeaponSettle(id,out state,out accepted)&&state==R24PickupSettleOutcome.WallMounted,"Wall disposition must be present immediately after spawn, before streaming");
            loop.Rig.SetSpawn(at+Vector3.up*2,at+Vector3.forward);Pump();end=Mathf.Min(deadline,Time.realtimeSinceStartup+6);while(!loop.Gameplay.IsWorldWeaponVisible(id)&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(loop.Gameplay.IsWorldWeaponVisible(id),"Actual mounted art failed to stream near player");yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();CheckMounted(id,at,rotation);
            loop.Rig.SetSpawn(at+Vector3.right*80+Vector3.up*2,at);Pump();end=Mathf.Min(deadline,Time.realtimeSinceStartup+3);while(loop.Gameplay.IsWorldWeaponVisible(id)&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(!loop.Gameplay.IsWorldWeaponVisible(id),"Source distance boundary did not withdraw mounted art");CheckMounted(id,at,rotation);
            loop.Rig.SetSpawn(at+Vector3.up*2,at+Vector3.forward);Pump();end=Mathf.Min(deadline,Time.realtimeSinceStartup+6);while(!loop.Gameplay.IsWorldWeaponVisible(id)&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(loop.Gameplay.IsWorldWeaponVisible(id),"Mounted art failed to return after source streaming boundary");yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();CheckMounted(id,at,rotation);Note(g,"One actual22-slot Asylum board, instance="+id+" retained WallMounted and exact source pose before/after native fixed updates and80m distance streaming. No floor settlement or wall-query substitute.");
        }
        void CheckMounted(string id,Vector3 before,Quaternion rotation){string instance;Vector3 center,at;Quaternion q;R24PickupSettleOutcome outcome;Vector3 accepted;Need(loop.Gameplay.TryReadWorldWeapon(id,out instance,out center,out at,out q)&&loop.Gameplay.IsWorldWeaponWallMounted(id)&&loop.Gameplay.TryReadWorldWeaponSettle(id,out outcome,out accepted)&&outcome==R24PickupSettleOutcome.WallMounted&&Vector3.Distance(at,before)<.001f&&Quaternion.Angle(q,rotation)<.05f,"Mounted instance fell/rotated/entered floor-settle path across streaming");}
        IEnumerator Pickup(Group g)
        {
            yield return Load("range");Need(initial!=null,"Original carried snapshot unavailable");initial.AssertUnchanged(loop);yield return WaitRangeVisuals();Need(loop.InventoryCapacity==3,"Source initial capacity must be3 before actual backpack pickup");string bag="r24-checkpoint/backpack/9";Vector3 at;Quaternion rotation;bool held;Need(loop.Supplies.TryReadWorldPose(bag,out at,out rotation,out held)&&!held,"Actual native source backpack missing");PlaceAt(at);loop.Gameplay.HolsterCurrent();loop.Consumables.Stow();loop.Explosives.Stow();Neutral();loop.Rig.Right.position=at;Physics.SyncTransforms();loop.Supplies.UpdateInputAvailability(new R24SupplyInputFrame{Enabled=true,LeftEligible=true,RightEligible=true});Need(loop.Supplies.TryCollect(bag,2)==1&&loop.InventoryCapacity==4&&loop.Supplies.TryCollect(bag,2)==0,"Actual backpack did not expand capacity once");collected=true;
            string wanted=null;foreach(var row in loop.WorldLoot.BaselineKit)if(row.Catalog==1)wanted=row.ActionId;Need(wanted!=null,"Actual MK18 source slot absent");string instance;Vector3 center;Need(loop.Gameplay.TryReadWorldWeapon(wanted,out instance,out center,out at,out rotation),"Actual finite ground MK18 missing");var finite=loop.Gameplay.ReadWeaponState(wanted);PlaceAt(center);Neutral();loop.Rig.Right.position=center;loop.Rig.Right.rotation=Quaternion.Inverse(R23TrackedGripPose.LocalRotation);Physics.SyncTransforms();ItemStep(0);var freshLeft=new HandSample{Tracked=true};var freshRight=new HandSample{Tracked=true,Grip=.85f};loop.Rig.SmokeTrackedHands(freshLeft,freshRight);loop.PrepareObjectiveGrips(freshLeft,freshRight,true,true,true);var objective=loop.SelectedGripObjective;Note(g,"Fresh actual Root objective="+objective.Kind+"/"+objective.Id+"/hand"+objective.Hand+" requested="+wanted);Need(objective.Kind==R23GripObjectiveKind.Weapon&&objective.Id==wanted&&objective.Hand==1,"Actual closest Root objective differs before committed grip");loop.StepItemInput(.02f,freshLeft,freshRight,true,false);
            Need(loop.Gameplay.HasWeapon&&loop.Gameplay.ActiveWeaponActionId==wanted&&Array.IndexOf(loop.Gameplay.OwnedWeaponActionIds,wanted)>=0,"Fresh Root objective grip did not acquire exact actual MK18 instance");var acquired=loop.Gameplay.ReadWeaponState(wanted);Need(acquired.Loaded==finite.Loaded&&acquired.Seated==finite.Seated&&acquired.Shots==finite.Shots,"Pickup recreated/regranted finite MK18 ammo");Need(loop.Gameplay.OwnedWeaponActionIds.Length==4&&!loop.Gameplay.IsWorldWeaponVisible(wanted),"Accepted custody duplicates actual ground item");initial.AssertOriginalOwners(loop);collected=true;ItemStep(0);yield return null;Note(g,"Real native reach/LOS backpack3â†’4 then Root fresh grip acquired exact independent MK18 finite owner. Original carried3+family reserves unchanged; no new spawn/grant/mock query. Later cleanup uses real RestartRound to restore initial loadout.");
        }
        IEnumerator Cleanup(Group g)
        {
            if(collected){loop.RestartRound();Neutral();initial.AssertUnchanged(loop);Need(loop.InventoryCapacity==3,"Genuine round restart failed to restore initial capacity");}
            loop.ReturnToLobby();if(loop.Map.MapId!=saved.Map){loop.SelectMap(saved.Map,false);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId==saved.Map,"Original map cleanup failed: "+loop.Failure);loop.ReturnToLobby();}Need(!loop.ArchiveWorldReady,"Lobby retains active archive planner");Note(g,"Actual original map/lobby restored; raw tracked poses, root pose and prior probe/input/persistence flags restored by finalizer. No source settings, frame rate, fake floors or health owners changed.");
        }
        void Pump(){loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();Physics.SyncTransforms();}
        void Neutral(){loop.Rig.GameplayBlocked=false;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);for(int i=0;i<6;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);Pump();}
        void ItemStep(float grip){var left=new HandSample{Tracked=true};var right=new HandSample{Tracked=true,Grip=grip};loop.Rig.SmokeTrackedHands(left,right);loop.PrepareObjectiveGrips(left,right,true,true,true);loop.StepItemInput(.02f,left,right,true,false);}
        RaycastHit Floor(Vector3 from,float depth){var best=new RaycastHit();float distance=float.PositiveInfinity;foreach(var hit in Physics.RaycastAll(from,Vector3.down,depth,1,QueryTriggerInteraction.Ignore))if(hit.collider!=null&&hit.collider.transform.IsChildOf(loop.Map.transform)&&hit.normal.y>.7f&&hit.distance<distance){best=hit;distance=hit.distance;}Need(!float.IsPositiveInfinity(distance),"Actual loaded map floor missing at "+from);return best;}
        void PlaceAt(Vector3 point){var floor=Floor(point+Vector3.up*2,20);loop.Rig.SetSpawn(floor.point+Vector3.up*.02f-Vector3.forward*.4f,floor.point+Vector3.forward);loop.Calibration.NotifyRootPoseReset();Physics.SyncTransforms();}
        static Transform Find(string name){Transform result=null;foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name==name){Need(result==null,"Duplicate physical source visual: "+name);result=t;}return result;}
        Transform WeaponVisual(string id){string instance;Vector3 center,at;Quaternion rotation;return loop.Gameplay.TryReadWorldWeapon(id,out instance,out center,out at,out rotation)?Find("R23 dropped source instance "+instance):null;}
        static Transform GrenadeVisual(string id,string kind){return Find("R23 "+kind+" "+id);}
        static void Same(string[] a,string[] b,string why){Need(a.Length==b.Length&&new HashSet<string>(a).SetEquals(b),why);}
        static void SameIds(R23BaselinePickup[] a,R23BaselinePickup[] b){Need(a.Length==b.Length,"Menu recreated baseline cardinality");for(int i=0;i<a.Length;i++)Need(a[i].ActionId==b[i].ActionId&&a[i].RequestedPosition==b[i].RequestedPosition,"Menu recreated/repositioned baseline ID");}
        void CaptureRange()
        {
            var reference=JsonUtility.FromJson<Reference>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Tools/PlayableR23/WorldLoot/ArchiveItems/reference.json"))));Need(reference.resultSha256=="adfd6a6975985f03a6820140326d4a94b46384b5d2d6082c5aaa502a7a967d63","Genuine source reference binding changed");
            foreach(var shot in reference.shots)if(shot.mapId=="range")
            {
                var origin=new Vector3(0,0,-600);Vector3 eye=loop.Map.transform.TransformPoint(origin+new Vector3(shot.eyeMapLocal[0],shot.eyeMapLocal[1],-shot.eyeMapLocal[2])),target=loop.Map.transform.TransformPoint(origin+new Vector3(shot.targetMapLocal[0],shot.targetMapLocal[1],-shot.targetMapLocal[2]));var go=new GameObject("R24 genuine Range source reference camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(loop.Rig.Head);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.cullingMask=(1<<0)|(1<<18);camera.fieldOfView=58;camera.aspect=1.5f;go.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,Vector3.up));var render=RenderTexture.GetTemporary(1200,800,24);var active=RenderTexture.active;Texture2D pixels=null;
                try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();string argument=R23PlayableBuild.Argument("-ashfallVRSmoke"),directory=string.IsNullOrEmpty(argument)?Path.Combine(Path.GetTempPath(),"ashfall-baseline-ground"):Path.GetDirectoryName(Path.GetFullPath(argument));Directory.CreateDirectory(directory);string file=Path.Combine(directory,"baseline-"+shot.name+".png");File.WriteAllBytes(file,pixels.EncodeToPNG());result.shots.Add(file);}finally{camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(render);if(pixels!=null)UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(go);}
            }
        }
        void Finish(){Passed=result.nativeExecuted&&result.error.Length==0&&result.groups.Count==6;foreach(var g in result.groups)Passed&=g.passed;result.success=Passed;Json=JsonUtility.ToJson(result,true);if(AutoFinish)R23SmokeControl.Finish(Passed,Json);}
        sealed class Inventory
        {
            readonly string[] ids;readonly Dictionary<string,string> snapshots=new Dictionary<string,string>();readonly Dictionary<string,int> reserves=new Dictionary<string,int>();
            public Inventory(R23PlayableLoop l){ids=l.Gameplay.OwnedWeaponActionIds;foreach(string id in ids)snapshots[id]=Read(l,id);foreach(string family in new[]{"rifle","sniper","smg","pistol","shells"})reserves[family]=l.Gameplay.ReadAmmoReserveFamily(family);}
            static string Read(R23PlayableLoop l,string id){var s=l.Gameplay.ReadWeaponState(id);return s.Loaded+"/"+s.Seated+"/"+s.Shots+"/"+s.Phase+"/"+s.Chambered+"/"+s.SpentChamber;}
            public void RequireSourceStart(R23PlayableLoop l){Need(ids.Length==3,"Fresh source initial carried3 required, fixture never grants/resets to manufacture baseline");var profiles=new HashSet<string>();foreach(string id in ids){string p=l.Gameplay.ResolveWeaponProfile(id);profiles.Add(p);var s=l.Gameplay.ReadWeaponState(id);Need(s.Loaded==(p=="cx4"?25:p=="px4"?16:p=="m1014"?6:-1)&&s.Shots==0,"Fresh original source carried ammo differs: "+p);}Need(profiles.SetEquals(new[]{"cx4","px4","m1014"}),"Initial source owned0/2/9 differs");}
            public void AssertOriginalOwners(R23PlayableLoop l){foreach(var row in snapshots)Need(Array.IndexOf(l.Gameplay.OwnedWeaponActionIds,row.Key)>=0&&Read(l,row.Key)==row.Value,"Original carried finite owner/ammo changed: "+row.Key);foreach(var row in reserves)Need(l.Gameplay.ReadAmmoReserveFamily(row.Key)==row.Value,"Actual reserve changed/regranted: "+row.Key);}
            public void AssertUnchanged(R23PlayableLoop l){Same(ids,l.Gameplay.OwnedWeaponActionIds,"Actual carried custody changed");AssertOriginalOwners(l);}
        }
        sealed class Saved
        {
            public readonly string Map;readonly bool manual,external,rig,pump,persist,blocked;readonly Vector3 origin,head,left,right,root;readonly Quaternion headQ,leftQ,rightQ,rootQ;
            public Saved(R23PlayableLoop l){Map=l.Map.MapId;manual=l.ManualSmoke;external=l.ExternalProbePump;rig=l.Rig.enabled;pump=l.Gameplay.ExternalInputPump;persist=l.Calibration.SuppressPersistence;blocked=l.Rig.GameplayBlocked;origin=l.Rig.TrackingOrigin.localPosition;head=l.Rig.Head.transform.localPosition;left=l.Rig.Left.localPosition;right=l.Rig.Right.localPosition;root=l.Rig.transform.position;rootQ=l.Rig.transform.rotation;headQ=l.Rig.Head.transform.localRotation;leftQ=l.Rig.Left.localRotation;rightQ=l.Rig.Right.localRotation;}
            public void Restore(R23PlayableLoop l){if(l==null)return;l.ManualSmoke=manual;l.ExternalProbePump=external;l.Rig.enabled=rig;l.Gameplay.ExternalInputPump=pump;l.Calibration.SuppressPersistence=persist;l.Rig.GameplayBlocked=blocked;l.Rig.TrackingOrigin.localPosition=origin;l.Rig.Head.transform.SetLocalPositionAndRotation(head,headQ);l.Rig.Left.SetLocalPositionAndRotation(left,leftQ);l.Rig.Right.SetLocalPositionAndRotation(right,rightQ);l.Rig.transform.SetPositionAndRotation(root,rootQ);l.Calibration.NotifyRootPoseReset();}
        }
    }
}
#endif
