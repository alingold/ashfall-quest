#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.UI {
 public sealed partial class R23VrUI {
  // PICKUP4 evidence seam: one press/release on a backpack card through the real gesture owner and publisher.
  internal bool Pickup4Gesture(string id,bool grip){if(R25NetBot.Arg("-ashfallNetBotPickup4")!="true"||UnityEngine.XR.XRSettings.isDeviceActive||Page!=R23UiPage.Loadout)return false;
   loadoutGesture.Reset();loadoutGesture.Sample(1,id,false,false,true,true);loadoutGesture.Sample(1,id,!grip,grip,true,true);var intent=loadoutGesture.Sample(1,id,false,false,true,true);if(intent==null)return false;PublishLoadout(intent);return true;}
 }
}
#endif
