#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable {
 public sealed partial class R23VRRig {
  internal void Pickup3Primary(bool held){if(R25NetBot.Arg("-ashfallNetBotPickup3")!="true"||UnityEngine.XR.XRSettings.isDeviceActive)return;var sample=RightInput;sample.Primary=held;RightInput=sample;}
 }
}
#endif
