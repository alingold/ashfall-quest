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
    public static class R25GrappleFixCapture
    {
        const string Key = "Ashfall.GrappleFix.Output";
        static int stuck;
        static IEnumerator work;
        static float deadline;
        static object waiting;
        static int lastFrame = -1;
        static R25GrappleFixCapture() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            string output = R23PlayableBuild.Argument("-ashfallGrappleFixOutput");
            if (String.IsNullOrEmpty(output) || Directory.Exists(output)) { EditorApplication.Exit(2); return; }
            Directory.CreateDirectory(output);
            Application.logMessageReceived+=CountRecovery;stuck=0;
            R23PlayableBuild.Prepare();
            R23PlayableBuild.Xr().InitManagerOnStart = false;
            SessionState.SetString(Key, output);
            EditorApplication.isPlaying = true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if (SessionState.GetString(Key, "").Length == 0) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            { deadline = Time.realtimeSinceStartup + 180; work = CaptureGrapple(); EditorApplication.update += Pump; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Pump; work = null;Application.logMessageReceived-=CountRecovery;
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
        static void CountRecovery(string message,string stack,LogType type){if(message.Contains("head recovery reason=Stuck"))stuck++;}
        static object Read(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
        static IEnumerator CaptureGrapple()
        {
            R23PlayableLoop loop=null;
            while(loop==null||!loop.Ready){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&loop.Failure!=null)throw new Exception(loop.Failure);yield return null;}
            loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
            var bot=loop.GetComponent<R25NetBot>();if(bot!=null)bot.enabled=false;
            loop.SelectMap("range",true);while(!loop.Ready){if(loop.Failure!=null)throw new Exception(loop.Failure);yield return null;}loop.StartPlaying();
            var rig=loop.Rig;rig.enabled=false;
            var folder=SessionState.GetString(Key,"");
            using(var csv=new StreamWriter(Path.Combine(folder,"frames.csv"))){
            csv.WriteLine("scenario,frame,headY,rootY,feetY,requestedX,requestedY,requestedZ,actualX,actualY,actualZ,attached");
            // Real Range geometry and actual equipped trigger path. Reset each trial.
            foreach(string trial in new[]{"floor-near","floor-far","wall-low","above","swing-release","floor-standing"}){
                rig.SetSpawn(loop.Map.Spawn,loop.Map.Spawn+Vector3.forward);rig.TrackingOrigin.localPosition=Vector3.zero;
                rig.Head.transform.localPosition=new Vector3(0,1.65f,0);rig.GameplayBlocked=false;rig.MenuLocomotion=false;rig.TraversalInputAllowed=true;rig.GrappleHands=loop.Gameplay;
                var game=loop.Gameplay;game.CanCollectWorldWeapon=null;game.MakeRoomForWorldWeapon=null;game.CanClaimWorldGrip=null;game.SlotCapacity=6;game.PreferredPickupHand=1;game.NotifyFocus(true);game.SetGameplayPaused(false);game.SetWeaponPresentationSuppressed(false);game.HolsterCurrent();
                game.UpdateInputAvailability(new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true});
                string id;Need(game.SpawnWorldWeapon("grapple",2,rig.Head.transform.position,Quaternion.identity,out id,true),"spawn grapple");Need(game.CollectCatalogWorldWeapon(id,1),"equip grapple");
                Vector3 start=rig.transform.position;
                // Controlled low wall and overhead targets supplement the unchanged Range floor.
                GameObject target=null;
                Vector3 anchor=start+new Vector3(0,-start.y,trial=="floor-near"?3:12);
                if(trial=="wall-low"||trial=="above"||trial=="swing-release"){
                    target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.layer=8;target.name="grapple fixture "+trial;
                    target.transform.position=start+new Vector3(0,trial=="wall-low"?.3f:7,8);target.transform.localScale=trial=="wall-low"?new Vector3(4,.5f,.15f):new Vector3(3,.3f,3);anchor=target.transform.position;
                }
                Physics.SyncTransforms();
                int attachedFrames=0;
                for(int f=0;f<420;f++){
                    Vector3 hand=rig.Head.transform.position+new Vector3(.25f,-.3f,.3f);Quaternion aim=Quaternion.LookRotation(anchor-hand);
                    float trigger=f>=90&&(trial!="swing-release"||f<190)?1:0;
                    game.NotifyFocus(true);game.SetGameplayPaused(false);game.SetWeaponPresentationSuppressed(false);
                    game.TickInput(.016666667f,new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true});
                    rig.Right.SetPositionAndRotation(hand,aim);
                    for(int align=0;align<3;align++){Vector3 muzzle;Quaternion muzzleRotation;if(game.TryReadGrappleMuzzle(1,out muzzle,out muzzleRotation)){rig.Right.rotation=Quaternion.LookRotation(anchor-muzzle)*Quaternion.Inverse(muzzleRotation)*rig.Right.rotation;}}
                    aim=rig.Right.rotation;
                    Vector3 before=rig.transform.position;
                    rig.NetBotSample(trial=="swing-release"&&f>=30&&f<90?Vector2.right:Vector2.zero,1.65f,rig.Head.transform.position+new Vector3(-.25f,-.3f,.3f),hand,aim,0,trigger,1f/60);
                    rig.GetType().GetMethod("TickHeadRecovery",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rig,null);
                    Need(rig.transform.position.y>=-.0001f,"feet below floor "+trial+" frame "+f);
                    Need(rig.HeadCollision.HeadClear(rig.Head.transform.position),"head endpoint "+trial+" frame "+f);
                    if(rig.WebState.RightAttached)attachedFrames++;
                    if(trial=="swing-release"&&f>190)Need(!rig.WebState.RightAttached,"release detaches");
                    game.RefreshFeedbackPresentation();
                    if(f==90){Vector3 muzzle;Quaternion rot;Debug.Log("GRAPPLE fixture "+trial+" muzzle="+game.TryReadGrappleMuzzle(1,out muzzle,out rot)+" menu="+rig.MenuLocomotion+" climb="+(rig.Climbing!=null)+" allowed="+rig.TraversalInputAllowed+" aim="+Read(rig,"webAim")+" primary="+Read(game,"primaryHand")+" paused="+Read(game,"gameplayPaused")+" focus="+Read(game,"hadFocus")+" holstered="+Read(game,"holstered"));}
                    var req=(Vector3)Read(rig,"grappleProbeRequested");
                    csv.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10}",trial,f,rig.Head.transform.position.y,rig.transform.position.y,req.x,req.y,req.z,(rig.transform.position-before).x,(rig.transform.position-before).y,(rig.transform.position-before).z,rig.WebState.RightAttached));
                    if(f==0||f==210||f==419||f==60)Save(rig.Head,trial+"-"+f);
                    if(f%30==0)yield return null;
                }
                Need(attachedFrames>40,"fixture must attach "+trial);
                game.DropCurrent(rig.Head.transform.position,Quaternion.identity);game.DespawnWorldWeapon(id);if(target!=null)UnityEngine.Object.Destroy(target);
            }}
            // Native victim-owner regression: same accepted event/motor, floor destination.
            rig.SetSpawn(loop.Map.Spawn,loop.Map.Spawn+Vector3.forward);rig.TrackingOrigin.localPosition=Vector3.zero;
            rig.GameplayBlocked=false;rig.MenuLocomotion=false;
            rig.NetBotSample(Vector2.zero,1.65f,rig.Left.position,rig.Right.position,Quaternion.identity,0,0,1f/60);
            rig.GetType().GetMethod("TickHeadRecovery",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rig,null);
            var pullStart=rig.transform.position;
            rig.AdoptGrapplePull(1,new Ashfall.Core.Float3(pullStart.x,pullStart.y+.2f,pullStart.z+10),0);
            Need(rig.BeingGrappled,"native victim owner adopted");
            for(int f=0;f<120;f++){
                rig.NetBotSample(Vector2.zero,1.65f,rig.Left.position,rig.Right.position,Quaternion.identity,0,0,1f/60);
                rig.GetType().GetMethod("TickHeadRecovery",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rig,null);
                Need(rig.transform.position.y>=-.0001f,"victim feet below floor");
                Need(rig.HeadCollision.HeadClear(rig.Head.transform.position),"victim head clear");
                if(f==0||f==60||f==119)Save(rig.Head,"player-pull-"+f);
                // Keep this isolated owner trial in one frame: the offline LAN
                // presenter correctly clears pulls without a replicated event.
            }
            Need(Vector3.Distance(pullStart,rig.transform.position)>.5f,"victim moved through production owner");
            Need(!rig.BeingGrappled,"victim exits cleanly");
            Need(stuck==0,"no Stuck recoveries");
            File.WriteAllText(Path.Combine(folder,"success.txt"),"PASS six native Range trigger/reel/release trials, 2520 frames. Every trial attached; feet never below floor; head endpoints clear; release detached; zero Stuck recoveries with TickHeadRecovery exercised each sample. Additional 120-frame native victim pull adopted, moved, stayed above floor and exited. Inspect frames.csv and eye PNGs.");
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
