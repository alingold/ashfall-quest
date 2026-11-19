#if UNITY_6000_0_OR_NEWER
using System;
using System.Globalization;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R25NetBot
    {
        // Explicit desktop bot route only: one setup spawn, then normal synthetic stick input.
        Vector3[] routePoints = new Vector3[0]; int routeIndex;
        float routeDelay = 3, routeSpeed = .65f, routePitch, routeFov = 80, routeObservation;
        // WIN2 headset realism (all inert unless given; see R23VRRig.NetBotHeadsetProfile).
        float routeHeight = 1.7088f, routeSway, routeLean, routeReach, routeLeanNow, routeStallSince = -1, routeLeanPhase;
        float routeSeated, routeSeatedRise; bool routeSeatedDone, routeStereo; Vector3 routeRendered, routeRenderedRight = Vector3.right; int routeRenderedFrame = -9;
        bool routeTrace; Vector3 routeLast; readonly System.Text.StringBuilder routeTraceText = new System.Text.StringBuilder();
        uint routeNoise = 2463534242;
        float RouteNoise() { routeNoise ^= routeNoise << 13; routeNoise ^= routeNoise >> 17; routeNoise ^= routeNoise << 5; return (routeNoise & 0xffff) / 32767.5f - 1; }
        bool ReadRouteHeadset()
        {
            if (!RouteOption("-ashfallNetBotRouteHeight", ref routeHeight, .8f, 2f)
                || !RouteOption("-ashfallNetBotHeadSway", ref routeSway, 0, .1f) || !RouteOption("-ashfallNetBotHeadLean", ref routeLean, 0, .6f)
                || !RouteOption("-ashfallNetBotReach", ref routeReach, 0, .8f) || !RouteOption("-ashfallNetBotSeated", ref routeSeated, .5f, 1.6f)
                || !RouteOption("-ashfallNetBotSeatedRise", ref routeSeatedRise, -.2f, .3f)) return false;
            // A seated player: the tracked head is about 1.05 m up and Set Height lifts the origin to the standing eye.
            if (routeSeated > 0) routeHeight = routeSeated;
            // A Quest 3 stereo camera: 64 mm between the eyes, 54 degree outer frustum edges, and
            // (as on the device) eye positions that still describe the frame rendered last.
            routeStereo = Arg("-ashfallNetBotStereo") == "quest3";
            if (routeStereo) Ashfall.Integration.Playable.Climbing.R23HeadCollision.NetBotStereo = delegate
            {
                // No frame of the route rendered yet (lobby, map entry): the camera is where the head is.
                if (Time.frameCount - routeRenderedFrame > 1 && root != null && root.Rig != null) { routeRendered = root.Rig.Head.transform.position; routeRenderedRight = root.Rig.transform.right; }
                return new Ashfall.Integration.Playable.Climbing.R23HeadCollision.NetBotStereoEyes
                { Left = routeRendered - routeRenderedRight * .032f, Right = routeRendered + routeRenderedRight * .032f, TangentX = 1.376f, TangentY = 1.376f };
            };
            routeTrace = Arg("-ashfallNetBotTrace") == "true";
            if (Arg("-ashfallNetBotHeadset") == "true" || routeSway > 0 || routeLean > 0 || routeSeated > 0 || routeStereo) R23VRRig.NetBotHeadset = new R23VRRig.NetBotHeadsetProfile();
            if (routeTrace) R23VRRig.NetBotTrace = delegate(string owner) { if (routeTraceText.Length < 600) routeTraceText.Append(owner).Append(' '); };
            return true;
        }
        // A standing player: the head sways and bobs with each step and never holds still;
        // stopped at an obstacle, they lean in to look or reach through, then straighten.
        void RouteHeadset(float now, bool walking)
        {
            var headset = R23VRRig.NetBotHeadset; if (headset == null) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime, .001f), t = now - playBegan;
            float speed = Vector3.ProjectOnPlane(root.Rig.transform.position - routeLast, Vector3.up).magnitude / dt; routeLast = root.Rig.transform.position;
            if (!walking || speed > .3f) routeStallSince = -1; else if (routeStallSince < 0) routeStallSince = now;
            bool stalled = walking && routeStallSince >= 0 && now - routeStallSince > .4f;
            if (stalled || routeLeanPhase > 0) routeLeanPhase += dt; if (routeLeanPhase > 3) routeLeanPhase = 0;
            routeLeanNow = Mathf.MoveTowards(routeLeanNow, routeLeanPhase > 0 && routeLeanPhase < 2 ? routeLean : 0, dt * .6f);
            float stride = walking ? 1 : .15f;
            headset.Offset = new Vector3(routeSway * stride * Mathf.Sin(t * 11.3f) + RouteNoise() * .001f,
                routeSway * .5f * stride * Mathf.Sin(t * 22.6f) - routeLeanNow * .35f + RouteNoise() * .001f, routeLeanNow + RouteNoise() * .001f);
            headset.LateOffset = new Vector3(RouteNoise(), RouteNoise(), RouteNoise()) * .0015f;
        }
        bool ReadWindowRoute()
        {
            string text = Arg("-ashfallNetBotRoute"); if (text.Length == 0) return true;
            if (mode != "solo" || UnityEngine.XR.XRSettings.isDeviceActive) return false;
            string[] points = text.Split(';'); if (points.Length < 2) return false;
            routePoints = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                string[] xyz = points[i].Split(','); float x, y, z;
                if (xyz.Length != 3 || !RouteNumber(xyz[0], out x) || !RouteNumber(xyz[1], out y) || !RouteNumber(xyz[2], out z)) return false;
                routePoints[i] = new Vector3(x,y,z);
            }
            return RouteOption("-ashfallNetBotRouteDelay", ref routeDelay, 1, 30)
                && RouteOption("-ashfallNetBotRouteSpeed", ref routeSpeed, .05f, 1)
                && RouteOption("-ashfallNetBotCameraPitch", ref routePitch, -60, 60)
                && RouteOption("-ashfallNetBotCameraFov", ref routeFov, 40, 110) && ReadRouteHeadset();
        }
        static bool RouteNumber(string text, out float value)
        { return Single.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !Single.IsNaN(value) && !Single.IsInfinity(value); }
        static bool RouteOption(string name, ref float value, float min, float max)
        { string text = Arg(name); return text.Length == 0 || RouteNumber(text, out value) && value >= min && value <= max; }
        void WindowRoute(float now, ref Vector2 move, ref float height, ref Vector3 left, ref Vector3 right, ref Quaternion aim, ref float grip, ref float trigger, ref string step)
        {
            if (playBegan < 0)
            {
                root.Rig.SetSpawn(root.Map.transform.TransformPoint(routePoints[0]), root.Map.transform.TransformPoint(routePoints[1]));
                playBegan = now; origin = routeLast = root.Rig.transform.position; routeIndex = 1;
                if (probe != null) probe.PlayBegan = now;
                root.Rig.Head.fieldOfView = routeFov;
                Write("route-start", "map-local=" + Arg("-ashfallNetBotRoute") + " delay=" + routeDelay + " stick=" + routeSpeed + "; setup teleport only; locomotion=R23VRRig.NetBotSample/Advance");
            }
            played = true; move = Vector2.zero; height = routeHeight; grip = trigger = 0;
            if (routeSeated > 0 && !routeSeatedDone && now - playBegan >= 1.2f && root.Rig.Calibration != null) { routeSeatedDone = true; root.Rig.Calibration.RequestRecalibration(); }
            if (routeSeated > 0 && now - playBegan >= 2f) height = routeSeated + routeSeatedRise;
            if (routeIndex < routePoints.Length)
            {
                Vector3 toward = Vector3.ProjectOnPlane(root.Map.transform.TransformPoint(routePoints[routeIndex]) - root.Rig.transform.position, Vector3.up);
                if (toward.magnitude < .15f) { routeIndex++; Write("route-waypoint", "reached=" + (routeIndex-1)); }
                else
                {
                    root.Rig.transform.rotation = Quaternion.LookRotation(toward);
                    if (now-playBegan >= routeDelay) move = new Vector2(0, routeSpeed);
                }
            }
            RouteHeadset(now, move.y > 0);
            Vector3 head = root.Rig.transform.position + Vector3.up * height;
            left = head + root.Rig.transform.rotation * new Vector3(-.32f,-.35f+routeReach*.4f,.35f+routeReach);
            right = head + root.Rig.transform.rotation * new Vector3(.32f,-.35f,.35f); aim = root.Rig.transform.rotation;
            moved |= Vector3.ProjectOnPlane(root.Rig.transform.position-origin,Vector3.up).magnitude > .5f;
            step = now-playBegan < routeDelay ? "route-settle" : routeIndex >= routePoints.Length ? "route-arrived" : "route-walk";
        }
        void ObserveWindowRoute(float now)
        {
            // Presentation pitch is applied after Advance; the walking heading remains level.
            root.Rig.Head.transform.localRotation = Quaternion.Euler(routePitch,0,0);
            if (now >= routeObservation) { routeObservation = now + .25f; WriteWindowPosition("route-position"); }
            if (routeTrace && now - playBegan >= routeDelay - .2f)
            {
                Vector3 p = root.Map.transform.InverseTransformPoint(root.Rig.transform.position), h = root.Map.transform.InverseTransformPoint(root.Rig.Head.transform.position);
                Write("trace", "f=" + Time.frameCount + " root=" + p.ToString("F3") + " head=" + h.ToString("F3") + " " + root.Rig.NetBotDiagnostics + " lean=" + routeLeanNow.ToString("F2",CultureInfo.InvariantCulture) + " origin=" + root.Rig.TrackingOrigin.localPosition.y.ToString("F3",CultureInfo.InvariantCulture) + " r=" + root.Rig.HeadCollision.EyeSafetyRadius.ToString("F3",CultureInfo.InvariantCulture) + " | " + routeTraceText);
            }
            routeTraceText.Length = 0; routeRendered = root.Rig.Head.transform.position; routeRenderedRight = root.Rig.transform.right; routeRenderedFrame = Time.frameCount;
        }
        void WriteWindowPosition(string kind)
        {
            if (root == null || root.Rig == null || root.Map == null) return;
            Vector3 p = root.Map.transform.InverseTransformPoint(root.Rig.transform.position);
            Vector3 h = root.Map.transform.InverseTransformPoint(root.Rig.Head.transform.position);
            Write(kind, "local=" + p.ToString("F4") + " head=" + h.ToString("F4") + " supported=" + root.Rig.Supported + " waypoint=" + routeIndex + "/" + routePoints.Length
                + " stick=" + root.Rig.LastFrame.MoveX.ToString("F3",CultureInfo.InvariantCulture) + "," + root.Rig.LastFrame.MoveY.ToString("F3",CultureInfo.InvariantCulture));
        }
    }
}
#endif
