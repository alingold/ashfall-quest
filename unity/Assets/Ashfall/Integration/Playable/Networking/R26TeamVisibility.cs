using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.Networking
{
    // Presentation policy only. Team identity comes from the host roster; vitals
    // come from TryReadHealth, which rejects stale epochs and spawn revisions.
    public static class R26TeamVisibility
    {
        public static bool Match(R24LanLobby lobby)
        {var map=lobby==null?null:lobby.SelectedMap;return map!=null&&!map.Cancelled&&!map.Home&&map.Rules!=null;}
        public static bool Teammate(int teams,int ownTeam,int otherTeam)
        {return teams>0&&ownTeam>=0&&otherTeam>=0&&ownTeam==otherTeam;}
        public static bool Show(bool match,int teams,int ownTeam,int otherTeam,bool self)
        {return self||!match||Teammate(teams,ownTeam,otherTeam);}
        public static bool ShowAvatar(bool match,bool teammate,bool known,LifeState life,bool enemyRevive)
        {
            // Spectators waiting for a spawn publish presence but have no Health record.
            return !match||teammate||known&&(life==LifeState.Alive||life==LifeState.Downed&&enemyRevive);
        }
        public static bool Inactive(LifeState life){return life!=LifeState.Alive;}
    }
}
