#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        bool MapGrapplePullEnabled()
        {
            var map=peer.Lobby.SelectedMap;
            return map!=null&&!map.Home&&map.SourceRules!=null&&map.SourceRules.GrappleGun&&map.SourceRules.GrapplePull;
        }
    }
}
#endif
