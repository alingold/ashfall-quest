using System;
namespace Ashfall.Modules.WeaponHandling
{
    // One reserve per stable weapon instance, retained through every ownership change.
    // Entitlement is supplied only at trusted grant; Take/Refund cannot mint above it.
    public sealed class FiniteAmmoReserve : IRevisionedAmmoReserve
    {
        readonly int capacity;int available;long revision;
        public FiniteAmmoReserve(int entitlement,int initial){if(entitlement<0||initial<0||initial>entitlement)throw new ArgumentOutOfRangeException();capacity=entitlement;available=initial;}
        public int Available{get{return available;}}
        public long Revision{get{return revision;}}
        public int Take(int requested){if(requested<=0||revision==long.MaxValue)return 0;int n=Math.Min(requested,available);if(n>0){available-=n;revision++;}return n;}
        public int Refund(int rounds){if(rounds<=0||revision==long.MaxValue)return 0;int n=Math.Min(rounds,capacity-available);if(n>0){available+=n;revision++;}return n;}
        internal byte[] ExportCheckpoint(string provenance)
        {
            Ashfall.RecoveryContracts.RecoveryWire.Fields(typeof(FiniteAmmoReserve),"available:System.Int32\ncapacity:System.Int32\nrevision:System.Int64");
            Ashfall.RecoveryContracts.RecoveryWire.Require(!String.IsNullOrEmpty(provenance)&&capacity<=1000000);
            using(var m=new System.IO.MemoryStream())using(var w=new System.IO.BinaryWriter(m)){
                Ashfall.RecoveryContracts.RecoveryWire.Text(w,provenance);w.Write(capacity);w.Write(available);w.Write(revision);return m.ToArray();}
        }
        internal static FiniteAmmoReserve RestoreCheckpoint(byte[] bytes,string provenance)
        {
            Ashfall.RecoveryContracts.RecoveryWire.Fields(typeof(FiniteAmmoReserve),"available:System.Int32\ncapacity:System.Int32\nrevision:System.Int64");
            using(var q=Ashfall.RecoveryContracts.RecoveryWire.Reader(bytes)){
                Ashfall.RecoveryContracts.RecoveryWire.Require(Ashfall.RecoveryContracts.RecoveryWire.Text(q)==provenance);
                int capacity=q.ReadInt32(),available=q.ReadInt32();long revision=q.ReadInt64();
                Ashfall.RecoveryContracts.RecoveryWire.Require(capacity>=0&&capacity<=1000000&&available>=0&&available<=capacity&&revision>=0);
                var n=new FiniteAmmoReserve(capacity,available);n.revision=revision;Ashfall.RecoveryContracts.RecoveryWire.End(q);return n;}
        }
    }
}
