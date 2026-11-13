using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.MatchZone;
namespace Ashfall.Integration.Playable.Networking
{
    // Reuses the current map-owned source plan and its native floor/headroom
    // adapter. No second archive catalog, collider set or actor-life registry.
    public sealed class R24LanSpawnNeighbour
    {
        public readonly string Actor;public readonly int Team;public readonly Float3 Position;
        public R24LanSpawnNeighbour(string actor,int team,Float3 position){if(String.IsNullOrEmpty(actor)||team< -1||team>7||!position.IsFinite)throw new ArgumentException("Actual host member pose");Actor=actor;Team=team;Position=position;}
    }
    public sealed class R24LanSourceSpawnChoice
    {
        public readonly Float3 Position;public readonly bool Validated,ZoneGrid;public readonly int Candidates;public readonly double Score;
        internal R24LanSourceSpawnChoice(Float3 position,bool valid,bool grid,int candidates,double score){Position=position;Validated=valid;ZoneGrid=grid;Candidates=candidates;Score=score;}
    }
    public static class R24LanSourceSpawnSelection
    {
        static double Distance(Float3 a,Float3 b){float x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return(float)Math.Sqrt(x*x+y*y+z*z);}
        // Explicit user-reviewed enclosed arena. Targets rank existing native-
        // validated source sites; they never become fabricated spawn positions.
        static Float3 GroundTeamTarget(R24ArchiveRecoveryInfo input,R24ArchiveSpawnPlan plan,string actor,int team,int teams,List<R24LanSpawnNeighbour> neighbours,int round)
        {
            foreach(var other in neighbours)if(other.Actor!=actor&&other.Team==team)return other.Position;
            return R24ArchiveRoundPolicy.TeamTarget(input,plan.SourceSpawn.Y,team,teams,round);
        }
        static bool Inside(R24ZoneSquare r,Float3 p){return p.X>=r.X&&p.X<r.MaxX&&p.Z>=r.Z&&p.Z<r.MaxZ;}
        public static R24LanSourceSpawnChoice Archive(R24ArchiveRecoveryInfo input,R24ArchiveSpawnPlan plan,IR24ArchiveRecoveryWorld queries,string actor,int actorRotation,int team,int teams,bool initial,IEnumerable<R24LanSpawnNeighbour> actualPoses,R24ZoneSquare? next,Func<int,int> uniformOffset,Func<Float3,bool> finalHostSafety,int round=1)
        {
            if(input==null||plan==null||!plan.Ready||queries==null||String.IsNullOrEmpty(actor)||actorRotation<0||teams<0||teams>8||(teams==0?team!=-1:team<0||team>=teams)||actualPoses==null||uniformOffset==null||finalHostSafety==null)throw new ArgumentException("Actual current host archive plan/member scope");
            var neighbours=new List<R24LanSpawnNeighbour>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in actualPoses){if(p==null||!seen.Add(p.Actor)||neighbours.Count>=64)throw new ArgumentException("Unique bounded host poses");neighbours.Add(p);}
            var candidates=new List<Float3>(R24ArchiveRoundPolicy.RoundSites(input,plan.Spawns,round));bool grid=false;bool forcefield=R24ArchiveRoundPolicy.Forcefield(input.Id);
            if(forcefield)foreach(var site in plan.Loot)candidates.Add(new Float3(site.X,site.Y-.48f,site.Z));
            if(forcefield){
                // Probe below elevated lobby geometry using the authored play bounds.
                float top=input.BoundsMax.Y+2,depth=top-input.SolidMin.Y+5;
                for(int z=0;z<19;z++)for(int x=0;x<19;x++){
                    var from=new Float3(input.BoundsMin.X+(input.BoundsMax.X-input.BoundsMin.X)*(x+.5f)/19,top,input.BoundsMin.Z+(input.BoundsMax.Z-input.BoundsMin.Z)*(z+.5f)/19);
                    Float3 floor;if(queries.Floor(from,depth,out floor)&&R24ArchiveRoundPolicy.ForcefieldInterior(input.Id,floor))candidates.Add(new Float3(floor.X,floor.Y+.02f,floor.Z));
                }
            }
            if(initial&&!R24ArchiveRoundPolicy.FiveLevels(input.Id)){bool separate=false;foreach(var p in candidates)if(Distance(p,plan.SourceWarmup)>=12){separate=true;break;}if(!separate)foreach(var p in plan.Loot)candidates.Add(new Float3(p.X,p.Y-.48f,p.Z));}
            if(next.HasValue){var zone=next.Value;for(int i=candidates.Count-1;i>=0;i--)if(!Inside(zone,candidates[i]))candidates.RemoveAt(i);
                if(candidates.Count==0){grid=true;if(zone.Size<=2)throw new ArgumentException("Positive source inset rectangle");double top=R24ArchiveRoundPolicy.GridTop(input,round);float depth=(float)(top-R24ArchiveRoundPolicy.GridBottom(input,round));
                    for(int z=0;z<7;z++)for(int x=0;x<7;x++){Float3 floor;var from=new Float3((float)(zone.X+1+(zone.Size-2)*(x+.5)/7),(float)top,(float)(zone.Z+1+(zone.Size-2)*(z+.5)/7));if(queries.Floor(from,depth,out floor)&&floor.IsFinite&&R24ArchiveRoundPolicy.OnRoundLevel(input,floor,round))candidates.Add(new Float3(floor.X,floor.Y+.12f,floor.Z));}
                }
            }
            int count=candidates.Count,offset=uniformOffset(Math.Max(1,count));if(offset<0||offset>=Math.Max(1,count))throw new InvalidOperationException("Actual host random source range");
            bool groundTeams=initial&&teams>0&&R24ArchiveRoundPolicy.GroupedTeams(input.Id);
            Float3 groundTarget=groundTeams?GroundTeamTarget(input,plan,actor,team,teams,neighbours,round):new Float3();
            Float3 best=plan.SourceSpawn;double bestScore=Double.NegativeInfinity;bool valid=false;
            for(int i=0;i<count;i++){var p=candidates[(i+actorRotation%count+offset)%count];
                if(forcefield){Float3 floor;if(!R24ArchiveRoundPolicy.ForcefieldInterior(input.Id,p)||!queries.Floor(new Float3(p.X,p.Y+.6f,p.Z),1,out floor))continue;p=new Float3(floor.X,floor.Y+.02f,floor.Z);}
                if(!plan.Safe(p,false)||!finalHostSafety(p))continue;double score=1000;
                foreach(var other in neighbours)if(other.Actor!=actor&&!(teams>0&&other.Team==team))score=Math.Min(score,Distance(p,other.Position));
                if(groundTeams)score=-Distance(p,groundTarget);
                if(initial&&!groundTeams&&Distance(p,plan.SourceWarmup)<12)score-=10000;if(score>bestScore){bestScore=score;best=p;valid=true;}
            }
            return new R24LanSourceSpawnChoice(best,valid,grid,count,bestScore);
        }
    }
}
