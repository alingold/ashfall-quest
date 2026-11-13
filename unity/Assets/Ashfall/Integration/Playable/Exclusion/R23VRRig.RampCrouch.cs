#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Climbing;
using Ashfall.Integration.Playable.Exclusion;
using Ashfall.Integration.Playable.Expansion;
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    // R7 (playtest 6) crouch overhaul: ONE owner of view height.
    //
    //   tracking origin Y = standing origin Y (from the height calibration) - crouch
    //
    // crouch = how far the eye must be lowered so the eye sphere fits in the
    // vertical space that is actually free above the floor at the head (and,
    // while walking, at the next step). It is written absolutely on every
    // sample. The calibration no longer writes the origin while this owns it,
    // so nothing accumulates: the old relative offsets fought the calibration
    // every frame (runaway offsets, delayed stand-up, and the view floating
    // up while the system menu stopped the calibration from ticking).
    public sealed partial class R23VRRig
    {
        public const float CrouchRiseSpeed = 2.5f, CrouchLowerSpeed = 3f, CrouchLookAhead = .35f, CrouchSkin = .01f, CrouchSearchStep = .04f;
        float crouch;
        int crouchFrame = -1; float crouchFrameStart, crouchFrameSeconds;
        public float RampCrouchOffset { get { return crouch; } }
        public bool RampCrouchBlocked { get; private set; }
        bool CanRampCrouch
        {
            get { return CrouchOwnsHeight && !CarrierActive && !headMotionOwned && !headJumpInProgress; }
        }
        bool CrouchOwnsHeight
        {
            get { return R25Features.RampCrouch && R23HeadCollision.FullEyeSafety && UseSourceHeadHull
                && HeadCollision != null && HeadCollision.Ready && TrackingOrigin != null && Head != null; }
        }
        float CrouchFrameSeconds
        {
            get
            {
#if UNITY_EDITOR
                if (HeadclipProbeOwnsTracking && ViewFade != null) return ViewFade.NativeProbeSeconds;
                if (fixtureFrame > 0) return fixtureSeconds;
#endif
                return Mathf.Min(Time.unscaledDeltaTime, .1f);
            }
        }
        // Origin Y with no crouch: the calibration's standing origin, or (no
        // calibration yet / editor fixtures) the current origin plus our crouch.
        float StandingOriginY()
        {
            float y;
            if (Calibration != null && Calibration.TryStandingOriginY(out y)) return y;
            return TrackingOrigin.localPosition.y + crouch;
        }
        void ApplyCrouchOrigin(float standingY)
        {
            Vector3 p = TrackingOrigin.localPosition; p.y = standingY - crouch; TrackingOrigin.localPosition = p;
            TraceLocomotion("view-height");
        }
        Vector3 StandingEye(float standingY)
        {
            return Head.transform.position + Vector3.up * (standingY - TrackingOrigin.localPosition.y);
        }
        void ResetRampCrouch()
        {
            if (TrackingOrigin != null && crouch > 0) { float y = StandingOriginY(); crouch = 0; ApplyCrouchOrigin(y); }
            crouch = 0; RampCrouchBlocked = false; crouchFrame = -1;
        }
        // Highest eye centre that fits at 'at' (x,z), reached from the clear eye
        // 'from' by going down if needed, across, then up. PositiveInfinity
        // means 'at' itself is free, or 'at' cannot be reached at any crouch
        // height (a wall: that is not a reason to crouch).
        float SpaceTop(Vector3 from, Vector3 at, float floorY)
        {
            float lowest = Mathf.Min(floorY + R25RampCrouch.MinimumEyeHeight, at.y);
            float startY = Mathf.Min(from.y, at.y);
            Vector3 flat = at - from; flat.y = 0;
            bool moving = flat.sqrMagnitude > .00000001f, hit;
            float reach = float.NaN;
            Vector3 support = Vector3.zero; bool found = false;
            for (int i = 0; i < 40 && !found; i++)
            {
                float h = startY - CrouchSearchStep * i;
                if (h < from.y - .0005f)
                {
                    if (float.IsNaN(reach))
                        reach = from.y > lowest + .001f ? from.y + HeadCollision.Sweep(from, Vector3.down * (from.y - lowest), false, out hit).y : from.y;
                    if (h < reach) { if (i > 0 && startY - CrouchSearchStep * (i - 1) <= reach + .0005f) break; h = reach; }
                }
                Vector3 a = new Vector3(from.x, h, from.z), b = new Vector3(at.x, h, at.z);
                if (!moving || HeadCollision.Clear(a, b - a)) { support = b; found = true; }
                if (h <= lowest + .0005f) break;
            }
            if (!found) return float.PositiveInfinity;
            if (at.y - support.y < .0005f) return float.PositiveInfinity;
            Vector3 up = HeadCollision.Sweep(support, Vector3.up * (at.y - support.y), false, out hit);
            float top = support.y + up.y;
            return top >= at.y - .0005f ? float.PositiveInfinity : top - CrouchSkin;
        }
        // Crouch needed for an uncrouched eye at 'standEye' (floor floorY).
        float CrouchNeed(Vector3 from, Vector3 standEye, float floorY)
        {
            float top = SpaceTop(from, standEye, floorY);
            if (float.IsPositiveInfinity(top)) return 0;
            float most = Mathf.Max(0, standEye.y - floorY - R25RampCrouch.MinimumEyeHeight);
            float need = standEye.y - top;
            if (need > most) { RampCrouchBlocked = true; need = most; }
            return Mathf.Max(0, need);
        }
        // Lower immediately to 'hard' (safety), ease toward 'soft' (look-ahead),
        // stand back up at a bounded rate. Rates are per rendered frame however
        // many times a frame samples (update, walk, render-time pose).
        float crouchLoweredAt = float.NegativeInfinity;
        void StepCrouch(float hard, float soft, bool mayLower, float maximum)
        {
            if (crouchFrame != SimFrame) { crouchFrame = SimFrame; crouchFrameStart = crouch; crouchFrameSeconds = CrouchFrameSeconds; }
            float target = Mathf.Max(hard, soft), now = ClearanceClock;
            if (!mayLower) { hard = Mathf.Min(hard, crouch); target = Mathf.Min(target, crouch); }
            if (crouch < hard) crouch = hard;
            if (crouch < target) crouch = Mathf.Max(crouch, Mathf.Min(target, crouchFrameStart + CrouchLowerSpeed * crouchFrameSeconds));
            // Stand back up only after 0.1 s without lowering, so two samples
            // that disagree by a centimetre never flip the view up and down.
            else if (crouch > target && now - crouchLoweredAt >= .1f) crouch = Mathf.Max(target, crouchFrameStart - CrouchRiseSpeed * crouchFrameSeconds);
            crouch = Mathf.Clamp(crouch, 0, Mathf.Max(0, maximum));
            if (crouch > crouchFrameStart + .002f) crouchLoweredAt = now;
        }
        Vector3 CrouchReference()
        {
            Vector3 eye = Head.transform.position;
            return haveSourceHead && !HeadCollision.HeadClear(eye) ? previousSourceHead : eye;
        }
        // Thumbstick walk: crouch for where this step lands (including an
        // uphill floor) before the head sweep moves there. The walk itself is
        // still limited by the ordinary head sweep.
        Vector3 CrouchWalk(Vector3 requested)
        {
            if (!CanRampCrouch || !grounded || requested.y > .001f) return requested;
            Vector3 step = requested; step.y = 0;
            if (step.sqrMagnitude < .00000001f) return requested;
            float standingY = StandingOriginY();
            Vector3 from = CrouchReference(), stand = StandingEye(standingY);
            float floorY = transform.position.y, rise = 0;
            RaycastHit ground;
            if (HeadCollision.Support(transform.position + step, from + step, out ground) && ground.point.y - floorY > -.3f && ground.point.y - floorY < .6f)
                rise = ground.point.y - floorY;
            Vector3 here = new Vector3(from.x, stand.y, from.z), there = stand + step + Vector3.up * rise;
            float hard = Mathf.Max(CrouchNeed(from, here, floorY), CrouchNeed(from, there, floorY + rise));
            float soft = CrouchNeed(from, there + step.normalized * CrouchLookAhead, floorY + rise);
            StepCrouch(hard, soft, true, stand.y - floorY - Mathf.Min(R25RampCrouch.MinimumEyeHeight, stand.y - floorY));
            ApplyCrouchOrigin(standingY);
            return requested;
        }
        // Every tracked sample (update and render-time): crouch for the space
        // at the real head. Also the only place the origin height is written.
        void CrouchPhysicalHead()
        {
            bool owns = CrouchOwnsHeight;
            if (Calibration != null) Calibration.ExternalHeightOwner = owns;
            if (!owns) { if (crouch > 0) ResetRampCrouch(); return; }
            float standingY = StandingOriginY();
            if (CarrierActive) { ApplyCrouchOrigin(standingY); return; }
            RampCrouchBlocked = false;
            Vector3 stand = StandingEye(standingY);
            Vector3 from = haveSourceHead ? previousSourceHead : Head.transform.position;
            float floorY = transform.position.y;
            float hard = Mathf.Max(CrouchNeed(from, new Vector3(from.x, stand.y, from.z), floorY), CrouchNeed(from, stand, floorY));
            StepCrouch(hard, hard, CanRampCrouch, stand.y - floorY - Mathf.Min(R25RampCrouch.MinimumEyeHeight, stand.y - floorY));
            ApplyCrouchOrigin(standingY);
        }
        // Uphill step whose eye rise is blocked overhead: crouch by the rise
        // (never below the minimum eye height) so the step can be taken.
        bool CrouchForRise(float add)
        {
            if (!CanRampCrouch || add <= .005f) return false;
            float standingY = StandingOriginY();
            crouch += add; ApplyCrouchOrigin(standingY);
            return true;
        }
        void UndoCrouchForRise(float add)
        {
            float standingY = StandingOriginY();
            crouch = Mathf.Max(0, crouch - add); ApplyCrouchOrigin(standingY);
        }
    }
}
#endif
