#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.MatchZone;using Ashfall.Modules.Sessions;using Ashfall.Integration.Playable.Calibration;using Ashfall.Integration.Playable.TeamLaunch;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        sealed partial class RootSpawnWorld:IR24LanSpawnWorld
        {
            readonly R24LanSession owner;readonly Collider[] overlaps=new Collider[64];
            internal RootSpawnWorld(R24LanSession owner){this.owner=owner;}
            bool Ready(string map){return owner.peer!=null&&owner.root!=null&&owner.root.Ready&&owner.root.Map.IsLoaded&&owner.root.Map.MapId==map&&owner.peer.Lobby.SelectedMap!=null&&owner.root.Map.ManifestSha256==owner.peer.Lobby.SelectedMap.ManifestHash;}
            public bool ReadyForSpawn(string map,bool warmup)
            {return Ready(map)&&(!map.StartsWith("archive:",StringComparison.Ordinal)||owner.root.ArchiveWorldReady&&owner.root.ArchiveWorldMapId==map)&&owner.root.LaunchMapCatalog!=null&&owner.root.LaunchMapCatalog.Ready&&(warmup||owner.root.LanRoundBound&&owner.root.MatchActive&&owner.root.MatchRound==owner.peer.Lobby.SelectedMap.Round&&owner.root.LanRoundAssetsReady(owner.peer.Lobby.SelectedMap.Rules));}
            static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
            bool Floor(Vector3 desired,out Vector3 feet)
            {feet=desired;RaycastHit hit;if(!Physics.Raycast(desired+Vector3.up*2,Vector3.down,out hit,6,1<<0,QueryTriggerInteraction.Ignore)||hit.normal.y<=.7f)return false;feet=hit.point+Vector3.up*.12f;return true;}
            public SpawnSite[] Sites(string map,bool warmup)
            {
                if(!ReadyForSpawn(map,warmup))return new SpawnSite[0];var root=owner.root;var sites=new List<SpawnSite>();bool archive=map.StartsWith("archive:",StringComparison.Ordinal);Vector3 origin=warmup?root.Map.WarmupSpawn:root.Map.GameSpawn;
                if(warmup)return WarmupSites(map,archive,origin);
                R24LanSpawn previousDeployment;bool firstDeployment=!owner.peer.Lobby.TryReadSpawn(owner.peer.Lobby.SpawnActorInFlight,out previousDeployment)||previousDeployment.Deaths==0;
                if(firstDeployment&&R26MapPodPolicy.UsesPods(map)&&root.TeamLaunch!=null&&root.TeamLaunch.Eligible&&root.TeamLaunch.Runtime.Layout!=null){
                    var layout=root.TeamLaunch.Runtime.Layout;
                    string actor=owner.peer.Lobby.SpawnActorInFlight;int team=-1;var assignments=new List<R24LaunchAssignment>();
                    foreach(var member in owner.peer.Lobby.Members){
                        if(member.Actor==actor)team=member.Team;R24LanSpawn other;R24LanPodState ride;if(owner.peer.Lobby.TryReadPod(member.Actor,out ride)||member.Team<0||!owner.peer.Lobby.TryReadSpawn(member.Actor,out other))continue;
                        int platform=layout.PlatformAt(other.Feet);if(platform<0)continue;var local=layout.Station(platform).Local(other.Feet);int slot=Mathf.RoundToInt(local.X/3.6f+5.5f);if(slot>=0&&slot<R24LaunchLayout.Slots)assignments.Add(new R24LaunchAssignment(member.Actor,layout.PodId(platform,slot),member.Team,slot,other.Feet,platform));
                    }
                    if(team>=0){var assigned=layout.Assign(actor,team,assignments,owner.peer.Lobby.PodSpent);if(assigned!=null)sites.Add(new SpawnSite("deck:"+team,assigned.Spawn,team,false,true));}return sites.ToArray();
                }else if(!archive){
                    // Source net_bridge1000 built-in arena candidates, reflected Z once.
                    R24LaunchMapInfo actualFrame;if(!root.LaunchMapCatalog.TryRead(map,out actualFrame))return new SpawnSite[0];
                    for(int i=0;i<R24LanLobby.Capacity;i++){
                        Vector3 desired=map=="cyber_bird"?root.Map.transform.TransformPoint(V(R24LanSpawnCoordinates.CyberArena(actualFrame.RepresentedOriginUnity,i))):V(R24LanSpawnCoordinates.BuiltinArena(F(origin),i));RaycastHit hit;
                        if(Physics.Raycast(desired+Vector3.up*20,Vector3.down,out hit,50,1<<0,QueryTriggerInteraction.Ignore)&&hit.normal.y>=.65f)sites.Add(new SpawnSite("arena:"+i,F(hit.point+Vector3.up*.2f),-1,false,false));
                    }
                }
                if(archive&&!warmup){
                    string actor=owner.peer.Lobby.SpawnActorInFlight;int rotation=0,team=-1;var neighbours=new List<R24LanSpawnNeighbour>();var members=owner.peer.Lobby.Members;Float3 varSource;
                    for(int i=0;i<members.Length;i++){
                        var member=members[i];if(member.Actor==actor){rotation=i;team=member.Team;}
                        R24LanRawTrackingSample raw;R24LanSpawn placed;
                        if(owner.peer.Lobby.TryReadRawTracking(member.Actor,owner.peer.Lobby.EstimatedHostTime,out raw)&&root.TryArchiveWorldToSource(V(raw.Pose.Root.Position),out varSource))neighbours.Add(new R24LanSpawnNeighbour(member.Actor,member.Team,varSource));
                        else if(owner.peer.Lobby.TryReadSpawn(member.Actor,out placed)&&root.TryArchiveWorldToSource(V(placed.Feet),out varSource))neighbours.Add(new R24LanSpawnNeighbour(member.Actor,member.Team,varSource));
                    }
                    var rules=owner.peer.Lobby.SelectedMap.Rules;if(rules==null)return new SpawnSite[0];R24LanSpawn previous;bool initial=!owner.peer.Lobby.TryReadSpawn(actor,out previous)||previous.Deaths==0;
                    bool boxedRespawn=BoxedFfaRespawn(actor);if(boxedRespawn)initial=false;
                    R24ZoneSquare? next=SpawnZone(actor);
                    var choice=R24LanSourceSpawnSelection.Archive(root.ArchiveWorldInfo,root.ArchiveWorldPlan,new ArchiveQueries(root),actor,rotation,team,rules.Teams,initial,neighbours,next,delegate(int count){return UnityEngine.Random.Range(0,count);},delegate(Float3 local){Vector3 world;return root.TryArchiveSourceToWorld(local,out world)&&IsSafe(map,actor,F(world));},owner.peer.Lobby.SelectedMap.Round);
                    Vector3 accepted;if(choice.Validated&&root.TryArchiveSourceToWorld(choice.Position,out accepted))sites.Add(new SpawnSite("archive:source-selected",F(accepted),-1,true,false));return sites.ToArray();
                }
                if(!archive&&sites.Count==0)sites.Add(new SpawnSite("prepared:game",F(origin),-1,false,false));return MatchSides(sites);
            }
            sealed class ArchiveQueries:IR24ArchiveRecoveryWorld
            {
                readonly R23PlayableLoop root;internal ArchiveQueries(R23PlayableLoop root){this.root=root;}
                public bool Floor(Float3 from,float depth,out Float3 point){return root.ArchiveWorldFloor(from,depth,out point);}
                public bool Outside(Float3 point){return root.ArchiveWorldOutside(point);}
                public bool Room(Float3 floor,bool initial){return !initial&&root.ArchiveWorldRoom(floor);}
            }
            R24ZoneSquare? SpawnZone(string actor)
            {
                var root=owner.root;var zone=root.MatchZoneOwner;
                if(zone==null||zone.State==null)return null;
                // An opening team row can lie outside the upcoming shrink while still in play.
                // Use the live boundary for first deployment and the destination for respawns.
                R24LanSpawn placed;
                if(owner.peer.Lobby.SelectedMap.SourceRules.Teams>0&&!owner.peer.Lobby.TryReadSpawn(actor,out placed))
                    return zone.State.Current(root.MatchClock);
                return zone.State.Next;
            }
            bool BoxedFfaRespawn(string actor)
            {
                var lobby=owner.peer.Lobby;var rules=lobby.SelectedMap.Rules;var info=owner.root.ArchiveWorldInfo;
                if(rules==null||rules.Teams!=0||rules.Mode!="respawn"||info==null||!R26MapPodPolicy.InteriorSpawn(lobby.MapId,info.SourceSha256))return false;
                var member=lobby.ReadAuthorityMember(actor);return member!=null&&member.Life==SessionLife.Eliminated;
            }
            public bool IsSafe(string map,string actor,Float3 position)
            {
                if(!Ready(map)||!position.IsFinite)return false;var root=owner.root;R24LaunchMapInfo info;
                if(root.LaunchMapCatalog==null||!root.LaunchMapCatalog.Ready||!root.LaunchMapCatalog.TryRead(map,out info))return false;
                Vector3 feet=V(position),local=root.Map.transform.InverseTransformPoint(feet)-V(info.RepresentedOriginUnity);float sourceX=local.x,sourceZ=-local.z;
                // Source native warmup hubs may lie outside deathmatch bounds.
                // Only host-supplied warmup sites enter Session.Place; retain them.
                bool warmup=owner.peer.Lobby.SelectedMap.Rules==null;
                if(!warmup&&(sourceX<info.CatalogMinX||sourceX>info.CatalogMaxX||sourceZ<info.CatalogMinZ||sourceZ>info.CatalogMaxZ))return false;
                bool deck=root.TeamLaunch!=null&&root.TeamLaunch.Eligible;
                if(root.MatchActive&&!deck){var zone=SpawnZone(actor);if(zone.HasValue&&!zone.Value.Contains(sourceX,sourceZ)&&!BoxedFfaRespawn(actor))return false;}
                RaycastHit floor;
                if(!Physics.Raycast(feet+Vector3.up*.3f,Vector3.down,out floor,.8f,1<<0,QueryTriggerInteraction.Ignore)||(map.StartsWith("archive:",StringComparison.Ordinal)?floor.normal.y<=.7f:floor.normal.y<.65f))return false;
                if(!root.Map.SpawnClearsShell(feet))return false;
                float height=R23CalibrationState.TargetHeadTop+.10f;const float radius=.27f;
                Vector3 bottom=feet+Vector3.up*(radius+.02f),top=feet+Vector3.up*(height-radius+.02f);
                int count=Physics.OverlapCapsuleNonAlloc(bottom,top,radius,overlaps,(1<<0)|(1<<8)|(1<<World.R23PlayableMap.PlayerOnlyLayer),QueryTriggerInteraction.Ignore);if(count>=overlaps.Length){Array.Clear(overlaps,0,overlaps.Length);return false;}
                bool blocked=count!=0;Array.Clear(overlaps,0,count);if(blocked)return false;
                foreach(var member in owner.peer.Lobby.Members)if(member.Actor!=actor){R24LanSpawn other;if(owner.peer.Lobby.TryReadSpawn(member.Actor,out other)){R24LanHealthState life;if(owner.peer.Lobby.TryReadHealth(member.Actor,out life)&&life.Life==Ashfall.Modules.HealthStates.LifeState.Dead)continue;R24LanRawTrackingSample raw;Vector3 occupied=owner.peer.Lobby.TryReadRawTracking(member.Actor,owner.peer.Lobby.EstimatedHostTime,out raw)?V(raw.Pose.Root.Position):V(other.Feet);Vector3 delta=occupied-feet;if(Mathf.Abs(delta.y)<height&&new Vector2(delta.x,delta.z).sqrMagnitude<radius*radius*4)return false;}}
                return true;
            }
        }
        bool ApplyActualLanSpawn(R24LanSpawn spawn)
        {return root!=null&&peer!=null&&spawn!=null&&spawn.Actor==peer.Lobby.OwnActor&&spawn.Epoch==peer.Lobby.Epoch&&root.ApplyLanSpawn(spawn);}
    }
}
#endif
