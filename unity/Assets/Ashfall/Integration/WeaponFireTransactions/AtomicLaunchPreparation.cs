using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;

namespace Ashfall.Integration.WeaponFireTransactions
{
    public static class AtomicLaunchPreparation
    {
        // Concrete safe probe for current composition. CombatHealthBridge has no
        // opaque prepared launch reservation and cannot publish future impacts
        // after WeaponLease retirement. Never debit ammo before that reservation.
        public static RuleResult Prepare(WeaponLifetimes weapons, CommandContext command,
            string instanceId, HandlingInput input, long equip, long life, long connection,
            long expires, IWeaponFireBinding binding)
        {
            if(weapons==null)return RuleResult.Reject("weapon ledger missing");
            IPreparedWeaponFire fire;
            RuleResult r=weapons.PrepareFire(command,instanceId,input,equip,life,connection,expires,binding,out fire);
            if(!r.Accepted)return r;
            try
            {
                r=fire.Validate(command.Tick);
                return r.Accepted?RuleResult.Reject("combat-prepared-launch-api-missing"):r;
            }
            finally { fire.Abort(); }
        }
    }
}
