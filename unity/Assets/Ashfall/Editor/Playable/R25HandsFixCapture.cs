#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.UI;
namespace Ashfall.Editor.Playable
{
    // Editor-only, bounded capture of the production UI from the actual head camera.
    [InitializeOnLoad]
    public static class R25HandsFixCapture
    {
        const string Key = "Ashfall.HandsFix.Output";
        static IEnumerator work;
        static float deadline;
        static object waiting;
        static int lastFrame = -1;
        static R25HandsFixCapture() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            string output = R23PlayableBuild.Argument("-ashfallHandsFixOutput");
            if (String.IsNullOrEmpty(output) || Directory.Exists(output)) { EditorApplication.Exit(2); return; }
            Directory.CreateDirectory(output);
            R23PlayableBuild.Prepare();
            R23PlayableBuild.Xr().InitManagerOnStart = false;
            SessionState.SetString(Key, output);
            EditorApplication.isPlaying = true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if (SessionState.GetString(Key, "").Length == 0) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            { deadline = Time.realtimeSinceStartup + 180; work = CaptureHands(); EditorApplication.update += Pump; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Pump; work = null;
                string output = SessionState.GetString(Key, ""); SessionState.EraseString(Key);
                R23PlayableBuild.Xr().InitManagerOnStart = true; AssetDatabase.SaveAssets();
                EditorApplication.Exit(File.Exists(Path.Combine(output, "success.txt")) ? 0 : 2);
            }
        }
        static void Pump()
        {
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("180 second capture deadline");
                // Editor update runs faster than play frames; honor coroutine waits.
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                var delay = waiting as CustomYieldInstruction;
                if (delay != null && delay.keepWaiting) return;
                if (work != null)
                {
                    if (!work.MoveNext()) { work = null; EditorApplication.isPlaying = false; }
                    else waiting = work.Current;
                }
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(SessionState.GetString(Key, ""), "error.txt"), e.ToString());
                Debug.LogException(e); work = null; EditorApplication.isPlaying = false;
            }
        }
        static void Need(bool value,string why){if(!value)throw new Exception(why);}
        static IEnumerator CaptureHands()
        {
            R23PlayableLoop loop=null;
            while(loop==null||!loop.Ready){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&loop.Failure!=null)throw new Exception(loop.Failure);yield return null;}
            loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
            var camera=new GameObject("Hands evidence camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=65;camera.nearClipPlane=.02f;camera.farClipPlane=120;
            var view=UnityEngine.Object.FindAnyObjectByType<Ashfall.Integration.Playable.Traversal.R25TraversalPresentation>();if(view==null){view=loop.Rig.gameObject.AddComponent<Ashfall.Integration.Playable.Traversal.R25TraversalPresentation>();view.Initialize();}
            foreach(string place in new[]{"lobby","range"})
            {
                if(place=="range"){loop.SelectMap("range",true);while(!loop.Ready){if(loop.Failure!=null)throw new Exception(loop.Failure);yield return null;}loop.StartPlaying();}
                for(int f=0;f<10;f++){loop.SmokeUiUpdate();yield return null;}
                Vector3 eye=loop.Rig.Head.transform.position;
                loop.Rig.Left.SetPositionAndRotation(eye+new Vector3(-.20f,-.28f,.40f),Quaternion.identity);
                loop.Rig.Right.SetPositionAndRotation(eye+new Vector3(.20f,-.28f,.40f),Quaternion.identity);
                camera.transform.SetPositionAndRotation(eye+new Vector3(-.20f,.04f,.02f),Quaternion.LookRotation(loop.Rig.Left.position-eye-new Vector3(0,.04f,.02f)));
                foreach(string state in new[]{"empty","grapple","rifle","after-jet","near-vehicle"})
                {
                    loop.Gameplay.CanCollectWorldWeapon=null;loop.Gameplay.MakeRoomForWorldWeapon=null;loop.Gameplay.CanClaimWorldGrip=null;loop.Gameplay.AuthorizeWorldDrop=null;loop.Gameplay.SlotCapacity=6;loop.Gameplay.PreferredPickupHand=0;string fixture=null;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);loop.Gameplay.SetWeaponPresentationSuppressed(false);loop.Gameplay.HolsterCurrent();loop.Gameplay.UpdateInputAvailability(new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true});
                    if(state=="grapple"||state=="rifle"){string id;Need(loop.Gameplay.SpawnWorldWeapon(state=="grapple"?"grapple":"mk18",2,loop.Rig.Left.position,Quaternion.identity,out id,true),"weapon spawn");Need(loop.Gameplay.CollectCatalogWorldWeapon(id,0),"equip fixture");fixture=id;}
                    loop.Gameplay.TickInput(.016f,new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true});loop.Gameplay.RefreshFeedbackPresentation();
                    if(state=="after-jet")view.Jet(eye,Quaternion.identity,true,1,.8f,loop.Rig.Left);
                    view.Jet(eye,Quaternion.identity,false,0,.8f,loop.Rig.Left);
                    // Matched legacy control: exact preserved fuel-arc formula, presentation only.
                    var arc=(LineRenderer)view.GetType().GetField("fuelArc",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
                    Need(!arc.enabled,"production jet does not draw offhand ring");arc.enabled=true;for(int k=0;k<33;k++){float a=k/32f*Mathf.PI*1.8f*.8f;arc.SetPosition(k,loop.Rig.Left.position+loop.Rig.Left.up*.065f+(loop.Rig.Left.right*Mathf.Cos(a)+loop.Rig.Left.forward*Mathf.Sin(a))*.07f);}
                    Save(camera,place+"-"+state+"-before-control");arc.enabled=false;Save(camera,place+"-"+state+"-after");if(fixture!=null){loop.Gameplay.DropCurrent(eye+Vector3.forward*2,Quaternion.identity);loop.Gameplay.DespawnWorldWeapon(fixture);}
                }
                if(place=="lobby")
                {
                    loop.Gameplay.HolsterCurrent();
                    var latch=(Ashfall.Integration.Playable.Exclusion.R25ObstructionLatch)loop.Gameplay.GetType().GetField("leftGloveLatch",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loop.Gameplay);
                    latch.Step(true,0);
                    var present=loop.Gameplay.GetType().GetMethod("PresentObstruction",BindingFlags.Instance|BindingFlags.NonPublic);
                    present.Invoke(loop.Gameplay,new object[]{false});
                    Need(loop.Gameplay.SourceLeftGloveRoot.GetComponentInChildren<Renderer>(true).forceRenderingOff,"latched hidden setup");
                    for(int f=0;f<15;f++){loop.Gameplay.RefreshFeedbackPresentation();present.Invoke(loop.Gameplay,new object[]{true});yield return new WaitForSecondsRealtime(.02f);}
                    Need(!latch.Hidden,"glove obstruction recovery after same-frame LateUpdate/before-render passes");
                    Need(!loop.Gameplay.SourceLeftGloveRoot.GetComponentInChildren<Renderer>(true).forceRenderingOff,"glove renderer restored");
                    loop.Gameplay.TickInput(.016f,new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame());Need(!loop.Gameplay.SourceLeftGloveRoot.gameObject.activeSelf,"tracking loss hides");
                    loop.Gameplay.TickInput(.016f,new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true});Need(loop.Gameplay.SourceLeftGloveRoot.gameObject.activeSelf,"tracking return restores");
                }
                if(place=="range")
                {
                    for(int f=0;f<80;f++){loop.SmokeUiUpdate();yield return null;}
                    camera.fieldOfView=75;Vector3 target=loop.Map.Spawn+new Vector3(0,.4f,0);camera.transform.SetPositionAndRotation(target+new Vector3(0,3,-4),Quaternion.LookRotation(new Vector3(0,-2.6f,4)));string grapple=loop.WorldLoot.RangeGrappleActionId;Need(grapple!=null,"Range grapple spawned");
                    var tableRoot=(GameObject)loop.WorldLoot.GetType().GetField("rangeGrappleTable",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loop.WorldLoot);loop.Gameplay.SetWorldWeaponVisible(grapple,false);tableRoot.SetActive(false);Save(camera,"range-weapons-before-control");tableRoot.SetActive(true);loop.Gameplay.SetWorldWeaponVisible(grapple,true);Save(camera,"range-weapons-after");
                    Vector3 table=loop.Map.Spawn+new Vector3(-.8f,.75f,1.6f);camera.fieldOfView=50;camera.transform.SetPositionAndRotation(table+new Vector3(0,.65f,-1.4f),Quaternion.LookRotation(new Vector3(0,-.65f,1.4f)));Save(camera,"range-grapple-table");
                    loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);loop.Gameplay.CanCollectWorldWeapon=null;loop.Gameplay.MakeRoomForWorldWeapon=null;loop.Gameplay.CanClaimWorldGrip=null;loop.Gameplay.SlotCapacity=6;
                    loop.Gameplay.UpdateInputAvailability(new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true});Need(loop.Gameplay.CollectCatalogWorldWeapon(grapple,0),"take Range grapple");
                    for(int f=0;f<20;f++)yield return null;
                    Need(loop.Gameplay.OwnsWeapon(grapple)&&loop.WorldLoot.RangeGrappleActionId!=grapple&&loop.WorldLoot.RangeGrappleActionId!=null,"taken gun retained and display replenished");
                }
            }
            UnityEngine.Object.Destroy(camera.gameObject);
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"success.txt"),"Glove latch recovery, tracking return, Range pickup/replenishment assertions PASS. Actual runtime hand/traversal presentation; fixed XR-off poses, no headset. Before-control ring uses preserved exact legacy formula; before-control Range hides added gun only. near-vehicle is a presentation state fixture, no mount simulation.");
        }
        static void Save(Camera camera,string name)
        {
            var target=RenderTexture.GetTemporary(1000,800,24);var prior=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1000,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1000,800),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(SessionState.GetString(Key,""),name+".png"),pixels.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)UnityEngine.Object.Destroy(pixels);}
        }
    }
}
#endif
