using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
namespace Ashfall.Integration.HostEpochAuthority
{
    // Dedicated ingress tombstone, independent of accepted gameplay persistence.
    // Missing/corrupt/truncated reopen never creates an open authority.
    internal sealed class ClosureJournal : IDisposable
    {
        readonly FileStream file;
        readonly byte[] identity;
        internal ClosureJournal(string path,string binding,bool create)
        {
            using(var sha=SHA256.Create())identity=sha.ComputeHash(Encoding.UTF8.GetBytes(binding));
            file=new FileStream(path,create?FileMode.CreateNew:FileMode.Open,FileAccess.ReadWrite,FileShare.Read);
            if(create) { try { Write(0,0); } catch {file.Dispose();throw;} }
            else {
                // Reopening is always quarantined, even for an intact pre-close header.
                try { if(file.Length!=56&&file.Length!=112)throw new InvalidDataException("tombstone-length");Read(0);if(file.Length==112)Read(1); }
                catch {file.Dispose();throw;}
            }
        }
        void Read(long kind)
        {
            var r=new BinaryReader(file,Encoding.UTF8);
            if(r.ReadInt64()!=0x314745484641||r.ReadInt64()!=kind)throw new InvalidDataException("tombstone-schema");
            byte[] b=r.ReadBytes(32);for(int i=0;i<32;i++)if(b[i]!=identity[i])throw new InvalidDataException("tombstone-binding");
            if(r.ReadInt64()<0)throw new InvalidDataException("tombstone-revision");
        }
        void Write(long kind,long revision)
        {
            var w=new BinaryWriter(file,Encoding.UTF8);w.Write(0x314745484641L);w.Write(kind);w.Write(identity);w.Write(revision);w.Flush();file.Flush(true);
        }
        internal void Close(long revision) { file.Position=56;Write(1,revision); }
        public void Dispose(){file.Dispose();}
    }
}
