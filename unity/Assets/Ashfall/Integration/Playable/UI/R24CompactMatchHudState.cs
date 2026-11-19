using System;
using System.Globalization;
using Ashfall.Integration.Playable.WorldScores;
using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable.UI
{
    // match_hud_r3 source coordinates and own-side-versus-leading-other selection.
    public sealed class R24CompactMatchHudState
    {
        public readonly string Left,Right,Cap,Clock,Personal;
        public readonly float LeftRatio,RightRatio;
        public readonly bool Visible,TeamRevive;
        public readonly int OtherTeam=-1;
        public readonly string Matchup="";
        static string Number(int? n){return n.HasValue?n.Value.ToString(CultureInfo.InvariantCulture):"\u2014";}
        static float Ratio(int? n,int cap){return !n.HasValue||cap<=0?0:Math.Max(0,Math.Min(1,(float)n.Value/cap));}
        // Keep practice free of competitive score/time clutter; world boards remain unchanged.
        public static R24WorldScoreState ForRules(R24WorldScoreState state,R24PlayableMatchRules rules)
        {return rules!=null&&rules.PresetId!="practice"?state:null;}
        public R24CompactMatchHudState(R24WorldScoreState state,string ownActor,int ownTeam)
        {
            if(state==null){Left=Right=Cap=Clock=Personal="";return;}
            TeamRevive=state.TeamRevive;Visible=state.Active&&state.Stage=="match";R24WorldScoreRow own=null,other=null;
            foreach(var row in state.Rows)
            {
                bool mine=state.Teams>0?row.Team==ownTeam:row.Id==ownActor;
                if(mine)own=row;else if(other==null||row.Score.HasValue&&(!other.Score.HasValue||row.Score.Value>other.Score.Value))other=row;
            }
            OtherTeam=other==null?-1:other.Team;
            if(state.Teams>2&&ownTeam>=0)Matchup="T"+(ownTeam+1)+(OtherTeam>=0?" vs T"+(OtherTeam+1):"");
            if(state.TeamRevive){
                Left=Number(own==null?(int?)0:own.Alive);Right=Number(other==null?(int?)0:other.Alive);Cap="";
                LeftRatio=Ratio(own==null?(int?)null:own.Alive,own==null?0:own.Members);RightRatio=Ratio(other==null?(int?)null:other.Alive,other==null?0:other.Members);
                Personal="";
                Clock=state.TimeUnlimited?"":state.ClockText;return;
            }
            // Source HUD gives an empty side zero; an existing side with no
            // authoritative score remains unavailable rather than becoming zero.
            Left=Number(own==null?(int?)0:own.Score);Right=Number(other==null?(int?)0:other.Score);
            LeftRatio=Ratio(own==null?(int?)null:own.Score,state.Cap);RightRatio=Ratio(other==null?(int?)null:other.Score,state.Cap);
            Cap=state.ScoreUnlimited?"\u221e":state.Cap.ToString(CultureInfo.InvariantCulture);
            R24WorldScoreRow player=null;foreach(var row in state.Players)if(row.Id==ownActor){player=row;break;}
            Personal="K "+Number(player==null?(int?)null:player.Kills)+"   D "+Number(player==null?(int?)null:player.Deaths);
            Clock=state.TimeUnlimited?"":"TIME "+state.ClockText;
        }
    }
}
