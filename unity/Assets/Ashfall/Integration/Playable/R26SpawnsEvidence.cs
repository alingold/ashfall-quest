#if UNITY_6000_0_OR_NEWER
using System.Collections;using System.Globalization;using UnityEngine;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R25NetBot
    {
        void Awake(){if(Arg("-ashfallSpawnsEvidence")=="true"){requestedMap=true;StartCoroutine(SpawnsEvidence());}}
        IEnumerator SpawnsEvidence()
        {
            while(root==null||!root.Ready||log==null||root.LanSession.Lobby==null)yield return null;
            var lobby=root.LanSession.Lobby;long seen=-1;int stage=0;float phaseAt=Time.realtimeSinceStartup;
            string[] presets={"free_for_all","team_respawn","team_revive"};
            while(!closed){
                if(lobby!=root.LanSession.Lobby){Write("spawns-failure","room-lost");yield break;}
                var map=lobby.SelectedMap;float now=Time.realtimeSinceStartup;
                if(mode=="host"&&lobby.MemberCount>=4&&stage==0){
                    var rules=R24PlayableMatchRules.ForPreset(presets[0],root.Map.MapId).With("zone_enabled",false).With("damage",0f);
                    if(lobby.BeginMapSelection(root.Map.MapId,root.Map.ManifestSha256,rules)){stage=1;phaseAt=now;Write("spawns-action","warmup requested");}
                }
                if(map!=null&&map.Active&&!root.LanSession.MapEntryBlocked){
                    bool all=true;foreach(var member in lobby.Members){R24LanSpawn p;if(!lobby.TryReadSpawn(member.Actor,out p))all=false;}
                    if(all&&seen!=map.Epoch){seen=map.Epoch;phaseAt=now;
                        foreach(var member in lobby.Members){R24LanSpawn p;if(lobby.TryReadSpawn(member.Actor,out p))Write("spawns-position","epoch="+map.Epoch+" phase="+(map.Rules==null?"warmup":map.Rules.Mode)+" teams="+map.SourceRules.Teams+" actor="+member.Actor+" team="+p.Team+" feet="+p.Feet.X.ToString("F3",CultureInfo.InvariantCulture)+","+p.Feet.Y.ToString("F3",CultureInfo.InvariantCulture)+","+p.Feet.Z.ToString("F3",CultureInfo.InvariantCulture));}
                        Capture(300+(int)map.Epoch);
                        Write("spawns-rig","epoch="+map.Epoch+" own="+lobby.OwnActor+" root="+root.Rig.transform.position.ToString("F3"));
                    }
                    if(mode=="host"&&all&&seen==map.Epoch&&now-phaseAt>8){
                        if(stage==1||stage==3||stage==5){int which=(stage-1)/2;var rules=R24PlayableMatchRules.ForPreset(presets[which],root.Map.MapId).With("zone_enabled",false).With("damage",0f);root.LanSession.RouteRoundRequest(rules);Write("spawns-action","START MATCH "+presets[which]);stage++;phaseAt=now;}
                        else if(stage==2||stage==4||stage==6){if(root.LanSession.HostReturnAllPlayersToWarmupSpawns()){Write("spawns-action","RETURN WARMUP");stage++;phaseAt=now;}}
                        else if(stage==7){Write("spawns-complete","warmup -> FFA -> warmup -> TDM -> warmup -> revive -> warmup");yield break;}
                    }
                }
                yield return new WaitForSeconds(.25f);
            }
        }
    }
}
#endif
