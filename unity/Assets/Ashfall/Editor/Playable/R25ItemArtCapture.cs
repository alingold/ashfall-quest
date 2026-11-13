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
    public static class R25ItemArtCapture
    {
        const string Key = "Ashfall.ItemArt.Output";
        static IEnumerator work;
        static float deadline;
        static object waiting;
        static int lastFrame = -1;
        static R25ItemArtCapture() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            string output = R23PlayableBuild.Argument("-ashfallItemArtOutput");
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
            loop.ManualSmoke = true; loop.ExternalProbePump = true;
            loop.Rig.enabled = false; loop.Gameplay.ExternalInputPump = true;
            var lobbyAmbient=RenderSettings.ambientLight;var lobbyMode=RenderSettings.ambientMode;var lobbyProbe=RenderSettings.ambientProbe;
            var lobbyLights=new System.Collections.Generic.Dictionary<Light,bool>();
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>()){lobbyLights.Add(light,light.enabled);light.enabled=false;light.cullingMask|=1<<30;}
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"lighting.txt"),"Lobby: production lights and ambient probe restored at the actual preview location. Hard sun: directional 1.1, rotation 35/25/0, ambient .15/.17/.20. Same camera and materials for each before/after pair.\n");
            RenderSettings.fog=false; RenderSettings.skybox=null;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.32f,.36f,.42f);
            var sun=new GameObject("Item art key").AddComponent<Light>();
            sun.type=LightType.Directional; sun.transform.rotation=Quaternion.Euler(35,25,0); sun.cullingMask=1<<30;
            var camera=new GameObject("Item art camera").AddComponent<Camera>();camera.enabled=false;
            camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.055f,.075f);
            camera.fieldOfView=36;camera.nearClipPlane=.01f;
            loop.UI.OpenAppearance();loop.Preview.RotationMultiplier=0;
            foreach(var style in loop.Appearance.AvailableStyles)
            {
                loop.UI.BrowseAppearance(style.Id);
                while(loop.Preview.Loading||loop.Preview.SelectedStyleId!=style.Id){if(loop.Preview.Failure!=null)throw new Exception(loop.Preview.Failure);yield return null;}
                for(int f=0;f<3;f++)yield return null;
                var avatar=GameObject.Find("R23 single world-fixed appearance podium").transform.Find("Full-size source character");
                var receipt=loop.Preview.InspectPresentation();
                const BindingFlags priv=BindingFlags.Instance|BindingFlags.NonPublic;
                var appearance=typeof(Ashfall.Integration.Playable.UI.R23AppearancePreview).GetField("appearance",priv).GetValue(loop.Preview);
                var asset=appearance.GetType().GetField("current",priv).GetValue(appearance);
                var body=(GameObject)asset.GetType().GetField("Root").GetValue(asset);
                var renderers=new System.Collections.Generic.List<Renderer>(body.GetComponentsInChildren<Renderer>(true));renderers.AddRange(avatar.GetComponentsInChildren<Renderer>(true));
                var originals=new System.Collections.Generic.Dictionary<Renderer,Material[]>();var controls=new System.Collections.Generic.List<Material>();
                foreach(var r in renderers){r.gameObject.layer=30;originals[r]=r.sharedMaterials;}
                foreach(string state in new[]{"before","after"}){
                    if(state=="before")foreach(var r in renderers){var mats=r.sharedMaterials;for(int j=0;j<mats.Length;j++){var m=new Material(mats[j]);m.DisableKeyword("_SPECULAR_SETUP");if(m.HasProperty("_AppearanceResponse"))m.SetVector("_AppearanceResponse",new Vector4(0,1,0,0));m.SetVector("_SpecColor",new Vector4(.2f,.2f,.2f,1));mats[j]=m;controls.Add(m);}r.sharedMaterials=mats;}
                    else foreach(var pair in originals)pair.Key.sharedMaterials=pair.Value;
                    foreach(string lighting in new[]{"lobby","sun"}){
                        foreach(var pair in lobbyLights)if(pair.Key!=null)pair.Key.enabled=lighting=="lobby"&&pair.Value;
                        sun.intensity=lighting=="sun"?1.1f:0;
                        RenderSettings.ambientMode=lighting=="lobby"?lobbyMode:UnityEngine.Rendering.AmbientMode.Flat;
                        RenderSettings.ambientLight=lighting=="lobby"?lobbyAmbient:new Color(.15f,.17f,.20f);
                        if(lighting=="lobby")RenderSettings.ambientProbe=lobbyProbe;
                        foreach(string view in new[]{"front","threequarter"}){
                            Vector3 at=avatar.TransformPoint(view=="front"?new Vector3(0,1.05f,3.5f):new Vector3(2.4f,1.25f,2.7f));
                            camera.transform.SetPositionAndRotation(at,Quaternion.LookRotation(avatar.TransformPoint(new Vector3(0,.95f,0))-at));
                            SaveCamera(camera,state+"-"+style.Id+"-"+lighting+"-"+view);
                        }
                    }
                }
                File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),style.Id+"-bindings.txt"),((Ashfall.Integration.Playable.Appearance.R23PlayerAppearance)appearance).InspectMaterialBindings());
                foreach(var m in controls)UnityEngine.Object.Destroy(m);
                // Clear isolated previous asset/gloves before the next commit.
                foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>())if(r.gameObject.layer==30)r.gameObject.layer=18;
            }
            var lastAvatar=GameObject.Find("R23 single world-fixed appearance podium").transform.Find("Full-size source character");
            loop.Preview.Hide();
            foreach(var pair in lobbyLights)if(pair.Key!=null)pair.Key.enabled=false;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            var bikeGo=new GameObject("Itemart bike 02");var bike=bikeGo.AddComponent<Ashfall.Integration.Playable.Vehicles.R25BikePresentation>();bike.Initialize();
            foreach(string lighting in new[]{"day","night"})foreach(int speed in new[]{0,24})foreach(string view in new[]{"front","side","rear","rider"}){
                sun.intensity=lighting=="day"?2.4f:.12f;RenderSettings.ambientLight=lighting=="day"?new Color(.32f,.36f,.42f):new Color(.045f,.055f,.09f);
                bike.Show(Vector3.zero,0,speed,speed==0?0:8,300,true,-1,true,"");
                foreach(var rr in bikeGo.GetComponentsInChildren<Renderer>(true))rr.gameObject.layer=30;
                Vector3 at=view=="front"?new Vector3(1.4f,1.0f,2.8f):view=="side"?new Vector3(3,1,0):view=="rear"?new Vector3(-1.8f,1,-2.3f):new Vector3(0,1.15f,-.35f);
                Vector3 target=view=="rider"?new Vector3(0,.4f,.7f):new Vector3(0,.1f,0);
                camera.fieldOfView=view=="rider"?75:48;camera.transform.SetPositionAndRotation(at,Quaternion.LookRotation(target-at));SaveCamera(camera,"bike-"+lighting+"-"+speed+"-"+view);
            }
            int triangles=0;var surfaces=new System.Collections.Generic.HashSet<Material>();
            foreach(var mf in bikeGo.GetComponentsInChildren<MeshFilter>(true))if(mf.gameObject.activeInHierarchy)triangles+=mf.sharedMesh.triangles.Length/3;
            foreach(var rr in bikeGo.GetComponentsInChildren<Renderer>(true))if(rr.gameObject.activeInHierarchy && rr.name!="Handlebar grip marker")foreach(var m in rr.sharedMaterials)surfaces.Add(m);
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"bike-budget.txt"),"Visible triangles="+triangles+" shared surface materials="+surfaces.Count);
            UnityEngine.Object.Destroy(bikeGo);yield return null;
            sun.intensity=2.2f;RenderSettings.ambientLight=new Color(.4f,.43f,.47f);camera.fieldOfView=38;
            var artRoot=new GameObject("Grenade comparison fixture").transform;
            var explosives=UnityEngine.Object.FindAnyObjectByType<Ashfall.Integration.Playable.Explosives.R23VRExplosives>();
            int index=0;foreach(string kind in new[]{"frag","flash","smoke"}){
                var template=(Transform)explosives.GetType().GetMethod("Template",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(explosives,new object[]{kind});
                var model=UnityEngine.Object.Instantiate(template,artRoot);model.gameObject.SetActive(true);model.localPosition=new Vector3((index-1)*.24f,.13f,0);model.localScale=Vector3.one*.9f;
                float bottom=float.MaxValue;foreach(var rr in model.GetComponentsInChildren<Renderer>(true))bottom=Mathf.Min(bottom,rr.bounds.min.y);
                model.position-=Vector3.up*bottom;
                index++;
            }
            var stage=GameObject.CreatePrimitive(PrimitiveType.Cube);stage.name="Capture ground";stage.transform.position=new Vector3(0,-.015f,0);stage.transform.localScale=new Vector3(1.5f,.02f,.7f);stage.layer=30;stage.GetComponent<Renderer>().sharedMaterial=Ashfall.Integration.Playable.Explosives.R25ItemArtGeometry.Surface(new Color(.09f,.11f,.14f),0,.1f);
            foreach(var rr in artRoot.GetComponentsInChildren<Renderer>(true))rr.gameObject.layer=30;
            camera.transform.SetPositionAndRotation(new Vector3(.24f,.36f,1.1f),Quaternion.LookRotation(new Vector3(0,.12f,0)-new Vector3(.24f,.36f,1.1f)));SaveCamera(camera,"grenades-ground");
            // Reuse the actual installed first-person glove geometry/materials as grip context.
            var gloveSource=lastAvatar.Find("Source gallery left glove");
            for(int i=0;i<3;i++){
                var grip=UnityEngine.Object.Instantiate(gloveSource,artRoot);grip.gameObject.SetActive(true);grip.localPosition=new Vector3((i-1)*.24f-.035f,.09f,-.035f);grip.localRotation=Quaternion.Euler(0,0,-75);
                foreach(var rr in grip.GetComponentsInChildren<Renderer>(true))rr.gameObject.layer=30;
            }
            stage.SetActive(false);artRoot.position+=Vector3.up*.12f;SaveCamera(camera,"grenades-in-hand");
            foreach(string kind in new[]{"flash","smoke"}){
                var type=typeof(Ashfall.Integration.Playable.Explosives.R23VRExplosives).Assembly.GetType("Ashfall.Integration.Playable.FlashSmoke.R25ThrowableIcons");
                var bytes=(byte[])type.GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{kind});File.WriteAllBytes(Path.Combine(SessionState.GetString(Key,""),"icon-"+kind+".png"),bytes);
            }
            var budget=new System.Text.StringBuilder();
            foreach(var mf in artRoot.GetComponentsInChildren<MeshFilter>(true))budget.AppendLine(mf.name+" triangles="+mf.sharedMesh.triangles.Length/3);
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"grenade-mesh-budget.txt"),budget.ToString());
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""), "success.txt"), "192 matched outfit images, 16 bike views, 2 grenade comparisons and 2 production icon images. Baseline uses original unbound material response on identical geometry.\n");
        }
        static void SaveCamera(Camera camera, string name)
        {
            var target=RenderTexture.GetTemporary(800,900,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(800,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,800,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(SessionState.GetString(Key,""),name+".png"),pixels.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(pixels);}
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
