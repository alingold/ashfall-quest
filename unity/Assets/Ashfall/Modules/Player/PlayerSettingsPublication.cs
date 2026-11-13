using System;
using Ashfall.Core;
namespace Ashfall.Modules.Player
{
    // Trusted single coordinator lane. This gate is bound once, never supplied by ingress.
    public sealed class PlayerSettingsPublication
    {
        readonly PlayerInput input; readonly PlayerTuning target; readonly IAuthorityGate authority;
        readonly string session,actor;readonly long epoch;
        public long Revision {get;private set;}
        public IPlayerSettingsPort Effective {get;private set;}
        public PlayerSettingsPublication(PlayerInput input,PlayerTuning target,IAuthorityGate authority,string session,string actor,long epoch)
        {
            if(input==null||target==null||authority==null||String.IsNullOrEmpty(session)||String.IsNullOrEmpty(actor)||epoch<0)throw new ArgumentException("settings binding");
            this.input=input;this.target=target;this.authority=authority;this.session=session;this.actor=actor;this.epoch=epoch;
        }
        public RuleResult Publish(CommandContext context,long revision,PlayerTuning next,IPlayerSettingsPort effective,DominantHand dominant,SprintBinding sprint)
        {
            if(revision!=Revision||Revision==long.MaxValue||!context.IsAuthority||context.SessionId!=session||context.ActorId!=actor||context.AuthorityEpoch!=epoch||
                String.IsNullOrEmpty(context.CommandId)||context.Tick<0)return RuleResult.Reject("Settings scope/revision");
            if(next==null||effective==null||next.MaximumWalkMultiplier!=target.MaximumWalkMultiplier||
                !PlayerInput.Finite(next.WalkMultiplier)||next.WalkMultiplier<.25f||next.WalkMultiplier>target.MaximumWalkMultiplier||
                !PlayerInput.Finite(next.SnapRadians)||next.SnapRadians<(float)(Math.PI/12)||next.SnapRadians>(float)Math.PI||
                !PlayerInput.Finite(next.SmoothRadiansPerSecond)||next.SmoothRadiansPerSecond<(float)(Math.PI/6)||next.SmoothRadiansPerSecond>(float)(Math.PI*4)||
                !Enum.IsDefined(typeof(TurnStyle),next.Turning)||!Enum.IsDefined(typeof(DominantHand),dominant)||!Enum.IsDefined(typeof(SprintBinding),sprint))
                return RuleResult.Reject("Settings profile/range");
            var sprintSettings=effective as IPlayerSprintSettingsPort;
            if(sprintSettings!=null&&(!PlayerInput.Finite(sprintSettings.StaminaSeconds)||sprintSettings.StaminaSeconds<2||sprintSettings.StaminaSeconds>20||
                !PlayerInput.Finite(sprintSettings.RecoverySeconds)||sprintSettings.RecoverySeconds<1||sprintSettings.RecoverySeconds>20))return RuleResult.Reject("Sprint settings range");
            var accepted=authority.Validate(context);if(!accepted.Accepted)return accepted;
            PlayerSettingsSnapshot snapshot;
            try{snapshot=PlayerSettingsSnapshot.Capture(effective);}
            catch(ArgumentException){return RuleResult.Reject("Settings snapshot values");}
            // All checks precede publication. No callbacks or allocations below this point.
            input.Disarm();target.WalkMultiplier=next.WalkMultiplier;target.SnapRadians=next.SnapRadians;
            target.SmoothRadiansPerSecond=next.SmoothRadiansPerSecond;target.SmoothAnalog=next.SmoothAnalog;
            target.SmoothEaseIn=next.SmoothEaseIn;target.Turning=next.Turning;
            input.Ownership.Dominant=dominant;input.Ownership.Sprint=sprint;Effective=snapshot;Revision++;
            return RuleResult.Accept();
        }
    }
}
