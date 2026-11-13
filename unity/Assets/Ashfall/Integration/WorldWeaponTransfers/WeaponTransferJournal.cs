using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.WorldWeaponTransfers
{
    public enum WeaponJournalStage { Prepared=1, Decision=2, Published=3, Aborted=4, Ack=5 }
    // Append-only host storage. Exclusive file handle prevents two coordinators owning
    // the same journal. Torn/corrupt/unknown frames refuse reopen, never truncate history.
    public sealed class WeaponTransferJournal : IDisposable
    {
        readonly FileStream file; readonly long maximumBytes; bool faulted,claimed;
        readonly List<byte[]> frames=new List<byte[]>();
        internal bool Claim(){if(claimed)return false;claimed=true;return true;}
        public bool HasHistory {get{return frames.Count!=0;}}
        public bool Faulted {get{return faulted;}}
        public byte[][] ReadFrames(){var a=new byte[frames.Count][];for(int i=0;i<a.Length;i++)a[i]=(byte[])frames[i].Clone();return a;}
        public WeaponTransferJournal(string path,long maximumBytes)
        {
            if(maximumBytes<4096||maximumBytes>67108864)throw new ArgumentException("journal capacity");this.maximumBytes=maximumBytes;
            file=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,4096,FileOptions.WriteThrough);
            try {
                if(file.Length>maximumBytes)throw new InvalidDataException("journal capacity");
                var reader=new BinaryReader(file,Encoding.UTF8);
                while(file.Position<file.Length){
                    if(file.Length-file.Position<4)throw new InvalidDataException("torn frame");
                    int length=reader.ReadInt32();if(length<1||length>1048576||file.Length-file.Position<length+32L)throw new InvalidDataException("frame length");
                    byte[] data=reader.ReadBytes(length),hash=reader.ReadBytes(32);using(var sha=SHA256.Create())if(!Equal(hash,sha.ComputeHash(data)))throw new InvalidDataException("frame hash");
                    if(data[0]<1||data[0]>5)throw new InvalidDataException("frame version/stage");frames.Add(data);
                }
            }catch{file.Dispose();throw;}
        }
        static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        internal bool CanAppend(int payloadBytes){return !faulted&&payloadBytes>0&&payloadBytes<=1048576&&file.Length<=maximumBytes-payloadBytes-36;}
        internal void Append(byte[] data)
        {
            if(!CanAppend(data.Length))throw new IOException("journal unavailable/capacity");
            try {byte[] hash;using(var sha=SHA256.Create())hash=sha.ComputeHash(data);
                var writer=new BinaryWriter(file,Encoding.UTF8);writer.Write(data.Length);writer.Write(data);writer.Write(hash);writer.Flush();file.Flush(true);frames.Add((byte[])data.Clone());
            }catch{faulted=true;throw;}
        }
        internal static byte[] Marker(WeaponJournalStage stage,long sequence)
        {using(var stream=new MemoryStream()){var w=new BinaryWriter(stream);w.Write((byte)stage);w.Write(sequence);w.Flush();return stream.ToArray();}}
        internal static byte[] Prepare(long intent,CommandContext c,IWeaponPrincipal principal,long deadline,WeaponWorldEvent e,Inventory inventory)
        {
            using(var stream=new MemoryStream()){
                var w=new BinaryWriter(stream,Encoding.UTF8);w.Write((byte)WeaponJournalStage.Prepared);w.Write(intent);w.Write(e.Sequence);w.Write("ashfall-weapon-intent-v1");
                w.Write(c.SessionId);w.Write(c.ActorId);w.Write(c.AuthorityEpoch);w.Write(c.CommandId);w.Write(c.Tick);w.Write(principal.ConnectionLife);w.Write(principal.Life);w.Write(deadline);
                w.Write(inventory.SelectedInstanceId);w.Write(inventory.Holstered);w.Write(e.Dropped);var records=e.Records;w.Write(records.Length);
                foreach(var r in records){
                    w.Write(r.Item.InstanceId);w.Write(r.Item.DefinitionId);w.Write(r.Item.Stars);
                    var p=r.Placement;w.Write(p.BodyId);w.Write(p.SemanticOwner);w.Write(p.LeaseId);w.Write(p.Generation);w.Write(p.Position.X);w.Write(p.Position.Y);w.Write(p.Position.Z);
                    w.Write(r.Session);w.Write(r.Actor);w.Write(r.Epoch);w.Write(r.ConnectionLife);w.Write(r.Life);w.Write(r.Deadline);w.Write(r.Generation);w.Write(r.Reserve);w.Write(r.ReserveRevision);
                    HandlingSnapshot m=r.Mechanics;w.Write((int)m.Phase);w.Write(m.Loaded);w.Write(m.Seated);w.Write(m.Chambered);w.Write(m.SpentChamber);w.Write(m.Supported);w.Write(m.ClaimsSupport);w.Write(m.TriggerReleaseRequired);w.Write(m.BoltTravel);w.Write(m.RecoilDistance);w.Write(m.RecoilPitch);w.Write(m.RecoilYaw);w.Write(m.Shots);w.Write(m.ReloadPending);w.Write(m.CooldownSeconds);w.Write(m.IdleSeconds);w.Write(m.RecoilPattern);
                }
                w.Flush();return stream.ToArray();
            }
        }
        public void Dispose(){file.Dispose();}
    }
}
