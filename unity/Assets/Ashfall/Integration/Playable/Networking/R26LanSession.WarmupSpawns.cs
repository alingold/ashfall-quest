#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        public bool HostReturnAllPlayersToWarmupSpawns()
        {return peer!=null&&peer.Lobby.HostReturnAllPlayersToWarmupSpawns();}
    }
}
#endif
