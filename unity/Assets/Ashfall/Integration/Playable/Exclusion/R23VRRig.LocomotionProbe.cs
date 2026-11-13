#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.Player;
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        public Action<string, Vector3, Vector3> LocomotionWrite;
        public bool LocomotionProbePaused;
        bool probeFocused = true, probePaused;

        public Vector3 LocomotionProbeCrossedWall(Vector3 clearEye, Vector3 crossedEye)
        {
            previousSourceHead = clearEye; haveSourceHead = true;
            return LevelStandingWalk(crossedEye, Vector3.zero, Vector3.zero);
        }

        // Same pose/calibration/input/recovery order as Update and LateUpdate.
        // Focus and pause call the production callbacks, with no XR device.
        public void LocomotionProbeSample(HandSample left, HandSample right, float dt, Vector3 rawHead)
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive) throw new InvalidOperationException("XR-off fixture required");
            enabled = false; Tracking = DisplayRunning = true; SessionFocused = !FixtureUnfocused;
            headclipProbeClock += dt;
            if (probeFocused != SessionFocused) { OnApplicationFocus(SessionFocused); probeFocused = SessionFocused; }
            if (probePaused != LocomotionProbePaused) { OnApplicationPause(LocomotionProbePaused); probePaused = LocomotionProbePaused; }
            Paused = LocomotionProbePaused;
            Head.transform.localPosition = rawHead; Head.transform.localRotation = Quaternion.identity;
            LeftInput = left; RightInput = right; Left.localPosition = U(left.Position); Right.localPosition = U(right.Position);
            if (UseSourceHeadHull && !CarrierActive) SynchronizeSourceHead();
            if (Calibration != null) Calibration.Tick(true, SessionFocused, Paused, dt);
            TraceLocomotion("calibration");
            Advance(R23InputProjection.Sample(SessionFocused, true, F(transform.InverseTransformPoint(Head.transform.position)),
                left, right, MotionPaused, PrimaryHand), dt);
            if (FixtureRunsRecovery) TickHeadRecovery();
            SynchronizeLateSourceHead();
            if (ViewFade != null) { ViewFade.NativeProbeSeconds = dt; ViewFade.NativeProbeRenderGuard(); }
        }
    }
}
#endif
