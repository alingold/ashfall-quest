#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Consumables;
using Ashfall.Integration.Playable.Gallery;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Explosives;
using Ashfall.Modules.WeaponHandling;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Finite native integration oracle. Real scene/services, simulated tracked
    // poses, actual pickup edges and native health. No reflection/fake actors.
    public sealed class R23ArsenalPickupSmoke
    {
        [Serializable]sealed class Group{public string name="",error="";public bool passed;public List<string> observations=new List<string>();}
        [Serializable]sealed class Result{public bool success,nativeExecuted,headsetVerified=false,listeningVerified=false;public string error="",startedUtc="",finishedUtc="";public List<Group> groups=new List<Group>();}
        const string Shaker1="r23-arsenal-smoke/shaker-1",Shaker2="r23-arsenal-smoke/shaker-2";
        R23PlayableLoop loop;Vector3 site,forward;Result result;
        public bool Passed {get;private set;}public string Json {get;private set;}
        static void Need(bool okay,string text){if(!okay)throw new InvalidOperationException(text);}
        Group Add(string name){var g=new Group{name=name};result.groups.Add(g);Debug.Log("R23 ARSENAL BEGIN "+name);return g;}
        static void Note(Group g,string text){g.observations.Add(text);Debug.Log("R23 ARSENAL "+g.name+": "+text);}
        static void Run(Group g,Action action){try{action();g.passed=true;Debug.Log("R23 ARSENAL PASS "+g.name);}catch(Exception e){g.error=e.ToString();Debug.LogError("R23 ARSENAL FAIL "+g.name+": "+e);}}
        public IEnumerator Start()
        {
            float end=Time.realtimeSinceStartup+120;R23PlayableLoop root=null;
            while(Time.realtimeSinceStartup<end){root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(root!=null&&(root.Ready||root.Failure!=null))break;yield return null;}
            if(root==null||!root.Ready){var failed=new Result{error=root==null?"Playable root absent":root.Failure??"Root timeout",startedUtc=DateTime.UtcNow.ToString("o"),finishedUtc=DateTime.UtcNow.ToString("o")};R23SmokeControl.Finish(false,JsonUtility.ToJson(failed,true));yield break;}
            float audioEnd=Time.realtimeSinceStartup+30;while(root.Consumables.Audio!=null&&!root.Consumables.Audio.Ready&&root.Consumables.Audio.Failure==null&&Time.realtimeSinceStartup<audioEnd)yield return null;
            var work=Execute(root);while(work.MoveNext())yield return work.Current;
            R23SmokeControl.Finish(Passed,Json);
        }
        public IEnumerator Execute(R23PlayableLoop root)
        {
            loop=root;result=new Result{startedUtc=DateTime.UtcNow.ToString("o"),nativeExecuted=true};var saved=new ScenePose(loop);bool touched=false;
            try
            {
                var setup=Add("ActualRootMapSetup");Run(setup,delegate{
                    Need(!UnityEngine.XR.XRSettings.isDeviceActive,"Arsenal smoke requires XR-off native simulated poses; never overwrite a live headset");
                    touched=true;
                    loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;loop.Gameplay.ExternalInputPump=true;
                    loop.StartPlaying();loop.Rig.GameplayBlocked=false;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);loop.Gameplay.SetWeaponPresentationSuppressed(false);
                    Need(loop.Ready&&loop.Map.IsLoaded&&loop.Gameplay.Ready&&loop.Consumables.Ready,"Actual loaded map/Hands/Consumables unavailable");
                    forward=Vector3.ProjectOnPlane(loop.Rig.Head.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)forward=Vector3.forward;
                    site=FindActualDropSite();Note(setup,"Actual map "+loop.Map.MapId+"; native floor/drop site "+site+"; no replacement platform");
                });
                var kit=Add("ActualSourceDefaultKit");Run(kit,delegate{ResetAtSite();Kit(kit);SourceWeaponTuning(kit);});
                var roof=Add("ActualR24RoofWarmupRestart");Run(roof,delegate{RoofWarmup(roof);});
                var hands=Add("ActualR24PostMotionFreeHands");Run(hands,delegate{PostMotionHands(hands);});
                var fire=Add("ActualSourceCarriedFireAndPhysicalReload");Run(fire,delegate{FireAndReload(fire);});
                var smg=Add("ActualFourSmgFireReloadAndLaser");Run(smg,delegate{SmgFireAndReload(smg);});
                var catalog=Add("ActualCatalogFiveFireAndPhysicalReload");Run(catalog,delegate{CatalogFiveFireAndReload(catalog);});
                var copies=Add("ActualIndependentWorldCopies");Run(copies,delegate{IndependentCopies(copies);});
                var swap=Add("ActualRemoteGripReplacementAndRearm");Run(swap,delegate{RemoteSwap(swap);});
                var stack=Add("ActualShakerStackAtomicWorldEviction");Run(stack,delegate{StackSwap(stack);});
                var refuse=Add("ActualNoFloorRefusesInventoryMutation");Run(refuse,delegate{NoFloor(refuse);});
                var objectives=Add("ActualSharedGripArbitration");Run(objectives,delegate{ObjectiveRouting(objectives);});
                var grenade=Add("ActualR24GrenadePickupAndSelfDamage");float grenadeDeadline=0,beforeBlast=0;
                float grenadeLoadEnd=Time.realtimeSinceStartup+30;while(loop.Explosives!=null&&!loop.Explosives.Ready&&loop.Explosives.Failure==null&&Time.realtimeSinceStartup<grenadeLoadEnd)yield return null;
                Run(grenade,delegate{beforeBlast=ThrowAtFeet(grenade);grenadeDeadline=Time.realtimeSinceStartup+6;});
                while(grenade.error.Length==0&&loop.Explosives.LiveProjectiles>0&&Time.realtimeSinceStartup<grenadeDeadline)yield return null;
                if(grenade.error.Length==0)Run(grenade,delegate{Need(loop.Explosives.LiveProjectiles==0,"Thrown native frag did not terminate within fuse bound");Need(loop.Gameplay.Health+loop.Gameplay.Shield<beforeBlast,"Real native grenade did not damage the player's Health/Shield at the head");Note(grenade,"Actual finite pickup, three-slot gun eviction, trigger throw, native fuse/physics and self damage; no health simulation");ResetAtSite();});
                var menus=Add("ActualR24LoadoutAndOutfitCards");Run(menus,delegate{SourceMenus(menus);});
                loop.ReturnToLobby();loop.UI.OpenAppearance();int goldCount=0;
                foreach(var style in loop.Appearance.AvailableStyles)if(style.SourceIndex==53||style.SourceIndex==74||style.SourceIndex==75||style.SourceIndex==76)
                {
                    goldCount++;string expected=style.Id;Run(menus,delegate{Need(loop.UI.BrowseAppearance(expected),"Real gold outfit card missing: "+expected);});float goldDeadline=Time.realtimeSinceStartup+30;
                    while(menus.error.Length==0&&(!loop.Preview.Ready||loop.Preview.Loading||loop.Preview.SelectedStyleId!=expected)&&loop.Preview.Failure==null&&Time.realtimeSinceStartup<goldDeadline)yield return null;
                    if(menus.error.Length==0)Run(menus,delegate{var receipt=loop.Preview.InspectPresentation();Need(loop.Preview.Failure==null&&loop.Preview.Ready&&loop.Preview.SelectedStyleId==expected&&receipt!=null&&receipt.ProfileId==expected&&receipt.VisibleRenderers>0&&receipt.HeadVisible,"Gold caption changed without an actual visible source model: "+expected+" / "+loop.Preview.Failure);Note(menus,"Actual R5 gold source "+style.SourceIndex+" preview rendered as "+receipt.ProfileId+" / "+receipt.VisibleRenderers+" meshes");});
                }
                if(menus.error.Length==0)Run(menus,delegate{Need(goldCount==4,"All four R5 gold source kits must be genuinely available");loop.StartPlaying();ResetAtSite();});
                var caches=Add("ActualRecoveredCacheFloorAndVisibility");Run(caches,delegate{RecoveredCaches(caches);});
                var alpha=Add("ActualAlphaCacheFloorAndMarkers");string previousMap=loop.Map.MapId;Vector3 previousSite=site;
                try{loop.SelectMap(Ashfall.Integration.Playable.World.R23MapCatalog.AlphaId,false);}catch(Exception e){alpha.error=e.ToString();}
                float mapDeadline=Time.realtimeSinceStartup+120;while(alpha.error.Length==0&&!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<mapDeadline)yield return null;
                Run(alpha,delegate{Need(alpha.error.Length==0&&loop.Ready&&loop.Map.MapId==Ashfall.Integration.Playable.World.R23MapCatalog.AlphaId,"Actual ALFA load/entry failed: "+(alpha.error.Length==0?loop.Failure:alpha.error));loop.StartPlaying();site=FindActualDropSite();RecoveredCaches(alpha);TowerOwners(alpha,930);Note(alpha,"Actual ALFA entry+restart; native source floor supports cache copies, baked red marker meshes absent");});
                if(alpha.error.Length==0)
                {
                    try{loop.SelectMap(Ashfall.Integration.Playable.World.R23MapCatalog.ClassicId,false);}catch(Exception e){alpha.error=e.ToString();}
                    mapDeadline=Time.realtimeSinceStartup+120;while(alpha.error.Length==0&&!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<mapDeadline)yield return null;
                    if(alpha.error.Length==0)Run(alpha,delegate{Need(loop.Ready&&loop.Map.MapId==Ashfall.Integration.Playable.World.R23MapCatalog.ClassicId,"Actual Classic tower map load failed: "+loop.Failure);loop.StartPlaying();TowerOwners(alpha,0);});
                }
                if(loop.Ready&&loop.Map.MapId!=previousMap)
                {
                    try{loop.SelectMap(previousMap,false);}catch(Exception e){alpha.passed=false;alpha.error+=" Restore: "+e;}
                    mapDeadline=Time.realtimeSinceStartup+120;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<mapDeadline)yield return null;
                    if(!loop.Ready||loop.Map.MapId!=previousMap){alpha.passed=false;alpha.error+=" Original map restoration failed: "+loop.Failure;}
                }
                site=previousSite;
                var gallery=Add("SixteenDecodedPrivateGalleryPreviews");
                IEnumerator iterator=null;try{loop.ReturnToLobby();loop.UI.OpenWeapons();iterator=R23ExpandedGalleryFixture.RunAll(loop.Gallery,delegate(string text){Note(gallery,text);},delegate(string suffix){loop.UI.BrowseWeapon(loop.Gallery.ViewedWeaponId);Note(gallery,"Desktop capture: "+R23SmokeControl.Capture(loop.Rig.Head,suffix));return null;});}catch(Exception e){gallery.error=e.ToString();}
                if(iterator!=null)
                {
                    var stackJobs=new Stack<IEnumerator>();stackJobs.Push(iterator);
                    while(stackJobs.Count>0&&gallery.error.Length==0)
                    {
                        bool moved=false;object value=null;try{moved=stackJobs.Peek().MoveNext();if(moved)value=stackJobs.Peek().Current;}catch(Exception e){gallery.error=e.ToString();Debug.LogError(e);}
                        if(gallery.error.Length!=0)break;if(!moved){var disposable=stackJobs.Pop() as IDisposable;if(disposable!=null)disposable.Dispose();continue;}
                        var nested=value as IEnumerator;if(nested!=null)stackJobs.Push(nested);else yield return value;
                    }
                    while(stackJobs.Count>0){var disposable=stackJobs.Pop() as IDisposable;if(disposable!=null)disposable.Dispose();}
                    if(gallery.error.Length==0){gallery.passed=true;Debug.Log("R23 ARSENAL PASS "+gallery.name);}
                }
            }
            finally
            {
                var cleanup=Add("SceneAndProbeCleanup");Run(cleanup,delegate{if(!touched){Note(cleanup,"No scene mutation before rejected XR/session guard");return;}saved.Restore(loop);Need(loop.Consumables.InspectItem(Shaker1)==null&&loop.Consumables.InspectItem(Shaker2)==null,"Probe supplies survived cleanup");Note(cleanup,"Default round/targets, input flags, tracking origin and simulated poses restored; source map geometry never replaced");});
                result.success=result.groups.Count==18&&result.error.Length==0;foreach(var g in result.groups)result.success&=g.passed&&g.error.Length==0;
                result.finishedUtc=DateTime.UtcNow.ToString("o");Passed=result.success;Json=JsonUtility.ToJson(result,true);Debug.Log("R23 ARSENAL TERMINAL "+Json);
            }
        }
        void RoofWarmup(Group g)
        {
            Need(loop.Map.MapId==Ashfall.Integration.Playable.World.R23MapCatalog.PaintballId,"Warmup case requires actual Paintball");
            Need(Vector3.Distance(loop.Map.WarmupSpawn,new Vector3(22.64996f,73.07249f,-14.51456f))<.02f,"Source white-ring roof warmup missing");
            Vector3 previous=loop.Rig.Head.transform.localPosition;
            try
            {
                loop.Rig.Head.transform.localPosition=new Vector3(.17f,1.1f,-.14f);loop.StartPlaying();NeedRoofFloor();loop.RestartRound();NeedRoofFloor();
                Note(g,"Source roof Start + restart have actual native support at nonzero room-scale head X/Z and seated 1.1m tracking pose");
            }
            finally{loop.Rig.Head.transform.localPosition=previous;ResetAtSite();}
        }
        void SourceWeaponTuning(Group g)
        {
            var h=loop.Gameplay;Need(h.EquipOwned("cx4",1),"Source tuning needs actual owned weapon");SetGunHandNearHead();
            try
            {
                h.SetWeaponTuning(0,0);Neutral();loop.Rig.Right.rotation=Quaternion.AngleAxis(20,Vector3.up)*loop.Rig.Right.rotation;h.TickInput(.02f,Frame());R23ShotPose pose;
                Need(h.TryReadDisplayedShotPose(out pose)&&Vector3.Angle(new Vector3(pose.Forward.X,pose.Forward.Y,pose.Forward.Z),R23TrackedGripPose.Forward(loop.Rig.Right))<.1f,"Zero damping still filters actual source aim");
                h.SetWeaponTuning(1,0);h.TickInput(.02f,Frame());h.TryReadDisplayedShotPose(out pose);Vector3 before=new Vector3(pose.Forward.X,pose.Forward.Y,pose.Forward.Z);
                loop.Rig.Right.rotation=Quaternion.AngleAxis(30,Vector3.up)*loop.Rig.Right.rotation;h.TickInput(.02f,Frame());h.TryReadDisplayedShotPose(out pose);Vector3 after=new Vector3(pose.Forward.X,pose.Forward.Y,pose.Forward.Z);float damped=Vector3.Angle(before,after);
                Need(damped>1&&damped<15,"Source96ms motion-damping curve did not affect actual weapon pose");
                loop.Rig.transform.rotation=Quaternion.AngleAxis(40,Vector3.up)*loop.Rig.transform.rotation;h.TickInput(.00001f,Frame());h.TryReadDisplayedShotPose(out pose);Need(Vector3.Angle(Quaternion.AngleAxis(40,Vector3.up)*after,new Vector3(pose.Forward.X,pose.Forward.Y,pose.Forward.Z))<.1f,"Locomotion yaw was damped as hand jitter");
                Note(g,"Actual weapon settings: zero/raw aim, source100%=96ms rotation smoothing and immediate artificial-yaw compensation; positions remain tracked");
            }
            finally{h.SetWeaponTuning(loop.UI.Settings.MotionDamping,loop.UI.Settings.OffhandInfluence);ResetAtSite();}
        }
        void NeedRoofFloor()
        {
            Physics.SyncTransforms();RaycastHit best=new RaycastHit();float distance=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(loop.Rig.Head.transform.position+Vector3.up*.1f,Vector3.down,3,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                if(hit.collider.transform.IsChildOf(loop.Map.transform)&&hit.normal.y>.7f&&hit.distance<distance){best=hit;distance=hit.distance;}
            Need(!float.IsPositiveInfinity(distance)&&Mathf.Abs(best.point.y-72.95249f)<.03f,"Actual HMD footprint missed authored Paintball roof floor");
        }
        void PostMotionHands(Group g)
        {
            ResetAtSite();loop.Gameplay.HolsterCurrent();SetGunHandNearHead();loop.Climbing.ResetMotion();
            var frame=new Ashfall.Core.InputFrame{Tracked=true};loop.Climbing.Evaluate(frame,.02f,true,true,true,true,Vector3.zero);
            Vector3 move=Vector3.Cross(Vector3.up,forward)*.18f;loop.Rig.transform.position+=move;Physics.SyncTransforms();loop.Climbing.AcceptMotion(move,.02f,false,false);
            Need(Vector3.Distance(loop.Climbing.LeftContactProxy,loop.Rig.Left.position)<.005f&&Vector3.Distance(loop.Climbing.RightContactProxy,loop.Rig.Right.position)<.005f,"Free contact proxies retain pre-strafe world positions");
            Note(g,"Both wall-query proxies follow accepted .18m root motion in the same publication; tracking/calibration not replaced");ResetAtSite();
        }
        float ThrowAtFeet(Group g)
        {
            ResetAtSite();Need(loop.Explosives!=null&&loop.Explosives.Ready,"Actual frag/zone assets failed to load: "+(loop.Explosives==null?"missing owner":loop.Explosives.Failure));
            SetGunHandNearHead();loop.Gameplay.SetPickupHome(loop.Rig.Head.transform.position-forward*10,Quaternion.LookRotation(forward));
            const string id="r23-arsenal-smoke/frag";R23GrenadeItemState fixtureItem;
            if(!loop.Explosives.TryReadItem(id,out fixtureItem))Need(loop.Explosives.Spawn(id,"frag",loop.Rig.Right.position+forward*.12f),"Actual finite grenade fixture registration refused");
            Need(loop.Explosives.RepositionWorldItem(id,loop.Rig.Right.position+forward*.12f),"Actual source frag could not be positioned at the physical hand");
            ObjectiveFrame(0,0);ObjectiveFrame(0,.8f);
            Need(loop.SelectedGripObjective.Kind==Ashfall.Integration.Playable.Input.R23GripObjectiveKind.Explosive&&loop.Explosives.HeldId==id&&loop.Gameplay.OwnedWeaponActionIds.Length==2&&!loop.Gameplay.HasWeapon,"Shared fresh grip failed real three-slot grenade replacement");
            var neutral=new Ashfall.Modules.Player.HandSample{Tracked=true};for(int i=0;i<4;i++)loop.StepItemInput(.1f,neutral,neutral,true,false);
            loop.Rig.Right.rotation=Quaternion.LookRotation(Vector3.down,forward)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);Physics.SyncTransforms();
            float before=loop.Gameplay.Health+loop.Gameplay.Shield;var trigger=new Ashfall.Modules.Player.HandSample{Tracked=true,Trigger=.9f};loop.StepItemInput(.02f,neutral,trigger,true,false);
            R23GrenadeItemState consumed;Need(loop.Explosives.LiveProjectiles==1&&loop.Explosives.TryReadItem(id,out consumed)&&consumed.Spent&&!consumed.Owned,"Real trigger throw did not consume exactly its finite item");
            Note(g,"Thrown native FRAG from tracked corrected grip aimed into real source floor");return before;
        }
        void SourceMenus(Group g)
        {
            loop.ReturnToLobby();loop.UI.OpenAppearance();int pictures=0;
            foreach(var child in loop.UI.GetComponentsInChildren<Transform>(true))if(child.gameObject.activeInHierarchy&&child.name.StartsWith("Verified R5 outfit screenshot ",StringComparison.Ordinal))pictures++;
            Need(pictures>0&&pictures<=6,"Named R5 screenshot viewport did not create actual available outfit pictures");
            loop.UI.OpenWeapons();bool weapons=false,outfits=false,back=false;
            foreach(var text in loop.UI.GetComponentsInChildren<TextMesh>(true))if(text.gameObject.activeInHierarchy){weapons|=text.text=="WEAPONS";outfits|=text.text=="OUTFITS";back|=text.text=="BACK";}
            Need(weapons&&outfits&&back,"R5 loadout footer navigation missing");Note(g,"R5 screenshot cards and weapon/outfit/back buttons exist in native UI; rendered readability still needs human check");
            loop.StartPlaying();ResetAtSite();
        }
        void TowerOwners(Group g,int basePanels)
        {
            var map=loop.Map;Need(map.DestructibleCount==basePanels+18,"Actual source tower owner count missing on "+map.MapId);var definitions=map.DestructibleDefinitions;
            try
            {
                for(int i=basePanels;i<definitions.Length;i++)
                {
                    map.ResetDestructibles();var row=definitions[i];Vector3 point=map.transform.TransformPoint(new Vector3(row.probe[0],row.probe[1],row.probe[2])),normal=map.transform.TransformDirection(new Vector3(row.normal[0],row.normal[1],row.normal[2])).normalized;
                    RaycastHit nearest=new RaycastHit();float distance=float.PositiveInfinity;
                    foreach(var hit in Physics.RaycastAll(point+normal*.12f,-normal,.24f,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(map.transform)&&hit.distance<distance){distance=hit.distance;nearest=hit;}
                    string id;bool broken;Need(nearest.collider!=null&&map.TryReadDestructible(nearest.collider,out id,out broken)&&id==row.id&&!broken,"Native tower main face is unowned/blocked: "+row.id);
                    Need(map.TryDamageCollider(nearest.collider,1)&&map.BrokenDestructibleCount==1&&!nearest.collider.enabled,"Native tower impact did not retire exact owner: "+row.id);
                    foreach(var renderer in map.GetComponentsInChildren<Renderer>(true))if(renderer.gameObject.name.EndsWith(" tower owner "+i,StringComparison.Ordinal))Need(!renderer.gameObject.activeInHierarchy,"Visual-only tower backing survived break: "+row.id);
                    map.ResetDestructibles();Need(nearest.collider.enabled&&map.BrokenDestructibleCount==0,"Native tower restart did not restore source collider: "+row.id);
                }
                Note(g,map.MapId+": all18 exact source tower faces/colliders, visual-only backing withdrawal and reset checked; no replacement geometry");
            }
            finally{map.ResetDestructibles();}
        }
        void ResetAtSite()
        {
            Need(loop.Ready&&loop.Map.IsLoaded,"Source map not ready");loop.StartPlaying();loop.WorldLoot.SetWorldEnabled(false);loop.Consumables.ClearItems();loop.RestartRound();loop.Rig.SetSpawn(site,site+forward);loop.Rig.GameplayBlocked=false;
            loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);loop.Gameplay.SetWeaponPresentationSuppressed(false);loop.Consumables.SetWorldVisible(true);
            loop.Gameplay.SetPickupHome(loop.Rig.Head.transform.position+forward*1.65f-Vector3.up*.45f,Quaternion.LookRotation(forward));Neutral();Physics.SyncTransforms();
        }
        Vector3 FindActualDropSite()
        {
            var origin=loop.Rig.transform.position;
            foreach(float radius in new[]{0f,2,4,7,10})for(int k=0;k<(radius==0?1:8);k++)
            {
                float a=k*Mathf.PI/4;Vector3 p=origin+new Vector3(Mathf.Cos(a)*radius,2,Mathf.Sin(a)*radius);RaycastHit best=new RaycastHit();float nearest=float.PositiveInfinity;
                foreach(var h in Physics.RaycastAll(p,Vector3.down,8,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore))if(h.collider.transform.IsChildOf(loop.Map.transform)&&h.normal.y>=.65f&&h.distance<nearest){best=h;nearest=h.distance;}
                if(float.IsPositiveInfinity(nearest))continue;loop.Rig.SetSpawn(best.point+Vector3.up*.025f,best.point+forward);
                Bounds b;Vector3 drop;Quaternion q;if(loop.Gameplay.TryGetWeaponBounds(loop.Gameplay.ActiveWeaponId,out b)&&R23WorldItemPlacement.TryDrop(loop.Rig.Head.transform,loop.Rig.transform,b,out drop,out q))return loop.Rig.transform.position;
            }
            throw new InvalidOperationException("No real authored map floor within10m supports a reachable source weapon drop");
        }
        void Kit(Group g)
        {
            var h=loop.Gameplay;var ids=h.OwnedWeaponIds;Need(ids.Length==3&&Has(ids,"cx4")&&Has(ids,"px4")&&Has(ids,"m1014"),"Actual source starter kit is not CX4/PX4/M1014");
            Need(h.ReadWeaponState("cx4").Loaded==25&&h.ReadWeaponState("px4").Loaded==16&&h.ReadWeaponState("m1014").Loaded==6,"Starter native capacities are not25/16/6");
            foreach(string id in new[]{"mk18","akm","spr","magnum"}){string instance;Vector3 center,position;Quaternion rotation;Need(!h.OwnsWeapon(id)&&h.TryReadWorldWeapon(id,out instance,out center,out position,out rotation)&&!String.IsNullOrEmpty(instance),"Actual unowned persistent source pickup missing: "+id);}
            Need(Slots()==3,"Source kit exceeds three slots");Note(g,"Real owned CX4/PX4/M1014 plus four real unowned persistent world instances; actual native loaded25/16/6");
        }
        void FireAndReload(Group g)
        {
            foreach(string id in new[]{"cx4","px4","m1014"})
            {
                ResetAtSite();var h=loop.Gameplay;Need(h.EquipOwned(id,1),"Real owned equip refused: "+id);h.SetPickupHome(loop.Rig.Head.transform.position-forward*10,Quaternion.LookRotation(forward));SetGunHandNearHead();Neutral();PlacePracticeOnActualFloor();AimAtPractice();int before=h.Ammo;long shots=h.ReadWeaponState(id).Shots;var target=h.ReadPracticeTarget(0);float vital=target.Health+target.Shield;
                Need(h.TickInput(.02f,Frame(0,.8f,1)),"Actual carried first discharge refused: "+id);
                Need(h.Ammo==before-1&&h.ReadWeaponState(id).Shots==shots+1,"Actual first shot did not debit exactly one: "+id);
                R23ShotPose accepted;Need(h.TryReadLastAcceptedShotPose(out accepted)&&accepted.Valid&&Vector3.Distance(h.TracerWorldStart,new Vector3(accepted.Origin.X,accepted.Origin.Y,accepted.Origin.Z))<.003f,"Native tracer does not start at the captured displayed firing muzzle: "+id);
                var after=h.ReadPracticeTarget(0);Need(after.Health+after.Shield<vital,"Actual native target health not hit by "+id);
                if(id=="m1014")
                {
                    Need(h.LastShotgunPelletCount==8&&h.ShotgunTracerCount==8&&h.LastShotgunTargetGroups>=1,"Actual M1014 first accepted eight-ray/tracer/target-group path missing");
                    for(int i=0;i<8;i++){Vector3 a,b;Need(h.TryReadShotgunTracer(i,out a,out b)&&(a-b).sqrMagnitude>.01f,"Actual first shotgun tracer missing: "+i);}
                }
                if(id=="px4")
                {h.TickInput(.25f,Frame(0,.8f,1));Need(h.Ammo==before-1,"PX4 held trigger repeated");h.TickInput(.02f,Frame(0,.8f,.5f));Need(h.TickInput(.02f,Frame(0,.8f,1))&&h.Ammo==before-2,"PX4 source release/repress failed");}
                else
                {h.TickInput(.15f,Frame(0,.8f,1));h.TickInput(.15f,Frame(0,.8f,1));Need(h.ReadWeaponState(id).Shots>shots+1&&h.Ammo<before-1,"Source automatic hold did not repeat: "+id);}
                if(id=="m1014")
                {
                    int fired=before-h.Ammo,reserve=h.Reserve;h.TickInput(.02f,Frame(0,.8f));h.TickInput(.02f,Frame(0,.8f,0,true));Need(h.ReloadStage==ReloadPhase.NeedsMagazine,"M1014 source eject failed");h.TickInput(.02f,Frame(0,.8f));
                    loop.Rig.Left.position=h.ReloadOfferPosition;h.TickInput(.02f,Frame(.8f,.8f));loop.Rig.Left.position=h.ReloadInsertionPosition;h.TickInput(.02f,Frame(.8f,.8f));Need(h.ReloadStage==ReloadPhase.NeedsRack,"M1014 actual shell pack UP insertion failed");
                    h.TickInput(.02f,Frame(0,.8f));loop.Rig.Left.position=h.ChargingGripPosition;h.TickInput(.02f,Frame(.8f,.8f));loop.Rig.Left.position-=R23TrackedGripPose.Forward(loop.Rig.Right)*.09f;h.TickInput(.02f,Frame(.8f,.8f));
                    Need(h.ReloadStage==ReloadPhase.Ready&&h.Ammo==6&&h.Reserve==reserve-fired,"M1014 actual BACK rack/full finite reload failed");
                }
                Note(g,id+": real accepted native source shots, real target health loss and finite loaded debit"+(id=="m1014"?", eight actual pellet rays/traces +full UP/BACK finite reload":""));
            }
        }
        void CatalogFiveFireAndReload(Group g)
        {
            foreach(string id in new[]{"1911","tec9","rfb","fal","s85"})
            {
                ResetAtSite();var h=loop.Gameplay;Need(Has(h.AvailableWeaponIds,id),"Gallery-only profile cannot pass actual carried check: "+id);
                h.SetPickupHome(loop.Rig.Head.transform.position-forward*10,Quaternion.LookRotation(forward));
                Bounds sourceBounds;Vector3 pickup=Vector3.zero;Quaternion pose=Quaternion.identity;Need(h.TryGetWeaponBounds(id,out sourceBounds)&&R23WorldItemPlacement.TryDrop(loop.Rig.Head.transform,loop.Rig.transform,sourceBounds,out pickup,out pose),"Actual source floor placement refused: "+id);
                string action;Need(h.SpawnWorldWeapon(id,1,pickup,pose,out action),"Actual map-copy source owner refused: "+id);
                AimWorldWeapon(action,1,1.1f);Neutral();h.TickInput(.02f,Frame(0,.8f));
                Need(h.HasWeapon&&h.ActiveWeaponId==id&&h.OwnsWeapon(action)&&Slots()==3,"Actual source pickup did not reach native carried owner: "+id);
                h.SetPickupHome(loop.Rig.Head.transform.position-forward*10,Quaternion.LookRotation(forward));SetGunHandNearHead();Neutral();PlacePracticeOnActualFloor();AimAtPractice();
                int initial=h.Ammo;var target=h.ReadPracticeTarget(0);float vital=target.Health+target.Shield;
                Need(h.TickInput(.02f,Frame(0,.8f,1))&&h.Ammo==initial-1,"Actual native shot/ammo debit missing: "+id);
                target=h.ReadPracticeTarget(0);Need(target.Health+target.Shield<vital,"Actual native Health was not hit: "+id);
                for(int sample=0;sample<3;sample++)h.TickInput(.1f,Frame(0,.8f,1));
                if(id=="1911"||id=="fal")Need(h.Ammo==initial-1,"Source semi-auto repeated held trigger: "+id);
                if(id=="s85")
                {
                    Need(h.Ammo==initial-1&&h.ReloadStage==ReloadPhase.NeedsRack,"Source S85 did not require its per-shot bolt");
                    h.TickInput(.02f,Frame(0,.8f));loop.Rig.Left.position=h.ChargingGripPosition;h.TickInput(.02f,Frame(.8f,.8f));loop.Rig.Left.position-=R23TrackedGripPose.Forward(loop.Rig.Right)*.11f;h.TickInput(.02f,Frame(.8f,.8f));
                    Need(h.ReloadStage==ReloadPhase.Ready&&h.Ammo==initial-1,"S85 bolt created rounds or did not complete");
                }
                else if(id=="rfb"||id=="tec9")Need(h.Ammo<initial-1,"Source automatic trigger failed: "+id);
                int spent=initial-h.Ammo,reserve=h.Reserve;h.TickInput(.02f,Frame(0,.8f));h.TickInput(.02f,Frame(0,.8f,0,true));
                Need(h.ReloadStage==ReloadPhase.NeedsMagazine,"Actual magazine eject missing: "+id);h.TickInput(.02f,Frame(0,.8f));
                loop.Rig.Left.position=h.ReloadOfferPosition;h.TickInput(.02f,Frame(.8f,.8f));Need(h.ReloadStage==ReloadPhase.HeldMagazine,"Actual source offer not grabbable: "+id);
                loop.Rig.Left.position=h.ReloadInsertionPosition;h.TickInput(.02f,Frame(.8f,.8f));Need(h.ReloadStage==ReloadPhase.NeedsRack,"Actual source insertion failed: "+id);
                h.TickInput(.02f,Frame(0,.8f));loop.Rig.Left.position=h.ChargingGripPosition;h.TickInput(.02f,Frame(.8f,.8f));loop.Rig.Left.position-=R23TrackedGripPose.Forward(loop.Rig.Right)*.11f;h.TickInput(.02f,Frame(.8f,.8f));
                Need(h.ReloadStage==ReloadPhase.Ready&&h.Ammo==initial&&h.Reserve==reserve-spent,"Actual finite physical reload failed: "+id);
                Note(g,id+": real pickup, carried native Health hit, source trigger policy and finite physical reload"+(id=="s85"?"; actual per-shot bolt without free rounds":""));
            }
        }
        void IndependentCopies(Group g)
        {
            ResetAtSite();var h=loop.Gameplay;h.SetPickupHome(loop.Rig.Head.transform.position-forward*10,Quaternion.LookRotation(forward));
            Bounds bounds;Vector3 point=Vector3.zero;Quaternion rotation=Quaternion.identity;Need(h.TryGetWeaponBounds("cx4",out bounds)&&R23WorldItemPlacement.TryDrop(loop.Rig.Head.transform,loop.Rig.transform,bounds,out point,out rotation),"No actual floor/drop for duplicate-copy native check");
            string first,second;Need(h.SpawnWorldWeapon("cx4",5,point,rotation,out first,false),"Actual five-star independent copy refused");
            Need(h.SpawnWorldWeapon("cx4",1,point+Vector3.Cross(Vector3.up,forward)*.65f,rotation,out second,false),"Actual second same-profile copy refused");
            Need(first!=second&&first!="cx4"&&second!="cx4","New copies alias source default identity");R23WeaponInstanceInfo a=null,b=null;Need(h.TryReadWeaponInstance(first,out a)&&h.TryReadWeaponInstance(second,out b),"Immutable independent instance metadata missing");
            Need(a.InstanceId!=b.InstanceId&&a.Capacity==45&&b.Capacity==25&&h.ReadWeaponState(first).Loaded==45&&h.ReadWeaponState(second).Loaded==25,"Independent source rarity/capacity grants incorrect");
            Need(!h.IsWorldWeaponVisible(first)&&!h.IsWorldWeaponVisible(second),"Dormant world copies eagerly allocated visible art");
            Need(h.SetWorldWeaponVisible(first,true),"Source copy art did not activate");AimWorldWeapon(first,1,1.1f);Neutral();h.TickInput(.02f,Frame(0,.8f));
            Need(h.HasWeapon&&h.ActiveWeaponActionId==first&&h.OwnsWeapon(first),"Actual same-profile pickup selected the default owner instead of this copy");
            SetGunHandNearHead();Neutral();PlacePracticeOnActualFloor();AimAtPractice();Need(h.TickInput(.02f,Frame(0,.8f,1)),"Independent native owner would not fire");h.TickInput(.02f,Frame(0,.8f));
            int loaded=h.ReadWeaponState(first).Loaded,reserve=h.ReadReserve(first);Need(loaded==44&&h.ReadWeaponState(second).Loaded==25&&h.ReadWeaponState("cx4").Loaded==25,"One copy's native shot mutated another magazine");
            Vector3 spentDrop=point-Vector3.Cross(Vector3.up,forward)*.65f;SupportedReachableDrop(spentDrop);
            Need(h.DropOwned(first,spentDrop,rotation),"Spent independent copy failed actual world drop");
            Need(h.SetWorldWeaponVisible(first,false)&&h.SetWorldWeaponVisible(first,true),"Copy art stream cycle failed");
            Need(h.ReadWeaponState(first).Loaded==loaded&&h.ReadReserve(first)==reserve,"Art stream reset native ammunition");
            AimWorldWeapon(first,1,1.1f);Neutral();h.TickInput(.02f,Frame(0,.8f));
            Need(h.ActiveWeaponActionId==first&&h.OwnsWeapon(first)&&h.ReadWeaponState(first).Loaded==loaded&&h.ReadReserve(first)==reserve,"Drop/recollect reset a real native owner");
            Need(h.DespawnWorldWeapon(second)&&!h.TryReadWeaponInstance(second,out b),"Unclaimed map copy failed lifetime retirement");
            Note(g,"Two independent CX4 native owners, five-star45/one-star25, real aim/grip pickup+fire, floor drop, dormant/visible cycle and recollect preserve identity44rounds/shared finite reserve");
        }
        void RecoveredCaches(Group g)
        {
            ResetAtSite();var loot=loop.WorldLoot;Need(loot!=null&&loot.Ready,"Source map cache service unavailable");loot.SetWorldEnabled(true);
            foreach(var renderer in loop.Map.GetComponentsInChildren<MeshRenderer>(true))Need(renderer.sharedMaterial==null||renderer.sharedMaterial.name!="p2a61c272","Baked red cache question/ring marker still allocates a native renderer");
            Need(loot.ValidatedSites>0&&loot.ActivatedSites>0&&loot.SpawnedCount>0,"Actual current map produced no native floor-validated supported caches");
            int owners=0;var identities=new HashSet<string>();
            foreach(string action in loot.WorldInstanceActionIds)
            {
                string instance;Vector3 center,position;Quaternion rotation;Need(loop.Gameplay.TryReadWorldWeapon(action,out instance,out center,out position,out rotation),"Activated cache copy is not a real native world item");Need(identities.Add(instance),"Two cache props alias one native owner");
                bool floor=false;foreach(var hit in Physics.RaycastAll(position+Vector3.up*.01f,Vector3.down,3,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform)&&hit.collider.gameObject.layer!=19&&hit.normal.y>.7f)floor=true;
                Need(floor,"Actual cache spawned without supporting native map floor");owners++;
            }
            Need(owners==loot.SpawnedCount,"Cache owner/prop count mismatch");int count=owners;loot.SetWorldEnabled(false);foreach(string action in loot.WorldInstanceActionIds)Need(!loop.Gameplay.IsWorldWeaponVisible(action),"Map cache art survived lobby/pause withdrawal");
            loot.SetWorldEnabled(true);Need(loot.SpawnedCount==count,"Resume minted new cache ammunition");loot.SetWorldEnabled(false);
            Note(g,loot.ValidatedSites+" native floor-validated sites; "+owners+" distinct actual weapon owners; "+loot.UnsupportedDraws+" unsupported source draws intentionally unspawned; pause/resume does not mint owners");
        }
        void RemoteSwap(Group g)
        {
            ResetAtSite();var h=loop.Gameplay;Need(h.EquipOwned("cx4",1),"Source outgoing CX4 equip failed");h.SetPickupHome(loop.Rig.Head.transform.position-forward*10,Quaternion.LookRotation(forward));SetGunHandNearHead();Neutral();Need(h.TickInput(.02f,Frame(0,.8f,1)),"Outgoing CX4 state could not be spent");h.TickInput(.02f,Frame(0,.8f));int ammo=h.Ammo,reserve=h.ReadReserve("cx4");string outgoing=h.WeaponInstanceId;
            h.SetPickupHome(loop.Rig.Head.transform.position+forward*1.65f-Vector3.up*.45f,Quaternion.LookRotation(forward));
            string incoming;Vector3 center,position;Quaternion rotation;Need(h.TryReadWorldWeapon("mk18",out incoming,out center,out position,out rotation),"Real MK18 world prop missing");AimWorldWeapon("mk18",1,1.35f);Neutral();AssertHint(1,"weapon/mk18");
            h.TickInput(.02f,Frame(0,.8f));Need(h.ActiveWeaponId=="mk18"&&h.HasWeapon&&h.OwnsWeapon("mk18")&&!h.OwnsWeapon("cx4")&&Slots()==3,"Full kit did not replace selected source slot");Need(h.WeaponInstanceId==incoming,"Incoming native world instance identity changed");
            string actual;Need(h.TryReadWorldWeapon("cx4",out actual,out center,out position,out rotation)&&actual==outgoing&&h.ReadWeaponState("cx4").Loaded==ammo&&h.ReadReserve("cx4")==reserve,"Outgoing actual spent native state/identity was lost");SupportedReachableDrop(position);
            AimWorldWeapon("akm",1,1.35f);h.TickInput(.02f,Frame(0,.8f));Need(h.ActiveWeaponId=="mk18"&&!h.OwnsWeapon("akm")&&Slots()==3,"Continuously held grip swapped a second world gun");
            Note(g,"Actual1.35m rendered-center aim → native MK18 grip replacement; outgoing CX4 identity/ammo/reserve preserved on reachable real floor; held grip cannot swap twice");
        }
        void SmgFireAndReload(Group g)
        {
            foreach(string id in new[]{"ump","p90","mp5","mp9"})
            {
                ResetAtSite();var h=loop.Gameplay;AimWorldWeapon(id,1,1.35f);Neutral();h.TickInput(.02f,Frame(0,.8f));
                Need(h.HasWeapon&&h.ActiveWeaponId==id&&h.OwnsWeapon(id)&&Slots()==3,"Actual unowned SMG did not enter through source full-slot grip pickup: "+id);
                h.SetPickupHome(loop.Rig.Head.transform.position-forward*10,Quaternion.LookRotation(forward));SetGunHandNearHead();Neutral();PlacePracticeOnActualFloor();AimAtPractice();
                int before=h.Ammo;float vital=h.ReadPracticeTarget(0).Health+h.ReadPracticeTarget(0).Shield;
                Need(h.TickInput(.02f,Frame(0,.8f,1))&&h.Ammo==before-1,"Actual carried SMG did not debit its real round: "+id);
                var after=h.ReadPracticeTarget(0);Need(after.Health+after.Shield<vital,"Actual carried SMG ray did not reduce native Health: "+id);
                h.TickInput(.1f,Frame(0,.8f,1));Need(h.Ammo<before-1,"Actual source automatic hold did not repeat: "+id);
                if(id=="mp9")
                {
                    // Production clips the visible beam in LateUpdate. This
                    // synchronous native group has not yielded that frame yet.
                    h.RefreshFeedbackPresentation();AimAtPractice();h.RefreshFeedbackPresentation();
                    Need(h.Mp9LaserVisible&&Vector3.Distance(h.Mp9LaserStart,h.Mp9LaserEnd)>0&&Vector3.Distance(h.Mp9LaserStart,h.Mp9LaserEnd)<Vector3.Distance(h.Mp9LaserStart,h.PracticeAimPoint(0,false))+.6f,"Actual MP9 laser failed to clip on the real native target");
                    int ammo=h.Ammo;h.SetGameplayPaused(true);Need(!h.Mp9LaserVisible&&h.Ammo==ammo,"MP9 pause failed to withdraw the beam without changing ammo");h.SetGameplayPaused(false);
                }
                int spent=before-h.Ammo,reserve=h.Reserve;h.TickInput(.02f,Frame(0,.8f));h.TickInput(.02f,Frame(0,.8f,0,true));Need(h.ReloadStage==ReloadPhase.NeedsMagazine,"Actual source SMG click eject failed: "+id);h.TickInput(.02f,Frame(0,.8f));
                loop.Rig.Left.position=h.ReloadOfferPosition;h.TickInput(.02f,Frame(.8f,.8f));Need(h.ReloadStage==ReloadPhase.HeldMagazine,"Actual source SMG offered magazine not acquired: "+id);
                var gunRotation=loop.Rig.Right.rotation*R23TrackedGripPose.LocalRotation;
                loop.Rig.Left.position=h.ReloadInsertionPosition+gunRotation*(id=="p90"?Vector3.forward:Vector3.up)*.015f;h.TickInput(.02f,Frame(.8f,.8f));Need(h.ReloadStage==ReloadPhase.NeedsRack,"Actual source SMG insertion direction failed: "+id);
                h.TickInput(.02f,Frame(0,.8f));loop.Rig.Left.position=h.ChargingGripPosition;h.TickInput(.02f,Frame(.8f,.8f));loop.Rig.Left.position-=R23TrackedGripPose.Forward(loop.Rig.Right)*.11f;h.TickInput(.02f,Frame(.8f,.8f));
                Need(h.ReloadStage==ReloadPhase.Ready&&h.Ammo==before&&h.Reserve==reserve-spent,"Actual finite shared-SMG physical reload failed: "+id);
                Note(g,id+": actual world pickup, source automatic native damage and real finite shared-SMG reload"+(id=="p90"?" with FORWARD top-feed insertion":"")+(id=="mp9"?"; clipped target laser and pause withdrawal":""));
            }
        }
        void BuildStack()
        {
            ResetAtSite();var h=loop.Gameplay;Need(h.EquipOwned("cx4",1),"Source full kit selected gun unavailable");Neutral();
            Vector3 first=loop.Rig.Head.transform.position+forward*.9f-Vector3.up*.45f,second=first+Vector3.Cross(Vector3.up,forward)*.35f;
            Need(loop.Consumables.Spawn(Shaker1,"shaker",first)&&loop.Consumables.Spawn(Shaker2,"shaker",second),"Actual source shaker asset registration failed");
            AimSupply(Shaker1,0,1);Supply(0,0);AssertHint(0,"consumable/"+Shaker1);Supply(.8f,0);
            Need(loop.Consumables.OwnedItems.Length==1&&loop.Consumables.HeldId==Shaker1&&h.OwnedWeaponIds.Length==2&&!h.OwnsWeapon("cx4")&&Slots()==3,"Actual first shaker failed full-kit selected-gun eviction");
            Vector3 dropped;Quaternion q;Need(h.TryGetDroppedPose("cx4",out dropped,out q),"Shaker replacement omitted outgoing real gun");SupportedReachableDrop(dropped);
            loop.Consumables.Stow();AimSupply(Shaker2,0,1);Supply(0,0);Supply(.8f,0);
            var owned=loop.Consumables.OwnedItems;Need(owned.Length==2&&owned[0].Slot==owned[1].Slot&&loop.Consumables.HeldId==Shaker2&&Slots()==3,"Actual second shaker did not stack in selected one slot");
        }
        void StackSwap(Group g)
        {
            BuildStack();var h=loop.Gameplay;float health=h.Health,shield=h.Shield;AimWorldWeapon("mk18",1,1.35f);h.UpdateInputAvailability(Frame());h.TickInput(.02f,Frame());Supply(0,0);AssertHint(1,"weapon/mk18");h.TickInput(.02f,Frame(0,.8f));
            Need(h.OwnsWeapon("mk18")&&h.OwnedWeaponIds.Length==3&&loop.Consumables.OwnedItems.Length==0&&loop.Consumables.HeldId==null&&Slots()==3,"Incoming actual weapon failed whole selected shaker stack eviction");
            foreach(string id in new[]{Shaker1,Shaker2}){Vector3 p=Vector3.zero;Quaternion q;bool owned;var snapshot=loop.Consumables.InspectItem(id);Need(snapshot!=null&&!snapshot.Removed&&snapshot.Owner==null&&loop.Consumables.TryReadWorldItem(id,out p,out q,out owned)&&!owned,"Source stack member missing/not world-owned after atomic drop: "+id);SupportedReachableDrop(p);}
            Need(h.Health==health&&h.Shield==shield,"Pickup-only sequence accidentally applied consumable effects");Note(g,"Full kit→actual shaker evicts selected gun; second actual shaker shares slot; incoming real MK18 atomically returns BOTH members to reachable native world poses, no consumption/grant");
        }
        void NoFloor(Group g)
        {
            ResetAtSite();var h=loop.Gameplay;Need(h.EquipOwned("cx4",1),"Source full kit missing before no-floor case");int ammo=h.Ammo;string instance=h.WeaponInstanceId;Vector3 isolated=loop.Map.Bounds.max+new Vector3(1000,1000,1000);
            try
            {
                loop.Rig.SetSpawn(isolated,isolated+forward);h.SetPickupHome(loop.Rig.Head.transform.position+forward*1.65f-Vector3.up*.45f,Quaternion.LookRotation(forward));Bounds bounds;Vector3 p;Quaternion q;Need(h.TryGetWeaponBounds("cx4",out bounds)&&!R23WorldItemPlacement.TryDrop(loop.Rig.Head.transform,loop.Rig.transform,bounds,out p,out q),"Isolated no-floor site unexpectedly has a supported drop");
                AimWorldWeapon("mk18",1,1.35f);Neutral();AssertHint(1,"weapon/mk18");h.TickInput(.02f,Frame(0,.8f));Need(h.OwnedWeaponIds.Length==3&&h.OwnsWeapon("cx4")&&!h.OwnsWeapon("mk18")&&h.WeaponInstanceId==instance&&h.Ammo==ammo,"No-floor refusal mutated the actual selected gun/inventory");
            }
            finally{loop.Rig.SetSpawn(site,site+forward);}
            BuildStack();string held=loop.Consumables.HeldId;
            try
            {
                loop.Rig.SetSpawn(isolated,isolated+forward);h.SetPickupHome(loop.Rig.Head.transform.position+forward*1.65f-Vector3.up*.45f,Quaternion.LookRotation(forward));AimWorldWeapon("mk18",1,1.35f);h.UpdateInputAvailability(Frame());h.TickInput(.02f,Frame());Supply(0,0);h.TickInput(.02f,Frame(0,.8f));
                Need(h.OwnedWeaponIds.Length==2&&!h.OwnsWeapon("mk18")&&loop.Consumables.OwnedItems.Length==2&&loop.Consumables.HeldId==held&&Slots()==3,"No-floor selected stack refusal partially dropped or replaced a member");foreach(string id in new[]{Shaker1,Shaker2})Need(loop.Consumables.InspectItem(id).Owner!=null,"No-floor drop partially changed member custody");
            }
            finally{loop.Rig.SetSpawn(site,site+forward);}
            Note(g,"Actual isolated no-floor query rejects gun and whole-stack replacements with no fake platform, no lost member or added fourth slot; actor restored afterward");
        }
        void ObjectiveRouting(Group g)
        {
            const string supply="r23-objective-smoke/shaker";
            try
            {
                ResetAtSite();var h=loop.Gameplay;Need(h.EquipOwned("cx4",1),"Actual source primary unavailable for objective routing");
                Need(loop.Consumables.Audio!=null&&loop.Consumables.Audio.Ready&&loop.Consumables.Audio.LoadedClipCount==10,"Ten original consumable WAVs did not actually decode");
                AimWorldWeapon("mk18",1,1.2f);string instance;Vector3 center,position;Quaternion rotation;
                Need(h.TryReadWorldWeapon("mk18",out instance,out center,out position,out rotation),"Actual routing gun unavailable");
                Need(loop.Consumables.Spawn(supply,"shaker",center+forward*.35f),"Actual competing source supply failed to spawn");
                ObjectiveFrame(0,0);ObjectiveFrame(0,.8f);
                Need(h.ActiveWeaponId=="mk18"&&h.HasWeapon&&!h.OwnsWeapon("cx4")&&loop.Consumables.OwnedItems.Length==0,"Earlier-sampled farther supply stole the closer actual gun grip");
                Need(loop.SelectedGripObjective.Kind==Ashfall.Integration.Playable.Input.R23GripObjectiveKind.Weapon&&loop.Rig.RightObjectiveGripReserved,"Actual selected gun claim did not reserve its hand before wall acquisition");
                Need(loop.Consumables.RepositionWorldItem(supply,loop.Rig.Right.position+R23TrackedGripPose.Forward(loop.Rig.Right)*.15f),"Actual competing supply relocation failed");
                ObjectiveFrame(0,.8f);Need(loop.Consumables.OwnedItems.Length==0&&h.ActiveWeaponId=="mk18","Held objective grip acquired a newly closer real supply");
                ObjectiveFrame(0,0);ObjectiveFrame(0,.8f);
                Need(loop.Consumables.HeldId==supply&&h.OwnedWeaponIds.Length==2&&!h.OwnsWeapon("mk18")&&!h.HasWeapon,"Fresh closer supply did not replace/holster the source primary");
                Need(loop.Consumables.Audio.PlayedCueCount>0&&loop.Consumables.Audio.LastAction=="item_equip","Actual accepted source supply equip did not publish its native audio cue");
                Bounds bounds=loop.Consumables.GetDropBounds(supply);Vector3 drop;Quaternion q;
                Need(R23WorldItemPlacement.TryDrop(loop.Rig.Head.transform,loop.Rig.transform,bounds,out drop,out q)&&loop.Consumables.Drop(supply,drop,q),"Actual source supply drop failed before free-slot case");
                ObjectiveFrame(0,0);Need(h.EquipOwned("px4",1),"Actual retained PX4 equip failed");int ammo=h.Ammo,reserve=h.Reserve;
                Need(loop.Consumables.RepositionWorldItem(supply,loop.Rig.Right.position+R23TrackedGripPose.Forward(loop.Rig.Right)*.15f),"Actual free-slot source supply placement failed");
                ObjectiveFrame(0,0);ObjectiveFrame(0,.8f);
                Need(loop.Consumables.HeldId==supply&&!h.HasWeapon&&h.OwnsWeapon("px4")&&h.OwnedWeaponIds.Length==2&&h.ReadWeaponState("px4").Loaded==ammo&&h.ReadReserve("px4")==reserve,"Free-slot consumable failed to holster/preserve the actual gun rather than evicting it");
                Need(h.TryReadWorldWeapon("mk18",out instance,out center,out position,out rotation),"Actual dropped gun missing for same-hand source replacement");
                Pose(1,center-forward*1.2f,forward);
                // Real sequential source drops can overlap. Read the actual
                // provider's closest rendered-center target before dispatch.
                var neutral=new Ashfall.Modules.Player.HandSample{Tracked=true};loop.PrepareObjectiveGrips(neutral,neutral,true,true,true);
                string aimed;Vector3 actualCenter;float aimedScore;Need(h.TryReadAimedWorldWeapon(1,out aimed,out actualCenter,out aimedScore),"No actual same-hand world gun is eligible");
                string aimedInstance;Need(h.TryReadWorldWeapon(aimed,out aimedInstance,out actualCenter,out position,out rotation),"Actual chosen world gun identity missing");
                loop.StepItemInput(.02f,neutral,neutral,true,false);ObjectiveFrame(0,.8f);
                Need(h.HasWeapon&&h.ActiveWeaponId==aimed&&h.WeaponInstanceId==aimedInstance&&h.OwnsWeapon(aimed)&&loop.Consumables.HeldId==null&&loop.Consumables.OwnedItems.Length==0,"A holding consumable hand could not source-replace its selected slot with the actual aimed gun: intended="+aimed+" selected="+loop.SelectedGripObjective.Id+" active="+h.ActiveWeaponId+" held="+loop.Consumables.HeldId);
                Note(g,"Actual shared Root dispatch: nearer gun beats farther supply; held grip cannot steal again; fresh nearer supply replaces selected full slot; free-slot supply holsters rather than deletes the retained primary; before-climb source reservation asserted");
            }
            finally{ResetAtSite();}
        }
        void ObjectiveFrame(float leftGrip,float rightGrip)
        {
            var left=new Ashfall.Modules.Player.HandSample{Tracked=true,Grip=leftGrip};var right=new Ashfall.Modules.Player.HandSample{Tracked=true,Grip=rightGrip};
            loop.PrepareObjectiveGrips(left,right,true,true,true);loop.StepItemInput(.02f,left,right,true,false);
        }
        void Neutral(){loop.Gameplay.TickInput(.02f,Frame());Supply(0,0);}
        void Supply(float left,float right)
        {loop.Consumables.SetOtherSlotsUsed(loop.Gameplay.OwnedWeaponIds.Length);loop.Consumables.Sample(new R23ConsumableSample{DeltaSeconds=.02f,Enabled=true,LeftReady=true,RightReady=true,LeftFree=!loop.Gameplay.LeftHandOccupied,RightFree=!loop.Gameplay.RightHandOccupied,LeftGrip=left,RightGrip=right});}
        R23HandsInputFrame Frame(float left=0,float right=0,float trigger=0,bool click=false)
        {return new R23HandsInputFrame{LeftTracked=true,RightTracked=true,LeftHandReserved=loop.Consumables.LeftOccupied,RightHandReserved=loop.Consumables.RightOccupied,LeftGrip=left,RightGrip=right,RightTrigger=trigger,RightStickClick=click};}
        void AimWorldWeapon(string id,int hand,float distance)
        {
            string instance,aimed=null;Vector3 center,position;Quaternion rotation;Need(loop.Gameplay.TryReadWorldWeapon(id,out instance,out center,out position,out rotation),"Actual world gun missing: "+id);
            // Two real rack rows may overlap along a straight-on ray. Approach
            // from a clear angle; keep the production nearest/LOS policy intact.
            foreach(float angle in new[]{0f,35,-35,65,-65,90,-90})
            {
                Vector3 direction=Quaternion.AngleAxis(angle,Vector3.up)*forward;Pose(hand,center-direction*distance,direction);loop.Gameplay.UpdateInputAvailability(Frame());float score;Vector3 read;
                if(loop.Gameplay.TryReadAimedWorldWeapon(hand,out aimed,out read,out score)&&aimed==id&&Vector3.Distance((hand==0?loop.Rig.Left:loop.Rig.Right).position,read)>.999f&&Vector3.Distance((hand==0?loop.Rig.Left:loop.Rig.Right).position,read)<2)return;
            }
            Need(false,"Actual1–2m aim/LOS selected another gun or none: "+id+" got "+aimed);
        }
        void AimSupply(string id,int hand,float distance)
        {Vector3 p;Quaternion q;bool owned;Need(loop.Consumables.TryReadWorldItem(id,out p,out q,out owned)&&!owned,"Actual world supply missing: "+id);Pose(hand,p-forward*distance,forward);}
        void Pose(int hand,Vector3 point,Vector3 direction){var t=hand==0?loop.Rig.Left:loop.Rig.Right;t.SetPositionAndRotation(point,Quaternion.LookRotation(direction,Vector3.up)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation));Physics.SyncTransforms();}
        void SetGunHandNearHead(){Pose(1,loop.Rig.Head.transform.position+forward*.3f-Vector3.up*.2f,forward);loop.Rig.Left.position=loop.Rig.Head.transform.position-Vector3.Cross(Vector3.up,forward)*.3f-Vector3.up*.5f;}
        void PlacePracticeOnActualFloor()
        {
            Need(loop.Gameplay.PracticeTargetCount>0,"Actual native practice targets unavailable");
            foreach(float angle in new[]{0f,.25f,-.25f,.5f,-.5f,.8f,-.8f,1.2f,-1.2f})
            {
                Vector3 direction=Quaternion.AngleAxis(angle*Mathf.Rad2Deg,Vector3.up)*forward,probe=site+direction*2.5f+Vector3.up*3;RaycastHit floor=new RaycastHit();float nearest=float.PositiveInfinity;
                foreach(var hit in Physics.RaycastAll(probe,Vector3.down,8,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform)&&hit.normal.y>=.65f&&hit.distance<nearest){floor=hit;nearest=hit.distance;}
                if(float.IsPositiveInfinity(nearest))continue;Vector3 point=floor.point+Vector3.up*1.16f;Pose(1,loop.Rig.Head.transform.position+direction*.3f-Vector3.up*.2f,(point-loop.Rig.Head.transform.position).normalized);
                var bore=Bore();for(int i=0;i<5;i++)loop.Rig.Right.rotation=Quaternion.FromToRotation(bore.forward,(point-bore.position).normalized)*loop.Rig.Right.rotation;
                if(!ClearMapSegment(loop.Rig.Head.transform.position,bore.position)||!ClearMapSegment(bore.position,point))continue;
                var positions=new Vector3[loop.Gameplay.PracticeTargetCount];Vector3 side=Vector3.Cross(Vector3.up,direction);for(int i=0;i<positions.Length;i++)positions[i]=floor.point+Vector3.up*.02f+side*(i*2);
                loop.Gameplay.RepositionTrainingTargets(positions,Quaternion.LookRotation(-direction));Physics.SyncTransforms();return;
            }
            throw new InvalidOperationException("Real loaded map has no unoccluded source practice firing lane at the chosen actual floor site");
        }
        bool ClearMapSegment(Vector3 from,Vector3 to)
        {Vector3 delta=to-from;if(delta.sqrMagnitude<.00001f)return true;foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,(1<<0)|(1<<8)|(1<<19),QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform)||hit.collider.gameObject.layer==8)return false;return true;}
        void AimAtPractice(){Need(loop.Gameplay.PracticeTargetCount>0,"Actual native practice target unavailable");var point=loop.Gameplay.PracticeAimPoint(0,false);for(int i=0;i<5;i++){var bore=Bore();loop.Rig.Right.rotation=Quaternion.FromToRotation(bore.forward,(point-bore.position).normalized)*loop.Rig.Right.rotation;Physics.SyncTransforms();for(int j=0;j<5;j++)loop.Gameplay.TickInput(.1f,Frame());}Physics.SyncTransforms();}
        Transform Bore(){foreach(var t in loop.Rig.Right.GetComponentsInChildren<Transform>(true))if(t.name=="Actual source muzzle")return t;throw new InvalidOperationException("Actual held source bore missing");}
        void AssertHint(int hand,string id){loop.RefreshWorldPickupHints(true);Need(loop.PickupHints!=null&&loop.PickupHints.Read(hand).TargetId==id,"Root actual-provider pickup hint mismatch for "+id);}
        void SupportedReachableDrop(Vector3 p){Need(Vector3.Distance(loop.Rig.Head.transform.position,p)<=2.201f,"Actual world drop outside source reachable range");bool floor=false;foreach(var hit in Physics.RaycastAll(p+Vector3.up*.1f,Vector3.down,3,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore))if(!hit.collider.transform.IsChildOf(loop.Rig.transform)&&hit.normal.y>=.65f)floor=true;Need(floor,"Actual dropped source item has no native floor support");}
        int Slots(){var slots=new HashSet<int>();foreach(var item in loop.Consumables.OwnedItems)slots.Add(item.Slot);return loop.Gameplay.OwnedWeaponIds.Length+slots.Count;}
        static bool Has(string[] values,string id){foreach(string value in values)if(value==id)return true;return false;}
        sealed class ScenePose
        {
            readonly bool manual,external,rigEnabled,pump,persistence,lobby;readonly Vector3 position,head,left,right,origin;readonly Quaternion rotation,headQ,leftQ,rightQ;
            public ScenePose(R23PlayableLoop loop){manual=loop.ManualSmoke;external=loop.ExternalProbePump;rigEnabled=loop.Rig.enabled;pump=loop.Gameplay.ExternalInputPump;persistence=loop.Calibration.SuppressPersistence;lobby=loop.Lobby.Visible;position=loop.Rig.transform.position;rotation=loop.Rig.transform.rotation;head=loop.Rig.Head.transform.localPosition;headQ=loop.Rig.Head.transform.localRotation;left=loop.Rig.Left.localPosition;leftQ=loop.Rig.Left.localRotation;right=loop.Rig.Right.localPosition;rightQ=loop.Rig.Right.localRotation;origin=loop.Rig.TrackingOrigin.localPosition;}
            public void Restore(R23PlayableLoop loop)
            {
                loop.Gallery.Hide();loop.StartPlaying();loop.Consumables.ClearItems();loop.RestartRound();if(lobby)loop.ReturnToLobby();loop.Rig.SetSpawn(position,position+rotation*Vector3.forward);loop.Rig.transform.rotation=rotation;loop.Rig.TrackingOrigin.localPosition=origin;
                loop.Rig.Head.transform.SetLocalPositionAndRotation(head,headQ);loop.Rig.Left.SetLocalPositionAndRotation(left,leftQ);loop.Rig.Right.SetLocalPositionAndRotation(right,rightQ);loop.RefreshWorldPickupHints(false);
                loop.Calibration.SuppressPersistence=persistence;loop.Gameplay.ExternalInputPump=pump;loop.Rig.enabled=rigEnabled;loop.ExternalProbePump=external;loop.ManualSmoke=manual;Physics.SyncTransforms();
            }
        }
    }
}
#endif
