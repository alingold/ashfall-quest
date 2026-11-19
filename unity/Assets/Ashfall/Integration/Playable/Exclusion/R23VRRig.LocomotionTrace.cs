#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        // WIN2: the explicit desktop route bot may listen in a player build
        // (which guard moved or refused the root this frame). Null in play.
        public static System.Action<string> NetBotTrace;
        void TraceLocomotion(string owner)
        {
#if UNITY_EDITOR
            if (LocomotionWrite != null) LocomotionWrite(owner, transform.position, TrackingOrigin.localPosition);
#endif
            if (NetBotTrace != null) NetBotTrace(owner);
        }
        // The numbers behind a refusal; formatted only while the bot listens.
        void TraceNote(string owner, float a, float b)
        {
            if (NetBotTrace != null) NetBotTrace(owner + "(" + a.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," + b.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ")");
        }
    }
}
#endif
