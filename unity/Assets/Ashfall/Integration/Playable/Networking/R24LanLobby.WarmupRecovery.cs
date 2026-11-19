using System;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.Playable.Networking
{
    // Bound by the actual host map owner; no client-supplied origin/height flag.
    public interface IR26LanFallFrame
    {bool IsBelowFallFailsafe(string map,Float3 representedWorldFeet);}
    public sealed partial class R24LanLobby
    {
        public int WarmupRecoveries{get;private set;}public string LastWarmupRecoveryFailure{get;private set;}
        bool TryWarmupRecoveryPose(string actor,out R24LanRawTrackingSample sample)
        {
            sample=null;R24LanRawTrackingHistory history;R24LanSpawn placement;
            if(State!=R24LanLobbyState.Hosting||!rawTracking.TryGetValue(actor,out history)||history.Latest==null||!TryReadSpawn(actor,out placement)||history.Latest.Pose.SpawnRevision!=placement.Revision||!RawTrackingMapReady(actor,history.Latest.Pose.MapHash))return false;
            // Source net.poses retains the latest accepted current-revision root.
            // This is recovery observation only, never the fresh combat-read API.
            sample=history.Latest;return true;
        }
        void TickWarmupRecovery()
        {
            var frame=spawnWorld.Bound as IR26LanFallFrame;
            if(State!=R24LanLobbyState.Hosting||hostHealth==null||frame==null||SelectedMap==null||SelectedMap.Rules!=null||!SelectedMap.Active||SelectedMap.Cancelled||sessions.Stage!=SessionStage.Lobby||!SpawnWorldReady())return;
            var rules=SelectedMap.SourceRules;if(rules==null)return;
            foreach(var member in sessions.CaptureMembers()){
                R24LanRawTrackingSample accepted;var health=hostHealth.Read(member.CanonicalActorId);
                if(!member.Loaded||!member.HasPosition||member.Life!=SessionLife.Alive||health==null||!health.Connected||health.Life!=LifeState.Alive||!TryWarmupRecoveryPose(member.ActorId,out accepted)||!frame.IsBelowFallFailsafe(MapId,accepted.Pose.Root.Position))continue;
                using(spawnWorld.Begin(MapId,member.ActorId,true)){
                    var sites=spawnWorld.Sites(MapId,true);if(sites==null)continue;
                    foreach(var site in sites){
                        if(site==null||site.CapsulePlatform||(site.Team!=-1&&site.Team!=member.Team)||!spawnWorld.IsSafe(MapId,member.ActorId,site.Feet))continue;
                        var result=sessions.RecoverWarmupHealth(LifecycleContext(),member.ActorId,hostHealth,site,rules.StartingHealth,rules.StartingShield,clock.Now+1);
                        if(result.Accepted){WarmupRecoveries++;LastWarmupRecoveryFailure=null;break;}LastWarmupRecoveryFailure=result.Reason;
                    }
                }
                SyncHostHealth();
            }
        }
    }
}
