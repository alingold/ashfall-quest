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
    public static class R25LegacyCapture
    {
        const string Key = "Ashfall.Legacy.Output";
        static IEnumerator work;
        static float deadline;
        static object waiting;
        static int lastFrame = -1;
        static R25LegacyCapture() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            string output = R23PlayableBuild.Argument("-ashfallLegacyOutput");
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
                var operation=waiting as AsyncOperation;if(operation!=null&&!operation.isDone)return;
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
            while(!R25LegacyPack.Ready)yield return null;
            camera.fieldOfView=65;
            var saved=loop.UI.Settings;
            loop.UI.OpenSettings();yield return new WaitForSecondsRealtime(2);Save(loop,"settings-pack");
            var field=typeof(R23VrUI).GetField("settingsPage",BindingFlags.Instance|BindingFlags.NonPublic);
            field.SetValue(loop.UI,3);Page(loop.UI,R23UiPage.Settings);yield return null;Save(loop,"legacy-off");
            var on=saved.With(retiredMaps:true,retiredSkins:true,retiredSounds:true);loop.UI.SetSettings(on);
            field.SetValue(loop.UI,3);Page(loop.UI,R23UiPage.Settings);yield return null;Save(loop,"legacy-on");
            loop.UI.OpenMaps();typeof(R23VrUI).GetField("mapPage",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(loop.UI,16);Page(loop.UI,R23UiPage.Maps);
            yield return new WaitForSecondsRealtime(2);Save(loop,"maps-retired");
            loop.UI.OpenAppearance();loop.UI.BrowseAppearance("camo");
            while(loop.Preview.Loading||loop.Preview.SelectedStyleId!="camo"){if(loop.Preview.Failure!=null)throw new Exception(loop.Preview.Failure);yield return null;}
            typeof(R23VrUI).GetMethod("ScrollOutfitRow",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(loop.UI,new object[]{9});
            yield return new WaitForSecondsRealtime(2);Save(loop,"outfits-retired");
            // Exercise actual worn-style reset, including the asynchronous asset loader.
            R25LegacyPack.Apply(on);loop.Appearance.SelectStyle("camo");
            while(loop.Appearance.Loading)yield return null;
            if(loop.Appearance.SelectedStyleId!="camo")throw new Exception("Legacy skin equip failed");
            R25LegacyPack.Apply(saved.With(retiredSkins:false));yield return null;
            while(loop.Appearance.Loading)yield return null;
            if(loop.Appearance.SelectedStyleId!=Ashfall.Integration.Playable.Appearance.R23PlayerAppearance.DefaultStyleId)throw new Exception("Legacy skin disable did not reset");
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"skin-reset.txt"),"Actual worn camo equipped; disabling retired skins returned to cinder_scout.\n");
            // Simulate an absent runtime manifest without moving installed pack files.
            var mf=typeof(R25LegacyPack).GetField("manifest",BindingFlags.Static|BindingFlags.NonPublic);var manifest=mf.GetValue(null);mf.SetValue(null,null);
            loop.UI.OpenSettings();yield return null;Save(loop,"settings-no-pack");mf.SetValue(null,manifest);
            loop.UI.SetSettings(saved);R23UiPreferences.Save(saved);
            int audioCount=0;
            var manifestText=System.IO.File.ReadAllText(System.IO.Path.Combine(Application.streamingAssetsPath,"PlayableR23/Legacy/manifest.json"));
            var audit=JsonUtility.FromJson<AudioAudit>(manifestText);
            foreach(string file in audit.audio)
            {
                using(var request=UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(R25LegacyPack.Url(file),AudioType.WAV))
                {
                    yield return request.SendWebRequest();
                    if(request.result!=UnityEngine.Networking.UnityWebRequest.Result.Success)throw new Exception(file+": "+request.error);
                    var clip=R25LegacyPack.Decoded(request);R25LegacyPack.Apply(saved.With(retiredSounds:false));
                    if(R25LegacyPack.Resolve(clip)!=clip)throw new Exception("Audio OFF "+file);
                    R25LegacyPack.Apply(saved.With(retiredSounds:true));
                    if(R25LegacyPack.Resolve(clip)==clip)throw new Exception("Audio ON "+file);
                    R25LegacyPack.Apply(saved.With(retiredSounds:false));
                    if(R25LegacyPack.Resolve(clip)!=clip)throw new Exception("Audio restore "+file);
                    UnityEngine.Object.Destroy(clip);audioCount++;
                }
            }
            R25LegacyPack.Apply(saved);
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"audio-audit.txt"),audioCount+" actual shipped cues decoded; OFF/ON/OFF selected shipped/legacy/shipped.\n");
            var map=new GameObject("Legacy map capture").AddComponent<Ashfall.Integration.Playable.World.R23PlayableMap>();
            map.Load("archive:5-levels-zyro");while(map.IsLoading)yield return null;
            if(!map.IsLoaded)throw new Exception("Retired map load: "+map.LoadError);
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"loaded.txt"),"archive:5-levels-zyro loaded; camo preview loaded; 44 maps / 112 skins / 97 audio.\n");
            UnityEngine.Object.Destroy(map.gameObject);
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"success.txt"),"Legacy Settings, maps and outfits captured; retired map loaded.\n");
        }
        [Serializable] sealed class AudioAudit { public string[] audio=null; }
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

