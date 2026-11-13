using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using Ashfall.Modules.Audio;
namespace Ashfall.Integration.AudioRuntime
{
    // Authority is supplied by root's genuine immutable approval service. This API
    // never issues receipts or promotes labels/existence into content approval.
    public interface IApprovedAudioCatalogAuthority : IApprovedAudioFileAttestation
    {
        bool IsCatalogApproved(string canonicalPath,string sha256);
        bool IsEntryApproved(string[] manifestFields,string canonicalPath,long length);
    }
    public sealed class ApprovedCatalogBinding
    {
        public const string Version="ASHFALL-BOUNDED-CATALOG/1";
        public readonly AudioCatalog Catalog;
        public readonly BoundedApprovedAudioFiles Files;
        private ApprovedCatalogBinding(AudioCatalog catalog,IApprovedAudioCatalogAuthority authority)
        {Catalog=catalog;Files=new BoundedApprovedAudioFiles(authority);}
        private static FileStream Open(string path,long limit)
        {
            for(string p=path;!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))
                if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Reparse deployment unsupported");
            var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length<12||stream.Length>limit){stream.Dispose();throw new InvalidDataException("Approved file budget");}
            return stream;
        }
        private static string Hash(Stream stream)
        {stream.Position=0;using(var sha=SHA256.Create()){var hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");stream.Position=0;return hash;}}
        public static ApprovedCatalogBinding Load(string manifest,IApprovedAudioCatalogAuthority authority)
        {
            if(authority==null)throw new ArgumentNullException("authority");
            string canonical=AudioPaths.Canonical(manifest);
            var held=new List<FileStream>();
            try
            {
                var text=Open(canonical,1048576);held.Add(text);
                if(!authority.IsCatalogApproved(canonical,Hash(text)))throw new InvalidDataException("Catalog receipt missing");
                // Framework StreamReader may omit decoder flush at a truncated EOF.
                // GetString validates the entire bounded byte image, including its tail.
                byte[] encoded=new byte[(int)text.Length];int count=0;
                while(count<encoded.Length){int read=text.Read(encoded,count,encoded.Length-count);if(read==0)throw new InvalidDataException("Truncated catalog");count+=read;}
                string content=new UTF8Encoding(false,true).GetString(encoded);
                if(content.Length>0&&content[0]=='\uFEFF')content=content.Substring(1);
                string[] lines=content.Split(new[]{'\n'});if(lines.Length>1026||lines[0].TrimEnd('\r')!="ASHFALL-AUDIO/1")throw new InvalidDataException("Catalog schema/count");
                var files=new Dictionary<string,FileStream>(StringComparer.OrdinalIgnoreCase);
                for(int i=1;i<lines.Length;i++)
                {
                    string line=lines[i].TrimEnd('\r');if(line.Length==0)continue;
                    var f=line.Split('\t');if(f.Length!=9)throw new InvalidDataException("Catalog row");
                    string path=AudioPaths.Canonical(f[2]);FileStream wav;
                    if(!files.TryGetValue(path,out wav))
                    {if(files.Count>=256)throw new InvalidDataException("Catalog file count");wav=Open(path,VerifiedPcm.MaxContainerBytes);held.Add(wav);files.Add(path,wav);}
                    if(!authority.IsEntryApproved((string[])f.Clone(),path,wav.Length)||!string.Equals(Hash(wav),f[3],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Entry receipt/hash missing");
                }
                // All exact paths are locked against writes/deletes for the entire
                // accepted loader call. Its allocations are now container-bounded.
                // Decoder already caps 32M float values; no accepted module edits.
                return new ApprovedCatalogBinding(AudioCatalog.Load(canonical),authority);
            }
            finally{for(int i=held.Count-1;i>=0;i--)held[i].Dispose();}
        }
        public AudioRuntimeSession CreateSession(IAudioOwnerLease leases){return new AudioRuntimeSession(Catalog,Files,leases);}
    }
}
