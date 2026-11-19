using System;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.Playable.Networking
{
    // Host composition binds the actual Root zone owner (R24MatchZoneHost via
    // R24MatchZone). worldRoot is the member's represented Unity world root;
    // protection is round-relative, matching the zone owner's own clock.
    public delegate bool R24LanZoneDamageQuery(string actor,Float3 worldRoot,double protectionUntilRoundSeconds,out float amount);
    public sealed partial class R24LanLobby
    {
        R24LanZoneDamageQuery zoneDamage;
        public int RemoteFallDeaths{get;private set;}public int RemoteZoneDamageEvents{get;private set;}
        public void BindZoneDamageQuery(R24LanZoneDamageQuery query)
        {Check();if(State!=R24LanLobbyState.Hosting||query==null||zoneDamage!=null)throw new InvalidOperationException("One host zone owner required");zoneDamage=query;}
        bool TryHazardPose(string actor,out R24LanRawTrackingSample sample)
        {return TryWarmupRecoveryPose(actor,out sample)&&clock.Now-sample.Time<=1;}
        // A connected member's R24LanPlayerHealthView cannot mutate host Health,
        // so its local map fall check and zone step are presentation only. The
        // Health owner applies the same source rules from the member's latest
        // accepted current-revision raw root. The listen host keeps its own
        // local Root path (R24LanPlayerHealthView -> ApplyHostHealth*).
        void TickRemoteHazards()
        {
            if(State!=R24LanLobbyState.Hosting||hostHealth==null||roundRules==null||SelectedMap==null||!SelectedMap.Active||SelectedMap.Cancelled||SelectedMap.Results||SelectedMap.Rules==null||sessions.Stage!=SessionStage.Match||RoundCountdown>0)return;
            var frame=spawnWorld.Bound as IR26LanFallFrame;bool changed=false;
            foreach(var member in sessions.CaptureMembers()){
                if(member.ActorId==OwnActor||!member.Loaded||!member.HasPosition||PlatformProtected(member.ActorId))continue;
                var health=hostHealth.Read(member.CanonicalActorId);R24LanRawTrackingSample sample;
                if(health==null||!health.Connected||(health.Life!=LifeState.Alive&&health.Life!=LifeState.Downed)||!TryHazardPose(member.ActorId,out sample))continue;
                var root=sample.Pose.Root.Position;
                if(frame!=null&&frame.IsBelowFallFailsafe(MapId,root)){
                    if(hostHealth.ApplyMatchFallDeath(LifecycleContext(),member.CanonicalActorId,health.Revision).Accepted){RemoteFallDeaths++;changed=true;}
                    continue;
                }
                float amount;double protection=health.ProtectionUntil<=0?0:Math.Max(0,health.ProtectionUntil-SelectedMap.StartsAt);
                if(health.Life!=LifeState.Alive||zoneDamage==null||clock.Now<health.ProtectionUntil||!zoneDamage(member.ActorId,root,protection,out amount)||Single.IsNaN(amount)||Single.IsInfinity(amount)||amount<=0)continue;
                // Zone: direct, bypasses shield and the generic damage multiplier (same as the local view).
                if(hostHealth.ApplyLoss(LifecycleContext(),member.CanonicalActorId,health.Revision,Math.Min(health.Health,amount),0,true).Accepted){RemoteZoneDamageEvents++;changed=true;}
            }
            if(changed)SyncHostHealth();
        }
    }
}
