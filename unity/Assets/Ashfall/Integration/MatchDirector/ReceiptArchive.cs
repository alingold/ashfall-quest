using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.Persistence;
namespace Ashfall.Integration.MatchDirector
{
    // Immutable original physical bytes. No writes, fallback repair, or later disk reads.
    public sealed class ReceiptFamilyArchive : IAtomicSettingsFiles
    {
        private readonly byte[] primary,backup;
        private ReceiptFamilyArchive(byte[] p,byte[] b){primary=p;backup=b;}
        public static byte[] Capture(string path,IAtomicSettingsFiles files)
        {
            path=Path.GetFullPath(path);
            using(files.AcquireFamily(path))
            {
                byte[] p=files.Exists(path)?files.Read(path,AtomicSettingsStore.MaxDocumentBytes):null;
                byte[] b=files.Exists(path+".backup")?files.Read(path+".backup",AtomicSettingsStore.MaxDocumentBytes):null;
                var family=new ReceiptFamilyArchive(p,b);family.Validate();
                using(var m=new MemoryStream())using(var w=new BinaryWriter(m))
                {w.Write(1);Put(w,p);Put(w,b);w.Flush();RecoveryWire.Require(m.Length<=RecoveryWire.Limit);return m.ToArray();}
            }
        }
        private static void Put(BinaryWriter w,byte[] b){w.Write(b!=null);if(b!=null)RecoveryWire.Blob(w,b);}
        private static byte[] Get(BinaryReader r){return RecoveryWire.Bool(r)?RecoveryWire.Blob(r):null;}
        public static ReceiptFamilyArchive Restore(byte[] bytes)
        {using(var r=RecoveryWire.Reader(bytes)){RecoveryWire.Require(r.ReadInt32()==1);var x=new ReceiptFamilyArchive(Get(r),Get(r));RecoveryWire.End(r);x.Validate();return x;}}
        private void Validate()
        {
            var x=new AtomicSettingsStore("receipt-quarantine",this);RecoveryWire.Require(x.Load(false).Accepted);
            if(backup!=null){var isolated=new ReceiptFamilyArchive(backup,null);var old=new AtomicSettingsStore("receipt-backup-quarantine",isolated);RecoveryWire.Require(old.Load(false).Accepted);}
        }
        public ScoreReceiptJournal OpenJournal()
        {var x=new AtomicSettingsStore("receipt-quarantine",this);RecoveryWire.Require(x.Load(false).Accepted);return new ScoreReceiptJournal(x);}
        private sealed class Lease : IDisposable {public void Dispose(){}}
        public IDisposable AcquireFamily(string path){return new Lease();}
        public bool Exists(string path){return path.EndsWith(".backup",StringComparison.Ordinal)?backup!=null:primary!=null;}
        public byte[] Read(string path,int maximum){byte[] b=path.EndsWith(".backup",StringComparison.Ordinal)?backup:primary;RecoveryWire.Require(b!=null&&b.Length<=maximum);return (byte[])b.Clone();}
        public void CreateDurable(string path,byte[] bytes){throw new IOException("quarantined-receipt-family");}
        public void Commit(string pending,string path,string backupPath,bool replacing){throw new IOException("quarantined-receipt-family");}
    }
    public sealed partial class CommittedAttribution
    {
        internal void WriteArchive(BinaryWriter w){w.Write(killers.Count);foreach(var p in killers){RecoveryWire.Text(w,p.Key);RecoveryWire.Text(w,p.Value);}}
        internal static CommittedAttribution ReadArchive(BinaryReader r,Ashfall.Modules.HealthStates.HealthService health)
        {var x=new CommittedAttribution(health);int n=RecoveryWire.Count(r,8192);for(int i=0;i<n;i++){string k=RecoveryWire.Text(r),v=RecoveryWire.Text(r);RecoveryWire.Require(k.Length>0&&v.Length>0);x.killers.Add(k,v);}return x;}
    }
}
