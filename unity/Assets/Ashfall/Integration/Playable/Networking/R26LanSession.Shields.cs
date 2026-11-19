#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  internal bool ShieldsOpenWarmup(string id){if(R25NetBot.Arg("-ashfallNetBotShields")!="true"||UnityEngine.XR.XRSettings.isDeviceActive||!Hosting||mapWorker!=null)return false;var choice=World.R23MapCatalog.Find(id);if(choice==null)return false;preparingMap=true;mapWorker=StartCoroutine(PrepareHostMap(choice,peer));return true;}
 }
}
#endif
