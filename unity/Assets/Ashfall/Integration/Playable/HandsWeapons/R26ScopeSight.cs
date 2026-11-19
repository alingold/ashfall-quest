#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.XR;

namespace Ashfall.Integration.Playable.HandsWeapons
{
    internal static class R26ScopeSight
    {
        // XR view matrices can belong to the last rendered frame. Use their separation
        // with the live head pose so a rig lift or a seated lean doesn't shift the eye box.
        internal static Vector3 EyeOffset(Camera head)
        {
            if(head.stereoEnabled){
                Vector3 left=head.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.GetColumn(3);
                Vector3 right=head.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse.GetColumn(3);
                float separation=Vector3.Distance(left,right);
                if(separation>.035f&&separation<.095f)return head.transform.right*(separation*.5f);
            }
            if(!XRSettings.isDeviceActive)return Vector3.zero;
            var hmd=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            Vector3 leftEye,rightEye;
            if(hmd.TryGetFeatureValue(CommonUsages.leftEyePosition,out leftEye)&&hmd.TryGetFeatureValue(CommonUsages.rightEyePosition,out rightEye)){
                float separation=Vector3.Distance(leftEye,rightEye);
                if(separation>.035f&&separation<.095f)return head.transform.right*(separation*.5f);
            }
            return head.transform.right*(Mathf.Clamp(head.stereoSeparation,.04f,.09f)*.5f);
        }

        internal static bool Viewing(Transform window,Transform rear,Transform front,Vector3 target,
            Vector3 center,Vector3 eyeOffset,Vector3 gaze,bool retained)
        {
            Transform opening=window!=null?window:rear;
            if(opening==null||(window==null&&front==null))return false;
            Vector3 axis=window!=null?window.forward:(front.position-rear.position).normalized;
            // Head direction is only a facing check. The player can rotate their eyes.
            if(Vector3.Dot(gaze,axis)<(retained?.74f:.82f))return false;
            return ThroughEye(opening,window!=null,axis,center-eyeOffset,target,retained)
                ||(eyeOffset.sqrMagnitude>0&&ThroughEye(opening,window!=null,axis,center+eyeOffset,target,retained));
        }

        static bool ThroughEye(Transform opening,bool window,Vector3 axis,Vector3 eye,Vector3 target,bool retained)
        {
            Vector3 offset=opening.position-eye;
            float relief=Vector3.Dot(offset,axis);
            if(relief<.015f||relief>(retained?.95f:.90f))return false;
            float margin=retained?.016f:.006f;
            if(!window){float radius=.024f+margin;return (offset-axis*relief).sqrMagnitude<=radius*radius;}
            Vector3 ray=target-eye;
            float depth=Vector3.Dot(ray,axis);
            if(depth<=relief)return false;
            // Same eye-to-dot intersection as the material, with a tracking margin in metres.
            Vector3 hit=eye+ray*(relief/depth)-opening.position;
            Vector3 scale=opening.lossyScale;
            return Mathf.Abs(Vector3.Dot(hit,opening.right))<=Mathf.Abs(scale.x)*.5f+margin
                &&Mathf.Abs(Vector3.Dot(hit,opening.up))<=Mathf.Abs(scale.y)*.5f+margin;
        }
    }
}
#endif
