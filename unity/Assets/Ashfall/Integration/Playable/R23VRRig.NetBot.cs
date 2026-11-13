#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.Player;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        // Synthetic device samples share the normal motor and network publishers.
        // Only an explicit command line can enable this; an active XR device refuses it.
        public void NetBotSample(Vector2 move, float height, Vector3 left, Vector3 right,
            Quaternion rightRotation, float grip, float trigger, float seconds)
        {
            if (!R25NetBot.Requested || UnityEngine.XR.XRSettings.isDeviceActive)
                throw new InvalidOperationException("Netbot requires explicit opt-in and XR disabled");
            enabled = false;
            Tracking = SessionFocused = DisplayRunning = true;
            Paused = false;
            Head.transform.localPosition = new Vector3(0, height, 0);
            Head.transform.localRotation = Quaternion.identity;
            Left.position = left; Right.position = right; Right.rotation = rightRotation;
            Left.rotation = transform.rotation;
            LeftInput = new HandSample { Tracked = true, Position = F(transform.InverseTransformPoint(left)), StickX = move.x, StickY = move.y };
            RightInput = new HandSample { Tracked = true, Position = F(transform.InverseTransformPoint(right)), Grip = grip, Trigger = trigger };
            LeftAimTracked = RightAimTracked = true;
            LeftAimPosition = Left.position; RightAimPosition = Right.position;
            LeftAimRotation = Left.rotation; RightAimRotation = Right.rotation;
            if (UseSourceHeadHull && !CarrierActive) SynchronizeSourceHead();
            if (Calibration != null) Calibration.Tick(true, true, false, seconds);
            Advance(R23InputProjection.Sample(true, true,
                F(transform.InverseTransformPoint(Head.transform.position)), LeftInput, RightInput, MotionPaused, PrimaryHand), seconds);
        }
    }
}
#endif

