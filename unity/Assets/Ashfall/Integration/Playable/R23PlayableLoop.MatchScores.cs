#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.UI;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        R24LocalMatchScores localMatchScores;
        int lastHudSecond=int.MinValue,lastHudDeaths=-1;
        long lastHudScoreRevision=-1;
        bool lastHudActive;
        void BeginLocalMatchScoreRound()
        {
            // The current LAN round synchronizes rules/clock only. It must not
            // publish this player's local health as shared combat authority.
            bool sharedRoster=LanSession!=null&&LanSession.Lobby!=null;
            localMatchScores=LanRoundBound||sharedRoster?null:new R24LocalMatchScores(ActualMatchRules,delegate{return Gameplay.LocalMatchHealth;});
            lastHudSecond=int.MinValue;lastHudDeaths=-1;lastHudScoreRevision=-1;
        }
        void ClearMatchScorePresentation()
        {
            localMatchScores=null;lastHudSecond=int.MinValue;lastHudDeaths=-1;lastHudScoreRevision=-1;lastHudActive=false;
            if(UI!=null)UI.SetCompactMatchState(null,"player",-1);
        }
        void ObserveMatchScores(HealthSnapshot actualHealth,bool force)
        {
            if(ActualMatchRules==null)return;
            if(localMatchScores!=null&&MatchActive)localMatchScores.Observe(actualHealth,MatchClock);
            int remaining=ActualMatchRules.TimeLimit==0?-1:(int)Math.Ceiling(Math.Max(0,ActualMatchRules.TimeLimit-MatchClock));
            long revision=localMatchScores==null?-1:localMatchScores.Revision;
            if(!force&&remaining==lastHudSecond&&actualHealth.Deaths==lastHudDeaths&&revision==lastHudScoreRevision&&MatchActive==lastHudActive)return;
            lastHudSecond=remaining;lastHudDeaths=actualHealth.Deaths;lastHudScoreRevision=revision;lastHudActive=MatchActive;
            var lobby=LanSession==null?null:LanSession.Lobby;
            UI.SetCompactMatchState(R24CompactMatchHudState.ForRules(ReadWorldScoreState(),ActualMatchRules),lobby==null?"player":lobby.OwnActor,LocalMatchTeam);
        }
    }
}
#endif
