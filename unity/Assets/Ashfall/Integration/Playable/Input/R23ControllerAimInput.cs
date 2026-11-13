#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;
using InputDevice=UnityEngine.InputSystem.InputDevice;
using InputSystem=UnityEngine.InputSystem.InputSystem;
using XRController=UnityEngine.InputSystem.XR.XRController;
namespace Ashfall.Integration.Playable.Input
{
    // Installed OpenXR1.18 Oculus/Meta Touch layout: devicePose=grip, pointer/aimPose=aim.
    // pointer's own tracking flags and pose are read; grip/deviceRotation is never substituted.
    // Returns Unity stage-local tracking-space values. Root applies TrackingOrigin exactly once.
    public static class R23ControllerAimInput
    {
        sealed class Controls
        {
            internal InputDevice Device;
            internal Vector3Control Position;internal QuaternionControl Rotation;
            internal ButtonControl Tracked,Menu;internal IntegerControl Tracking;
            internal void Bind(InputDevice device)
            {
                Device=device;Position=null;Rotation=null;Tracked=null;Tracking=null;Menu=null;if(device==null)return;
                foreach(string path in new[]{"pointer","aimPose"}) {
                    var position=device.TryGetChildControl<Vector3Control>(path+"/position");var rotation=device.TryGetChildControl<QuaternionControl>(path+"/rotation");
                    var tracked=device.TryGetChildControl<ButtonControl>(path+"/isTracked");var tracking=device.TryGetChildControl<IntegerControl>(path+"/trackingState");
                    if(position!=null&&rotation!=null&&tracked!=null&&tracking!=null){Position=position;Rotation=rotation;Tracked=tracked;Tracking=tracking;break;}
                }
                foreach(string name in new[]{"menu","menuButton","systemButton"}){Menu=device.TryGetChildControl<ButtonControl>(name);if(Menu!=null)break;}
                if(Menu==null)foreach(var control in device.allControls){var button=control as ButtonControl;if(button==null)continue;foreach(var usage in button.usages)if(usage.ToString()=="MenuButton"){Menu=button;break;}if(Menu!=null)break;}
            }
        }
        static readonly Controls[] controls={new Controls(),new Controls()};
        static int Hand(XRNode node){return node==XRNode.LeftHand?0:node==XRNode.RightHand?1:-1;}
        static InputDevice Preferred(int hand){return hand==0?XRController.leftHand:XRController.rightHand;}
        static bool Available(InputDevice device){return device!=null&&device.added&&device.enabled;}
        static bool Matches(InputDevice device,int hand){if(!Available(device))return false;string wanted=hand==0?"LeftHand":"RightHand";foreach(var usage in device.usages)if(usage.ToString()==wanted)return true;return false;}
        static Controls Bind(int hand,InputDevice device){var value=controls[hand];if(value.Device!=device)value.Bind(device);return value;}
        static bool ReadAim(int hand,InputDevice device,out Vector3 position,out Quaternion rotation)
        {
            position=Vector3.zero;rotation=Quaternion.identity;if(!Available(device))return false;var value=Bind(hand,device);
            if(value.Position==null||value.Rotation==null||value.Tracked==null||value.Tracking==null)return false;
            var p=value.Position.ReadValue();var q=value.Rotation.ReadValue();R23AimPoseSample normalized;
            if(!R23AimPoseRules.TryNormalize(new R23AimPoseSample(value.Tracked.isPressed,value.Tracking.ReadValue(),p.x,p.y,p.z,q.x,q.y,q.z,q.w),out normalized))return false;
            position=new Vector3(normalized.X,normalized.Y,normalized.Z);rotation=new Quaternion(normalized.Qx,normalized.Qy,normalized.Qz,normalized.Qw);return true;
        }
        public static bool TryReadAim(XRNode node,out Vector3 stagePosition,out Quaternion stageRotation)
        {
            stagePosition=Vector3.zero;stageRotation=Quaternion.identity;int hand=Hand(node);if(hand<0)return false;var preferred=Preferred(hand);
            if(ReadAim(hand,preferred,out stagePosition,out stageRotation))return true;
            foreach(var device in InputSystem.devices)if(device!=preferred&&Matches(device,hand)&&ReadAim(hand,device,out stagePosition,out stageRotation))return true;
            return false;
        }
        static bool Menu(int hand,InputDevice device){if(!Available(device))return false;var value=Bind(hand,device);return value.Menu!=null&&value.Menu.isPressed;}
        public static bool TryReadMenu(XRNode node)
        {
            int hand=Hand(node);if(hand<0)return false;var preferred=Preferred(hand);
            // Menu is an explicit button action, not conditional on pose tracking. The installed
            // Touch profile maps left menu to /input/menu/click; right system may be OS-reserved.
            if(Menu(hand,preferred))return true;
            foreach(var device in InputSystem.devices)if(device!=preferred&&Matches(device,hand)&&Menu(hand,device))return true;
            var legacy=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);bool pressed;
            return legacy.isValid&&legacy.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton,out pressed)&&pressed;
        }
    }
}
#endif
