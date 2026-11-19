#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable {
 public sealed class R26ShieldsStarter:MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(R25NetBot.Arg("-ashfallNetBotShields")!="true"||UnityEngine.XR.XRSettings.isDeviceActive)return;var go=new GameObject("R26 Shields evidence");DontDestroyOnLoad(go);go.AddComponent<R26ShieldsStarter>();}
  void Update(){var bot=FindAnyObjectByType<R25NetBot>();if(bot==null)return;bot.ShieldsBegin();Destroy(gameObject);}
 }
}
#endif
