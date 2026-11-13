using System;
using System.IO;
using Ashfall.Modules.Audio;
namespace Ashfall.Integration.AudioRuntime
{
    public static class VerifiedPcm
    {
        public const int MaxContainerBytes=67108864,MaxSamples=32000000;
        public const long MaxRetainedBytes=268435456;
        internal static void CheckContainerBudget(int maxBytes){if(maxBytes<12||maxBytes>MaxContainerBytes)throw new ArgumentOutOfRangeException("maxBytes");}
        // Hash sharing never grants authority to another path or provenance identity.
        internal static void VerifyCached(AudioAsset asset,IApprovedAudioBytes bytes)
        {
            if(asset==null||bytes==null)throw new ArgumentNullException();
            if(!asset.Approval.StartsWith("accepted:",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(asset.Version)||string.IsNullOrWhiteSpace(asset.License))throw new InvalidDataException("Missing audio provenance fields");
            var bounded=bytes as BoundedApprovedAudioFiles;
            var snapshot=bounded!=null?bounded.ReadSnapshot(asset,MaxContainerBytes):AudioSnapshot.CloneLegacy(bytes.Read(asset),MaxContainerBytes);
            snapshot.VerifyIdentity(asset);
        }
        public static PcmWave Load(AudioAsset asset,IApprovedAudioBytes bytes){return Load(asset,bytes,MaxContainerBytes,MaxSamples);}
        public static PcmWave Load(AudioAsset asset,IApprovedAudioBytes bytes,int maxBytes,int maxSamples)
        {
            if(asset==null||bytes==null)throw new ArgumentNullException();CheckContainerBudget(maxBytes);
            if(!asset.Approval.StartsWith("accepted:",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(asset.Version)||string.IsNullOrWhiteSpace(asset.License))throw new InvalidDataException("Missing audio provenance fields");
            if(maxSamples<1||maxSamples>MaxSamples)throw new ArgumentOutOfRangeException("maxSamples");
            if((long)asset.Frames*asset.Channels>maxSamples)throw new InvalidDataException("Decoded sample budget before allocation");
            var bounded=bytes as BoundedApprovedAudioFiles;
            // Compatibility only: cannot bound allocations inside a legacy provider.
            var snapshot=bounded!=null?bounded.ReadSnapshot(asset,maxBytes):AudioSnapshot.CloneLegacy(bytes.Read(asset),maxBytes);
            return snapshot.Decode(asset,maxSamples);
        }
    }
}
