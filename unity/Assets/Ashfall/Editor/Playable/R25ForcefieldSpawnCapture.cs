#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ashfall.Core;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Match;
using UnityEditor;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    [InitializeOnLoad]
    public static class R25ForcefieldSpawnCapture
    {
        const string Key="Ashfall.Forcefield.Output";
        static Stack<IEnumerator> work;
        static float deadline;
        static int lastFrame=-1;
        static R25ForcefieldSpawnCapture(){EditorApplication.playModeStateChanged+=Changed;}
        public static void Run()
        {
            string output=R23PlayableBuild.Argument("-ashfallForcefieldOutput");
            if(String.IsNullOrEmpty(output)||Directory.Exists(output)){EditorApplication.Exit(2);return;}
            Directory.CreateDirectory(output);R23PlayableBuild.Prepare();
            R23PlayableBuild.Xr().InitManagerOnStart=false;
            SessionState.SetString(Key,output);EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(SessionState.GetString(Key,"").Length==0)return;
            if(state==PlayModeStateChange.EnteredPlayMode){deadline=Time.realtimeSinceStartup+600;work=new Stack<IEnumerator>();work.Push(Capture());EditorApplication.update+=Pump;}
            if(state==PlayModeStateChange.EnteredEditMode){EditorApplication.update-=Pump;work=null;string output=SessionState.GetString(Key,"");SessionState.EraseString(Key);R23PlayableBuild.Xr().InitManagerOnStart=true;AssetDatabase.SaveAssets();EditorApplication.Exit(File.Exists(Path.Combine(output,"success.txt"))?0:2);}
        }
        static void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        static void Pump()
        {
            try{
                Need(Time.realtimeSinceStartup<deadline,"600 second capture limit");
                if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
                while(work.Count>0){var top=work.Peek();if(!top.MoveNext()){work.Pop();continue;}var nested=top.Current as IEnumerator;if(nested!=null){work.Push(nested);continue;}return;}
                EditorApplication.isPlaying=false;
            }catch(Exception e){File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"error.txt"),e.ToString());Debug.LogException(e);EditorApplication.isPlaying=false;}
        }
        static IEnumerator Capture()
        {
            R23PlayableLoop root=null;
            while(root==null||!root.Ready){root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(root!=null)Need(root.Failure==null,root.Failure);yield return null;}
            root.ManualSmoke=true;root.ExternalProbePump=true;root.Rig.enabled=false;root.Gameplay.ExternalInputPump=true;root.Calibration.SuppressPersistence=true;
            foreach(string slug in new[]{"flash-gods-pitdevil","candle-magic-gods-goodwitch","beast-mode-gods-pitdevil"}){
                string id="archive:"+slug;root.ReturnToLobby();root.SelectMap(id,false);
                while(!root.Ready){Need(root.Failure==null,root.Failure);yield return null;}
                root.StartPlaying();while(root.SourceArchiveEntryPending){root.SmokeUiUpdate();yield return null;}
                Need(String.IsNullOrEmpty(root.SourceArchiveEntryFailure),root.SourceArchiveEntryFailure);
                while(!root.ArchiveWorldReady){root.PrepareArchiveWorld(true);yield return null;}
                foreach(var point in root.ArchiveWorldPlan.Spawns)Debug.Log("FORCE SPAWN "+point+" interior="+R24ArchiveRoundPolicy.ForcefieldInterior(id,point)+" safe="+root.ArchiveWorldSafeSpawn(point));
                foreach(var point in root.ArchiveWorldPlan.Loot)if(R24ArchiveRoundPolicy.ForcefieldInterior(id,point))Debug.Log("FORCE LOOT "+point+" safe="+root.ArchiveWorldSafeSpawn(new Float3(point.X,point.Y-.58f,point.Z)));
                root.UI.OpenMatchRules();root.UI.SelectMatchPreset("team_revive");Need(root.UI.RequestMatchStart(),"Normal UI match start");
                while(root.SourceArchiveEntryPending||!root.MatchActive){root.SmokeUiUpdate();Need(String.IsNullOrEmpty(root.SourceArchiveEntryFailure),root.SourceArchiveEntryFailure);yield return null;}
                while(!root.ArchiveWorldReady){root.PrepareArchiveWorld(true);root.SmokeUiUpdate();yield return null;}
                Physics.SyncTransforms();Vector3 feet=root.Rig.transform.position;Float3 source;
                Need(root.TryArchiveWorldToSource(feet,out source)&&R24ArchiveRoundPolicy.ForcefieldInterior(id,source),"Inside audited boundary inset: "+id+" world="+feet+" source="+source);
                Need(root.TeamLaunch==null||(!root.TeamLaunch.Eligible&&root.TeamLaunch.DockCount==0&&root.TeamLaunch.FlightCount==0),"No deck or pod runtime");
                Need(!root.OnLaunchDeck&&GameObject.Find("Original team launch decks R12")==null,"No platform object");
                RaycastHit hit;Need(Physics.Raycast(feet+Vector3.up*.01f,Vector3.down,out hit,.06f,1,QueryTriggerInteraction.Ignore)&&hit.normal.y>.7f,"Standable floor within five centimetres");
                float gap=feet.y-hit.point.y;Need(gap>=-.005f&&gap<=.05f,"Feet supported");
                string output=SessionState.GetString(Key,"");File.AppendAllText(Path.Combine(output,"placements.txt"),id+" source="+source+" world="+feet.ToString("F5")+" floorGap="+gap+" collider="+hit.collider.name+" deck=false docks=0 flights=0\n");
                root.SmokeUiUpdate();yield return null;Save(root.Rig.Head,Path.Combine(output,slug+"-head.png"));
                var cameraObject=new GameObject("Forcefield audit top camera");var cam=cameraObject.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=slug.StartsWith("beast")?300:245;cam.farClipPlane=2000;cam.nearClipPlane=.1f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.025f,.03f,.05f);
                Vector3 center=slug.StartsWith("candle")?new Vector3(-44.4f,0,11.4f):Vector3.zero;center=root.Map.transform.TransformPoint(center);cam.transform.SetPositionAndRotation(center+Vector3.up*1100,Quaternion.Euler(90,0,0));
                var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Verified spawn marker";marker.transform.position=feet+Vector3.up*8;marker.transform.localScale=Vector3.one*7;marker.GetComponent<Renderer>().material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));marker.GetComponent<Renderer>().material.color=Color.magenta;
                Save(cam,Path.Combine(output,slug+"-top.png"));UnityEngine.Object.Destroy(marker);UnityEngine.Object.Destroy(cameraObject);
            }
            File.WriteAllText(Path.Combine(SessionState.GetString(Key,""),"success.txt"),"Three normal team-revive starts: interior, supported within 5 cm, no launch deck/pods. Six PNGs.\n");root.ReturnToLobby();
        }
        static void Save(Camera camera,string path)
        {
            var target=RenderTexture.GetTemporary(1400,1000,24);var old=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1400,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1400,1000),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)UnityEngine.Object.Destroy(pixels);}
        }
    }
}
#endif
