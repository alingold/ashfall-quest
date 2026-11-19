#if UNITY_6000_0_OR_NEWER
using System;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.UI;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        Func<double> lanRoundElapsed;R24LanRoundRandom lanZoneRandom;int lanRoundIndex,lanTeam=-1;
        public bool LanRoundBound{get{return lanRoundElapsed!=null;}}
        string MatchAuthorityLabel{get{return LanRoundBound?"LAN ROUND | HOST HEALTH / COMBAT":"LOCAL OFFLINE";}}
        public string ValidateLanRound(R24PlayableMatchRules rules)
        {if(rules==null)return "No validated match rules";if(rules.StartingBuilds!=999)return "Starting builds currently supports source999";if(rules.MaxPlayers>R24LanLobby.Capacity)return "Rooms support up to "+R24LanLobby.Capacity+" players";return null;}
        public bool LanRoundAssetsReady(R24PlayableMatchRules rules)
        {bool deployment=rules.Mode=="revive"&&rules.Teams>0;return (!(rules.ZoneEnabled||deployment)||LaunchMapCatalog!=null&&LaunchMapCatalog.Ready)&&(!deployment||TeamLaunch!=null&&TeamLaunch.Ready);}
        public string LanRoundAssetFailure(R24PlayableMatchRules rules)
        {if((rules.ZoneEnabled||rules.Mode=="revive"&&rules.Teams>0)&&LaunchMapCatalog!=null&&LaunchMapCatalog.Failure!=null)return LaunchMapCatalog.Failure;if(rules.Mode=="revive"&&rules.Teams>0&&TeamLaunch!=null&&TeamLaunch.Failure!=null)return TeamLaunch.Failure;return null;}
        public bool BeginLanRound(R24PlayableMatchRules rules,int seed,int round,int team,Func<double> elapsed)
        {
            if(!Ready||Map.MapId!=rules.MapId||!Map.IsLoaded||elapsed==null||ValidateLanRound(rules)!=null||!LanRoundAssetsReady(rules)||seed<=0||round<1||round>rules.Rounds)return false;
            EndMatch();lanRoundElapsed=elapsed;lanZoneRandom=new R24LanRoundRandom(seed);lanRoundIndex=round;lanTeam=team;pendingMatch=rules;activeSpawn=Map.GameSpawn;StartPlaying();return ActualMatchRules==rules&&LanRoundBound&&MatchActive;
        }
        void ClearLanRoundBinding(){ClearLanPlayerHealth();lanRoundElapsed=null;lanZoneRandom=null;lanRoundIndex=0;lanTeam=-1;}
        bool AdvanceMatchClock(float dt){return LanRoundBound?localMatchClock.Synchronize(lanRoundElapsed()):localMatchClock.Advance(dt);}
        void BindLanClock(){if(LanRoundBound)localMatchClock.BindNetworkRound(lanRoundIndex,lanRoundElapsed());}
        public void EndLanSessionRound(){if(!LanRoundBound)return;EndMatch();if(Ready)ReturnToLobby();}
        public void FinishLanRound(){if(!LanRoundBound||!MatchActive)return;localMatchClock.Finish();EndTeamLaunch();if(MatchZone!=null)MatchZone.Suspend();Gameplay.SetGameplayPaused(true);SetMatchStatus("LAN MATCH RESULTS | HOST CONTROLS NEXT START");if(ActualMatchRules.Mode=="revive")PublishMatchLife(true);}
    }
}
#endif
