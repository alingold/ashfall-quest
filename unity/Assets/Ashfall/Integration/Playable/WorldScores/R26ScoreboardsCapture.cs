#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.IO;using Ashfall.Integration.Playable.World;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;
namespace Ashfall.Integration.Playable.WorldScores {
 // Inert unless explicitly requested by an XR-off NetBot evidence run.
 public sealed class R26ScoreboardsCapture:MonoBehaviour {
  string output;Camera camera;StreamWriter log;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install(){string path=R25NetBot.Arg("-ashfallScoreboardsCapture");if(path.Length==0||!R25NetBot.Requested||UnityEngine.XR.XRSettings.isDeviceActive)return;var hook=new GameObject("R26 opt-in scoreboard capture").AddComponent<R26ScoreboardsCapture>();hook.output=path;}
  static Vector3 V(float[] a){return new Vector3(a[0],a[1],a[2]);}
  IEnumerator Start(){
   Directory.CreateDirectory(output);log=new StreamWriter(Path.Combine(output,"R26-runtime-audit.txt"));log.AutoFlush=true;R23PlayableLoop loop=null;float until=Time.realtimeSinceStartup+180;
   while(Time.realtimeSinceStartup<until){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&loop.Ready&&loop.Map.IsLoaded&&loop.WorldScores!=null&&loop.WorldScores.Visible&&loop.WorldScores.CanvasUpdates>0)break;yield return null;}
   if(loop==null||loop.WorldScores==null||!loop.WorldScores.Visible){log.WriteLine("FAIL scoreboard ready timeout");yield break;}
   var rows=R26WorldScoreboardFrames.Read(loop.Map);if(rows.Length==0){log.WriteLine("FAIL no approved frame "+loop.Map.MapId);yield break;}
   var node=new GameObject("R26 scoreboard evidence camera");camera=node.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.05f;camera.farClipPlane=1500;camera.fieldOfView=55;camera.cullingMask=loop.Rig.Head.cullingMask&~(1<<30);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.08f,.11f);camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
   log.WriteLine("map="+loop.Map.MapId+" source="+loop.Map.SourceSha256+" faces="+loop.WorldScores.FaceCount+" detected="+rows.Length+" failure="+loop.WorldScores.Failure+" detectorFailure="+R26WorldScoreboardFrames.Failure);
   // Capture every detected aperture; counts alone cannot prove it is visible.
   for(int k=0;k<rows.Length;k++){
    var row=rows[k];Vector3 center=loop.Map.transform.TransformPoint(V(row.center)+V(row.normal)*row.frontOffset),normal=loop.Map.transform.TransformDirection(V(row.normal)),right=loop.Map.transform.TransformDirection(V(row.right));float distance=Mathf.Max(row.height,row.width)*1.2f;
    camera.nearClipPlane=Mathf.Clamp(Mathf.Min(row.width,row.height)*.005f,.001f,.05f);camera.transform.SetPositionAndRotation(center+normal*distance+right*(k==1?distance*.18f:0),Quaternion.LookRotation(-normal-right*(k==1?.18f:0),Vector3.Cross(right,normal)));
    yield return new WaitForSeconds(1.2f);loop.WorldScores.Refresh();Save("R26-filled-"+k+".png");var state=loop.WorldScores.LastState;
    log.WriteLine("capture="+k+" frame="+row.id+" center="+V(row.center)+" normal="+normal+" width="+row.width+" height="+row.height+" offset="+row.frontOffset+" updates="+loop.WorldScores.CanvasUpdates+" active="+state.Active+" revive="+state.TeamRevive+" rows="+state.Rows.Length+" players="+state.Players.Length);
    if(k==0){foreach(var player in state.Players)log.WriteLine("player="+player.Name+" life="+player.Life+" alive="+player.Alive+" downed="+player.Downed);SaveCanvas(loop.WorldScores.SharedCanvas);}
   }
   log.WriteLine("DONE");log.Dispose();log=null;Destroy(camera.gameObject);camera=null;
  }
  void Save(string name){var target=new RenderTexture(1280,960,24);target.Create();try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});Pixels(target,name);}finally{target.Release();Destroy(target);}}
  void SaveCanvas(RenderTexture target){Pixels(target,"R26-shared-live-canvas.png");}
  void Pixels(RenderTexture target,string name){var prior=RenderTexture.active;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());}finally{RenderTexture.active=prior;Destroy(pixels);}}
  void OnDestroy(){if(camera!=null)Destroy(camera.gameObject);if(log!=null)log.Dispose();}
 }
}
#endif
