#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Integration.Playable.WorldScores;
namespace Ashfall.Integration.Playable {
 public sealed partial class R23PlayableLoop {
  R24WorldScoreState retainedLocalBoard;
  public R24WorldScoreboards WorldScores{get;private set;}
  void InitializeWorldScores(){WorldScores=new UnityEngine.GameObject("Source world scoreboards").AddComponent<R24WorldScoreboards>();WorldScores.transform.SetParent(transform,false);WorldScores.Initialize(Map,ReadWorldBoardScoreState,delegate{return Ready&&roundStarted&&!switchingMap;});}
  R24WorldScoreState ReadWorldBoardScoreState(){
   var lobby=LanSession==null?null:LanSession.Lobby;
   if(lobby==null)return ActualMatchRules!=null?ReadWorldScoreState():retainedLocalBoard??new R24WorldScoreState();
   var history=lobby.RetainedScoreboard;
   if(history==null)return ActualMatchRules!=null?ReadWorldScoreState():new R24WorldScoreState();
   var players=new R24WorldScoreRow[history.Count];
   for(int i=0;i<players.Length;i++)players[i]=new R24WorldScoreRow{Name=history[i].Key,Kills=history[i].Value};
   return new R24WorldScoreState{Players=players};
  }
  static int TeamSharedScore(List<R24WorldScoreRow> players,int team){int total=0;foreach(var row in players)if(row.Team==team)total+=row.Score??0;return total;}
  public R24WorldScoreState ReadWorldScoreState(){
   var rules=ActualMatchRules??UI.SelectedMatchRules??Ashfall.Integration.Playable.Match.R24PlayableMatchRules.ForPreset("team_revive",Map.MapId);int teams=rules==null?2:rules.Teams;int limit=rules==null?0:rules.TimeLimit;int cap=rules==null?0:rules.ScoreLimit;
   var state=new R24WorldScoreState{TeamRevive=rules.Mode=="revive",Active=ActualMatchRules!=null,Stage=MatchActive?"match":ActualMatchRules!=null?"results":"lobby",Teams=teams,Seconds=rules.Mode=="revive"?(ActualMatchRules==null?0:(int)MatchClock):ActualMatchRules!=null&&limit>0?Math.Max(0,(int)Math.Ceiling(limit-MatchClock)):limit,TimeUnlimited=rules.Mode!="revive"&&limit==0,Cap=cap,ScoreUnlimited=cap==0};
   var players=new List<R24WorldScoreRow>();var lobby=LanSession==null?null:LanSession.Lobby;var health=Gameplay==null?null:Gameplay.LocalMatchHealth;
   // Other members' deaths come from the host's replicated Health receipts (never local guesses).
   if(lobby!=null){foreach(var member in lobby.Members){Ashfall.Integration.Playable.Networking.R24LanHealthState shared;int? deaths=member.Actor==lobby.OwnActor&&health!=null?(int?)health.Deaths:lobby.TryReadHealth(member.Actor,out shared)?(int?)shared.Deaths:null;Ashfall.Integration.Playable.Networking.R24LanScoreRow score;lobby.TryReadScore(member.Actor,out score);players.Add(new R24WorldScoreRow{Id=member.Actor,Name=member.Name,Team=member.Team,Deaths=deaths,Kills=score==null?(int?)null:score.Kills,Score=score==null?(int?)null:score.Score});}}
   else players.Add(new R24WorldScoreRow{Id="player",Name=R26PlayerName.Current,Team=LocalMatchTeam,Score=localMatchScores==null?(int?)null:localMatchScores.PersonalKills,Kills=localMatchScores==null?(int?)null:localMatchScores.PersonalKills,Deaths=health==null?(int?)null:health.Deaths});
   if(state.TeamRevive)foreach(var player in players){
    string life=null;
    if(lobby!=null)lobby.TryReadSurvivalLife(player.Id,out life);
    else if(health!=null)life=health.Life.ToString().ToLowerInvariant();
    player.Life=life;player.Members=life=="spectator"?0:1;player.Alive=life==null?(int?)null:life=="alive"?1:0;player.Downed=life==null?(int?)null:life=="downed"?1:0;player.Score=player.Alive;
   }
   R24WorldScoreState.Rank(players);state.Players=players.ToArray();
   if(teams>0){var rows=new List<R24WorldScoreRow>();var used=new HashSet<int>();for(int i=0;i<teams;i++)used.Add(i);foreach(var p in players)if(p.Team>=0)used.Add(p.Team);foreach(int team in used)rows.Add(new R24WorldScoreRow{Id=team.ToString(),Name="TEAM "+(team+1),Team=team,Score=lobby!=null?TeamSharedScore(players,team):localMatchScores==null?(int?)null:localMatchScores.TeamKills(team)});R24WorldScoreState.Rank(rows,true);state.Rows=rows.ToArray();}else state.Rows=state.Players;
   if(state.TeamRevive&&teams>0){foreach(var row in state.Rows){row.Alive=0;row.Downed=0;foreach(var player in players)if(player.Team==row.Team){row.Members+=player.Members;row.Alive+=player.Alive;row.Downed+=player.Downed;}row.Score=row.Alive;}var rows=new List<R24WorldScoreRow>(state.Rows);R24WorldScoreState.Rank(rows,true);state.Rows=rows.ToArray();}
   return state;
  }
 }
}
#endif
