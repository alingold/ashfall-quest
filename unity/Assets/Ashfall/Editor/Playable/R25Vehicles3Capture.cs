#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Ashfall.Integration.Playable.Vehicles;
namespace Ashfall.Editor.Playable
{
    public static class R25Vehicles3Capture
    {
        public static void Run()
        {
            try{
                string output=R23PlayableBuild.Argument("-ashfallVehicleOutput");Directory.CreateDirectory(output);
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera=new GameObject("Vehicle review camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=48;camera.nearClipPlane=.03f;camera.farClipPlane=300;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.18f);
                var sun=new GameObject("Key").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(40,-40,0);
                var view=new GameObject("Production tank").AddComponent<R25ArmourPresentation>();view.Initialize();
                foreach(bool night in new[]{false,true})foreach(bool moving in new[]{false,true}){
                    sun.intensity=night?.25f:2;RenderSettings.ambientLight=night?new Color(.12f,.15f,.25f):new Color(.55f,.6f,.7f);
                    view.Show(Vector3.zero,0,moving?30:0,moving?7:0,1800,0,false,true,0);
                    Vector3[] eyes={new Vector3(0,1.8f,7),new Vector3(7,1.8f,0),new Vector3(5,3,6),new Vector3(0,1.65f,-.4f)};
                    string[] names={"front","side","three-quarter","rider"};
                    for(int i=0;i<eyes.Length;i++){
                        camera.transform.position=eyes[i];camera.transform.LookAt(i==3?new Vector3(0,1.55f,15):new Vector3(0,.2f,.5f));
                        view.Show(Vector3.zero,0,moving?30:0,moving?7:0,1800,0,i==3,true,0);
                        var rt=RenderTexture.GetTemporary(1200,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"tank-"+(night?"night":"day")+"-"+(moving?"moving":"idle")+"-"+names[i]+".png"),pixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(pixels);
                    }
                }
                File.WriteAllText(Path.Combine(output,"success.txt"),"16 production tank geometry views; isolated editor fixture, not gameplay or headset verification.");
                EditorSceneManager.CloseScene(scene,true);EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        public static void Helicopter()
        {
            try{
                string output=R23PlayableBuild.Argument("-ashfallVehicleOutput");Directory.CreateDirectory(output);
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera=new GameObject("Vehicle review camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=48;camera.nearClipPlane=.03f;camera.farClipPlane=300;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.18f);
                var sun=new GameObject("Key").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(40,-40,0);
                var view=new GameObject("Production helicopter").AddComponent<R25HelicopterPresentation>();view.Initialize();
                foreach(bool night in new[]{false,true})foreach(bool moving in new[]{false,true}){
                    sun.intensity=night?.25f:2;RenderSettings.ambientLight=night?new Color(.12f,.15f,.25f):new Color(.55f,.6f,.7f);
                    view.Show(Vector3.zero,0,moving?.4f:0,moving?.8f:0,600,moving,true,0,night,moving?1:0);
                    Vector3[] eyes={new Vector3(0,2.5f,11),new Vector3(11,2.5f,-1),new Vector3(8,4.5f,9),new Vector3(0,1.65f,-.4f)};
                    string[] names={"front","side","three-quarter","rider"};
                    for(int i=0;i<eyes.Length;i++){
                        camera.transform.position=eyes[i];camera.transform.LookAt(i==3?new Vector3(0,1.55f,15):new Vector3(0,.65f,-.8f));
                        view.Show(Vector3.zero,0,moving?.4f:0,moving?.8f:0,600,moving,true,0,night,moving?1:0);
                        var rt=RenderTexture.GetTemporary(1200,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"helicopter-"+(night?"night":"day")+"-"+(moving?"moving":"idle")+"-"+names[i]+".png"),pixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(pixels);
                    }
                }
                File.WriteAllText(Path.Combine(output,"success.txt"),"16 production helicopter geometry views; isolated editor fixture, not gameplay or headset verification.");
                EditorSceneManager.CloseScene(scene,true);EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
    }
}
#endif
