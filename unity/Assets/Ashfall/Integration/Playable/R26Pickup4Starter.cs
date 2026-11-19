#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable {
 // PICKUP4 evidence: starts the opt-in routine (-ashfallNetBotPickup4 true) without editing the stock bot.
 public sealed class R26Pickup4Starter:MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(R25NetBot.Arg("-ashfallNetBotPickup4")!="true"||UnityEngine.XR.XRSettings.isDeviceActive)return;var go=new GameObject("R26 Pickup4 starter");DontDestroyOnLoad(go);go.AddComponent<R26Pickup4Starter>();}
  void Update(){var bot=FindAnyObjectByType<R25NetBot>();if(bot==null)return;bot.Pickup4Begin();Destroy(gameObject);}
 }
}
#endif
