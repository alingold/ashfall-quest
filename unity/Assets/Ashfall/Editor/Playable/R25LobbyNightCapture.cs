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
    public static class R25LobbyNightCapture
    {
        const string Key = "Ashfall.LobbyNight.Output";
        static IEnumerator work;
        static float deadline;
        static object waiting;
        static int lastFrame = -1;
        static R25LobbyNightCapture() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            string output = R23PlayableBuild.Argument("-ashfallLobbyNightOutput");
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
            var lobby=UnityEngine.Object.FindAnyObjectByType<Ashfall.Integration.Playable.Lobby.R23Lobby>();
            var camera=loop.Rig.Head;var at=camera.transform.position;var rotation=camera.transform.rotation;
            camera.fieldOfView=90;
            foreach(bool night in new[]{false,true})
            {
                Ashfall.Integration.Playable.Expansion.R25Features.LobbyNight=night;lobby.SetNightPresentation(night);
                string prefix=night?"night-":"day-";
                camera.transform.SetPositionAndRotation(at,rotation);
                loop.Preview.Hide();
                Page(loop.UI,R23UiPage.Ready);
                yield return new WaitForSecondsRealtime(6);
                Save(loop,prefix+"start-wordmark");
                loop.UI.SetPlaying();
                foreach(string view in new[]{"forward","left","right","up","behind"})
                {
                    var q=view=="left"?Quaternion.Euler(0,-75,0):view=="right"?Quaternion.Euler(0,75,0):view=="up"?Quaternion.Euler(-48,0,0):view=="behind"?Quaternion.Euler(0,180,0):Quaternion.identity;
                    camera.transform.SetPositionAndRotation(at,rotation*q);yield return null;Save(loop,prefix+view);
                }
                camera.transform.SetPositionAndRotation(at,rotation);
                Page(loop.UI,R23UiPage.Weapons);yield return new WaitForSecondsRealtime(3);Save(loop,prefix+"gallery");
                camera.fieldOfView=50;camera.transform.SetPositionAndRotation(at,rotation*Quaternion.Euler(16,0,0));Save(loop,prefix+"gallery-close");camera.fieldOfView=90;camera.transform.SetPositionAndRotation(at,rotation);
                loop.UI.OpenAppearance();loop.Preview.RotationMultiplier=0;
                foreach(var style in loop.Appearance.AvailableStyles)
                {
                    camera.transform.SetPositionAndRotation(at,rotation);
                    loop.UI.BrowseAppearance(style.Id);
                    while(loop.Preview.Loading||loop.Preview.SelectedStyleId!=style.Id){if(loop.Preview.Failure!=null)throw new Exception(loop.Preview.Failure);yield return null;}
                    for(int f=0;f<3;f++)yield return null;
                    var avatar=GameObject.Find("R23 single world-fixed appearance podium").transform.Find("Full-size source character");
                    avatar.rotation=Quaternion.Euler(0,180,0);
                    var panel=(Transform)typeof(R23VrUI).GetField("panel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loop.UI);
                    panel.gameObject.SetActive(false);
                    foreach(string view in new[]{"front","threequarter"})
                    {
                        Vector3 pos=avatar.TransformPoint(view=="front"?new Vector3(0,1.05f,3.5f):new Vector3(2.4f,1.25f,2.7f));
                        camera.fieldOfView=36;camera.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(avatar.TransformPoint(new Vector3(0,.95f,0))-pos));
                        Save(loop,prefix+style.Id+"-"+view);
                    }
                    if(night && style.Id=="cinder_scout")
                    {
                        // One-factor controls on the same pose/materials/camera.
                        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>())
                        {if(light.name=="Source sun"){light.intensity=1.4f;light.color=Color.white;}if(light.name=="Source blue fill"){light.intensity=4;light.color=new Color(.714f,.906f,1);}}
                        Save(loop,"control-original-direct");lobby.SetNightPresentation(true);
                        RenderSettings.ambientLight=new Color(.733f,.851f,.929f)*.55f;
                        yield return null;Save(loop,"control-original-ambient");lobby.SetNightPresentation(true);
                        RenderSettings.reflectionIntensity=1;Save(loop,"control-reflection-one");lobby.SetNightPresentation(true);
                        Save(loop,"control-night-baseline");
                        var lighting=new System.Text.StringBuilder();
                        lighting.AppendLine("ambient="+RenderSettings.ambientLight+" reflectionIntensity="+RenderSettings.reflectionIntensity+" ambientMode="+RenderSettings.ambientMode+" defaultReflectionMode="+RenderSettings.defaultReflectionMode);
                        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>())if(light.isActiveAndEnabled)lighting.AppendLine(light.name+" intensity="+light.intensity+" color="+light.color+" range="+light.range+" mask="+light.cullingMask);
                        File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"actual-lights.txt"),lighting.ToString());
                    }
                    panel.gameObject.SetActive(true);
                }
                camera.fieldOfView=90;camera.transform.SetPositionAndRotation(at,rotation);
                Save(loop,prefix+"pedestal-menu");
            }
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"lighting.txt"),"Day control: original directional 1.4, two blue point 4, flat ambient #bbd9ed*.55. Night: neutral directional .62, blue fill .35, local key .85/rim .55, flat ambient .075/.082/.10, reflectionIntensity .12. Existing appearance materials unchanged. All cameras use production lighting and actual pedestal location.\n");
            int triangles=0;var materials=new System.Collections.Generic.HashSet<Material>();
            var city=UnityEngine.Object.FindAnyObjectByType<Ashfall.Integration.Playable.Lobby.R25NightCity>();
            foreach(var mf in city.GetComponentsInChildren<MeshFilter>(true))if(mf.name!="" && mf.sharedMesh!=null && mf.sharedMesh.isReadable)triangles+=mf.sharedMesh.triangles.Length/3;
            foreach(var r in city.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)materials.Add(m);
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"budget.txt"),"Additional readable triangles="+triangles+" materials="+materials.Count);
            // Exercise live day switch and map-return environment restoration.
            Ashfall.Integration.Playable.Expansion.R25Features.LobbyNight=false;lobby.SetNightPresentation(false);
            foreach(var light in city.GetComponentsInChildren<Light>(true))
            {
                if(light.name=="Source sun" && Mathf.Abs(light.intensity-1.4f)>.001f)throw new Exception("day sun not restored");
                if(light.name=="Neutral outfit key" && light.gameObject.activeInHierarchy)throw new Exception("night stage still active");
            }
            lobby.Hide();var restoredSky=RenderSettings.skybox;var restoredAmbient=RenderSettings.ambientLight;float restoredReflection=RenderSettings.reflectionIntensity;
            Ashfall.Integration.Playable.Expansion.R25Features.LobbyNight=true;lobby.SetNightPresentation(true);lobby.Show();lobby.Hide();
            if(RenderSettings.skybox!=restoredSky || RenderSettings.ambientLight!=restoredAmbient || Mathf.Abs(RenderSettings.reflectionIntensity-restoredReflection)>.0001f)throw new Exception("map environment restoration failed");
            lobby.Show();
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"restoration.txt"),"Live day switch: original sun and night-stage inactivity PASS. Hide/show/hide: map sky, ambient and reflection restored PASS.");
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"success.txt"),"Day/night panoramic views, start wordmark, gallery, pedestal menu and all 24 outfits front/three-quarter.\n");
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

