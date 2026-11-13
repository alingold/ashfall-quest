using System;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.Combat;
using Ashfall.Integration.ProjectileWeapons;

namespace Ashfall.Integration.SourceExplosiveAmmo
{
    // Compatibility admission, NOT an implemented explosive mechanic. Inventory
    // identities cannot represent the two throws held inside each source instance.
    // No speculative launch reservation, removal, rifle shot or refill is allowed.
    public static class ExplosiveAmmoAdmission
    {
        public const string MissingThrow = "missing-owner-api:WeaponLifetimes.PrepareSourceThrow";
        public const string MissingRocket = "missing-owner-api:WeaponLifetimes.PrepareSourceRocketFire";

        public static RuleResult Prepare(Inventory inventory, WeaponLifetimes weapons,
            IWeaponFireBinding binding, CommandContext host, CommandContext actor,
            WeaponLease lease, string launchId, HandlingInput input, long life,
            long connection, long expires, out PreparedShotLaunch prepared)
        {
            prepared = null;
            if (inventory == null || weapons == null || binding == null || lease == null ||
                !host.IsAuthority || !actor.IsAuthority || host.SessionId != actor.SessionId ||
                host.AuthorityEpoch != actor.AuthorityEpoch || host.Tick != actor.Tick ||
                actor.Tick < 0 || actor.AuthorityEpoch < 0 ||
                String.IsNullOrEmpty(actor.ActorId) || String.IsNullOrEmpty(actor.SessionId) ||
                String.IsNullOrEmpty(actor.CommandId) || actor.CommandId.Length > 128 ||
                String.IsNullOrEmpty(launchId) || launchId.Length > 64 ||
                lease.OwnerId != actor.ActorId || life < 0 || connection < 0 || input.Sequence < 0 ||
                expires < actor.Tick || expires - actor.Tick > WeaponLifetimes.MaximumPreparedFireTicks)
                return RuleResult.Reject("source-ammo-envelope");
            var snapshot = inventory.Capture();
            if (snapshot.SessionId != actor.SessionId || snapshot.ActorId != actor.ActorId ||
                snapshot.Epoch != actor.AuthorityEpoch || snapshot.Handoff || snapshot.Holstered ||
                snapshot.SelectedInstanceId != lease.InstanceId)
                return RuleResult.Reject("source-ammo-inventory-binding");
            ItemInstance item = null;
            foreach (var member in snapshot.Items)
                if (member.InstanceId == lease.InstanceId) item = member;
            if (item == null || item.DefinitionId != lease.Archetype ||
                (item.DefinitionId != "frag" && item.DefinitionId != "zone" && item.DefinitionId != "smaw"))
                return RuleResult.Reject("source-ammo-kind");
            var result = binding.Validate(actor, lease.InstanceId, lease.Generation, life, connection, input.Sequence);
            if (!result.Accepted) return result;
            // Never call PrepareFire with a fabricated firearm mechanism. Upstream
            // PreparedShotLaunch explicitly refuses these exact source archetypes.
            // These reason codes identify concrete missing OWNER ports, documented
            // with required parameters/state in API-REQUESTS.md, not a bool adapter.
            return RuleResult.Reject(item.DefinitionId == "smaw" ? MissingRocket : MissingThrow);
        }
    }
}
