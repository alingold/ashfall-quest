using System;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.InventoryItems;
using Ashfall.Core;
using Ashfall.Integration.InventoryRecovery;
namespace Ashfall.Integration.ConsumableTransactions
{
    // Inventory segment only. Root must checkpoint Health, Player stamina, Pickups
    // custody/replay/soda and SessionWeapons at the SAME durable sequence before
    // adoption. Returning quarantine confers no authority to resume gameplay.
    public static class ConsumableInventoryCheckpoint
    {
        public const string ApiVersion="ConsumableInventoryCheckpoint/1+InventoryRecovery/schema1";
        public static byte[] Capture(CheckpointBinding binding,Inventory inventory)
        {
            if(inventory==null)throw new ArgumentNullException("inventory");
            var catalog=SourceCatalog.Create();
            foreach(var item in inventory.Items)if(catalog.Get(item.DefinitionId).IsWeapon)
                throw new InvalidOperationException("Mixed custody needs actual weapon checkpoint members");
            return ColdInventoryCheckpoint.Capture(binding,catalog,inventory,new WeaponCheckpointMember[0]);
        }
        public static bool TryRestore(byte[] image,TrustedCheckpointReceipt receipt,IInventoryBoundary boundary,IAuthorityGate authority,out QuarantinedInventory quarantine)
        {
            quarantine=null;QuarantinedInventory candidate;var catalog=SourceCatalog.Create();
            if(!ColdInventoryCheckpoint.TryRestore(image,receipt,catalog,boundary,authority,out candidate))return false;
            foreach(var item in candidate.Inventory.Items)if(catalog.Get(item.DefinitionId).IsWeapon)return false;
            quarantine=candidate;return true;
        }
        public static string LiveAdoptionBlocker {get{return "Inventory-only quarantine; compose actual PickupCustody/schema1 at common durable Health/stamina/build/reserve/registry sequence with ACK prefix and root canonical reconnect adoption";}}
    }
}
