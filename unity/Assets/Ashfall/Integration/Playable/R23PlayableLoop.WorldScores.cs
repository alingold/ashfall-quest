#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Integration.Playable.WorldScores;
namespace Ashfall.Integration.Playable {
 public sealed partial class R23PlayableLoop {
  public R24WorldScoreboards WorldScores{get;private set;}
  void InitializeWorldScores(){WorldScores=new UnityEngine.GameObject("Source world scoreboards").AddComponent<R24WorldScoreboards>();WorldScores.transform.SetParent(transform,false);WorldScores.Initialize(Map,ReadWorldScoreState,delegate{return Ready&&roundStarted&&!switchingMap;});}
  static int TeamSharedScore(List<R24WorldScoreRow> players,int team){int total=0;foreach(var row in players)if(row.Team==team)total+=row.Score??0;return total;}
  public R24WorldScoreState ReadWorldScoreState(){
   var rules=ActualMatchRules??UI.SelectedMatchRules??Ashfall.Integration.Playable.Match.R24PlayableMatchRules.ForPreset("team_revive",Map.MapId);int teams=rules==null?2:rules.Teams;int limit=rules==null?0:rules.TimeLimit;int cap=rules==null?0:rules.ScoreLimit;
   var state=new R24WorldScoreState{Active=ActualMatchRules!=null,Stage=MatchActive?"match":ActualMatchRules!=null?"results":"lobby",Teams=teams,Seconds=ActualMatchRules!=null&&limit>0?Math.Max(0,(int)Math.Ceiling(limit-MatchClock)):limit,TimeUnlimited=limit==0,Cap=cap,ScoreUnlimited=cap==0};
   var players=new List<R24WorldScoreRow>();var lobby=LanSession==null?null:LanSession.Lobby;var health=Gameplay==null?null:Gameplay.LocalMatchHealth;
   // Other members' deaths come from the host's replicated Health receipts (never local guesses).
   if(lobby!=null){foreach(var member in lobby.Members){Ashfall.Integration.Playable.Networking.R24LanHealthState shared;int? deaths=member.Actor==lobby.OwnActor&&health!=null?(int?)health.Deaths:lobby.TryReadHealth(member.Actor,out shared)?(int?)shared.Deaths:null;Ashfall.Integration.Playable.Networking.R24LanScoreRow score;lobby.TryReadScore(member.Actor,out score);players.Add(new R24WorldScoreRow{Id=member.Actor,Name=member.Name,Team=member.Team,Deaths=deaths,Kills=score==null?(int?)null:score.Kills,Score=score==null?(int?)null:score.Score});}}
   else players.Add(new R24WorldScoreRow{Id="player",Name="Player",Team=LocalMatchTeam,Score=localMatchScores==null?(int?)null:localMatchScores.PersonalKills,Kills=localMatchScores==null?(int?)null:localMatchScores.PersonalKills,Deaths=health==null?(int?)null:health.Deaths});
   R24WorldScoreState.Rank(players);state.Players=players.ToArray();
   if(teams>0){var rows=new List<R24WorldScoreRow>();var used=new HashSet<int>();for(int i=0;i<teams;i++)used.Add(i);foreach(var p in players)if(p.Team>=0)used.Add(p.Team);foreach(int team in used)rows.Add(new R24WorldScoreRow{Id=team.ToString(),Name="TEAM "+(team+1),Team=team,Score=lobby!=null?TeamSharedScore(players,team):localMatchScores==null?(int?)null:localMatchScores.TeamKills(team)});R24WorldScoreState.Rank(rows,true);state.Rows=rows.ToArray();}else state.Rows=state.Players;
   return state;
  }
 }
}
#endif
