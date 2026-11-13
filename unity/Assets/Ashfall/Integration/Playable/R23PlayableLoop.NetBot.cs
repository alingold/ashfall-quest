#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        public bool NetBotEquipOwned(string id)
        {
            return R25NetBot.Requested && !UnityEngine.XR.XRSettings.isDeviceActive && TryEquipOwnedItem(id, 1);
        }
    }
}
#endif
