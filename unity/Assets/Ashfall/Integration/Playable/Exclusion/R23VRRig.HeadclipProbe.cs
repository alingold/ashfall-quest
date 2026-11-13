#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using Ashfall.Modules.Player;
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        // The editor harness owns synthetic device poses, not a different motor.
        // Prime with NetBotSample once; retain normal LateUpdate/before-render
        // exclusion and fade while suppressing XR polling and autonomous Update.
        public bool HeadclipProbeOwnsTracking;
        // R6: editor fixtures that run many steps per editor frame advance this
        // simulated frame/clock before each step (per-frame budgets and timers).
        int fixtureFrame; float fixtureClock, fixtureSeconds;
        public void FixtureTick(float seconds) { fixtureFrame++; fixtureClock += seconds; fixtureSeconds = seconds; }
        float headclipProbeClock;
        bool headclipRootWasProtected;
        public bool HeadclipPhysicalHold { get { return headclipRootWasProtected; } }
        public void HeadclipProbeStep(Vector2 move, Vector3 localHead, float turn, float seconds)
        {
            if (!HeadclipProbeOwnsTracking || !R25NetBot.Requested || UnityEngine.XR.XRSettings.isDeviceActive)
                throw new System.InvalidOperationException("Explicit XR-off headclip probe required");
            Head.transform.localPosition = localHead;
            headclipProbeClock += seconds;
            if (ViewFade != null) ViewFade.NativeProbeSeconds = seconds;
            Left.localPosition = localHead + new Vector3(-.3f, -.35f, .25f);
            Right.localPosition = localHead + new Vector3(.3f, -.35f, .25f);
            LeftInput = new HandSample { Tracked = true, StickX = move.x, StickY = move.y };
            RightInput = new HandSample { Tracked = true, StickX = turn };
            if (!CarrierActive) SynchronizeSourceHead();
            headclipRootWasProtected = false;
            Advance(R23InputProjection.Sample(true, true, F(transform.InverseTransformPoint(Head.transform.position)),
                LeftInput, RightInput, false, PrimaryHand), seconds);
            TickHeadRecovery();
        }
    }
}
#endif
