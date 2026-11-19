#if UNITY_6000_0_OR_NEWER
using System;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        R24LanPlayerHealthView lanPlayerHealth;long appliedLanHealthGeneration;int appliedLanDeaths;
        void BindLanPlayerHealth()
        {
            if(!LanRoundBound)return;if(LanSession==null||LanSession.Lobby==null||LanSession.Lobby.SelectedMap==null||LanSession.Lobby.SelectedMap.Rules==null)throw new InvalidOperationException("Actual LAN round required for Health view");
            if(lanPlayerHealth!=null)throw new InvalidOperationException("LAN Health binding replacement requires lifecycle cleanup");lanPlayerHealth=new R24LanPlayerHealthView(LanSession.Lobby);Gameplay.BindPlayerHealthView(lanPlayerHealth);appliedLanHealthGeneration=0;appliedLanDeaths=0;UI.SetAuthoritativeHealthPending(true);
        }
        public bool BindLanWarmupHealth()
        {
            var lobby=LanSession==null?null:LanSession.Lobby;
            if(lanPlayerHealth!=null||LanRoundBound||!Ready||!roundStarted||lobby==null||lobby.SelectedMap==null||lobby.SelectedMap.Rules!=null||!lobby.SelectedMap.Active||lobby.SelectedMap.Cancelled||Map.MapId!=lobby.SelectedMap.MapId||Map.ManifestSha256!=lobby.SelectedMap.ManifestHash)return false;
            lanPlayerHealth=new R24LanPlayerHealthView(lobby);Gameplay.BindPlayerHealthView(lanPlayerHealth);appliedLanHealthGeneration=0;appliedLanDeaths=0;UI.SetAuthoritativeHealthPending(true);return true;
        }
        void ClearLanPlayerHealth()
        {if(UI!=null)UI.SetAuthoritativeHealthPending(false);if(lanPlayerHealth==null)return;if(Gameplay!=null&&!Gameplay.UnbindPlayerHealthView(lanPlayerHealth))throw new InvalidOperationException("LAN player Health view ownership changed");lanPlayerHealth.Dispose();lanPlayerHealth=null;appliedLanHealthGeneration=0;appliedLanDeaths=0;}
        public void RefreshLanPlayerHealth()
        {
            if(lanPlayerHealth==null||LanSession==null||LanSession.Lobby==null||!Ready||Gameplay==null||Lobby!=null&&Lobby.Visible)return;
            var lobby=LanSession.Lobby;R24LanHealthState health;R24LanSpawn placement;
            if(lanPlayerHealth.Read()==null||!lobby.TryReadHealth(lobby.OwnActor,out health)||!lobby.TryReadSpawn(lobby.OwnActor,out placement)||!ApplyLanSpawn(placement)){UI.SetAuthoritativeHealthPending(true);UI.SetLocalMatchLifeState(null,0);return;}
            if(appliedLanHealthGeneration!=0&&appliedLanHealthGeneration!=health.LifeGeneration)ResetActorLifePresentation();
            if(appliedLanHealthGeneration!=0&&health.Deaths>appliedLanDeaths&&ActualMatchRules!=null)
                LanSession.RestoreRespawnLoadout(ActualMatchRules);
            LanSession.ObserveInventoryLife(health.Epoch,health.Life==Ashfall.Modules.HealthStates.LifeState.Dead);
            appliedLanDeaths=health.Deaths;appliedLanHealthGeneration=health.LifeGeneration;PublishMatchLife(false);UI.SetAuthoritativeHealthPending(false);
        }
    }
}
#endif
