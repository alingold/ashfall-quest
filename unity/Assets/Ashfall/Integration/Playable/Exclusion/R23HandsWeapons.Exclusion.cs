#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Integration.Playable.Exclusion;
using Ashfall.Integration.Playable.Expansion;
using UnityEngine;

namespace Ashfall.Integration.Playable.HandsWeapons
{
    // D. Presentation-only handling of held weapons and gloves that are behind a
    // wall, using the same head->muzzle / head->hand world segment as the firing
    // rule. Never changes the gameplay muzzle, shot poses, tracked transforms,
    // glove art poses (sent to peers) or any hit/authority rule.
    public sealed partial class R23HandsWeapons
    {
        // Root wires this to the rig's obstructed-view fade (B). While the view is
        // fully faded the weapon, its feedback and gloves are not drawn.
        public Func<bool> ViewObstructed;
        readonly R25WeaponObstruction weaponObstruction = new R25WeaponObstruction();
        readonly R25ObstructionLatch specialWeaponLatch = new R25ObstructionLatch();
        readonly R25ObstructionLatch leftGloveLatch = new R25ObstructionLatch();
        readonly R25ObstructionLatch rightGloveLatch = new R25ObstructionLatch();
        readonly List<Renderer> hiddenWeaponRenderers = new List<Renderer>();
        readonly List<Renderer> hiddenDualRenderers = new List<Renderer>();
        readonly List<Renderer> hiddenLeftGlove = new List<Renderer>();
        readonly List<Renderer> hiddenRightGlove = new List<Renderer>();
        readonly RaycastHit[] obstructionHits = new RaycastHit[32];
        bool weaponArtHidden, dualArtHidden, leftGloveHidden, rightGloveHidden;
        float obstructionClock = -1;

        public bool WeaponPresentationObstructed { get { return weaponObstruction.Blocked || specialWeaponLatch.Hidden; } }
        public float WeaponPresentationRetract { get { return weaponObstruction.Retract; } }

        bool StandardGunPresentation { get { return !IsDual && !IsMelee && !IsSmaw && !IsAwp && !IsM60; } }

        // Called after every final pose write (LateUpdate and the before-render
        // pass). late=true is the last pass before rendering: only it casts the
        // extra glove/special-weapon rays; LateUpdate reuses their latched state.
        void PresentObstruction(bool late)
        {
            float now = Time.unscaledTime;
            float seconds = obstructionClock < 0 ? 0 : Mathf.Clamp(now - obstructionClock, 0, .25f);
            obstructionClock = now;
            bool enabled = R25Features.ObstructedWeaponPresentation && head != null;
            bool faded = ViewObstructed != null && ViewObstructed();
            bool hideWeapon = false, hideDual = false;
            if (enabled && FeedbackAllowed && weapon != null && muzzle != null && displayedPoseValid && selectedWeapon < weaponModels.Count)
            {
                Vector3 eye = head.transform.position;
                if (StandardGunPresentation)
                {
                    specialWeaponLatch.Reset();
                    // Reuse ResolveDisplayedAim's head->muzzle segment result.
                    Vector3 segment = From(displayedShot.Origin) - eye;
                    float length = segment.magnitude;
                    float hitDistance = displayedAimBlocked ? Vector3.Distance(eye, displayedAimPoint) : length;
                    bool haptic;
                    float retract = weaponObstruction.Step(displayedAimBlocked, length, hitDistance, seconds, out haptic);
                    if (haptic) Pulse(primaryHand, weaponObstruction.HapticAmplitude, weaponObstruction.HapticDuration);
                    if (weaponObstruction.Ghosted) hideWeapon = true;
                    else if (retract > 0 && length > .0001f && weaponModels[selectedWeapon] != null)
                        weaponModels[selectedWeapon].position -= segment / length * retract;
                    // Flash, reticle and ammo panel sit at the real (blocked) muzzle.
                    if (displayedAimBlocked && gunFeedback != null) gunFeedback.HideHeldPresentation();
                    if (displayedAimBlocked && mp9Laser != null) mp9Laser.Hide();
                }
                else
                {
                    weaponObstruction.Reset();
                    if (late)
                    {
                        bool wasHidden = specialWeaponLatch.Hidden;
                        bool blocked = IsDual ? SegmentBlocked(eye, left.position) || SegmentBlocked(eye, right.position)
                            : IsMelee ? SegmentBlocked(eye, PrimaryTransform.position)
                            : SegmentBlocked(eye, From(displayedShot.Origin));
                        specialWeaponLatch.Step(blocked, seconds);
                        if (specialWeaponLatch.Hidden && !wasHidden) Pulse(primaryHand, weaponObstruction.HapticAmplitude, weaponObstruction.HapticDuration);
                    }
                    hideWeapon = specialWeaponLatch.Hidden;
                    hideDual = hideWeapon && IsDual;
                }
            }
            else
            {
                weaponObstruction.Reset();
                specialWeaponLatch.Reset();
            }
            hideWeapon |= faded;
            hideDual |= faded && IsDual;
            SetArtHidden(hiddenWeaponRenderers, weapon, ref weaponArtHidden, hideWeapon);
            var dualHost = ActiveDualView == null ? null : ActiveDualView.transform;
            SetArtHidden(hiddenDualRenderers, dualHost, ref dualArtHidden, hideDual);
            if (hideWeapon && gunFeedback != null) gunFeedback.HideHeldPresentation();
            if (hideWeapon && mp9Laser != null) mp9Laser.Hide();
            if (late)
            {
                StepGlove(leftGloveLatch, leftVisual, enabled, seconds);
                StepGlove(rightGloveLatch, rightVisual, enabled, seconds);
            }
            SetArtHidden(hiddenLeftGlove, leftVisual, ref leftGloveHidden, faded || leftGloveLatch.Hidden);
            SetArtHidden(hiddenRightGlove, rightVisual, ref rightGloveHidden, faded || rightGloveLatch.Hidden);
        }

        void StepGlove(R25ObstructionLatch latch, Transform glove, bool enabled, float seconds)
        {
            if (!enabled || glove == null || !glove.gameObject.activeInHierarchy) { latch.Reset(); return; }
            latch.Step(SegmentBlocked(head.transform.position, glove.position), seconds);
        }

        // Same predicate as Nearest(..., ballistic:true), without allocating.
        bool SegmentBlocked(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < .0001f || float.IsNaN(length) || float.IsInfinity(length)) return false;
            int count = Physics.RaycastNonAlloc(from, delta / length, obstructionHits, length, Physics.DefaultRaycastLayers & Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = obstructionHits[i].collider;
                if (c != null && !Excluded(c) && !R23ImpactMarkSurface.ExcludesBallistic(c)) return true;
            }
            return false;
        }

        // Hide by forceRenderingOff so no other owner's enabled/active state is
        // touched; restore exactly the renderers this class hid.
        static void SetArtHidden(List<Renderer> hidden, Transform root, ref bool state, bool hide)
        {
            if (hide == state) return;
            if (hide)
            {
                if (root == null) return;
                root.GetComponentsInChildren(true, hidden);
                for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].forceRenderingOff = true;
            }
            else
            {
                for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].forceRenderingOff = false;
                hidden.Clear();
            }
            state = hide;
        }
    }
}
#endif
