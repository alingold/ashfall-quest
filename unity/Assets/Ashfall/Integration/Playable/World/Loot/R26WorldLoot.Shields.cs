#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.World.Loot {
 public sealed partial class R23WorldLoot {
  internal void ShieldsActivate(UnityEngine.Vector3 at){if(R25NetBot.Arg("-ashfallNetBotShields")=="true"&&!UnityEngine.XR.XRSettings.isDeviceActive&&prepared){var old=actor.position;try{actor.position=at;Activate(false);}finally{actor.position=old;}}}
 }
}
#endif
