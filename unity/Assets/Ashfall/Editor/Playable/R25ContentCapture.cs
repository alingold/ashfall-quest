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
    public static class R25ContentCapture
    {
        const string Key = "Ashfall.Content.Output";
        static IEnumerator work;
        static float deadline;
        static object waiting;
        static int lastFrame = -1;
        static R25ContentCapture() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            string output = R23PlayableBuild.Argument("-ashfallContentOutput");
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
            { deadline = Time.realtimeSinceStartup + 600; work = CapturePages(); EditorApplication.update += Pump; }
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
                if (Time.realtimeSinceStartup > deadline) throw new Exception("600 second capture deadline");
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
        static void Page(R23VrUI ui, R23UiPage page)
        { typeof(R23VrUI).GetMethod("SetPage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, new object[] { page }); }
        static IEnumerator CapturePages()
        {
            R23PlayableLoop loop = null;
            while (loop == null || !loop.Ready)
            {
                loop = UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();
                if (loop != null && loop.Failure != null) throw new Exception(loop.Failure);
                yield return null;
            }
            loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;
            loop.Calibration.SuppressPersistence=true;
            var camera=loop.Rig.Head;var at=camera.transform.position;var rotation=camera.transform.rotation;
            camera.fieldOfView=90;loop.UI.OpenAppearance();loop.Preview.RotationMultiplier=0;
            foreach(string id in new[]{"pban","obsidian","claude"})
            {
                camera.fieldOfView=90;camera.transform.SetPositionAndRotation(at,rotation);
                loop.UI.BrowseAppearance(id);
                typeof(R23VrUI).GetMethod("ScrollOutfitRow",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(loop.UI,new object[]{7});
                while(loop.Preview.Loading||loop.Preview.SelectedStyleId!=id)
                {if(loop.Preview.Failure!=null)throw new Exception(loop.Preview.Failure);yield return null;}
                yield return new WaitForSecondsRealtime(3);
                Save(loop,id+"-cards");
                var avatar=GameObject.Find("R23 single world-fixed appearance podium").transform.Find("Full-size source character");
                avatar.rotation=Quaternion.Euler(0,180,0);
                var panel=(Transform)typeof(R23VrUI).GetField("panel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loop.UI);
                panel.gameObject.SetActive(false);
                foreach(string view in new[]{"front","threequarter","hands"})
                {
                    var target=avatar.TransformPoint(view=="hands"?new Vector3(0,1.05f,0):new Vector3(0,.95f,0));
                    var pos=avatar.TransformPoint(view=="front"?new Vector3(0,1.05f,3.5f):view=="hands"?new Vector3(1.2f,1.1f,1.8f):new Vector3(2.4f,1.25f,2.7f));
                    camera.fieldOfView=view=="hands"?32:36;
                    camera.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(target-pos));
                    yield return null;Save(loop,id+"-"+view);
                }
                panel.gameObject.SetActive(true);
                File.AppendAllText(Path.Combine(SessionState.GetString(Key,""),"loaded.txt"),id+" preview loaded, gloves ready; thumbnail="+Resources.Load<Texture2D>("R24OutfitThumbnails/"+(id=="pban"?"024":id=="obsidian"?"025":"026"))+"\n");
            }
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"success.txt"),"Three restored skins on night pedestal and production outfit cards.\n");
        }
        static void Save(R23PlayableLoop loop, string name)
        {
            loop.UI.RefreshTextLayout();
            Camera camera = loop.Rig.Head;
            var target = RenderTexture.GetTemporary(1600,1000,24);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(1600,1000,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(SessionState.GetString(Key,""),name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); if(pixels!=null)UnityEngine.Object.Destroy(pixels);
            }
        }
    }
}
#endif

