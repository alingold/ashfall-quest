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
        static Float3 NearestAnchor(Float3 point,Float3[] anchors){
            var best=anchors[0];double distance=double.PositiveInfinity;
            foreach(var p in anchors){double dx=point.X-p.X,dz=point.Z-p.Z,d=dx*dx+dz*dz;if(d<distance){distance=d;best=p;}}
            return best;
        }
        static bool Inside(R24ZoneSquare r,Float3 p){return p.X>=r.X&&p.X<r.MaxX&&p.Z>=r.Z&&p.Z<r.MaxZ;}
        public static R24LanSourceSpawnChoice Archive(R24ArchiveRecoveryInfo input,R24ArchiveSpawnPlan plan,IR24ArchiveRecoveryWorld queries,string actor,int actorRotation,int team,int teams,bool initial,IEnumerable<R24LanSpawnNeighbour> actualPoses,R24ZoneSquare? next,Func<int,int> uniformOffset,Func<Float3,bool> finalHostSafety,int round=1)
        {
            if(input==null||plan==null||!plan.Ready||queries==null||String.IsNullOrEmpty(actor)||actorRotation<0||teams<0||teams>8||(teams==0?team!=-1:team<0||team>=teams)||actualPoses==null||uniformOffset==null||finalHostSafety==null)throw new ArgumentException("Actual current host archive plan/member scope");
            var neighbours=new List<R24LanSpawnNeighbour>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in actualPoses){if(p==null||!seen.Add(p.Actor)||neighbours.Count>=64)throw new ArgumentException("Unique bounded host poses");neighbours.Add(p);}
            var candidates=new List<Float3>(R24ArchiveRoundPolicy.RoundSites(input,plan.Spawns,round));bool grid=false;bool boxed=Ashfall.Integration.Playable.TeamLaunch.R26MapPodPolicy.InteriorSpawn(input.Id,input.SourceSha256);bool forcefield=!boxed&&R24ArchiveRoundPolicy.Forcefield(input.Id);
            if(forcefield)foreach(var site in plan.Loot)candidates.Add(new Float3(site.X,site.Y-.48f,site.Z));
            if(forcefield){
                // Probe below elevated lobby geometry using the authored play bounds.
                float top=input.BoundsMax.Y+2,depth=top-input.SolidMin.Y+5;
                for(int z=0;z<19;z++)for(int x=0;x<19;x++){
                    var from=new Float3(input.BoundsMin.X+(input.BoundsMax.X-input.BoundsMin.X)*(x+.5f)/19,top,input.BoundsMin.Z+(input.BoundsMax.Z-input.BoundsMin.Z)*(z+.5f)/19);
                    Float3 floor;if(queries.Floor(from,depth,out floor)&&R24ArchiveRoundPolicy.ForcefieldInterior(input.Id,floor))candidates.Add(new Float3(floor.X,floor.Y+.02f,floor.Z));
                }
            }
            if(!boxed&&!R24ArchiveRoundPolicy.FiveLevels(input.Id)){bool separate=false;foreach(var p in candidates)if(Distance(p,plan.SourceWarmup)>=12){separate=true;break;}if(!separate||candidates.Count<R24LanLobby.Capacity)foreach(var p in plan.Loot)candidates.Add(new Float3(p.X,p.Y-.48f,p.Z));}
            // Assign sides BEFORE zone filtering, so a shrinking zone cannot swap teams.
            int[] sides=teams>0?R26SpawnSides.Split(candidates,teams):null;
            int anchorSide=R26SpawnSides.AnchorSide(team,teams,candidates.Count);
            if(sides!=null)for(int i=candidates.Count-1;i>=0;i--)if(sides[i]!=anchorSide)candidates.RemoveAt(i);
            // Sparse rows still need room for all room members. Probe only near
            // that team's own validated anchors, never another team's fallback.
            int needed=teams>0?(R24LanLobby.Capacity+teams-1)/teams:R24LanLobby.Capacity;
            int allies=1;foreach(var n in neighbours)if(n.Actor!=actor&&n.Team==team)allies++;
            if(candidates.Count>0&&candidates.Count<Math.Max(needed,allies)){
                var anchors=candidates.ToArray();foreach(var anchor in anchors)for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++){
                    if(x==0&&z==0)continue;Float3 floor;
                    if(queries.Floor(new Float3(anchor.X+x*.9f,anchor.Y+1,anchor.Z+z*.9f),3,out floor)&&Math.Abs(floor.Y-anchor.Y)<1&&R24ArchiveRoundPolicy.OnRoundLevel(input,floor,round))candidates.Add(new Float3(floor.X,floor.Y+.12f,floor.Z));
                }
            }
            // BOXED arenas only certify their interior rows. A late square can miss every row.
            var interiorBackup=boxed&&!initial&&teams==0&&next.HasValue?candidates.ToArray():null;
            if(next.HasValue){var zone=next.Value;for(int i=candidates.Count-1;i>=0;i--)if(!Inside(zone,candidates[i]))candidates.RemoveAt(i);
                if(!boxed&&teams==0&&candidates.Count==0){grid=true;if(zone.Size<=2)throw new ArgumentException("Positive source inset rectangle");double top=R24ArchiveRoundPolicy.GridTop(input,round);float depth=(float)(top-R24ArchiveRoundPolicy.GridBottom(input,round));
                    for(int z=0;z<7;z++)for(int x=0;x<7;x++){Float3 floor;var from=new Float3((float)(zone.X+1+(zone.Size-2)*(x+.5)/7),(float)top,(float)(zone.Z+1+(zone.Size-2)*(z+.5)/7));if(queries.Floor(from,depth,out floor)&&floor.IsFinite&&R24ArchiveRoundPolicy.OnRoundLevel(input,floor,round))candidates.Add(new Float3(floor.X,floor.Y+.12f,floor.Z));}
                }
            }
            int count=candidates.Count,offset=uniformOffset(Math.Max(1,count));if(offset<0||offset>=Math.Max(1,count))throw new InvalidOperationException("Actual host random source range");
            Float3 best=plan.SourceSpawn;double bestScore=Double.NegativeInfinity;bool valid=false;
            for(int i=0;i<count;i++){var p=candidates[(i+actorRotation%count+offset)%count];
                if(forcefield){Float3 floor;if(!R24ArchiveRoundPolicy.ForcefieldInterior(input.Id,p)||!queries.Floor(new Float3(p.X,p.Y+.6f,p.Z),1,out floor))continue;p=new Float3(floor.X,floor.Y+.02f,floor.Z);}
                if(!plan.Safe(p,false)||!finalHostSafety(p))continue;double score=1000;
                foreach(var other in neighbours)if(other.Actor!=actor)score=Math.Min(score,Distance(p,other.Position));
                if(initial&&teams==0&&Distance(p,plan.SourceWarmup)<12)score-=10000;if(score>bestScore){bestScore=score;best=p;valid=true;}
            }
            if(!valid&&interiorBackup!=null){
                // Prefer a floor inside the square and inside the reviewed arena footprint.
                // Probe at arena height so an overhead warm-up platform cannot become a spawn.
                var zone=next.Value;var anchors=interiorBackup;
                if(anchors.Length>0){
                    float minX=anchors[0].X,maxX=minX,minZ=anchors[0].Z,maxZ=minZ;
                    foreach(var p in anchors){minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minZ=Math.Min(minZ,p.Z);maxZ=Math.Max(maxZ,p.Z);}
                    double x0=Math.Max(zone.X,minX),x1=Math.Min(zone.MaxX,maxX),z0=Math.Max(zone.Z,minZ),z1=Math.Min(zone.MaxZ,maxZ);
                    if(x0<=x1&&z0<=z1)for(int k=0;k<49;k++){
                        int cell=(k+actorRotation)%49;var from=new Float3((float)(x0+(x1-x0)*(cell%7+.5)/7),0,(float)(z0+(z1-z0)*(cell/7+.5)/7));
                        var anchor=NearestAnchor(from,anchors);Float3 floor;
                        if(!queries.Floor(new Float3(from.X,anchor.Y+1,from.Z),2,out floor)||Math.Abs(floor.Y-anchor.Y)>1)continue;
                        var feet=new Float3(floor.X,floor.Y+.12f,floor.Z);
                        if(!R24ArchiveRoundPolicy.OnRoundLevel(input,feet,round)||!plan.Safe(feet,false)||!finalHostSafety(feet))continue;
                        return new R24LanSourceSpawnChoice(feet,true,true,49,0);
                    }
                    // A square can finish completely outside the playable interior. Use its nearest
                    // available reviewed row; ordinary protection and zone damage still apply.
                    foreach(var p in anchors){
                        double dx=Math.Max(0,Math.Max(zone.X-p.X,p.X-zone.MaxX)),dz=Math.Max(0,Math.Max(zone.Z-p.Z,p.Z-zone.MaxZ));
                        double score=-(dx*dx+dz*dz);if(score<=bestScore||!plan.Safe(p,false)||!finalHostSafety(p))continue;
                        bestScore=score;best=p;valid=true;
                    }
                }
            }
            return new R24LanSourceSpawnChoice(best,valid,grid,count,bestScore);
        }
    }
}
