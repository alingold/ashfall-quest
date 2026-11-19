namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        // MATCHEND calls once after the celebration. The ordinary map barrier
        // retires match Health/poses and places every admitted member in warmup.
        // Same map and manifest: no map payload reload or room departure needed.
        public bool HostReturnAllPlayersToWarmupSpawns()
        {
            Check();if(State!=R24LanLobbyState.Hosting||SelectedMap==null||SelectedMap.Cancelled)return false;
            if(SelectedMap.Rules==null)return true;
            return BeginMapSelection(MapId,MapManifestHash,SelectedMap.SourceRules);
        }
    }
}
