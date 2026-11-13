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
    // Editor-only production AWP/audio evidence. No player build or network peer.
    [InitializeOnLoad]
    public static class R25BuildSoundAwpCapture
    {
        const string Key = "Ashfall.BuildSoundAwp.Output";
        static IEnumerator work;
        static float deadline;
        static object waiting;
        static int lastFrame = -1;
        static R25BuildSoundAwpCapture() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            string output = R23PlayableBuild.Argument("-ashfallBuildSoundAwpOutput");
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
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static object Read(object o,string n){return o.GetType().GetField(n,Flags).GetValue(o);}
        static void Call(object o,string n,params object[] a){o.GetType().GetMethod(n,Flags).Invoke(o,a);}
        static IEnumerator CaptureHands()
        {
            R23PlayableLoop loop=null;
            while(loop==null||!loop.Ready){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&loop.Failure!=null)throw new Exception(loop.Failure);yield return null;}
            loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;
            loop.SelectMap("range",true);while(!loop.Ready)yield return null;loop.StartPlaying();
            var g=loop.Gameplay;g.CanCollectWorldWeapon=null;g.MakeRoomForWorldWeapon=null;g.CanClaimWorldGrip=null;g.SlotCapacity=6;
            g.NotifyFocus(true);g.SetGameplayPaused(false);g.SetWeaponPresentationSuppressed(false);
            g.UpdateInputAvailability(new Ashfall.Integration.Playable.HandsWeapons.R23HandsInputFrame{LeftTracked=true,RightTracked=true});
            string id;Need(g.SpawnWorldWeapon("awp",2,loop.Rig.Right.position,Quaternion.identity,out id,true),"spawn AWP");Need(g.CollectCatalogWorldWeapon(id,1),"equip AWP");
            while(!g.AwpReady)yield return null;
            var view=(Ashfall.Integration.Playable.HandsWeapons.R24AwpView)Read(g,"awpView");var scope=view.Scope;
            // Isolated native wall fixture avoids interference from map geometry.
            loop.Rig.transform.position+=new Vector3(10000,100,10000);
            loop.Rig.Right.rotation=Quaternion.Inverse(Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.LocalRotation);
            var camera=loop.Rig.Head;
            var notes=new System.Text.StringBuilder();
            foreach(float distance in new[]{10f,50f})
            {
                g.GetType().GetField("haveTrackedControls",Flags).SetValue(g,false);
                g.GetType().GetMethod("TickAwp",Flags).Invoke(g,new object[]{.016f,false,false});
                Call(g,"RenderAwpPose");Vector3 origin=view.Muzzle.position,forward=view.Muzzle.forward;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Native AWP wall "+distance;wall.transform.SetPositionAndRotation(origin+forward*distance,Quaternion.LookRotation(forward));wall.transform.localScale=new Vector3(12,12,.1f);
                var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",new Color(.16f,.24f,.31f));wall.GetComponent<Renderer>().sharedMaterial=mat;
                camera.transform.SetPositionAndRotation(origin-forward*.6f+view.Muzzle.up*.08f,Quaternion.LookRotation(forward,view.Muzzle.up));Physics.SyncTransforms();
                scope.SetInput(new Ashfall.Integration.Playable.HandsWeapons.R24AwpScopeInput{Valid=true,Supported=true,SupportHeld=true,PrimaryReady=true,SupportReady=true});Call(scope,"LateUpdate");
                // Actual production shot resolver, raycast, impact and tracer spawn.
                bool shot=(bool)g.GetType().GetMethod("TickAwp",Flags).Invoke(g,new object[]{.016f,true,false});
                Need(shot,"production TickAwp must accept shot; allowed="+g.GetType().GetProperty("FeedbackAllowed",Flags).GetValue(g,null));
                notes.AppendLine("distance="+distance+" actual TickAwp accepted");
                var line=(LineRenderer)Read(g,"tracer");var optical=(Camera)Read(scope,"view");var overlay=(MeshRenderer)Read(scope,"renderer");
                Vector3 end=line.GetPosition(1);Vector3 worldMuzzle=view.Muzzle.position;
                var begin=(Action<UnityEngine.Rendering.ScriptableRenderContext,Camera>)Delegate.CreateDelegate(typeof(Action<UnityEngine.Rendering.ScriptableRenderContext,Camera>),scope,scope.GetType().GetMethod("BeginCamera",Flags));
                var finish=(Action<UnityEngine.Rendering.ScriptableRenderContext,Camera>)Delegate.CreateDelegate(typeof(Action<UnityEngine.Rendering.ScriptableRenderContext,Camera>),scope,scope.GetType().GetMethod("EndCamera",Flags));Need(Vector3.Distance(end,origin+forward*(distance-.05f))<.08f,"tracer uses wall hit");
                for(int frame=0;frame<3;frame++)
                {
                    // Matched sub-frame pose samples inside the 45 ms effect lifetime.
                    line.enabled=true;line.SetPosition(0,worldMuzzle+new Vector3(.018f*frame,.008f*frame,0));
                    UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering-=begin;UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=finish;
                    optical.Render();SaveTexture(optical.targetTexture,"scope-before-control-"+distance+"m-"+frame);
                    UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering+=begin;UnityEngine.Rendering.RenderPipelineManager.endCameraRendering+=finish;
                    optical.Render();SaveTexture(optical.targetTexture,"scope-"+distance+"m-"+frame);
                    camera.transform.SetPositionAndRotation(origin-forward*.7f+Vector3.up*.25f,Quaternion.LookRotation(end-(origin-forward*.7f+Vector3.up*.25f)));
                    bool shown=overlay.enabled;overlay.enabled=false;Save(camera,"head-outside-"+distance+"m-"+frame);overlay.enabled=shown;
                    Need(line.GetPosition(1)==end,"accepted impact remains fixed");
                    Need(Vector3.Distance(line.GetPosition(0),worldMuzzle+new Vector3(.018f*frame,.008f*frame,0))<.0001f,"outside world origin restored after scope render");
                    notes.AppendLine("sample="+frame+" start="+line.GetPosition(0)+" impact="+end+" scopePose="+optical.transform.position);
                }
                scope.Shutdown();wall.SetActive(false);UnityEngine.Object.Destroy(wall);UnityEngine.Object.Destroy(mat);
                // Reset weapon owner for the next independently accepted shot.
                g.DropCurrent(origin,Quaternion.identity);g.DespawnWorldWeapon(id);
                if(distance==10){Need(g.SpawnWorldWeapon("awp",2,origin,Quaternion.identity,out id,true),"second AWP");Need(g.CollectCatalogWorldWeapon(id,1),"second equip");}
            }
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"optics-evidence.txt"),notes.ToString());
            // Place through the actual construction input/release path.
            loop.Rig.SetSpawn(new Vector3(100,.04f,-600),new Vector3(100,.04f,-599));
            loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.35f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.35f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent((Transform)Read(loop.Activities,"map"),false);floor.transform.position=new Vector3(100,-.1f,-600);floor.transform.localScale=new Vector3(40,.2f,40);Physics.SyncTransforms();
            var a=loop.Activities;loop.Gameplay.HolsterCurrent();
            var audio=(Ashfall.Integration.Playable.Activities.R23ConstructionAudio)Read(a,"audio");
            for(int i=0;i<120;i++){if(audio.LoadFailure!=null)throw new Exception(audio.LoadFailure);yield return null;}
            loop.Rig.Right.rotation=Quaternion.Inverse(Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.LocalRotation);
            a.TickConstruction(new Ashfall.Core.InputFrame{Tick=900,Tracked=true},true,true,false,false);
            a.TickConstruction(new Ashfall.Core.InputFrame{Tick=901,Tracked=true,TriggerRight=true},true,true,false,false);
            a.TickConstruction(new Ashfall.Core.InputFrame{Tick=902,Tracked=true},true,true,false,false);
            Need(a.PanelCount>0,"actual release placed panel: "+a.BuildStatus);
            int playing=0;foreach(var voice in audio.GetComponentsInChildren<AudioSource>())if(voice.isPlaying&&voice.clip!=null){var pcm=new float[voice.clip.samples*voice.clip.channels];Need(voice.clip.GetData(pcm,0),"PCM readable");float peak=0;foreach(float v in pcm)peak=Mathf.Max(peak,Mathf.Abs(v));Need(peak>0,"nonzero PCM");notes.AppendLine("placement clip="+voice.clip.name+" playing="+voice.isPlaying+" peak="+peak+" spatialBlend="+voice.spatialBlend);playing++;}
            Need(playing>0,"placement voice started");
            var placed=a.PanelSnapshot[0];audio.Stop();
            // Same revision projection used by LAN, without starting a network peer.
            var remote=new Ashfall.Modules.Construction.PlacedPanel("remote-audio-proof","remote",placed.Shape,placed.Health);
            a.SharedPanelsProvider=delegate{return new[]{placed,remote};};
            Call(a,"ReconcileNetworkConstruction");
            int remotePlaying=0;foreach(var v in audio.GetComponentsInChildren<AudioSource>())if(v.isPlaying&&v.clip!=null&&v.clip.name.StartsWith("build_place")){Need(v.spatialBlend==1,"remote spatial cue");remotePlaying++;notes.AppendLine("remote placement clip="+v.clip.name+" playing="+v.isPlaying+" position="+v.transform.position);}
            Need(remotePlaying==1,"one remote placement cue");
            audio.Stop();Call(a,"ReconcileNetworkConstruction");foreach(var v in audio.GetComponentsInChildren<AudioSource>())Need(!v.isPlaying,"same revision does not replay");
            a.SharedPanelsProvider=null;
            while(!R25LegacyPack.Ready)yield return null;
            R25LegacyPack.Apply(loop.UI.Settings.With(retiredSounds:true));Need(R25LegacyPack.SoundsEnabled,"bounded retired pack available");
            audio.BuildCue("place",new Vector3(100,1,-598));int legacyPlaying=0;
            foreach(var v in audio.GetComponentsInChildren<AudioSource>())if(v.isPlaying&&v.clip!=null){var pcm=new float[v.clip.samples*v.clip.channels];Need(v.clip.GetData(pcm,0),"retired PCM readable");float peak=0;foreach(float value in pcm)peak=Mathf.Max(peak,Mathf.Abs(value));Need(peak>0,"retired nonzero PCM");legacyPlaying++;notes.AppendLine("retired placement clip="+v.clip.name+" playing="+v.isPlaying+" peak="+peak);}
            Need(legacyPlaying==1,"one retired placement voice");R25LegacyPack.Apply(loop.UI.Settings);
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"evidence.txt"),notes.ToString());
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"success.txt"),"Native production AWP trace/impact and placement input capture completed. XR off; sub-frame samples, no headset.");
        }
        static void SaveTexture(RenderTexture target,string name)
        {
            var active=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(SessionState.GetString(Key,""),name+".png"),pixels.EncodeToPNG());RenderTexture.active=active;UnityEngine.Object.Destroy(pixels);
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
