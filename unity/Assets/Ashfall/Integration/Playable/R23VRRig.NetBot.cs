#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.Player;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        // WIN2: headset realism for the scripted route bot. Null (the default) keeps
        // the plain sample: a head that never moves physically, no render-time pose
        // and no recovery tick, which is why r12's bot passed windows a player could not.
        public sealed class NetBotHeadsetProfile
        {
            public Vector3 Offset;     // physical head motion in tracking space this frame (sway, lean), metres
            public Vector3 LateOffset; // further motion of the render-time pose
            public bool Late = true, Recovery = true;
        }
        public static NetBotHeadsetProfile NetBotHeadset;
        // FLY4: a scripted body pose (arms spread) and, like a headset whose cameras lose a controller,
        // hands that are no longer optically tracked while their poses stay valid.
        public delegate void NetBotPoseHook(R23VRRig rig, ref Vector2 move, ref Vector3 left, ref Vector3 right, ref bool tracked);
        public static NetBotPoseHook NetBotPose;
        public static bool NetBotInferredHands;
        public string NetBotDiagnostics
        {
            get
            {
                var c = System.Globalization.CultureInfo.InvariantCulture;
                return "eye=" + (Head.transform.position.y - transform.position.y).ToString("F3", c) + " crouch=" + crouch.ToString("F3", c)
                    + " grounded=" + grounded + " hold=" + HoldPhysicalSupport + " blocked=" + HeadClearanceBlocked + " physHold=" + physicalSupportHold
                    + " lead=" + Vector3.ProjectOnPlane(Head.transform.position - transform.position, Vector3.up).magnitude.ToString("F3", c)
                    + " headClear=" + HeadCollision.HeadClear(Head.transform.position) + " eyeWithin5cm=" + HeadCollision.EyeObstructed(Head.transform.position, .05f);
            }
        }
        // Synthetic device samples share the normal motor and network publishers.
        // Only an explicit command line can enable this; an active XR device refuses it.
        public void NetBotSample(Vector2 move, float height, Vector3 left, Vector3 right,
            Quaternion rightRotation, float grip, float trigger, float seconds, float leftGrip = 0, Quaternion? leftRotation = null)
        {
            if (!R25NetBot.Requested || UnityEngine.XR.XRSettings.isDeviceActive)
                throw new InvalidOperationException("Netbot requires explicit opt-in and XR disabled");
            enabled = false;
            Tracking = SessionFocused = DisplayRunning = true;
            Paused = false;
            var headset = NetBotHeadset;
            Head.transform.localPosition = headset == null ? new Vector3(0, height, 0) : new Vector3(headset.Offset.x, height + headset.Offset.y, headset.Offset.z);
            Head.transform.localRotation = Quaternion.identity;
            bool tracked = true; if (NetBotPose != null) NetBotPose(this, ref move, ref left, ref right, ref tracked);
            NetBotInferredHands = !tracked;
            Left.position = left; Right.position = right; Right.rotation = rightRotation;
            Left.rotation = leftRotation ?? transform.rotation;
            LeftInput = new HandSample { Tracked = tracked, Position = F(transform.InverseTransformPoint(left)), Grip = leftGrip, StickX = move.x, StickY = move.y };
            RightInput = new HandSample { Tracked = tracked, Position = F(transform.InverseTransformPoint(right)), Grip = grip, Trigger = trigger };
            LeftAimTracked = RightAimTracked = true;
            LeftAimPosition = Left.position; RightAimPosition = Right.position;
            LeftAimRotation = Left.rotation; RightAimRotation = Right.rotation;
            if (UseSourceHeadHull && !CarrierActive) SynchronizeSourceHead();
            if (Calibration != null) Calibration.Tick(true, true, false, seconds);
            Advance(R23InputProjection.Sample(true, true,
                F(transform.InverseTransformPoint(Head.transform.position)), LeftInput, RightInput, MotionPaused, PrimaryHand), seconds);
            if (headset == null) return;
            // The rest of a headset frame: Update's recovery tick, then the before-render pose.
            if (headset.Recovery) TickHeadRecovery();
            if (headset.Late) { Head.transform.localPosition += headset.LateOffset; SynchronizeLateSourceHead(); }
        }
    }
}
#endif

