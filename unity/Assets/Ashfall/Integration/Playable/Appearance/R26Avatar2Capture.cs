#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Ashfall.Integration.Playable.UI;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Core;
using Ashfall.Integration.Playable.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Ashfall.Integration.Playable.Appearance
{
    // Explicit Linux evidence hook. No component is created without this argument;
    // real XR devices refuse it. It never changes saved avatar or gameplay settings.
    public sealed class R26Avatar2Capture:MonoBehaviour
    {
        string output;StreamWriter audit;Camera capture;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string folder=R25NetBot.Arg("-ashfallAvatar2Capture");if(folder.Length==0||!R25NetBot.Requested||UnityEngine.XR.XRSettings.isDeviceActive)return;
            var hook=new GameObject("R26 opt-in AVATAR2 capture").AddComponent<R26Avatar2Capture>();hook.output=folder;
        }
        static T Field<T>(object obj,string name){return (T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);audit=new StreamWriter(Path.Combine(output,"runtime-audit.txt"));audit.AutoFlush=true;
            float deadline=Time.realtimeSinceStartup+180;R23PlayableLoop loop=null;
            while(Time.realtimeSinceStartup<deadline){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&loop.Ready&&loop.RangeLane!=null&&loop.RangeLane.Active&&loop.Appearance.Ready)break;yield return null;}
            if(loop==null||!loop.Ready||!loop.RangeLane.Active){audit.WriteLine("FAIL range ready timeout");Application.Quit(2);yield break;}
            var bot=loop.GetComponent<R25NetBot>();if(bot!=null)bot.enabled=false;loop.enabled=false;
            var mirror=loop.RangeLane.GetComponentInChildren<R26RangeMirror>();
            Vector3 feet=mirror.transform.position+mirror.transform.forward*2.4f;feet.y=mirror.transform.position.y-1.3f;
            loop.Rig.SetSpawn(feet,feet-mirror.transform.forward);Quaternion facing=Quaternion.LookRotation(-mirror.transform.forward);
            Vector3 eye=feet+Vector3.up*1.68f;
            loop.Rig.NetBotSample(Vector2.zero,1.68f,eye+facing*new Vector3(-.3f,-.5f,.22f),eye+facing*new Vector3(.3f,-.5f,.22f),facing,0,0,.016f);
            loop.Rig.Head.transform.rotation=facing*Quaternion.Euler(8,0,0);loop.Appearance.SetPresentationState(true,false);loop.Gameplay.HolsterCurrent();
            yield return new WaitForSeconds(2);
            mirror.RenderReflection(loop.Rig.Head);Save(loop.Rig.Head,"range-mirror.png");
            audit.WriteLine("Mirror renders="+mirror.RenderCount+" texture="+R26RangeMirror.TextureSize+" ownerHeadMask="+((loop.Rig.Head.cullingMask&(1<<R23PlayerAppearance.MirrorHeadLayer))!=0));
            int before=mirror.RenderCount;Quaternion eyeRotation=loop.Rig.Head.transform.rotation;Vector3 eyePosition=loop.Rig.Head.transform.position;
            loop.Rig.Head.transform.rotation=Quaternion.LookRotation(mirror.transform.forward);yield return new WaitForSeconds(.25f);audit.WriteLine("Mirror away-facing render delta="+(mirror.RenderCount-before));
            loop.Rig.Head.transform.position=mirror.transform.position+mirror.transform.forward*9;loop.Rig.Head.transform.rotation=eyeRotation;before=mirror.RenderCount;yield return new WaitForSeconds(.25f);audit.WriteLine("Mirror 9m render delta="+(mirror.RenderCount-before));loop.Rig.Head.transform.position=eyePosition;
            var cameraNode=new GameObject("AVATAR2 evidence camera");capture=cameraNode.AddComponent<Camera>();capture.enabled=false;capture.nearClipPlane=.02f;capture.farClipPlane=80;capture.fieldOfView=45;capture.cullingMask=1<<31;capture.backgroundColor=new Color(.12f,.16f,.2f);capture.clearFlags=CameraClearFlags.SolidColor;capture.GetUniversalAdditionalCameraData().allowXRRendering=false;
            var anchor=new GameObject("AVATAR2 diagnostic podium anchor").transform;anchor.position=feet+facing*new Vector3(4,0,0);
            var preview=new GameObject("AVATAR2 actual menu preview").AddComponent<R23AppearancePreview>();preview.Initialize(loop.Rig.Head.transform);preview.SetWorldAnchor(anchor,false);preview.RotationMultiplier=0;
            foreach(string style in new[]{"cinder_scout","obsidian","claude"})
            {
                preview.Show(style);float until=Time.realtimeSinceStartup+30;while((!preview.Ready||preview.Loading||preview.SelectedStyleId!=style)&&Time.realtimeSinceStartup<until)yield return null;
                if(!preview.Ready||preview.Loading||preview.SelectedStyleId!=style){audit.WriteLine("FAIL preview "+style+" "+preview.Failure);continue;}
                var avatar=Field<Transform>(preview,"avatar");var appearance=Field<R23PlayerAppearance>(preview,"appearance");Vector3[] reference=null;
                foreach(var node in Field<Transform>(preview,"fixture").GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
                foreach(var node in appearance.CurrentVisualRoot.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
                foreach(int angle in new[]{0,90,180,270})
                {
                    avatar.rotation=Quaternion.Euler(0,angle,0);for(int j=0;j<80;j++)yield return null;
                    capture.transform.SetPositionAndRotation(anchor.position+new Vector3(0,1.25f,3.7f),Quaternion.LookRotation(new Vector3(0,-.22f,-3.7f)));
                    Save(capture,"pedestal-"+style+"-"+angle+".png");
                    var points=ArmPoints(appearance,avatar);if(reference==null)reference=points;float error=0;for(int j=0;j<points.Length;j++)error=Mathf.Max(error,Vector3.Distance(reference[j],points[j]));
                    audit.WriteLine(style+" yaw="+angle+" bodyRelativeArmMaxMetres="+error.ToString("G9")+" vertices="+points.Length);
                }
                avatar.rotation=Quaternion.identity;var hand=Field<Transform>(preview,"right");Quaternion neutral=hand.localRotation;
                foreach(int bend in new[]{0,45,65})
                {
                    hand.localRotation=neutral*Quaternion.Euler(bend,0,0);for(int j=0;j<80;j++)yield return null;
                    Transform arm=appearance.CurrentVisualRoot.Find("R continuous arm");var vertices=arm.GetComponent<MeshFilter>().sharedMesh.vertices;
                    int first=R26AvatarArmSurface.WristStart*R26AvatarArmSurface.Stride;
                    Bounds bounds=new Bounds(arm.TransformPoint(vertices[first]),Vector3.zero);
                    for(int v=first+1;v<R26AvatarArmSurface.SleeveEnd;v++)bounds.Encapsulate(arm.TransformPoint(vertices[v]));
                    capture.fieldOfView=35;capture.transform.SetPositionAndRotation(bounds.center+new Vector3(.15f,.06f,.53f),Quaternion.LookRotation(new Vector3(-.15f,-.06f,-.53f)));
                    Save(capture,"wrist-"+style+"-"+bend+".png");capture.fieldOfView=45;
                }
                hand.localRotation=neutral;
            }
            var spinning=Field<Transform>(preview,"avatar");preview.RotationMultiplier=3;Quaternion startRotation=spinning.rotation;float startTime=Time.unscaledTime;yield return new WaitForSeconds(1);float spinSeconds=Time.unscaledTime-startTime;
            audit.WriteLine("Pedestal measured degrees/second="+(Quaternion.Angle(startRotation,spinning.rotation)/spinSeconds).ToString("G9")+" expected="+(.81f*Mathf.Rad2Deg).ToString("G9"));preview.Hide();
            var remote=new GameObject("AVATAR2 actual remote replica").AddComponent<R24LanRemoteAvatar>();Vector3 remoteAt=anchor.position;remote.Initialize("avatar2","REMOTE ROTATION CHECK",capture,RemotePose(remoteAt,0));
            float remoteDeadline=Time.realtimeSinceStartup+30;while(!remote.Ready&&Time.realtimeSinceStartup<remoteDeadline)yield return null;
            if(!remote.Ready)audit.WriteLine("FAIL remote ready "+remote.Failure);
            else
            {
                foreach(var node in remote.transform.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
                foreach(var node in remote.Appearance.CurrentVisualRoot.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
                Vector3[] reference=null;
                foreach(int angle in new[]{0,90,180,270})
                {
                    remote.Receive(RemotePose(remoteAt,angle));for(int j=0;j<100;j++)yield return null;
                    capture.transform.SetPositionAndRotation(anchor.position+new Vector3(0,1.25f,3.7f),Quaternion.LookRotation(new Vector3(0,-.22f,-3.7f)));Save(capture,"remote-turn-"+angle+".png");
                    var points=ArmPoints(remote.Appearance,remote.transform);if(reference==null)reference=points;float error=0;for(int j=0;j<points.Length;j++)error=Mathf.Max(error,Vector3.Distance(reference[j],points[j]));audit.WriteLine("Remote replica yaw="+angle+" bodyRelativeArmMaxMetres="+error.ToString("G9"));
                }
            }
            audit.WriteLine("DONE");audit.Dispose();audit=null;Application.Quit(0);
        }
        static R24LanTransform Pose(Vector3 position,Quaternion q){return new R24LanTransform(new Float3(position.x,position.y,position.z),new R24LanRotation(q.x,q.y,q.z,q.w));}
        static R24LanPose RemotePose(Vector3 at,float angle)
        {
            Quaternion q=Quaternion.Euler(0,angle,0),hand=q*Quaternion.LookRotation(Vector3.down,Vector3.forward);
            return new R24LanPose("menu",0,15,Pose(at,q),Pose(at+Vector3.up*1.68f,q),Pose(at+q*new Vector3(-.3f,1.15f,.25f),hand),Pose(at+q*new Vector3(.3f,1.15f,.25f),hand),new Float3(),0,0);
        }
        static Vector3[] ArmPoints(R23PlayerAppearance appearance,Transform avatar)
        {
            var points=new System.Collections.Generic.List<Vector3>();
            foreach(var filter in appearance.CurrentVisualRoot.GetComponentsInChildren<MeshFilter>())
                if(filter.name.Contains("Arm")||filter.name.Contains("Forearm")||filter.name.Contains("continuous arm"))foreach(var point in filter.sharedMesh.vertices)points.Add(avatar.InverseTransformPoint(filter.transform.TransformPoint(point)));
            return points.ToArray();
        }
        void Save(Camera camera,string name)
        {
            var target=new RenderTexture(1280,960,24);target.Create();var prior=RenderTexture.active;var pixels=new Texture2D(1280,960,TextureFormat.RGB24,false);
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,960),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());audit.WriteLine("capture "+name);}
            finally{RenderTexture.active=prior;target.Release();Destroy(target);Destroy(pixels);}
        }
        void OnDestroy(){if(audit!=null)audit.Dispose();}
    }
}
#endif
