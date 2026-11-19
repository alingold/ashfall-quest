using System;using System.Collections.Generic;
namespace Ashfall.Integration.Playable.Networking
{
    // Immutable final host receipt, presented only after the results transition.
    public sealed class R26MatchEndRow
    {
        public readonly string Actor,Name;public readonly int Team,Kills,Deaths,Score,Rank,Outcome;public readonly long AliveHealthRevision;
        // Outcome: 1 victory, 0 draw, -1 defeat. Equal kills/deaths share room rank.
        public R26MatchEndRow(string actor,string name,int team,int kills,int deaths,int score,int rank,int outcome,long aliveHealthRevision=0){AliveHealthRevision=aliveHealthRevision;Actor=actor;Name=name;Team=team;Kills=kills;Deaths=deaths;Score=score;Rank=rank;Outcome=outcome;}
    }
    public sealed class R26MatchEndResults
    {
        public const double Duration=10;
        public readonly double EndsAt;public readonly bool Teams;public readonly R26MatchEndRow[] Rows;
        public R26MatchEndResults(double endsAt,bool teams,R26MatchEndRow[] rows){EndsAt=endsAt;Teams=teams;Rows=(R26MatchEndRow[])rows.Clone();}
        public R26MatchEndRow ForActor(string actor){foreach(var row in Rows)if(row.Actor==actor)return row;return null;}
        public static string Placement(int rank){int n=rank%100;return rank+(n>=11&&n<=13?"th":rank%10==1?"st":rank%10==2?"nd":rank%10==3?"rd":"th");}
        public static R26MatchEndResults Rank(double endsAt,bool teams,List<R26MatchEndRow> players,Dictionary<int,int> teamScores)
        {
            players.Sort(delegate(R26MatchEndRow a,R26MatchEndRow b){int c=b.Kills.CompareTo(a.Kills);if(c==0)c=a.Deaths.CompareTo(b.Deaths);return c!=0?c:StringComparer.Ordinal.Compare(a.Actor,b.Actor);});
            int best=int.MinValue,winners=0;foreach(int value in teamScores.Values){if(value>best){best=value;winners=1;}else if(value==best)winners++;}
            var rows=new R26MatchEndRow[players.Count];int rank=1;
            for(int i=0;i<rows.Length;i++){var p=players[i];if(i>0&&(p.Kills!=players[i-1].Kills||p.Deaths!=players[i-1].Deaths))rank=i+1;int outcome=rank==1?1:-1;if(teams){int score;outcome=p.Team<0||!teamScores.TryGetValue(p.Team,out score)?-1:score<best?-1:winners>1?0:1;}rows[i]=new R26MatchEndRow(p.Actor,p.Name,p.Team,p.Kills,p.Deaths,p.Score,rank,outcome,p.AliveHealthRevision);}
            return new R26MatchEndResults(endsAt,teams,rows);
        }
    }
}
