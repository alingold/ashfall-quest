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
    public static class R25UiFixCapture
    {
        const string Key = "Ashfall.UiFix.Output";
        static IEnumerator work;
        static float deadline;
        static object waiting;
        static int lastFrame = -1;
        static R25UiFixCapture() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            string output = R23PlayableBuild.Argument("-ashfallUiFixOutput");
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
            { deadline = Time.realtimeSinceStartup + 180; work = CapturePages(); EditorApplication.update += Pump; }
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
            loop.Rig.Head.fieldOfView = 90;
            loop.ManualSmoke = true; loop.ExternalProbePump = true;
            loop.Rig.enabled = false; loop.Gameplay.ExternalInputPump = true;
            loop.Calibration.SuppressPersistence = true;
            // Loading completion varies; reset only the presentation clock so the two
            // requested title samples have known elapsed times in every capture run.
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var clock = typeof(R23VrUI).GetField("titleClock", fields).GetValue(loop.UI);
            clock.GetType().GetField("<Elapsed>k__BackingField", fields).SetValue(clock, 0f);
            clock.GetType().GetField("settleAt", fields).SetValue(clock, -1f);
            typeof(R23VrUI).GetField("titleHeadStart", fields).SetValue(loop.UI, loop.Rig.Head.transform.position);
            yield return new WaitForSecondsRealtime(1);
            Save(loop, "start-1s");
            yield return new WaitForSecondsRealtime(5);
            Save(loop, "start-settled");
            foreach (R23UiPage page in new[] { R23UiPage.Appearance, R23UiPage.Weapons, R23UiPage.Loadout, R23UiPage.Maps, R23UiPage.MatchRules, R23UiPage.Credits, R23UiPage.Pause, R23UiPage.Dead, R23UiPage.Settings })
            {
                Page(loop.UI, page);
                yield return new WaitForSecondsRealtime(2);
                if (page == R23UiPage.Appearance)
                    while (loop.Preview.Loading) yield return null;
                Save(loop, page.ToString());
            }
            Page(loop.UI, R23UiPage.MatchRules);
            for (int tab = 1; tab < 7; tab++)
            {
                typeof(R23VrUI).GetField("matchTab", fields).SetValue(loop.UI, tab);
                typeof(R23VrUI).GetMethod("Build", fields).Invoke(loop.UI, null);
                for (int frame = 0; frame < 5; frame++) yield return null;
                Save(loop, "MatchRules-" + tab);
            }
            typeof(R23VrUI).GetField("matchTab", fields).SetValue(loop.UI, 0);
            // Hosting tab uses production roster presentation, without opening a socket/peer.
            loop.UI.SetLanLobby(Ashfall.Integration.Playable.Networking.R24LanLobbyState.Hosting, "Capture fixture", "UI CHECK", new[] { "HOST", "GUEST" }, "192.168.1.10");
            Page(loop.UI, R23UiPage.MatchRules);
            yield return new WaitForSecondsRealtime(1); Save(loop, "LAN-lobby");
            loop.UI.SetPlaying();
            foreach (int value in new[] { 100, 50, 7 })
            {
                loop.UI.UpdateHud(new R23HudSnapshot(value,100,0,0,false,false,"",0,value,100,6,6,10));
                for (int i = 0; i < 5; i++) yield return null;
                Save(loop, "HUD-" + value);
            }
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""), "success.txt"), "21 production head-camera captures; no headset, player build or network peer.\n");
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
