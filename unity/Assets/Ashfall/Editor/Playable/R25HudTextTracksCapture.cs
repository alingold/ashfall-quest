#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEditor;
using Ashfall.Integration.Playable.UI;
using Ashfall.Integration.Playable.Vehicles;
using Ashfall.Integration.Playable.WorldScores;
using Ashfall.Integration.Playable.Activities;
namespace Ashfall.Editor.Playable
{
    [InitializeOnLoad]
    public static class R25HudTextTracksCapture
    {
        const string Key="Ashfall.HudTextTracks.Capture";
        static IEnumerator work;
        static string output;
        static R25HudTextTracksCapture(){EditorApplication.playModeStateChanged+=Changed;}
        public static void Run()
        {
            output=R23PlayableBuild.Argument("-hudTextTracksOutput");
            if(String.IsNullOrEmpty(output))throw new InvalidOperationException("Output required");
            Directory.CreateDirectory(output);SessionState.SetString(Key,output);
            EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(String.IsNullOrEmpty(SessionState.GetString(Key,"")))return;
            if(state==PlayModeStateChange.EnteredPlayMode){output=SessionState.GetString(Key,"");work=Capture();EditorApplication.update+=Pump;}
            if(state==PlayModeStateChange.EnteredEditMode){EditorApplication.update-=Pump;SessionState.EraseString(Key);EditorApplication.Exit(File.Exists(Path.Combine(output,"success.txt"))?0:2);}
        }
        static void Pump()
        {
            try{if(work!=null&&!work.MoveNext()){work=null;EditorApplication.update-=Pump;EditorApplication.isPlaying=false;}}
            catch(Exception e){File.WriteAllText(Path.Combine(output,"error.txt"),e.ToString());Debug.LogException(e);work=null;EditorApplication.isPlaying=false;}
        }
        static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static IEnumerator Capture()
        {
            Need(!UnityEngine.XR.XRSettings.isDeviceActive,"XR must be off");
            // Isolate actual production presentation on a capture-only layer.
            var eye=new GameObject("HUD tracks capture camera").AddComponent<Camera>();eye.enabled=false;
            eye.clearFlags=CameraClearFlags.SolidColor;eye.backgroundColor=new Color(.12f,.14f,.17f);eye.cullingMask=1<<30;
            eye.fieldOfView=70;eye.aspect=1.4f;eye.nearClipPlane=.02f;eye.farClipPlane=100;eye.stereoTargetEye=StereoTargetEyeMask.None;
            var owner=new GameObject("HUD capture fixture");var ui=owner.AddComponent<R23VrUI>();ui.Initialize(eye);ui.SetPlaying();
            var state=new R24WorldScoreState{Active=true,Stage="match",Cap=25,Seconds=600,Players=new[]{new R24WorldScoreRow{Id="self",Kills=0,Deaths=0}}};
            ui.SetCompactMatchState(state,"self",0);ui.RefreshTextLayout();
            foreach(var renderer in ui.CompactMatchHudRoot.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=30;
            yield return null;ui.RefreshTextLayout();Save(eye,"hud-warmup.png");ui.RefreshTextLayout();
            var preview=new R23ConstructionPreview(owner.transform);
            preview.Label("Wall \u00b7 LIGHT \u00b7 1 CHARGES",eye.transform);
            Need(preview.Root.GetComponentsInChildren<TextMesh>(true).Length==0,"Removed caption must not allocate a text element");
            preview.Dispose();
            var root=ui.CompactMatchHudRoot;var nodes=root.GetComponentsInChildren<Transform>();
            var positions=new Vector3[nodes.Length];for(int i=0;i<nodes.Length;i++)positions[i]=nodes[i].localPosition;
            // Exact pre-edit depth control; all other transforms, texts, camera and materials identical.
            foreach(var node in nodes){var pos=node.localPosition;if(node.name=="Source score backdrop")pos.z=.804f;else if(node.name=="Own score remaining"||node.name=="Other score remaining")pos.z=.802f;else if(node.name=="Source cap plate")pos.z=.799f;else if(node.GetComponent<TextMesh>()!=null)pos.z=node.GetComponent<TextMesh>().text=="25"?.796f:.797f;node.localPosition=pos;}
            ui.RefreshTextLayout();Save(eye,"hud-before.png");for(int i=0;i<nodes.Length;i++)nodes[i].localPosition=positions[i];ui.RefreshTextLayout();Save(eye,"hud-after.png");
            owner.SetActive(false);foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=0;
            var tank=new GameObject("Actual armour presentation capture").AddComponent<R25ArmourPresentation>();tank.Initialize();
            foreach(var renderer in tank.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=30;
            var sun=new GameObject("Track capture light").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-45,0);
            RenderSettings.ambientLight=Color.gray;var log=new System.Text.StringBuilder();
            var wheel=tank.transform.Find("Planted hull/Road wheel");var tread=tank.transform.Find("Planted hull/Scrolling tread shoes");
            Vector3 at=Vector3.zero;
            foreach(bool mounted in new[]{true,false})foreach(float yaw in new[]{0f,90f})
            {
                string prefix=(mounted?"local":"remote")+"-yaw"+yaw.ToString("0");
                tank.Show(at,yaw,yaw,4,1000,0,mounted,true,0);yield return null;
                foreach(int direction in new[]{1,-1})
                {
                    for(int frame=0;frame<3;frame++)
                    {
                        yield return null;float dt=Time.unscaledDeltaTime;float before=wheel.localEulerAngles.x;float treadBefore=tread.localPosition.z;
                        at+=Quaternion.Euler(0,yaw,0)*Vector3.forward*(direction*2*dt);
                        tank.Show(at,yaw,yaw,2,1000,0,mounted,true,0);
                        float rotation=Mathf.DeltaAngle(before,wheel.localEulerAngles.x);
                        float expected=direction*2*dt;
                        Need(Mathf.Abs(Mathf.Repeat(treadBefore+expected,.22f)-tread.localPosition.z)<.0001f,"Signed tread direction "+prefix);
                        Need(direction*tank.SignedForwardSpeed>0&&direction*rotation>0,"Signed wheel direction "+prefix);
                        log.AppendLine(prefix+" direction="+direction+" frame="+frame+" speed="+tank.SignedForwardSpeed+" wheelDelta="+rotation+" treadZ="+tread.localPosition.z);
                        eye.transform.position=at+Quaternion.Euler(0,yaw,0)*new Vector3(4,2.3f,3);
                        eye.transform.LookAt(at+Vector3.up*.1f);Save(eye,prefix+(direction>0?"-forward-":"-reverse-")+frame+".png");
                    }
                }
                yield return null;tank.Show(at,yaw,yaw,2,1000,0,mounted,true,0);Need(Mathf.Abs(tank.SignedForwardSpeed)<.0001f,"stationary unsigned caller must not animate");
                at+=Vector3.one*10;yield return null;tank.Show(at,yaw,yaw,2,1000,0,mounted,true,0);Need(tank.SignedForwardSpeed==0,"teleport must reset motion");
            }
            File.WriteAllText(Path.Combine(output,"motion.txt"),log.ToString());
            UnityEngine.Object.Destroy(owner);UnityEngine.Object.Destroy(tank.gameObject);UnityEngine.Object.Destroy(sun.gameObject);UnityEngine.Object.Destroy(eye.gameObject);
            File.WriteAllText(Path.Combine(output,"success.txt"),"Actual production HUD before/after and 24 track frames. Local/remote, yaw 0/90, forward/reverse, stopped and teleport assertions passed. No headset or network peers.");
        }
        static void Save(Camera camera,string name)
        {
            if(name.StartsWith("hud-"))
            {
                var text=new System.Text.StringBuilder();text.AppendLine("camera="+camera.transform.position+" aspect="+camera.aspect+" fov="+camera.fieldOfView);
                foreach(var node in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude))
                    if(node.gameObject.layer==30)text.AppendLine("caption="+node.text+" local="+node.transform.localPosition.ToString("F6")+" viewport="+camera.WorldToViewportPoint(node.GetComponent<Renderer>().bounds.center).ToString("F6"));
                File.WriteAllText(Path.Combine(output,name+".layout.txt"),text.ToString());
            }
            var target=RenderTexture.GetTemporary(1400,1000,24);var prior=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1400,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1400,1000),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)UnityEngine.Object.Destroy(pixels);}
        }
    }
}
#endif
