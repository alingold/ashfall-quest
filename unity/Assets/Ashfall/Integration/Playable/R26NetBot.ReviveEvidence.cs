#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R25NetBot
    {
        bool pairProfile;
        internal void SampleRevivePair(bool approach,Vector3 facing,Vector3? touch=null,bool spin=false)
        {
            float now=Time.realtimeSinceStartup;
            if(!pairProfile){if(!ReadRouteHeadset())throw new System.ArgumentException("Invalid revive headset profile");pairProfile=true;playBegan=now;}
            if(routeSeated>0&&!routeSeatedDone&&now-playBegan>=1.2f){root.Rig.Calibration.RequestRecalibration();routeSeatedDone=true;}
            if(facing.sqrMagnitude>.0001f)root.Rig.transform.rotation=Quaternion.LookRotation(facing);
            Vector2 stick=Vector2.zero;string step=null;if(approach)AiHelpAlly(root.LanSession.Lobby,now,ref stick,ref step);
            RouteHeadset(now,stick!=Vector2.zero);
            var rig=root.Rig;Vector3 eye=rig.TrackingOrigin.TransformPoint(Vector3.up*routeHeight);
            var yaw=rig.transform.rotation;var left=eye+yaw*new Vector3(-.3f,-.35f,.3f);var right=touch??(eye+yaw*new Vector3(.3f,-.35f,.3f));
            var leftRotation=yaw;var rightRotation=yaw;
            if(spin){
                float angle=now*8;var orbit=yaw*new Vector3(0,Mathf.Sin(angle)*.11f,Mathf.Cos(angle)*.11f);
                left+=orbit;right-=orbit;
                var up=yaw*(Quaternion.AngleAxis(angle*Mathf.Rad2Deg,Vector3.right)*Vector3.up);
                var grip=Quaternion.Inverse(HandsWeapons.R23TrackedGripPose.LocalRotation);
                leftRotation=Quaternion.LookRotation(yaw*Vector3.right,up)*grip;
                rightRotation=Quaternion.LookRotation(yaw*Vector3.left,up)*grip;
            }
            rig.NetBotSample(stick,routeHeight,left,right,rightRotation,0,0,Mathf.Clamp(Time.unscaledDeltaTime,.001f,.1f),0,leftRotation);
            if(routeTrace||routeStereo)ObserveWindowRoute(now);
        }
    }
}
#endif
