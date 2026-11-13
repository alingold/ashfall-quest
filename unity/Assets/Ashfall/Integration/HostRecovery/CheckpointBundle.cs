using System;
using System.IO;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Modes;
using Ashfall.Integration.InventoryRecovery;
using Ashfall.Integration.MatchRecovery;
using RoundController=Ashfall.Integration.MatchRounds.RoundController;
namespace Ashfall.Integration.HostRecovery
{
    // Parsing context supplied through the trusted root. This is NOT an authenticity receipt.
    public sealed class BundleBinding
    {
        public readonly CheckpointBinding Owner;
        public readonly string HealthCombatPublicationDigest;
        public readonly long Generation,HealthSequence,AcknowledgedThrough;
        public readonly double LogicalTime;
        public BundleBinding(CheckpointBinding owner,string publication,long generation,long sequence,long ack,double time)
        {
            RecoveryWire.Require(owner!=null&&generation>0&&sequence>=0&&ack>=0&&ack<=sequence&&owner.Sequence>=sequence&&time>=0&&!Double.IsNaN(time)&&!Double.IsInfinity(time));
            CheckHash(publication);CheckHash(owner.Build);Owner=owner;HealthCombatPublicationDigest=publication;Generation=generation;HealthSequence=sequence;AcknowledgedThrough=ack;LogicalTime=time;
        }
        internal static void CheckHash(string value){RecoveryWire.Require(value!=null&&value.Length==64);foreach(char c in value)RecoveryWire.Require(c>='0'&&c<='9'||c>='a'&&c<='f');}
        internal MatchOwnerBinding Match{get{return new MatchOwnerBinding(Owner,HealthCombatPublicationDigest,HealthSequence,AcknowledgedThrough,LogicalTime);}}
        internal void Write(BinaryWriter w){Owner.Write(w);RecoveryWire.Text(w,HealthCombatPublicationDigest);w.Write(Generation);w.Write(HealthSequence);w.Write(AcknowledgedThrough);w.Write(LogicalTime);}
        internal void Check(BinaryReader r){Owner.Check(r);RecoveryWire.Require(RecoveryWire.Text(r)==HealthCombatPublicationDigest&&r.ReadInt64()==Generation&&r.ReadInt64()==HealthSequence&&r.ReadInt64()==AcknowledgedThrough&&r.ReadDouble()==LogicalTime);}
    }
    // No restored owner is exposed. Several legacy factories return mutable non-quarantined
    // objects; holding them privately here avoids accidentally granting gameplay authority.
    public sealed class ValidatedPartialBundle
    {
        readonly byte[] image;
        internal ValidatedPartialBundle(byte[] value){image=value;}
        public byte[] CopyOriginalBytes(){return (byte[])image.Clone();}
        public string Digest{get{return RecoveryWire.Hash(image);}}
        public string UnsupportedGates{get{return CheckpointBundle.UnsupportedGates;}}
    }
    public static class CheckpointBundle
    {
        public const string Codec="HostRecoveryPartial/1";
        public const int MaximumBytes=4*1024*1024;
        public const string UnsupportedGates="NO LIVE ADOPTION: Combat/publication physical primary+backup original bytes and trusted ACK receipts; Director/physical score journal and producer state; common host-lane quiescence and all prepared participants; canonical network/root/world identities; WeaponLifetimes/SessionWeapons/equip/world custody; consumable Health/stamina/build/reserve effect cuts; durable adoption intent and epoch tombstones. Global retirement and gameplay resume unavailable.";
        static readonly string[] Schemas={"HealthService/schema2","HealthArchive/1","PartialSessionRounds/1","InventoryRecovery/schema1","PickupCustody/schema1"};
        static void Header(BinaryWriter w,BundleBinding b){w.Write(0x31425248);w.Write(1);RecoveryWire.Text(w,Codec);b.Write(w);w.Write(Schemas.Length);}
        static CommandContext Host(BundleBinding b){return new CommandContext(b.Owner.Session,"host","bundle-parse",Int64.MaxValue,b.Owner.Epoch,true);}
        // Trusted serialized caller only. Header time is a binding, not proof of root quiescence.
        // Every returned byte is a local partial image; no complete Combat/effect capture claim.
        public static byte[] CapturePartial(BundleBinding binding,HealthService health,HealthSnapshot[] history,HealthTransition[] transitions,Session session,RoundController rounds,ItemCatalog catalog,Inventory inventory,WeaponCheckpointMember[] weapons,PickupRules pickups)
        {
            RecoveryWire.Require(binding!=null&&health!=null);
            byte[][] parts={health.CreateCheckpoint(Host(binding),binding.Owner.Build),MatchCheckpointRecords.Capture(binding.Owner,history,transitions),MatchOwnerCheckpoint.Capture(session,rounds,binding.Match),ColdInventoryCheckpoint.Capture(binding.Owner,catalog,inventory,weapons),PickupCheckpointCodec.Capture(pickups,binding.Owner)};
            CheckHealthHeader(parts[0],binding);
            long size=0;foreach(byte[] p in parts){size+=p.Length;RecoveryWire.Require(size<=MaximumBytes);}
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
                Header(w,binding);for(int i=0;i<parts.Length;i++){RecoveryWire.Text(w,Schemas[i]);RecoveryWire.Text(w,RecoveryWire.Hash(parts[i]));RecoveryWire.Blob(w,parts[i]);}
                w.Flush();RecoveryWire.Require(m.Length<=MaximumBytes);return m.ToArray();
            }
        }
        // expectedDigest/floors must be independently authenticated by root. Self hashes are
        // integrity checks only. No IO, callbacks asserting success, publication or live rollback.
        public static ValidatedPartialBundle ValidatePartial(byte[] untrusted,string expectedDigest,BundleBinding expected,long generationFloor,long sequenceFloor,ItemCatalog catalog,IInventoryBoundary inventoryBoundary,IAuthorityGate gate,IHealthWorld healthWorld,IInteractionQuery query,int teams,bool respawn,ModeRules modeRules,IModeRoster roster,int roundCapacity,ISessionClock clock,IReconnectTokens tokens,ISpawnWorld spawn,ISessionManagement management,IPickupActors pickupActors,IPickupTransactions pickupTransactions)
        {
            RecoveryWire.Require(untrusted!=null&&untrusted.Length<=MaximumBytes&&expected!=null&&generationFloor>0&&sequenceFloor>=0&&expected.Generation>=generationFloor&&expected.HealthSequence>=sequenceFloor);
            BundleBinding.CheckHash(expectedDigest);byte[] image=(byte[])untrusted.Clone();RecoveryWire.Require(RecoveryWire.Hash(image)==expectedDigest);
            byte[][] parts=new byte[Schemas.Length][];
            using(var r=RecoveryWire.Reader(image)){
                RecoveryWire.Require(r.ReadInt32()==0x31425248&&r.ReadInt32()==1&&RecoveryWire.Text(r)==Codec);expected.Check(r);RecoveryWire.Require(r.ReadInt32()==Schemas.Length);
                for(int i=0;i<parts.Length;i++){RecoveryWire.Require(RecoveryWire.Text(r)==Schemas[i]);string hash=RecoveryWire.Text(r);BundleBinding.CheckHash(hash);parts[i]=RecoveryWire.Blob(r);RecoveryWire.Require(RecoveryWire.Hash(parts[i])==hash);}RecoveryWire.End(r);
            }
            RecoveryWire.Require(healthWorld!=null&&clock!=null&&healthWorld.Now==expected.LogicalTime&&clock.Now==expected.LogicalTime);
            HealthService h;RecoveryWire.Require(HealthService.TryRestoreCheckpoint(Host(expected),expected.Owner.Build,RecoveryWire.Hash(parts[0]),parts[0],teams,respawn,gate,healthWorld,query,out h));
            CheckHealthHeader(parts[0],expected);
            HealthSnapshot[] hs;HealthTransition[] ht;MatchCheckpointRecords.Restore(parts[1],expected.Owner,out hs,out ht);
            Session s;RoundController round;MatchOwnerCheckpoint.Reconstruct(parts[2],RecoveryWire.Hash(parts[2]),expected.Match,modeRules,roster,"host",roundCapacity,gate,clock,tokens,spawn,management,out s,out round);
            QuarantinedInventory inv;RecoveryWire.Require(ColdInventoryCheckpoint.TryRestore(parts[3],new TrustedCheckpointReceipt(expected.Owner,RecoveryWire.Hash(parts[3]),expected.Owner.Sequence),catalog,inventoryBoundary,gate,out inv));
            var p=PickupCheckpointCodec.Restore(parts[4],expected.Owner,gate,pickupActors,pickupTransactions,query);RecoveryWire.Require(p.IsQuarantined);
            // Exact references that can be checked without inventing missing Director/custody APIs.
            foreach(var record in hs)RecoveryWire.Require(h.Read(record.Id)!=null);
            foreach(var record in ht)RecoveryWire.Require(record.Sequence<=expected.HealthSequence);
            foreach(var item in inv.Inventory.Items){CanonicalPickupSource source;if(p.TryReadCanonicalSource(item.InstanceId,out source))RecoveryWire.Require(!source.Removed&&source.Owner==expected.Owner.Owner&&source.Kind==item.DefinitionId);}
            return new ValidatedPartialBundle(image);
        }
        // Read only the frozen schema2 prefix after the actual owner codec has validated.
        // This is not a second state codec or reflection bridge.
        static void CheckHealthHeader(byte[] bytes,BundleBinding b)
        {
            using(var r=new BinaryReader(new MemoryStream(bytes,false))){
                RecoveryWire.Require(r.ReadInt32()==2&&r.ReadString()==b.Owner.Build&&r.ReadString()==b.Owner.Session&&r.ReadInt64()==b.Owner.Epoch);
                r.ReadInt32();r.ReadBoolean();r.ReadDouble();r.ReadDouble();r.ReadDouble();
                RecoveryWire.Require(r.ReadInt64()==b.HealthSequence);r.ReadInt64();RecoveryWire.Require(r.ReadDouble()==b.LogicalTime);
            }
        }
        public static void RestoreWholeEffect(){throw new NotSupportedException(UnsupportedGates);}
    }
}

