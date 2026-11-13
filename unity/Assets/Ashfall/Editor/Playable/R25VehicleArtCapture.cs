#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Ashfall.Integration.Playable.Vehicles;
namespace Ashfall.Editor.Playable {
public static class R25VehicleArtCapture {
static string output;
public static void Run(){try{
output=R23PlayableBuild.Argument("-ashfallVehicleOutput");Directory.CreateDirectory(output);
var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
var cam=new GameObject("Vehicle camera").AddComponent<Camera>();cam.tag="MainCamera";cam.nearClipPlane=.02f;cam.farClipPlane=100;cam.clearFlags=CameraClearFlags.SolidColor;
var sun=new GameObject("Key").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(40,-40,0);
RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
foreach(string kind in new[]{"bike","tank","helicopter"}){
var go=new GameObject("Production "+kind);R25BikePresentation bike=null;R25ArmourPresentation tank=null;R25HelicopterPresentation heli=null;
if(kind=="bike"){bike=go.AddComponent<R25BikePresentation>();bike.Initialize();}else if(kind=="tank"){tank=go.AddComponent<R25ArmourPresentation>();tank.Initialize();}else{heli=go.AddComponent<R25HelicopterPresentation>();heli.Initialize();}
int maxTris=0;var surfaces=new HashSet<Material>();
foreach(bool night in new[]{false,true})foreach(bool moving in new[]{false,true})foreach(string view in new[]{"front","side","rear","rider"}){
sun.intensity=night?.25f:2;RenderSettings.ambientLight=night?new Color(.12f,.15f,.25f):new Color(.55f,.60f,.7f);cam.backgroundColor=new Color(.065f,.08f,.105f);
float scale=kind=="bike"?1:kind=="tank"?2.4f:3.4f;
Vector3 target=kind=="bike"?new Vector3(0,.2f,0):kind=="tank"?new Vector3(0,.2f,.4f):new Vector3(0,.6f,-1);
Vector3 eye=view=="front"?new Vector3(0,.8f,3.4f)*scale:view=="side"?new Vector3(3.5f,.8f,0)*scale:view=="rear"?new Vector3(-2.8f,1.5f,-3f)*scale:new Vector3(0,kind=="bike"?1.15f:1.65f,-.4f);
if(view=="rider")target=kind=="bike"?new Vector3(0,.5f,2):new Vector3(0,1.55f,15);
cam.fieldOfView=view=="rider"?75:48;cam.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
if(bike!=null)bike.Show(Vector3.zero,0,moving?24:0,moving?8:0,300,true,-1,true,"");
if(tank!=null)tank.Show(Vector3.zero,0,moving?30:0,moving?7:0,1800,0,view=="rider",true,-1);
if(heli!=null)heli.Show(Vector3.zero,0,moving?.4f:0,moving?.8f:0,600,moving,true,-1,night,moving?1:0);
int tris=0;foreach(var f in go.GetComponentsInChildren<MeshFilter>())if(f.gameObject.activeInHierarchy&&f.GetComponent<TextMesh>()==null)tris+=f.sharedMesh.triangles.Length/3;
maxTris=Mathf.Max(maxTris,tris);foreach(var r in go.GetComponentsInChildren<Renderer>())if(r.gameObject.activeInHierarchy&&r.GetComponent<TextMesh>()==null)foreach(var m in r.sharedMaterials)surfaces.Add(m);
Save(cam,kind+"-"+(night?"night":"day")+"-"+(moving?"moving":"idle")+"-"+view);
}
File.AppendAllText(Path.Combine(output,"budgets.txt"),kind+" max visible triangles="+maxTris+" materials="+surfaces.Count+" (excludes text, inactive damage effects)\n");
// True black silhouette: replace every visible surface, disable labels, orthographic matched scale.
if(bike!=null)bike.Show(Vector3.zero,0,0,0,300,true,-1,true,"");if(tank!=null)tank.Show(Vector3.zero,0,0,0,1800,0,true,true,-1);if(heli!=null)heli.Show(Vector3.zero,0,0,0,600,false,true,-1,false,0);
var black=new Material(Shader.Find("Unlit/Color"));black.color=Color.black;
foreach(var r in go.GetComponentsInChildren<Renderer>()){if(r.GetComponent<TextMesh>()!=null||r is LineRenderer)r.enabled=false;else r.sharedMaterial=black;r.SetPropertyBlock(null);}
cam.orthographic=true;cam.orthographicSize=kind=="bike"?1.45f:kind=="tank"?3.2f:4.5f;cam.backgroundColor=Color.white;
foreach(string view in new[]{"front","side"}){Vector3 target=kind=="bike"?new Vector3(0,.25f,0):kind=="tank"?new Vector3(0,.5f,.4f):new Vector3(0,.7f,-1);cam.transform.position=target+(view=="front"?Vector3.forward:Vector3.right)*20;cam.transform.LookAt(target);Save(cam,kind+"-silhouette-"+view);}
cam.orthographic=false;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(black);
}
File.WriteAllText(Path.Combine(output,"success.txt"),"54 inspected-state production model renders. Static editor fixture; no gameplay simulation.");EditorSceneManager.CloseScene(scene,true);EditorApplication.Exit(0);
}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}}
static void Save(Camera cam,string name){var rt=RenderTexture.GetTemporary(1200,900,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(pixels);}
}}
#endif
