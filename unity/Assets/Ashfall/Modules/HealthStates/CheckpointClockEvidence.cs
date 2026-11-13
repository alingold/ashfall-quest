using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
namespace Ashfall.Modules.HealthStates
{
    public sealed partial class HealthService
    {
        // Retained current snapshots and undrained transitions only; not a complete historical journal.
        public CheckpointClockReceipt CaptureClockEvidence(CommandContext host,CheckpointBinding binding)
        {
            lock(sync) {
                RecoveryWire.Require(binding!=null&&binding.Session==session&&binding.Epoch==epoch&&binding.Sequence>=eventSequence);
                byte[] image=CreateCheckpoint(host,binding.Build);
                var ids=new List<string>(players.Keys);ids.Sort(StringComparer.Ordinal);
                var snapshots=new HealthSnapshot[ids.Count];for(int i=0;i<ids.Count;i++)snapshots[i]=Read(ids[i]);
                byte[] archive=MatchCheckpointRecords.Capture(binding,snapshots,transitions.ToArray());
                return new CheckpointClockReceipt(this,binding,image,archive,lastTime,eventSequence,ids.ToArray());
            }
        }
        public sealed class CheckpointClockReceipt
        {
            readonly HealthService producer;readonly CheckpointBinding binding;readonly byte[] image,archive;readonly string[] members;
            public readonly double RecordedTime;public readonly long EventSequence;
            public const string ClockDomain="HealthService.lastTime/seconds";
            public const string TickRate="unspecified-command-ticks; recorded-time-unit=seconds";
            internal CheckpointClockReceipt(HealthService p,CheckpointBinding b,byte[] bytes,byte[] records,double time,long sequence,string[] ids)
            {producer=p;binding=b;image=bytes;archive=records;RecordedTime=time;EventSequence=sequence;members=ids;}
            public string Digest{get{return RecoveryWire.Hash(image);}}
            public string RecordsDigest{get{return RecoveryWire.Hash(archive);}}
            public byte[] CopyArchive(){return (byte[])image.Clone();}
            public byte[] CopyRetainedRecords(){return (byte[])archive.Clone();}
            public string[] CopyCanonicalMembers(){return (string[])members.Clone();}
            public void Verify(HealthService actual,CommandContext host,CheckpointBinding expected,byte[] bytes,byte[] records)
            {
                RecoveryWire.Require(Object.ReferenceEquals(actual,producer)&&bytes!=null&&records!=null&&RecoveryWire.Hash(bytes)==Digest&&RecoveryWire.Hash(records)==RecordsDigest);
                var current=actual.CaptureClockEvidence(host,expected);
                RecoveryWire.Require(current.Digest==Digest&&current.RecordsDigest==RecordsDigest&&expected.Session==binding.Session&&expected.Epoch==binding.Epoch);
            }
        }
    }
}
