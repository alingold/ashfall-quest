using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
namespace Ashfall.Integration.InventoryTransfers
{
    // Host-only, side-effect-free eligibility. Must authenticate context and derive
    // alive/ghost/tracking/paused/climb/reload/input eligibility from server state.
    public interface ITransferEligibility { RuleResult Validate(CommandContext context); }
    // Host physics reservation of ALL exact members as one immutable batch. No
    // callbacks. Commit false means zero mutation; success cannot throw. Abort
    // releases every reservation, is idempotent and never undoes committed drops.
    public interface IDropBatch
    {
        EvictedInstance[] Placements { get; }
        bool StillValid { get; }
        bool Commit();
        void Abort();
    }
    public interface IDropPlanner
    {
        bool Prepare(CommandContext context, ItemInstance[] members, out IDropBatch batch);
    }
}
