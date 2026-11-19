#if UNITY_6000_0_OR_NEWER
using Ashfall.Modules.HealthStates;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        void TickLocalMatchFallFailsafe()
        {
            var life=ActualLocalLife;
            if(roundStarted&&!switchingMap&&(life==LifeState.Alive||life==LifeState.Downed)&&Map.IsBelowFallFailsafe(Rig.transform.position.y))
                Gameplay.ApplyLocalMatchFall();
        }

        void TickLocalWarmupFallFailsafe()
        {
            if(!roundStarted||switchingMap||ActualMatchRules!=null||Lobby.Visible||
                LanSession!=null&&LanSession.Lobby!=null||!Map.IsBelowFallFailsafe(Rig.transform.position.y))return;
            // The current local warm-up entry already selected and committed this map's spawn.
            if(!Gameplay.ResetMapVitals())return;
            ResetActorLifePresentation();
            Rig.SetSpawn(activeSpawn,Map.Bounds.center);Calibration.NotifyRootPoseReset();
        }
    }
}
#endif
