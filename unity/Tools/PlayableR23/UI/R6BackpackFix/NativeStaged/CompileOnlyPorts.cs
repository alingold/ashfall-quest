#if R6_UI_COMPILE_PORTS && UNITY_EDITOR
using System;
using Ashfall.Integration.Playable;
using Ashfall.Modules.Player;
namespace Ashfall.Editor.Playable {
// API-shape placeholders ONLY. Throw if executed. Never copy into Assets.
// Real Editor-only instance methods added by Root take precedence over extensions.
static class R6UiMissingPorts {
 public static void SmokeTrackedHands(this R23VRRig rig,HandSample left,HandSample right){throw new NotSupportedException("Missing real Root-owned simulated hand port");}
 public static void SmokeUiUpdate(this R23PlayableLoop root){throw new NotSupportedException("Missing real Root.Update smoke pump");}
}
}
#endif
