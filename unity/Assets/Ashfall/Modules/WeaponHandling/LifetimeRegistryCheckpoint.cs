using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Wire = Ashfall.RecoveryContracts.RecoveryWire;

namespace Ashfall.Modules.WeaponHandling
{
    public sealed partial class WeaponHandling
    {
        // Owner-private profile read; does not expose a mutable reserve or mechanics.
        internal WeaponMechanism LifetimeCheckpointMechanism { get { return mechanism; } }
        internal bool IsSourceExplosive { get { return sourceMechanics!=null; } }
    }
    // Root supplies canonical per-owner life/connection bindings and finite entitlement
    // provenance. These are codec inputs, never authentication or adoption receipts.
    public sealed class LifetimeCheckpointIdentity
    {
        public readonly string InstanceId, Provenance;
        public readonly CheckpointBinding Binding;
        public LifetimeCheckpointIdentity(string instanceId, string provenance, CheckpointBinding binding)
        {
            Wire.Require(!String.IsNullOrEmpty(instanceId) && !String.IsNullOrEmpty(provenance) && binding != null);
            InstanceId = instanceId; Provenance = provenance; Binding = binding;
        }
    }

    public sealed partial class WeaponLifetimes
    {
        public int CheckpointMemberCount { get { return entries.Count; } }
        public bool MatchesCheckpointMechanism(string id, WeaponMechanism expected)
        {
            Entry entry;
            if (id == null || !entries.TryGetValue(id, out entry)) return false;
            var handler = entry.Active ?? (entry.Pending == null ? null : entry.Pending.Frozen);
            return handler != null && !handler.IsSourceExplosive && handler.LifetimeCheckpointMechanism == expected;
        }
        // No publication into an existing registry. Root must authenticate the complete
        // containing checkpoint and prepare all authority/geometry before adopting this.
        public static WeaponLifetimes RestoreQuarantinedRegistry(byte[] bytes, CheckpointBinding expected,
            IWeaponLifetimeAuthority approval, IAuthorityGate authority)
        {
            Wire.Require(expected != null && approval != null && authority != null);
            ValidateCheckpointLayout();
            var registry = new WeaponLifetimes(expected.Session, approval);
            using (var reader = Wire.Reader(bytes))
            {
                Wire.Require(reader.ReadInt32() == 0x314c5741 && reader.ReadInt32() == 1);
                expected.Check(reader);
                int count = Wire.Count(reader, 64);
                var provenance = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < count; index++)
                {
                    string id = Wire.Text(reader), p = Wire.Text(reader);
                    string actor = Wire.Text(reader); long epoch = reader.ReadInt64();
                    long life = reader.ReadInt64(), connection = reader.ReadInt64();
                    bool pending = Wire.Bool(reader);
                    Wire.Require(id.Length > 0 && actor.Length > 0 && p.Length > 0 && provenance.Add(p) && !registry.entries.ContainsKey(id));
                    var binding = new CheckpointBinding(expected.Build, expected.Catalog, expected.Session,
                        actor, epoch, life, connection, expected.Sequence);
                    FiniteAmmoReserve reserve;
                    var handler = WeaponCheckpointCodec.Restore(Wire.Blob(reader), binding, p, authority, out reserve);
                    Wire.Require(handler.Live);
                    var entry = new Entry { Actor = actor, Epoch = epoch };
                    if (pending) entry.Pending = new WeaponTransfer(registry, handler, expected.Session, id, actor, epoch);
                    else entry.Active = handler;
                    registry.entries.Add(id, entry);
                }
                Wire.End(reader);
            }
            return registry;
        }

        static void ValidateCheckpointLayout()
        {
            Wire.Fields(typeof(WeaponLifetimes), "approval:Ashfall.Modules.WeaponHandling.IWeaponLifetimeAuthority\nentries:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.WeaponHandling.WeaponLifetimes+Entry]\nsession:System.String");
            Wire.Fields(typeof(Entry), "Active:Ashfall.Modules.WeaponHandling.WeaponHandling\nActor:System.String\nEpoch:System.Int64\nPending:Ashfall.Modules.WeaponHandling.WeaponTransfer");
        }

        public byte[] CaptureRegistryCheckpoint(CheckpointBinding binding, LifetimeCheckpointIdentity[] identities)
        {
            Wire.Require(binding != null && binding.Session == session && identities != null && entries.Count <= 64 && identities.Length == entries.Count);
            // Audit only this owner's layout. Newly introduced owner state must acquire
            // deliberate codec coverage instead of silently disappearing on restart.
            ValidateCheckpointLayout();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var provenance = new HashSet<string>(StringComparer.Ordinal);
            var handlers = new List<WeaponHandling>();
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0x314c5741); writer.Write(1); binding.Write(writer); writer.Write(identities.Length);
                foreach (var identity in identities)
                {
                    Entry entry;
                    Wire.Require(identity != null && ids.Add(identity.InstanceId) && provenance.Add(identity.Provenance) && entries.TryGetValue(identity.InstanceId, out entry));
                    // Assignment separately keeps the C#5 definite-assignment boundary explicit.
                    entry = entries[identity.InstanceId]; var b = identity.Binding;
                    Wire.Require(b.Build == binding.Build && b.Catalog == binding.Catalog && b.Session == session && b.Sequence == binding.Sequence && b.Owner == entry.Actor && b.Epoch == entry.Epoch);
                    bool pending = entry.Pending != null;
                    Wire.Require((entry.Active != null) != pending);
                    var handler = pending ? entry.Pending.Frozen : entry.Active;
                    if (pending) Wire.Require(entry.Pending.Ledger == this && entry.Pending.InstanceId == identity.InstanceId && entry.Pending.PreviousActor == entry.Actor && entry.Pending.PreviousEpoch == entry.Epoch);
                    foreach (var previous in handlers) Wire.Require(!Object.ReferenceEquals(previous, handler) && !previous.SharesReserveWith(handler));
                    handlers.Add(handler); Wire.Require(handler.Live);
                    Wire.Text(writer, identity.InstanceId); Wire.Text(writer, identity.Provenance); Wire.Text(writer, entry.Actor);
                    writer.Write(entry.Epoch); writer.Write(b.Life); writer.Write(b.Connection); writer.Write(pending);
                    // Actual owner codec rejects live fire/suspension/transfer reservations.
                    // This depends on the pinned reservation-accounted owner, never old R1.
                    byte[] mechanics = WeaponCheckpointCodec.Capture(handler, b, identity.Provenance);
                    Wire.Require(stream.Position <= Wire.Limit - 4L - mechanics.Length);
                    Wire.Blob(writer, mechanics);
                }
                writer.Flush(); byte[] result = stream.ToArray(); Wire.Require(result.Length <= Wire.Limit); return result;
            }
        }

        // For composition of fresh quarantine only. No receipt is rebound to a different
        // registry, and a pending lifetime is not implicitly resumed or granted rounds.
        public bool TryReadCheckpointMember(string id, out WeaponHandling active, out WeaponTransfer pending,
            out string actor, out long epoch)
        {
            Entry entry; active = null; pending = null; actor = null; epoch = 0;
            if (id == null || !entries.TryGetValue(id, out entry)) return false;
            active = entry.Active; pending = entry.Pending; actor = entry.Actor; epoch = entry.Epoch; return true;
        }
    }
}
