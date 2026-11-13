using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Integration.InventoryRecovery;
using Wire=Ashfall.RecoveryContracts.RecoveryWire;
namespace Ashfall.Integration.ConsumableTransactions
{
    public static class ConsumablePickupCheckpoint
    {
        // Receipt is a trusted root input; constructing it from this image is not authentication.
        public static bool TryRestore(byte[] untrusted,TrustedCheckpointReceipt receipt,IAuthorityGate authority,IPickupActors actors,IPickupTransactions transactions,IInteractionQuery query,out PickupRules quarantine)
        {
            quarantine=null;
            try{
                Wire.Require(receipt!=null&&untrusted!=null&&untrusted.Length<=Wire.Limit);
                byte[] image=(byte[])untrusted.Clone();Wire.Require(Wire.Hash(image)==receipt.Digest);
                quarantine=PickupCheckpointCodec.Restore(image,receipt.Expected,authority,actors,transactions,query);return true;
            }catch(InvalidDataException){return false;}catch(EndOfStreamException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
        }
        public static string LiveAdoptionBlocker {get{return "Pickup quarantine has no adoption port. Common durable Inventory/Health sequence and ACK prefix, stamina, build/reserve receipts, SessionWeapons/world registry and root epoch/connection adoption remain required.";}}
    }
}
