using System;
using System.IO;
using Ashfall.Modules.Audio;
namespace Ashfall.Integration.AudioRuntime
{
    // External coordinator verifies a genuine immutable receipt including version/license/hash.
    public interface IApprovedAudioFileAttestation
    { bool IsApproved(AudioAsset asset,string canonicalPath,long length); }
    public sealed class BoundedApprovedAudioFiles : IApprovedAudioBytes
    {
        private readonly IApprovedAudioFileAttestation approval;
        public BoundedApprovedAudioFiles(IApprovedAudioFileAttestation authority)
        {if(authority==null)throw new ArgumentNullException("authority");approval=authority;}
        public byte[] Read(AudioAsset asset){return ReadSnapshot(asset,VerifiedPcm.MaxContainerBytes).Copy();}
        internal AudioSnapshot ReadSnapshot(AudioAsset asset,int maxBytes)
        {
            if(asset==null)throw new ArgumentNullException("asset");
            VerifiedPcm.CheckContainerBudget(maxBytes);
            string path=AudioPaths.Canonical(asset.Path);
            // Junction/symlink deployment is deliberately unsupported; approval is exact path+bytes.
            for(string p=path;!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))
                if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Reparse deployment unsupported");
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
            {
                long length=stream.Length;
                if(length<12||length>maxBytes)throw new InvalidDataException("Container budget");
                if(!approval.IsApproved(asset,path,length))throw new InvalidDataException("Missing immutable audio approval receipt");
                return AudioSnapshot.ReadExact(stream,length,maxBytes);
            }
        }
    }
    internal sealed class AudioSnapshot
    {
        private readonly byte[] data;
        private AudioSnapshot(byte[] owned){data=owned;}
        internal static AudioSnapshot ReadExact(Stream stream,long length,int maxBytes)
        {
            if(length<12||length>maxBytes)throw new InvalidDataException("Container budget");
            var owned=new byte[(int)length];int offset=0;
            while(offset<owned.Length){int n=stream.Read(owned,offset,Math.Min(65536,owned.Length-offset));if(n==0)throw new EndOfStreamException("Audio truncated");offset+=n;}
            if(stream.ReadByte()!=-1||stream.Length!=length)throw new InvalidDataException("Audio grew/changed length");
            return new AudioSnapshot(owned);
        }
        internal static AudioSnapshot CloneLegacy(byte[] provider,int maxBytes)
        {if(provider==null)throw new FileNotFoundException("Approved WAV missing");if(provider.Length>maxBytes)throw new InvalidDataException("Container budget before clone");return new AudioSnapshot((byte[])provider.Clone());}
        internal byte[] Copy(){return (byte[])data.Clone();}
        internal void VerifyIdentity(AudioAsset asset)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())
                if(!string.Equals(BitConverter.ToString(sha.ComputeHash(data)).Replace("-",""),asset.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Approved byte identity changed");
        }
        internal PcmWave Decode(AudioAsset asset,int maxSamples)
        {
            VerifyIdentity(asset);
            // Walk RIFF before decoder allocations, including malicious catalog shape mismatches.
            if(data.Length<12)throw new InvalidDataException("WAV");
            long pos=12;
            while(pos<data.Length)
            {
                if(pos+8>data.Length)throw new InvalidDataException("Chunk header");
                uint length=BitConverter.ToUInt32(data,(int)pos+4);
                if(pos+8+length+(length%2)>data.Length)throw new InvalidDataException("Chunk length");
                if(BitConverter.ToUInt32(data,(int)pos)==0x61746164u&&length/2>maxSamples)throw new InvalidDataException("Decoded sample budget");
                pos+=8+length+(length%2);
            }
            var pcm=PcmWave.Decode(data);
            if(pcm.Channels!=asset.Channels||pcm.Rate!=asset.Rate||pcm.Frames!=asset.Frames)throw new InvalidDataException("Catalog PCM shape changed");
            return pcm;
        }
    }
}
