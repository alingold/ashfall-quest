using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.ProjectileWeapons
{
    // Bounded admission adapter against the ACTUAL WeaponHandling-r3/1 API.
    // Call serially under the shared host lane. No Capture/Restore, Apply(Fire),
    // reflection, replacement ammo, or caller-supplied commit callback is used.
    // It deliberately cannot return success until prepared fire exists upstream.
    public sealed class PreparedProjectileWeapons
    {
        public const string MissingFireApi="projectile-prepared-fire-api-missing";
        readonly WeaponLifetimes weapons;
        readonly IAuthorityGate authority;
        readonly string session;
        readonly long epoch;
        public PreparedProjectileWeapons(WeaponLifetimes ledger,IAuthorityGate gate,string sessionId,long authorityEpoch)
        {
            if(ledger==null||gate==null||String.IsNullOrEmpty(sessionId)||authorityEpoch<0)throw new ArgumentException("projectile weapons configuration");
            weapons=ledger;authority=gate;session=sessionId;epoch=authorityEpoch;
        }
        // Verify current equipped identity using the real opaque transfer reservation,
        // then ABORT it. A transfer is never mistaken for a shot or ammo reservation.
        // launchId/expiry bounds match the projectile producer; no identities are consumed.
        public RuleResult PrepareFire(CommandContext actor,string instanceId,string launchId,long expiresTick)
        {
            if(!actor.IsAuthority||actor.SessionId!=session||actor.AuthorityEpoch!=epoch||String.IsNullOrEmpty(actor.ActorId)||
               String.IsNullOrEmpty(actor.CommandId)||actor.CommandId.Length>128||actor.Tick<0||
               String.IsNullOrEmpty(instanceId)||instanceId.Length>64||String.IsNullOrEmpty(launchId)||launchId.Length>64||
               expiresTick<actor.Tick||expiresTick-actor.Tick>600)return RuleResult.Reject("projectile-fire-envelope");
            RuleResult allowed=authority.Validate(actor);if(!allowed.Accepted)return allowed;
            IPreparedWeaponTransfer ownership;
            RuleResult prepared=weapons.PrepareTransfer(actor,instanceId,actor.ActorId,actor.AuthorityEpoch,out ownership);
            if(!prepared.Accepted)return prepared;
            try {
                RuleResult valid=ownership.Validate();if(!valid.Accepted)return valid;
                return RuleResult.Reject(MissingFireApi);
            } finally {ownership.Abort();}
        }
    }
}
