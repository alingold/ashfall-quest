using System;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Modes;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Presentation;

namespace Ashfall.Integration.PersonalSettings
{
    // Called after authoritative mutations on the coordinator lane. Does not drain
    // Health's shared outbox; the publisher can fan out each real transition here.
    public sealed class PresentationStateBinding : IPresentationStateSource
    {
        private readonly HealthService health;
        private readonly Session session;
        private readonly RoundController round;
        private readonly string actor;
        private readonly Func<double> now;
        private long sequence=-1;
        private HealthSnapshot previous;
        private readonly HudModel hud;
        public float Stamina, StaminaMaximum=6;
        // Zone geometry remains World-owned. Coordinator supplies its current result.
        public bool ZoneActive, OutsideZone;
        public bool NoTimeCap, FinalZone, ZoneMoving;
        public double ZoneHoldSeconds;
        public float ZoneDamagePerSecond;
        public PresentationStateBinding(HealthService health,Session session,RoundController round,string actor,Func<double> now,HudModel hud)
        {
            if(health==null||session==null||round==null||String.IsNullOrEmpty(actor)||now==null||hud==null)throw new ArgumentException("presentation sources");
            this.health=health;this.session=session;this.round=round;this.actor=actor;this.now=now;this.hud=hud;
            previous=health.Read(actor);
        }
        public void OnHealthTransition(HealthTransition change)
        {
            if(change==null||change.After==null||change.After.Id!=actor||change.SessionId!=session.Id||change.AuthorityEpoch!=session.Epoch||change.Sequence<=sequence)return;
            sequence=change.Sequence;
            if(previous!=null && previous.LifeGeneration==change.After.LifeGeneration && change.Kind=="damage")
                hud.Damage(Math.Max(0,previous.Health+previous.Shield-change.After.Health-change.After.Shield));
            previous=change.After;
        }
        public HudInput Capture()
        {
            var h=health.Read(actor);var member=session.Get(actor);
            bool absent=h==null||member==null||!h.Connected;
            return new HudInput {Health=h==null?0:h.Health,Shield=h==null?0:h.Shield,
                Stamina=Stamina,StaminaMaximum=StaminaMaximum,
                Dead=absent||h.Life!=LifeState.Alive,
                NetworkGhost=!absent&&(member.Life==SessionLife.Spectator||h.Life==LifeState.Downed),
                Complete=absent||session.Stage==SessionStage.Loading||round.Stage==RoundStage.Complete||round.Stage==RoundStage.Results,
                MatchSeconds=round.Stage==RoundStage.Match?round.SecondsRemaining(now()):0,
                ZoneActive=round.Stage==RoundStage.Match&&ZoneActive,OutsideZone=OutsideZone,
                NoTimeCap=NoTimeCap,FinalZone=FinalZone,ZoneMoving=ZoneMoving,
                ZoneHoldSeconds=ZoneHoldSeconds,ZoneDamagePerSecond=ZoneDamagePerSecond};
        }
    }
}
