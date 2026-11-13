#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.World;

namespace Ashfall.Editor.Playable
{
    /// <summary>Actual source camera recipes, shared projection and fresh unedited renders.</summary>
    public sealed class R24ParityCapture
    {
        [Serializable] public sealed class Shot {public string name="";public float[] eye=null,target=null;}
        [Serializable] public sealed class Map {public string id="";public Shot[] shots=null;}
        [Serializable] public sealed class Recipe {public int width=1200,height=800;public float verticalFov=58;public bool menus=true;public Map[] maps=null;}
        [Serializable] sealed class Result {public string error="",recipe="";public bool headsetVerified=false;public string[] images=null;public int width,height;public float verticalFov;}
        static Vector3 Reflected(float[] a){if(a==null||a.Length!=3)throw new InvalidDataException("Bad parity camera vector");return new Vector3(a[0],a[1],-a[2]);}
        public IEnumerator Start()
        {
            var result=new Result{recipe=R23PlayableBuild.Argument("-ashfallParityRecipe")};Recipe recipe=null;R23PlayableLoop loop=null;
            try{recipe=JsonUtility.FromJson<Recipe>(File.ReadAllText(result.recipe));if(recipe.maps==null||recipe.width!=1200||recipe.height!=800||Mathf.Abs(recipe.verticalFov-58)>.001f)throw new InvalidDataException("Actual source map projection required");}
            catch(Exception e){result.error=e.ToString();}
            if(result.error.Length>0){R23SmokeControl.Finish(false,JsonUtility.ToJson(result,true));yield break;}
            float end=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready){result.error=loop==null?"Playable loop missing":loop.Failure??"Initial source map timeout";R23SmokeControl.Finish(false,JsonUtility.ToJson(result,true));yield break;}
            loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;
            var images=new List<string>();var cameraObject=new GameObject("Source-matched static parity camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.fieldOfView=recipe.verticalFov;camera.nearClipPlane=.05f;camera.farClipPlane=4000;camera.cullingMask=1;
            result.width=recipe.width;result.height=recipe.height;result.verticalFov=recipe.verticalFov;
            foreach(var map in recipe.maps)
            {
                if(result.error.Length>0)break;
                loop.SelectMap(map.id,true);end=Time.realtimeSinceStartup+150;
                while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
                try{if(!loop.Ready||loop.Map.MapId!=map.id)throw new InvalidOperationException(loop.Failure??"Matched map failed to load");loop.StartPlaying();loop.Map.BindCaptureCamera(camera);}
                catch(Exception e){result.error=e.ToString();break;}
                foreach(var shot in map.shots)
                {
                    camera.transform.position=loop.Map.transform.TransformPoint(Reflected(shot.eye));camera.transform.LookAt(loop.Map.transform.TransformPoint(Reflected(shot.target)));
                    for(int frame=0;frame<4;frame++)yield return null;
                    try{images.Add(Render(camera,recipe.width,recipe.height,map.id.Replace(':','-')+"-"+shot.name));}
                    catch(Exception e){result.error=e.ToString();break;}
                }
                loop.ReturnToLobby();
            }
            if(result.error.Length==0&&recipe.menus)
            {
                // Menu renders are actual current world UI. Their desktop camera
                // is recorded separately; they are not headset projection proof.
                loop.Rig.TrackingOrigin.localPosition=Vector3.zero;loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Head.fieldOfView=75;
                loop.ReturnToLobby();loop.UI.ShowReady();yield return null;
                try{images.Add(Render(loop.Rig.Head,1280,800,"menu"));loop.UI.OpenAppearance();}
                catch(Exception e){result.error=e.ToString();}
                end=Time.realtimeSinceStartup+30;while(loop.Preview.Loading&&Time.realtimeSinceStartup<end)yield return null;
                yield return null;
                try{images.Add(Render(loop.Rig.Head,1280,800,"outfits"));loop.UI.OpenWeapons();}
                catch(Exception e){result.error=e.ToString();}
                end=Time.realtimeSinceStartup+30;while(loop.Gallery.Loading&&Time.realtimeSinceStartup<end)yield return null;
                yield return null;
                try{images.Add(Render(loop.Rig.Head,1280,800,"weapons"));loop.UI.OpenSettings();}
                catch(Exception e){result.error=e.ToString();}
                yield return null;
                try{images.Add(Render(loop.Rig.Head,1280,800,"settings"));loop.UI.OpenWeaponSettings();}
                catch(Exception e){result.error=e.ToString();}
                yield return null;
                try{images.Add(Render(loop.Rig.Head,1280,800,"weapon-settings"));}
                catch(Exception e){result.error=e.ToString();}
            }
            UnityEngine.Object.Destroy(cameraObject);result.images=images.ToArray();R23SmokeControl.Finish(result.error.Length==0,JsonUtility.ToJson(result,true));
        }
        static string Render(Camera camera,int width,int height,string name)
        {
            string result=R23PlayableBuild.Argument("-ashfallVRSmoke");string path=Path.Combine(Path.GetDirectoryName(result),"unity-"+name+".png");
            if(File.Exists(path))throw new IOException("Preserve prior comparison image");
            var target=RenderTexture.GetTemporary(width,height,24);var previous=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)UnityEngine.Object.Destroy(pixels);}
            return path;
        }
    }
}
#endif
