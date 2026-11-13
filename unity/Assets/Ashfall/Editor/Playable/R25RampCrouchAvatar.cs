#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Networking;
using UnityEngine;

namespace Ashfall.Editor.Playable
{
    public static class R25RampCrouchAvatar
    {
        [Serializable] sealed class Receipt
        {
            public bool nativeExecuted=true,actualRemoteAvatarRendered=true,networkPeerOpened=false;
            public int codecRoundTrips=3;
            public float[] eyeHeights=new float[3],poseErrors=new float[3];
        }
        static R24LanTransform Transform(Transform value)
        {
            Vector3 p=value.position;Quaternion q=value.rotation;
            return new R24LanTransform(new Float3(p.x,p.y,p.z),new R24LanRotation(q.x,q.y,q.z,q.w));
        }
        public static R24LanPose Pose(R23VRRig rig)
        {
            return new R24LanPose("menu",0,9,Transform(rig.transform),Transform(rig.Head.transform),Transform(rig.Left),Transform(rig.Right),new Float3(),0,0);
        }
        static R24LanTransform Shift(R24LanTransform value,Float3 shift)
        {
            return new R24LanTransform(new Float3(value.Position.X+shift.X,value.Position.Y+shift.Y,value.Position.Z+shift.Z),value.Rotation);
        }
        public static IEnumerator Run(List<R24LanPose> poses,string output)
        {
            if(poses.Count!=3)throw new InvalidOperationException("Standing/crouched/returned native poses required");
            var center=new Vector3(6000,0,6000);
            var cam=new GameObject("Native remote-avatar inspection camera").AddComponent<Camera>();cam.enabled=false;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.06f,.08f,.12f);cam.farClipPlane=30;cam.nearClipPlane=.05f;
            cam.transform.position=center+new Vector3(2.3f,1.65f,-2.5f);cam.transform.LookAt(center+Vector3.up*.85f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=center-Vector3.up*.1f;floor.transform.localScale=new Vector3(6,.2f,6);
            var avatar=new GameObject("Actual LAN remote avatar without a network peer").AddComponent<R24LanRemoteAvatar>();
            var receipt=new Receipt();
            try
            {
                for(int i=0;i<poses.Count;i++)
                {
                    R24LanPose input=poses[i],decoded;
                    if(!R24LanPose.TryDecode(input.Encode(),out decoded))throw new InvalidOperationException("Existing render codec rejected crouched pose");
                    Float3 delta=new Float3(center.x-decoded.Root.Position.X,center.y-decoded.Root.Position.Y,center.z-decoded.Root.Position.Z);
                    var shifted=new R24LanPose(decoded.Scene,decoded.Style,decoded.Flags,Shift(decoded.Root,delta),Shift(decoded.Head,delta),Shift(decoded.Left,delta),Shift(decoded.Right,delta),decoded.Velocity,0,0);
                    if(i==0)avatar.Initialize("native-crouch-probe","",cam,shifted);else avatar.Receive(shifted);
                    float until=Time.realtimeSinceStartup+90;
                    while(!avatar.Ready&&avatar.Failure==null&&Time.realtimeSinceStartup<until)yield return null;
                    if(!avatar.Ready||avatar.Failure!=null)throw new InvalidOperationException("Actual avatar failed: "+avatar.Failure);
                    // Unscaled time in an uncapped batch run can be very small.
                    // Verify the actual interpolation, rather than assuming a
                    // fixed frame count has reached the transmitted pose.
                    var head=avatar.transform.Find("Remote head");
                    Vector3 wanted=new Vector3(shifted.Head.Position.X,shifted.Head.Position.Y,shifted.Head.Position.Z);
                    until=Time.realtimeSinceStartup+5;
                    while(Vector3.Distance(head.position,wanted)>.0005f&&Time.realtimeSinceStartup<until)yield return null;
                    if(Vector3.Distance(head.position,wanted)>.001f)throw new InvalidOperationException("Avatar pose did not converge");
                    receipt.eyeHeights[i]=head.position.y-avatar.transform.position.y;
                    receipt.poseErrors[i]=Vector3.Distance(head.position,wanted);
                    string name=i==0?"standing":i==1?"crouched":"returned";
                    yield return new R25HeadclipNative.Audit{Run=delegate {
                        R25RampCrouchVideo.Render(cam,Path.Combine(output,"avatar-"+name+".png"));
                        File.WriteAllBytes(Path.Combine(output,"avatar-"+name+"-wire.bin"),input.Encode());
                    }};
                }
                File.WriteAllText(Path.Combine(output,"avatar-result.json"),JsonUtility.ToJson(receipt,true));
            }
            finally {UnityEngine.Object.Destroy(avatar.gameObject);UnityEngine.Object.Destroy(cam.gameObject);UnityEngine.Object.Destroy(floor);}
        }
    }
}
#endif
