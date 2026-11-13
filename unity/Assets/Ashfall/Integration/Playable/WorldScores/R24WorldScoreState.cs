using System;using System.Collections.Generic;
namespace Ashfall.Integration.Playable.WorldScores {
 public sealed class R24WorldScoreRow {
  public string Id,Name;public int Team,Rank;public int? Score,Kills,Deaths;
 }
 // Source world_score_state/layout_r18 presentation. Unknown authority fields
 // remain unknown; practice targets are never silently counted as real players.
 public sealed class R24WorldScoreState {
  public bool Active,TimeUnlimited,ScoreUnlimited;public string Stage;public int Teams,Seconds,Cap;
  public R24WorldScoreRow[] Rows=new R24WorldScoreRow[0],Players=new R24WorldScoreRow[0];
  public string Header{get{return Teams>0?"TEAM SCORES":"PLAYER SCORES";}}
  public string ClockText{get{return TimeUnlimited?"UNLIMITED":(Seconds/60).ToString("00")+":"+(Seconds%60).ToString("00");}}
  public string CapText{get{return ScoreUnlimited?"UNLIMITED":Cap.ToString();}}
  public static void Rank(List<R24WorldScoreRow> rows,bool teams=false){rows.Sort(delegate(R24WorldScoreRow a,R24WorldScoreRow b){int score=Nullable.Compare(b.Score,a.Score);if(score!=0)return score;if(!teams){int name=StringComparer.OrdinalIgnoreCase.Compare(a.Name,b.Name);if(name!=0)return name;}return StringComparer.Ordinal.Compare(a.Id,b.Id);});for(int i=0;i<rows.Count;i++)rows[i].Rank=i+1;}
  public static string Number(int? value){return value.HasValue?value.Value.ToString():"—";}
 }
}
