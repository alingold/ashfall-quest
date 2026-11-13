using System;
using Ashfall.RecoveryContracts;
using Wire=Ashfall.RecoveryContracts.RecoveryWire;
namespace Ashfall.Integration.InventoryRecovery
{
    // Provision through an authenticated root channel, never derive from untrusted file bytes.
    public sealed class TrustedCheckpointReceipt
    {
        public readonly CheckpointBinding Expected;
        public readonly string Digest;
        public TrustedCheckpointReceipt(CheckpointBinding expected,string digest,long minimumSequence){Wire.Require(expected!=null&&expected.Sequence>=minimumSequence&&digest!=null&&digest.Length==64);Expected=expected;Digest=digest;}
    }
}

