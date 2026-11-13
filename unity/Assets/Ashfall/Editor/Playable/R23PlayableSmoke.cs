#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Diagnostics;
namespace Ashfall.Editor.Playable
{
    [InitializeOnLoad]
    public static class R23SmokeControl
    {
        const string Key="Ashfall.R23.SmokeOutput";
        static IEnumerator running;
        static R23SmokeControl(){EditorApplication.playModeStateChanged+=Changed;}
        public static void Run()
        {
            try
            {
                string priorReport=R23PlayableBuild.Argument("-ashfallLastBuildReport");if(!String.IsNullOrEmpty(priorReport))R23QuestBuildReport.Export(priorReport);
                R23PlayableBuild.Prepare();var xr=R23PlayableBuild.Xr();xr.InitManagerOnStart=false;
                string path=R23PlayableBuild.Argument("-ashfallVRSmoke");if(String.IsNullOrEmpty(path)||File.Exists(path))throw new Exception("New -ashfallVRSmoke output required");
                SessionState.SetString(Key,path);SessionState.SetBool(Key+"ok",false);EditorApplication.isPlaying=true;
                SessionState.SetBool(Key+"floorOnly",R23PlayableBuild.Argument("-ashfallFloorOnly")=="true");
                SessionState.SetBool(Key+"sourceUi",R23PlayableBuild.Argument("-ashfallSourceUI")=="true");
                SessionState.SetBool(Key+"motionOnly",R23PlayableBuild.Argument("-ashfallMotionOnly")=="true");
                SessionState.SetBool(Key+"valleyOnly",R23PlayableBuild.Argument("-ashfallValleyOnly")=="true");
                SessionState.SetBool(Key+"humanBugs",R23PlayableBuild.Argument("-ashfallHumanBugs")=="true");
                SessionState.SetBool(Key+"arsenalOnly",R23PlayableBuild.Argument("-ashfallArsenalOnly")=="true");
                SessionState.SetBool(Key+"feedbackOnly",R23PlayableBuild.Argument("-ashfallFeedbackOnly")=="true");
                SessionState.SetBool(Key+"entryMap",!String.IsNullOrEmpty(R23PlayableBuild.Argument("-ashfallEntryMap")));
                SessionState.SetBool(Key+"parity",!String.IsNullOrEmpty(R23PlayableBuild.Argument("-ashfallParityRecipe")));
                SessionState.SetBool(Key+"r6Ui",R23PlayableBuild.Argument("-ashfallR6UI")=="true");
                SessionState.SetBool(Key+"r6Gameplay",R23PlayableBuild.Argument("-ashfallR6Gameplay")=="true");
                SessionState.SetBool(Key+"appearanceOnly",R23PlayableBuild.Argument("-ashfallAppearanceOnly")=="true");
                SessionState.SetBool(Key+"lanSpawnHealth",R23PlayableBuild.Argument("-ashfallLanSpawnHealth")=="true");
                SessionState.SetBool(Key+"videoFeedback",!String.IsNullOrEmpty(R23PlayableBuild.Argument("-ashfallVideoFeedback")));
                SessionState.SetBool(Key+"lowerGrilles",R23PlayableBuild.Argument("-ashfallLowerGrilles")=="true");
                SessionState.SetBool(Key+"familyPassages",R23PlayableBuild.Argument("-ashfallFamilyPassages")=="true");
                SessionState.SetBool(Key+"towerFamily",R23PlayableBuild.Argument("-ashfallTowerFamily")=="true");
                SessionState.SetBool(Key+"classicPassages",R23PlayableBuild.Argument("-ashfallClassicPassages")=="true");
                SessionState.SetBool(Key+"classicSourceCuts",R23PlayableBuild.Argument("-ashfallClassicSourceCuts")=="true");
                SessionState.SetBool(Key+"localMatch",R23PlayableBuild.Argument("-ashfallLocalMatch")=="true");
                SessionState.SetBool(Key+"ghostReload",R23PlayableBuild.Argument("-ashfallGhostReload")=="true");
                SessionState.SetBool(Key+"sourceLoot",R23PlayableBuild.Argument("-ashfallSourceLoot")=="true");
                SessionState.SetBool(Key+"archiveGhost",R23PlayableBuild.Argument("-ashfallArchiveGhost")=="true");
                SessionState.SetBool(Key+"archiveItems",R23PlayableBuild.Argument("-ashfallArchiveItems")=="true");
                SessionState.SetBool(Key+"menuUsability",R23PlayableBuild.Argument("-ashfallMenuUsability")=="true");
                SessionState.SetBool(Key+"mixedCache",R23PlayableBuild.Argument("-ashfallMixedCache")=="true");
                SessionState.SetBool(Key+"workingArt",R23PlayableBuild.Argument("-ashfallWorkingArt")=="true");
                SessionState.SetBool(Key+"rarityHighlights",R23PlayableBuild.Argument("-ashfallRarityHighlights")=="true");
                SessionState.SetBool(Key+"postPlaytest",R23PlayableBuild.Argument("-ashfallPostPlaytest")=="true");
                SessionState.SetBool(Key+"baselineGround",R23PlayableBuild.Argument("-ashfallBaselineGround")=="true");
                SessionState.SetBool(Key+"sourceTraining",R23PlayableBuild.Argument("-ashfallSourceTraining")=="true");
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        static void Changed(PlayModeStateChange state)
        {
            string path=SessionState.GetString(Key,"");if(path.Length==0)return;
            if(state==PlayModeStateChange.EnteredPlayMode){running=SessionState.GetBool(Key+"videoFeedback",false)?new R24VideoFeedbackSmoke().Start():SessionState.GetBool(Key+"lanSpawnHealth",false)?new R24LanSpawnHealthSmoke().Start():SessionState.GetBool(Key+"lowerGrilles",false)?new R24LowerGrilleSmoke().Start():SessionState.GetBool(Key+"rarityHighlights",false)?new R24RarityHighlightCapture().Run():SessionState.GetBool(Key+"mixedCache",false)?new R24MixedCacheSmoke().Run():SessionState.GetBool(Key+"menuUsability",false)?new R24MenuUsabilitySmoke().Run():SessionState.GetBool(Key+"workingArt",false)?new R24WorkingArtSmoke().Run():SessionState.GetBool(Key+"postPlaytest",false)?new R24PostPlaytestSmoke(R23PlayableBuild.Argument("-ashfallPostPlaytestFixes")=="true").Run():SessionState.GetBool(Key+"baselineGround",false)?new R24BaselineGroundSmoke().Run():SessionState.GetBool(Key+"sourceTraining",false)?new R24TrainingSourceSmoke(R23PlayableBuild.Argument("-ashfallSourceTrainingFixes")=="true",R23PlayableBuild.Argument("-ashfallTrainingCaptureOnly")=="true").Run():SessionState.GetBool(Key+"archiveItems",false)?new R24ArchiveItemsSmoke().Run():SessionState.GetBool(Key+"sourceLoot",false)?new R24SourceLootSmoke().Run():SessionState.GetBool(Key+"archiveGhost",false)?new R24ArchiveGhostRecoverySmoke().Run():SessionState.GetBool(Key+"ghostReload",false)?new R24GhostReloadSmoke().Run():SessionState.GetBool(Key+"localMatch",false)?new R24LocalMatchSmoke(R23PlayableBuild.Argument("-ashfallLocalMatchFixes")=="true").Run():SessionState.GetBool(Key+"classicSourceCuts",false)?new R24TowerFamilySmoke(true,true).Start():SessionState.GetBool(Key+"classicPassages",false)?new R24TowerFamilySmoke(true).Start():SessionState.GetBool(Key+"familyPassages",false)?new R24TowerFamilySmoke(false,false,true).Start():SessionState.GetBool(Key+"towerFamily",false)?new R24TowerFamilySmoke().Start():SessionState.GetBool(Key+"appearanceOnly",false)?new R24AppearanceSmoke().Start():SessionState.GetBool(Key+"r6Gameplay",false)?new R24R6GameplaySmoke().Run():SessionState.GetBool(Key+"r6Ui",false)?new R24R6UiSmoke().Run():SessionState.GetBool(Key+"parity",false)?new R24ParityCapture().Start():SessionState.GetBool(Key+"arsenalOnly",false)?new R23ArsenalPickupSmoke().Start():SessionState.GetBool(Key+"entryMap",false)?new R23MapEntrySmoke().Start():SessionState.GetBool(Key+"feedbackOnly",false)?new R23FeedbackSmoke().Start():SessionState.GetBool(Key+"humanBugs",false)?new R23HumanBugSmoke().Start():SessionState.GetBool(Key+"valleyOnly",false)?new R23ValleySmoke().Start():SessionState.GetBool(Key+"floorOnly",false)?new R23FloorProbe().Start(Finish):SessionState.GetBool(Key+"sourceUi",false)?new R23SourceUiCapture().Start():SessionState.GetBool(Key+"motionOnly",false)?new R23SourceMotionSmoke().Start():new R23PlayableSmoke().Start();EditorApplication.update+=Pump;}
            if(state==PlayModeStateChange.EnteredEditMode)
            {EditorApplication.update-=Pump;running=null;bool ok=SessionState.GetBool(Key+"ok",false);SessionState.EraseString(Key);R23PlayableBuild.Xr().InitManagerOnStart=true;AssetDatabase.SaveAssets();EditorApplication.Exit(ok?0:2);}
        }
        static void Pump(){if(running!=null&&!running.MoveNext()){running=null;EditorApplication.update-=Pump;}}
        public static void Finish(bool success,string json)
        {
            string path=SessionState.GetString(Key,"");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,json);SessionState.SetBool(Key+"ok",success);EditorApplication.isPlaying=false;
        }
        public static string Capture(Camera camera,string suffix=null)
        {
            foreach(var ui in UnityEngine.Object.FindObjectsByType<Ashfall.Integration.Playable.UI.R23VrUI>())ui.RefreshTextLayout();
            foreach(var text in UnityEngine.Object.FindObjectsByType<TextMesh>())if(text.gameObject.activeInHierarchy&&text.transform.IsChildOf(UnityEngine.Object.FindAnyObjectByType<Ashfall.Integration.Playable.UI.R23VrUI>().transform))
            {var renderer=text.GetComponent<MeshRenderer>();Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"UI CAPTION {0} scale={1} localBounds={2} worldBounds={3} order={4}",text.text,text.transform.localScale,renderer.localBounds.size,renderer.bounds.size,renderer.sortingOrder);}
            string path=Path.ChangeExtension(SessionState.GetString(Key,""),"png");if(suffix!=null)path=Path.Combine(Path.GetDirectoryName(path),"smoke-"+suffix+".png");
            var target=RenderTexture.GetTemporary(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            Texture2D pixels=null;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());return path;
            }
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)UnityEngine.Object.Destroy(pixels);}
        }
    }
    public sealed class R23PlayableSmoke
    {
        [Serializable]sealed class Result
        {public bool map,movement,pickup,fire,damage,restart,targetHit,menu,skins,calibration,alpha,activities,preview,loadout,headHull,jumpPads;public bool headsetVerified=false;public string screenshot="",error="";}
        public IEnumerator Start()
        {
            var result=new Result();R23PlayableLoop loop=null;float until=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<until)
            {loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            yield return null;
            try
            {
                Need(loop!=null&&loop.Ready,loop==null?"Scene loop missing":loop.Failure??"Map/visual load timeout");
                result.map=loop.Map.IsLoaded;Need(result.map,"Actual source map not loaded");
                Need(loop.UI.Page==Ashfall.Integration.Playable.UI.R23UiPage.Ready&&loop.UI.BlocksGameplay,"Loading must lead to the Start menu before gameplay");
                R23SmokeControl.Capture(loop.Rig.Head,"menu");
                var startButton=loop.UI.transform.Find("R23 eye-level menu/START MATCH");Need(startButton!=null,"Ready button missing");
                Vector3 ray=loop.Rig.Head.transform.position,direction=(startButton.position-ray).normalized;
                loop.UI.UpdatePointer(1,ray,direction,false,true);loop.UI.UpdatePointer(1,ray,direction,true,true);loop.UI.UpdatePointer(1,ray,direction,false,true);
                if(loop.UI.Page==Ashfall.Integration.Playable.UI.R23UiPage.SpawnChoice)
                {
                if(loop.UI.Page==Ashfall.Integration.Playable.UI.R23UiPage.SpawnChoice)
                {
                var spawnButton=loop.UI.transform.Find("R23 eye-level menu/GAME SPAWN");Need(spawnButton!=null,"Game spawn button missing");
                direction=(spawnButton.position-ray).normalized;
                loop.UI.UpdatePointer(1,ray,direction,false,true);loop.UI.UpdatePointer(1,ray,direction,true,true);loop.UI.UpdatePointer(1,ray,direction,false,true);
                }
                }
                result.menu=loop.UI.Page==Ashfall.Integration.Playable.UI.R23UiPage.Playing;Need(result.menu,"VR release-click Game Spawn did not enter gameplay");
                loop.ManualSmoke=true;loop.UI.SetPlaying();loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);
                loop.Gameplay.TickInput(0,new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true});
                loop.Calibration.RequestRecalibration();for(int i=0;i<8;i++)loop.Calibration.Tick(true,true,false,.05f);
                result.calibration=loop.Calibration.Calibrated&&Mathf.Abs(loop.Rig.Head.transform.position.y-loop.Rig.transform.position.y-1.7088f)<.002f;Need(result.calibration,"Stable native standing-height calibration failed");
                Physics.SyncTransforms();Vector3 start=loop.Rig.transform.position;
                RaycastHit floor;Need(Physics.Raycast(start+Vector3.up*.2f,Vector3.down,out floor,1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),"Spawn has no native map floor");
                for(int i=0;i<4;i++)loop.Rig.SmokeXrMove(Vector2.zero,.05f);
                foreach(var axis in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
                {for(int i=0;i<6;i++)loop.Rig.SmokeXrMove(axis,.05f);if(Vector3.ProjectOnPlane(loop.Rig.transform.position-start,Vector3.up).magnitude>.1f)break;}
                result.movement=Vector3.ProjectOnPlane(loop.Rig.transform.position-start,Vector3.up).magnitude>.1f;Need(result.movement,"No actual horizontal controller movement in map");
                loop.Rig.Right.position=loop.Gameplay.WeaponGripPosition;Physics.SyncTransforms();loop.Gameplay.TickInput(.02f,new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true,Grip=.7f});result.pickup=loop.Gameplay.HasWeapon;Need(result.pickup,"Analog grip proximity pickup failed");
                loop.Gameplay.TickControls(.1f,false,false);loop.Gameplay.TickControls(.1f,false,false);int ammo=loop.Gameplay.Ammo;
                result.fire=loop.Gameplay.Fire(.12f)&&loop.Gameplay.Ammo==ammo-1;Need(result.fire,"Actual fire/ammo debit failed");
                if(loop.Gameplay.PracticeTargetCount>0)
                {
                    Vector3 aim=loop.Gameplay.PracticeAimPoint(0,false);Transform muzzle=null;
                    foreach(var part in loop.Rig.Right.GetComponentsInChildren<Transform>())if(part.name=="Actual source muzzle")muzzle=part;
                    Need(muzzle!=null,"Held weapon bore missing");loop.Rig.Right.position=loop.Rig.Head.transform.position+loop.Rig.Head.transform.right*.25f-Vector3.up*.2f;
                    for(int i=0;i<4;i++)loop.Rig.Right.rotation=Quaternion.FromToRotation(muzzle.forward,(aim-muzzle.position).normalized)*loop.Rig.Right.rotation;
                    Physics.SyncTransforms();var before=loop.Gameplay.ReadPracticeTarget(0);float total=before.Health+before.Shield;
                    loop.Gameplay.TickControls(.1f,false,false);loop.Gameplay.Fire(.12f);var after=loop.Gameplay.ReadPracticeTarget(0);
                    result.targetHit=after.Health+after.Shield<total;Need(result.targetHit,"Map-occluded native muzzle ray did not damage the practice mannequin");
                }
                float playerBefore=loop.Gameplay.Health+loop.Gameplay.Shield;loop.Gameplay.ApplyPlayerDamage(35);result.damage=Mathf.Abs(playerBefore-loop.Gameplay.Health-loop.Gameplay.Shield-35)<.001f;Need(result.damage,"Player shield/health damage failed");
                loop.Gameplay.ApplyPlayerDamage(1000);Need(loop.Gameplay.Health==0,"Lethal damage failed");loop.RestartRound();
                result.restart=loop.Gameplay.Health==100&&!loop.Gameplay.HasWeapon&&loop.Gameplay.Hits==0&&Vector3.Distance(loop.Rig.transform.position,loop.Map.Spawn)<.01f;Need(result.restart,"Restart failed");
                R23ActivitySmoke.Run(loop);result.activities=true;
                R23LoadoutSmoke.Run(loop);result.loadout=true;
                R23HeadHullSmoke.Run(loop);result.headHull=true;
                loop.UI.UpdateHud(new Ashfall.Integration.Playable.UI.R23HudSnapshot(loop.Gameplay.Health,loop.Gameplay.MaxHealth,loop.Gameplay.Ammo,loop.Gameplay.Reserve,loop.Gameplay.HasWeapon,loop.Gameplay.Reloading,loop.Gameplay.ActiveWeaponName,loop.Gameplay.Hits,loop.Gameplay.Shield,loop.Gameplay.MaxShield,loop.Rig.Stamina,loop.Rig.StaminaMaximum,loop.Activities.Balance,loop.Activities.Mode.ToString(),loop.Gameplay.ReloadInstruction));
            }
            catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
            yield return null;
            try{if(result.error.Length==0)result.screenshot=R23SmokeControl.Capture(loop.Rig.Head);}catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
            if(result.error.Length==0)
            {
                var previewObject=new GameObject("R23 source skin verification camera");var preview=previewObject.AddComponent<Camera>();preview.fieldOfView=45;preview.nearClipPlane=.03f;preview.farClipPlane=20;preview.clearFlags=CameraClearFlags.SolidColor;preview.backgroundColor=new Color(.12f,.15f,.19f);preview.cullingMask=1<<23;
                preview.transform.position=loop.Rig.transform.position+loop.Rig.transform.forward*3+Vector3.up;preview.transform.LookAt(loop.Rig.transform.position+Vector3.up*.9f);
                loop.Rig.Left.position=loop.Rig.Head.transform.position-loop.Rig.Head.transform.right*.4f-Vector3.up*.35f;loop.Rig.Right.position=loop.Rig.Head.transform.position+loop.Rig.Head.transform.right*.4f-Vector3.up*.35f;
                foreach(var style in loop.Appearance.AvailableStyles)
                {
                    loop.Appearance.SelectStyle(style.Id);float end=Time.realtimeSinceStartup+30;
                    while(loop.Appearance.Loading&&Time.realtimeSinceStartup<end)yield return null;
                    loop.Appearance.SetPresentationState(true,false);yield return null;
                    try
                    {
                        Need(loop.Appearance.Ready&&loop.Appearance.SelectedStyleId==style.Id,"Selected source skin failed: "+style.Id+" "+loop.Appearance.Failure);
                        var root=GameObject.Find("R23 appearance "+style.Id);Need(root!=null,"Source appearance graph missing");
                        foreach(var node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=23;
                        R23SmokeControl.Capture(preview,"skin-"+style.Id);
                    }
                    catch(Exception e){result.error=e.ToString();Debug.LogException(e);break;}
                }
                result.skins=result.error.Length==0;UnityEngine.Object.Destroy(previewObject);
            }
            if(result.error.Length==0)
            {
                loop.UI.OpenAppearance();loop.Preview.Reanchor();loop.Preview.Show("cinder_scout");float previewEnd=Time.realtimeSinceStartup+30;
                while(loop.Preview.Loading&&Time.realtimeSinceStartup<previewEnd)yield return null;
                yield return null;
                try{Need(loop.Preview.Ready,"Full source appearance preview failed: "+loop.Preview.Failure);R23SmokeControl.Capture(loop.Rig.Head,"appearance-menu");result.preview=true;}
                catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
                loop.Preview.Hide();loop.UI.SetPlaying();
            }
            if(result.error.Length==0)
            {
                const string secondMap="archive:the-fortress-gods-alamo-hank";loop.SelectMap(secondMap,true);float end=Time.realtimeSinceStartup+120; // ALFA was retired 2026-11-09; any GODS archive map exercises the same async transition.
                while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
                try
                {
                    Need(loop.Ready&&loop.Map.IsLoaded&&loop.Map.MapId==secondMap,"Actual archive async transition failed: "+loop.Failure);
                    Need(loop.UI.BlocksGameplay,"Map switch skipped Ready entry");loop.UI.SetPlaying();Physics.SyncTransforms();RaycastHit ground;
                    Need(Physics.Raycast(loop.Rig.transform.position+Vector3.up*.2f,Vector3.down,out ground,1,Physics.AllLayers,QueryTriggerInteraction.Ignore),"Actual archive game entry has no native floor");
                    result.alpha=true;result.jumpPads=false;R23SmokeControl.Capture(loop.Rig.Head,"archive-second-map"); // No GODS map carries authored jump pads, so the ALFA pad sub-check no longer runs here.
                }
                catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
            }
            bool ok=result.map&&result.movement&&result.pickup&&result.fire&&result.damage&&result.restart&&result.error.Length==0;
            R23SmokeControl.Finish(ok,JsonUtility.ToJson(result,true));
        }
        static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
    }
}
#endif
