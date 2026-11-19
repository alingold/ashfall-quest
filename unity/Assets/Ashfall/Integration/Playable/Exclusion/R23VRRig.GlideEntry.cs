#if UNITY_6000_0_OR_NEWER
using Ashfall.Modules.Flight;
using UnityEngine;
using UnityEngine.XR;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        void GlideEntryFacts(ref Ashfall.Core.InputFrame frame, Vector3 travel)
        {
            if (!frame.Tracked || frame.Paused) return;
            if (!frame.HandsTracked && FlightPosesInferred()) frame.HandsTracked = true;
            if (!grounded || travel.sqrMagnitude < .25f || !UseSourceHeadHull || HeadCollision == null || !HeadCollision.Ready) return;
            RaycastHit floor;
            if (!HeadCollision.Support(transform.position, Head.transform.position, out floor)) return;
            // Directional grade rejects a contour traverse and an uphill run on the same ramp.
            frame.GlideDownhillRun = Vector3.Dot(floor.normal, travel.normalized) >= (.5773503f - .0001f) * floor.normal.y;
        }
        // Arms held out to the sides (or swept back) leave the headset cameras' view. The
        //    runtime then reports isTracked=false but keeps estimating the controller pose, and
        //    Pose() keeps applying it. Flight needs where the hands are, not how they are tracked.
        public bool FlightPosesInferred()
        {
            if (!R26GlideEntry.InferredPoseRule) return false;
            if (R25NetBot.Requested && !XRSettings.isDeviceActive) return NetBotInferredHands;
            return PoseAlive(XRNode.LeftHand) && PoseAlive(XRNode.RightHand);
        }
        static bool PoseAlive(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node); InputTrackingState state; Vector3 p;
            return device.isValid && device.TryGetFeatureValue(CommonUsages.trackingState, out state)
                && (state & (InputTrackingState.Position | InputTrackingState.Rotation)) != 0
                && device.TryGetFeatureValue(CommonUsages.devicePosition, out p) && !float.IsNaN(p.x + p.y + p.z) && !float.IsInfinity(p.x + p.y + p.z);
        }
    }
}
#endif
