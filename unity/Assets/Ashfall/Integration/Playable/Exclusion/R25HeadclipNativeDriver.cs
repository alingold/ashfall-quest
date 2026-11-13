#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    // Editor delegates drive the real player loop. Samples run in Update and
    // independent geometry audits/captures run after the game's LateUpdates.
    [DefaultExecutionOrder(2000)]
    public sealed class R25HeadclipNativeDriver : MonoBehaviour
    {
        public static Action AdvanceFrame;
        public static Action AuditFrame;
        void Update() { if (AdvanceFrame != null) AdvanceFrame(); }
        void LateUpdate() { if (AuditFrame != null) AuditFrame(); }
        void OnDestroy() { AdvanceFrame = null; AuditFrame = null; }
    }
}
#endif
