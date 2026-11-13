using System;
using System.Collections.Generic;
using Ashfall.RecoveryContracts;
namespace Ashfall.Modules.Sessions
{
    public sealed partial class Session
    {
        // No new service state: the existing codec field census remains exact.
        // Must run on the existing exclusive host lane, just like CaptureCheckpoint.
        public CheckpointClockReceipt CaptureClockEvidence(CheckpointBinding binding)
        {
            byte[] image=CaptureCheckpoint(binding);
            var ids=new List<string>();
            foreach(var p in players.Values)ids.Add(p.CanonicalId);
            foreach(var p in reservations.Values)ids.Add(p.Player.CanonicalId);
            ids.Sort(StringComparer.Ordinal);
            for(int i=1;i<ids.Count;i++)RecoveryWire.Require(ids[i]!=ids[i-1]);
            return new CheckpointClockReceipt(this,binding,image,lastTime,ids.ToArray());
        }
        public sealed class CheckpointClockReceipt
        {
            readonly Session producer;readonly CheckpointBinding binding;readonly byte[] image;readonly string[] members;
            public readonly double RecordedTime;
            public const string ClockDomain="Session.lastTime/seconds";
            // Seconds are continuous doubles, not ticks. CommandContext.Tick has no declared rate.
            public const string TickRate="unspecified-command-ticks; recorded-time-unit=seconds";
            internal CheckpointClockReceipt(Session p,CheckpointBinding b,byte[] bytes,double time,string[] ids)
            {producer=p;binding=b;image=bytes;RecordedTime=time;members=ids;}
            public string Digest{get{return RecoveryWire.Hash(image);}}
            public byte[] CopyArchive(){return (byte[])image.Clone();}
            public string[] CopyCanonicalMembers(){return (string[])members.Clone();}
            public void Verify(Session actual,CheckpointBinding expected,byte[] archive)
            {
                RecoveryWire.Require(Object.ReferenceEquals(actual,producer)&&archive!=null&&RecoveryWire.Hash(archive)==Digest);
                // Re-encode through the actual owner, never parse private state in a consumer.
                RecoveryWire.Require(RecoveryWire.Hash(actual.CaptureCheckpoint(expected))==Digest);
                RecoveryWire.Require(expected.Session==binding.Session&&expected.Epoch==binding.Epoch);
            }
        }
    }
}
