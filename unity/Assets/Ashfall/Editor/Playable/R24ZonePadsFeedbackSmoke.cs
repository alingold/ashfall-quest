#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.IO;using System.Reflection;
using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.MatchZone;
using UnityEngine;using UnityEditor;
namespace Ashfall.Editor.Playable {
 public static partial class R24Feedback6237Smoke {
  static IEnumerator ZoneHum(R23PlayableLoop loop,Action<string> note) {
   var frame=new GameObject("Owned zone-audio boundary control");frame.transform.position=new Vector3(2000,0,2000);
   var service=new GameObject("Actual production zone voice").AddComponent<R24MatchZone>();var prior=loop.Rig.Head.transform.position;
   try {
    service.Initialize(frame.transform,loop.Rig.transform,loop.Rig.Head,null,Vector3.zero);
    float end=Time.realtimeSinceStartup+15;while(!service.AudioReady&&service.AudioFailure==null&&Time.realtimeSinceStartup<end)yield return null;
    Need(service.AudioReady,"Actual hum WAV decoded: "+service.AudioFailure);
    var edge=(AudioSource)Read(service,"edgeAudio");var outside=(AudioSource)Read(service,"outsideAudio");var square=new R24ZoneSquare(-10,-10,20);var plan=new R24ZonePlan(square,square,0,1,0,true);
    float previous=Single.PositiveInfinity;
    foreach(float x in new[]{-10f,-8f,-6f,-5f,-16f}) {
     loop.Rig.Head.transform.position=frame.transform.TransformPoint(new Vector3(x,1.7f,0));service.Present(plan,0,.02f,true);
     note("Zone localX="+x+" edgeDistance="+service.EdgeDistance+" volume="+edge.volume+" playing="+edge.isPlaying+" outsideVoice="+outside.isPlaying);
     Need(!outside.isPlaying&&outside.volume==0,"No unlimited outside loop");
     if(service.EdgeDistance<5){Need(edge.volume>0&&edge.volume<=previous,"Smooth close-only falloff");previous=edge.volume;}else Need(edge.volume==0&&!edge.isPlaying,"Zone silent beyond5m on either side");
    }
    service.SetSfxVolume(0);loop.Rig.Head.transform.position=frame.transform.TransformPoint(new Vector3(-10,1.7f,0));service.Present(plan,0,.02f,true);Need(edge.volume==0&&!edge.isPlaying,"Actual SFX mute honored");
    var data=new float[edge.clip.samples];Need(edge.clip.GetData(data,0),"Read actual decoded hum PCM");double sum=0,diff=0;for(int i=1;i<data.Length;i++){sum+=data[i]*data[i];double d=data[i]-data[i-1];diff+=d*d;}
    Need(Math.Sqrt(sum/data.Length)>.03&&diff/sum<.02,"Low hum retained without broadband hiss");note("Actual decoded hum RMS="+Math.Sqrt(sum/data.Length)+" normalized difference energy="+diff/sum+". Voice/PCM evidence, not headset audibility.");
   }finally{loop.Rig.Head.transform.position=prior;UnityEngine.Object.Destroy(service.gameObject);UnityEngine.Object.Destroy(frame);}
  }
  static IEnumerator ZoneMist(R23PlayableLoop loop,Action<string> note) {
   var material=(Material)loop.Explosives.GetType().GetProperty("ZoneMaterial",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loop.Explosives,null);
   var mesh=(Mesh)loop.Explosives.GetType().GetProperty("ZoneMesh",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loop.Explosives,null);
   Need(material.shader.name=="Ashfall/R24 Transparent Zone Mist"&&Mathf.Abs(material.GetColor("_BaseColor").a-.06f)<.0001f,"Actual projectile mist binds dedicated alpha shader");
   var root=new GameObject("Owned mist render control");root.transform.position=new Vector3(2000,10,2000);GameObject cameraObject=null;Material black=null;
   try {
    cameraObject=new GameObject("Mist control camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.white;camera.fieldOfView=58;camera.cullingMask=1<<18;camera.transform.position=root.transform.position+Vector3.back*4;camera.transform.LookAt(root.transform.position);
    black=new Material(Shader.Find("Universal Render Pipeline/Unlit"));black.SetColor("_BaseColor",new Color(.06f,.06f,.06f,1));
    for(int i=-1;i<=1;i++){var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);bar.layer=18;bar.transform.SetParent(root.transform,false);bar.transform.localPosition=new Vector3(i*.6f,0,.9f);bar.transform.localScale=new Vector3(.2f,2,.1f);bar.GetComponent<Renderer>().sharedMaterial=black;}
    var mist=new GameObject("Actual grenade sphere and material");mist.layer=18;mist.transform.SetParent(root.transform,false);mist.AddComponent<MeshFilter>().sharedMesh=mesh;mist.AddComponent<MeshRenderer>().sharedMaterial=material;
    string dir=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),"zone-mist-images");Directory.CreateDirectory(dir);
    mist.SetActive(false);Capture(camera,Path.Combine(dir,"background-control.png"));mist.SetActive(true);Capture(camera,Path.Combine(dir,"actual-purple-mist.png"));
    foreach(var msg in ShaderUtil.GetShaderMessages(material.shader))Need(msg.severity.ToString()!="Error",msg.message);
    note("Actual live grenade shared sphere/material rendered over opaque bars;2UNMATCHEDUnity controls at "+dir+". Personal image inspection required; no HMD/Android proof.");yield return null;
   }finally{UnityEngine.Object.Destroy(root);if(cameraObject!=null)UnityEngine.Object.Destroy(cameraObject);if(black!=null)UnityEngine.Object.Destroy(black);}
  }
  static IEnumerator ClassicPads(R23PlayableLoop loop,Action<string> note) {
   loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("archive:classic-bigbox-vr"),"Actual Classic card");float end=Time.realtimeSinceStartup+90;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
   Need(loop.Ready&&loop.Map.MapId=="archive:classic-bigbox-vr","Actual Classic loaded");loop.StartPlaying();
   end=Time.realtimeSinceStartup+15;while(!loop.JumpPads.AudioReady&&loop.JumpPads.AudioFailure==null&&Time.realtimeSinceStartup<end)yield return null;
   Need(loop.JumpPads.Ready&&loop.JumpPads.PadCount==31&&loop.JumpPads.AudioReady,"Actual Classic31 and cue decoded: "+loop.JumpPads.AudioFailure);
   var pads=loop.JumpPads.AuthoredPads;bool launched=false;int attempted=0;
   foreach(var pad in pads) {
    var c=pad.Pad.Center;var at=new Vector3(c.X,c.Y,c.Z);loop.Rig.SetSpawn(at,at+Vector3.forward);loop.Calibration.NotifyRootPoseReset();loop.Climbing.ResetMotion();loop.Activities.ResetRound();for(int i=0;i<8;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);loop.JumpPads.ResetContacts();attempted++;
    if(loop.JumpPads.Step(at,.02f,true,false,false)){launched=true;note("Actual source pad accepted id="+loop.JumpPads.LastLaunchedPadId+" velocity="+loop.Rig.MotionVelocity);break;}
   }
   Need(launched&&loop.JumpPads.LaunchCount==1,"Actual source contact -> Root launch owner -> one cue");var voice=(AudioSource)Read(loop.JumpPads,"launchVoice");Need(voice.clip!=null&&voice.clip.samples==11520&&voice.isPlaying,"Accepted launch schedules actual new cue");
   int count=loop.JumpPads.LaunchCount;Need(!loop.JumpPads.Step(loop.Rig.transform.position,.02f,false,false,false)&&loop.JumpPads.LaunchCount==count,"Unavailable input cannot trigger another launch/cue");
   note("Native Classic31 rows loaded; first accepted contact after "+attempted+" attempts. Not all31 trajectories/chains/HMD audibility acceptance.");loop.RestartRound();yield return null;
  }
 }
}
#endif
