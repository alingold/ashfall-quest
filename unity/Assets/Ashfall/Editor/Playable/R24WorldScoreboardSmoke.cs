#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using System.IO;
using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.WorldScores;using UnityEngine;using UnityEditor;
namespace Ashfall.Editor.Playable {
 public static partial class R24Feedback6237Smoke {
  static IEnumerator WorldScoreboards(R23PlayableLoop loop,Action<string> note,bool legacyOnly=false){
   var maps=legacyOnly?new[]{"archive:melipop-gods-poplightning"}:new[]{"archive:battlefield-paintball-gods-pitdevil","archive:alfa-aglae-souza","archive:melipop-gods-poplightning"};var counts=legacyOnly?new[]{4}:new[]{1,3,4};
   string output=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),"world-score-images");Directory.CreateDirectory(output);
   int initialUpdates=loop.WorldScores.CanvasUpdates;
   for(int n=0;n<maps.Length;n++){
    loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap(maps[n]),"Actual source map card");float end=Time.realtimeSinceStartup+90;while((!loop.Ready||loop.Map.MapId!=maps[n])&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
    Need(loop.Ready&&loop.Map.MapId==maps[n],"Actual map load for source score faces");loop.StartPlaying();
    end=Time.realtimeSinceStartup+15;while((!loop.WorldScores.Ready||loop.WorldScores.BoundMap!=maps[n]||!loop.WorldScores.Visible)&&loop.WorldScores.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
    Need(loop.WorldScores.Ready&&loop.WorldScores.Failure==null&&loop.WorldScores.FaceCount==counts[n],"Exact source board count "+maps[n]+": "+loop.WorldScores.Failure);
    var state=loop.ReadWorldScoreState();Need(!state.Active,"Real lobby/practice state");loop.WorldScores.Refresh();
    if(n==0&&!legacyOnly){
     loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset("free_for_all");Need(loop.UI.SetMatchRule("time_limit",120),"Real120s match setting");Need(loop.UI.RequestMatchStart(),"Actual Start Match callback");end=Time.realtimeSinceStartup+20;while(!loop.MatchActive&&Time.realtimeSinceStartup<end)yield return null;Need(loop.MatchActive,"Real local match owner active");
     state=loop.ReadWorldScoreState();Need(state.Active&&state.Teams==0&&state.Players.Length==1&&state.Rows.Length==1&&state.Seconds<=120&&state.Seconds>=118,"Actual FFA clock/roster snapshot");int before=loop.Gameplay.LocalMatchHealth.Deaths;Need(loop.Gameplay.ApplyLocalMatchFall(),"Actual local fall transition for death binding");state=loop.ReadWorldScoreState();Need(state.Players[0].Deaths==before+1&&state.Players[0].Score==0&&state.Players[0].Kills==0,"Actual local score owner remains zero; world fall awards no kill");loop.WorldScores.Refresh();
     WriteScoreCanvas(loop.WorldScores.SharedCanvas,Path.Combine(output,"actual-ffa-canvas.png"));note("Actual FFA source clock="+state.ClockText+" own deaths="+state.Players[0].Deaths+" offline score/kills remain zero; no shared combat authority implied");
    }
    var faces=(List<Mesh>)Read(loop.WorldScores,"faces");var root=(Transform)Read(loop.WorldScores,"faceRoot");Need(root.GetComponentsInChildren<Collider>(true).Length==0,"Score presentation adds no collision");
    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()){Need(renderer.sharedMaterial.mainTexture==loop.WorldScores.SharedCanvas,"All map faces share one canvas");foreach(var message in ShaderUtil.GetShaderMessages(renderer.sharedMaterial.shader))Need(message.severity.ToString()!="Error",message.message);}
    var cameraObject=new GameObject("Source-bound board aperture camera");try{var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=58;camera.nearClipPlane=.05f;camera.farClipPlane=200;camera.cullingMask=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.62f,.71f,.78f);var mesh=faces[0];var indices=mesh.triangles;var v=mesh.vertices;Vector3 center=root.TransformPoint(mesh.bounds.center),normal=root.TransformDirection(Vector3.Cross(v[indices[1]]-v[indices[0]],v[indices[2]]-v[indices[0]]).normalized);Vector3 groupCenter=Vector3.zero;foreach(var face in faces)groupCenter+=root.TransformPoint(face.bounds.center);groupCenter/=faces.Count;if(Vector3.Dot(normal,center-groupCenter)<-.01f)normal=-normal;float distance=Mathf.Max(mesh.bounds.size.magnitude,4)*1.1f;camera.transform.position=center+normal*distance;camera.transform.LookAt(center,Vector3.up);Capture(camera,Path.Combine(output,legacyOnly?"legacy-exterior-actual-board-unmatched.png":"map-"+n+"-actual-board-unmatched.png"));note("Loaded "+maps[n]+" boards="+faces.Count+" canvas="+loop.WorldScores.SharedCanvas.width+"x"+loop.WorldScores.SharedCanvas.height+" updates="+loop.WorldScores.CanvasUpdates+" first aperture center="+center+" camera="+camera.transform.position+" normal="+normal+". UNMATCHEDUnity camera, not Godot image parity.");}finally{UnityEngine.Object.Destroy(cameraObject);}
    loop.ReturnToLobby();yield return null;Need(!loop.WorldScores.Visible,"Score faces hidden on actual menu return");
   }
   Need(loop.WorldScores.CanvasUpdates>initialUpdates,"Actual shared canvas was rendered");note(legacyOnly?"Only4legacy Melipop faces loaded,1exterior screenshot; no othermap/FFA replay or headset proof.":"All8 reference archive score faces exercised on3actual maps; no network score authority/remote deaths/two-device/HMD proof.");
  }
  static void WriteScoreCanvas(RenderTexture target,string path){var old=RenderTexture.active;Texture2D pixels=null;try{RenderTexture.active=target;pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}finally{RenderTexture.active=old;if(pixels!=null)UnityEngine.Object.Destroy(pixels);}}
 }
}
#endif
