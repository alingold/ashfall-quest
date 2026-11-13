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
    public static class R25ForcefieldLookCapture
    {
        const string Key="Ashfall.ForcefieldLook.Output";
        static Stack<IEnumerator> work;
        static float deadline;
        static int lastFrame=-1;
        static R25ForcefieldLookCapture(){EditorApplication.playModeStateChanged+=Changed;}
        public static void Run()
        {
            string output=R23PlayableBuild.Argument("-ashfallForcefieldLookOutput");
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
        [Serializable] sealed class AuditBlock { public float[] minimum=null,maximum=null; }
        [Serializable] sealed class AuditMap { public string map="";public AuditBlock[] blocks=null; }
        [Serializable] sealed class AuditRows { public AuditMap[] rows=null; }
        static IEnumerator Capture()
        {
            R23PlayableLoop root=null;
            while(root==null||!root.Ready){root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(root!=null)Need(root.Failure==null,root.Failure);yield return null;}
            root.ManualSmoke=true;root.ExternalProbePump=true;root.Rig.enabled=false;root.Gameplay.ExternalInputPump=true;root.Calibration.SuppressPersistence=true;
            string output=SessionState.GetString(Key,"");
            foreach(string slug in new[]{"flash-gods-pitdevil","candle-magic-gods-goodwitch"})
            {
                root.ReturnToLobby();root.SelectMap("archive:"+slug,false);
                while(!root.Ready){Need(root.Failure==null,root.Failure);yield return null;}
                root.StartPlaying();while(root.SourceArchiveEntryPending){root.SmokeUiUpdate();yield return null;}
                Need(String.IsNullOrEmpty(root.SourceArchiveEntryFailure),root.SourceArchiveEntryFailure);
                while(!root.ArchiveWorldReady){root.PrepareArchiveWorld(true);yield return null;}
                Physics.SyncTransforms();
                var walls=new List<Renderer>();var after=new List<Material>();var before=new List<Material>();
                float best=0;
                foreach(var r in root.Map.GetComponentsInChildren<Renderer>())
                {
                    var m=r.sharedMaterial;if(m==null||m.name!="pb8a19331")continue;Need(!ShaderUtil.ShaderHasError(m.shader),"Field shader compile");
                    walls.Add(r);after.Add(m);
                    var old=new Material(Resources.Load<Shader>("R23PlayableWorld"));old.SetTexture("_BaseMap",m.GetTexture("_BaseMap"));old.SetFloat("_Cutoff",.4f);old.SetFloat("_Roughness",.72f);before.Add(old);

                }
                // Native chunks can contain two perpendicular walls. Use the audited connected
                // boundary component bounds instead of aiming at an empty chunk-bounds corner.
                string auditPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../../maintenance/20261110-playtest/features/forcefield-spawns-policy.json"));
                var audit=JsonUtility.FromJson<AuditRows>("{\"rows\":"+File.ReadAllText(auditPath)+"}");
                Bounds bounds=new Bounds();
                foreach(var row in audit.rows)if(row.map=="archive:"+slug)foreach(var block in row.blocks)
                {
                    Vector3 lo=new Vector3(block.minimum[0],block.minimum[1],block.minimum[2]);
                    Vector3 hi=new Vector3(block.maximum[0],block.maximum[1],block.maximum[2]);
                    var b=new Bounds((lo+hi)*.5f,hi-lo);float length=Mathf.Max(b.size.x,b.size.z);
                    if(b.size.y>20 && Mathf.Min(b.size.x,b.size.z)<3 && length>best){bounds=b;best=length;}
                }
                Need(best>0 && walls.Count>0,"Audited native boundary present: "+slug);
                Vector3 localCenter=root.Map.transform.InverseTransformPoint(root.Map.Bounds.center);Vector3 inward;
                if(bounds.size.x<bounds.size.z)inward=new Vector3(Mathf.Sign(localCenter.x-bounds.center.x),0,0);
                else inward=new Vector3(0,0,Mathf.Sign(localCenter.z-bounds.center.z));
                Vector3 surface=Vector3.zero;bool hitField=false;
                // Find an unobstructed interior sightline: huge lettering/candles can hide a wall.
                foreach(float height in new[]{.5f,.75f,.25f})
                {
                    foreach(float along in new[]{0f,-.3f,.3f})
                    {
                        Vector3 candidate=bounds.center+Vector3.Scale(inward,bounds.extents);candidate.y=bounds.min.y+bounds.size.y*height;
                        if(bounds.size.x<bounds.size.z)candidate.z+=bounds.size.z*along;else candidate.x+=bounds.size.x*along;
                        candidate=root.Map.transform.TransformPoint(candidate);
                        Vector3 direction=root.Map.transform.TransformDirection(inward).normalized;
                        float nearest=Single.MaxValue;RaycastHit first=new RaycastHit();
                        foreach(var hit in Physics.RaycastAll(candidate+direction*25,-direction,30,~0,QueryTriggerInteraction.Ignore))
                            if(hit.distance<nearest){nearest=hit.distance;first=hit;}
                        if(nearest==Single.MaxValue)continue;
                        var r=first.collider.GetComponent<Renderer>();
                        if(r!=null && r.sharedMaterial!=null && r.sharedMaterial.name=="pb8a19331")
                        {surface=first.point;hitField=true;break;}
                    }
                    if(hitField)break;
                }
                inward=root.Map.transform.TransformDirection(inward).normalized;
                Need(hitField,"Unobstructed view to actual field collider: "+slug);
                var cameraObject=new GameObject("Forcefield look audit camera");var cam=cameraObject.AddComponent<Camera>();cam.fieldOfView=70;cam.nearClipPlane=.05f;cam.farClipPlane=2500;cam.clearFlags=root.Rig.Head.clearFlags;cam.backgroundColor=root.Rig.Head.backgroundColor;
                File.AppendAllText(Path.Combine(output,"capture.txt"),slug+" wall="+"audited component"+" bounds="+bounds+" surface="+surface.ToString("F4")+" inward="+inward+" speed=0.2m/s repeat=12m opacity=0.22 to 0.52 cull=back\n");
                foreach(float distance in new[]{1f,25f})
                {
                    cam.transform.SetPositionAndRotation(surface+inward*distance,Quaternion.LookRotation(-inward));
                    string prefix=Path.Combine(output,slug+"-"+distance.ToString("0")+"m");
                    for(int i=0;i<walls.Count;i++)walls[i].sharedMaterial=before[i];
                    Save(cam,prefix+"-before.png");
                    for(int i=0;i<walls.Count;i++){walls[i].sharedMaterial=after[i];after[i].SetFloat("_CaptureTime",0);}
                    Save(cam,prefix+"-after-t0.png");
                    foreach(var m in after)m.SetFloat("_CaptureTime",1);
                    Save(cam,prefix+"-after-t1.png");
                    foreach(var r in walls)r.enabled=false;
                    Save(cam,prefix+"-field-hidden-control.png");
                    foreach(var r in walls)r.enabled=true;
                }
                if(slug.StartsWith("flash"))
                {
                    string frames=Path.Combine(output,"scroll-frames");Directory.CreateDirectory(frames);
                    cam.transform.SetPositionAndRotation(surface+inward*4,Quaternion.LookRotation(-inward));
                    // Native consecutive renders at a deterministic 30 Hz clock: no interpolated frames.
                    for(int frame=0;frame<150;frame++)
                    {
                        foreach(var m in after)m.SetFloat("_CaptureTime",frame/30f);
                        Save(cam,Path.Combine(frames,frame.ToString("D4")+".png"));yield return null;
                    }
                }
                foreach(var m in after)m.SetFloat("_CaptureTime",-1);
                foreach(var m in before)UnityEngine.Object.Destroy(m);
                UnityEngine.Object.Destroy(cameraObject);
            }
            File.WriteAllText(Path.Combine(output,"success.txt"),"Two maps, matched native before/after at 1m and 25m, t=0/1 pair and hidden-field control. 150 consecutive native scroll frames at 30Hz.\n");root.ReturnToLobby();
        }
        static void Save(Camera camera,string path)
        {
            var target=RenderTexture.GetTemporary(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);if(pixels!=null)UnityEngine.Object.Destroy(pixels);}
        }
    }
}
#endif
