#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.Sessions;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        sealed partial class RootSpawnWorld
        {
            SpawnSite[] MatchSides(List<SpawnSite> sites)
            {
                var rules=owner.peer.Lobby.SelectedMap.Rules;if(rules==null||rules.Teams==0)return sites.ToArray();
                var points=new List<Float3>();foreach(var site in sites)points.Add(site.Feet);var sides=R26SpawnSides.Split(points,rules.Teams);
                string actor=owner.peer.Lobby.SpawnActorInFlight;var member=owner.peer.Lobby.ReadAuthorityMember(actor);
                if(member==null)return new SpawnSite[0];
                int side=R26SpawnSides.AnchorSide(member.Team,rules.Teams,sites.Count);var result=new List<SpawnSite>();
                for(int i=0;i<sites.Count;i++)if(sides[i]==side)result.Add(new SpawnSite(sites[i].Id,sites[i].Feet,member.Team,sites[i].Interior,false));
                int allies=0;foreach(var row in owner.peer.Lobby.Members)if(row.Team==member.Team)allies++;
                if(result.Count<allies||sites.Count<rules.Teams)foreach(var anchor in result.ToArray())
                    for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++){
                        Vector3 feet;if((x!=0||z!=0)&&Floor(V(anchor.Feet)+new Vector3(x*1.4f,0,z*1.4f),out feet)&&Mathf.Abs(feet.y-anchor.Feet.Y)<1)
                            result.Add(new SpawnSite(anchor.Id+":"+x+":"+z,F(feet),member.Team,anchor.Interior,false));
                    }
                return result.ToArray();
            }
            SpawnSite[] WarmupSites(string map,bool archive,Vector3 origin)
            {
                var root=owner.root;var anchors=new List<Vector3>();
                if(archive){foreach(var p in root.ArchiveWorldPlan.WarmupSites){Vector3 at;if(root.TryArchiveSourceToWorld(p,out at))anchors.Add(at);}if(anchors.Count==0&&root.TryArchiveSourceToWorld(root.ArchiveWorldPlan.SourceWarmup,out origin))anchors.Add(origin);}
                else anchors.Add(origin);
                string actor=owner.peer.Lobby.SpawnActorInFlight;var members=owner.peer.Lobby.Members;int index=0;for(int i=0;i<members.Length;i++)if(members[i].Actor==actor)index=i;
                SpawnSite best=null;float score=-1;
                foreach(var anchor in anchors)for(int slot=0;slot<81;slot++){
                    // Authored feet first, then a small validated grid on that same lobby platform.
                    int cell=(slot+index)%81;Vector3 desired=anchor+new Vector3((cell%9-4)*.9f,0,(cell/9-4)*.9f),feet;
                    if(!Floor(desired,out feet)||Mathf.Abs(feet.y-anchor.y)>1.5f||!IsSafe(map,actor,F(feet)))continue;
                    float separation=1000000;
                    foreach(var member in members)if(member.Actor!=actor){R24LanSpawn other;if(owner.peer.Lobby.TryReadSpawn(member.Actor,out other)){
                        separation=Mathf.Min(separation,(feet-V(other.Feet)).sqrMagnitude);
                        R24LanRawTrackingSample raw;if(owner.peer.Lobby.TryReadRawTracking(member.Actor,owner.peer.Lobby.EstimatedHostTime,out raw))separation=Mathf.Min(separation,(feet-V(raw.Pose.Root.Position)).sqrMagnitude);
                    }}
                    if(best==null||separation>score){score=separation;best=new SpawnSite("warmup:"+index,F(feet),-1,archive,false);}
                }
                return best==null?new SpawnSite[0]:new[]{best};
            }
        }
    }
}
#endif
