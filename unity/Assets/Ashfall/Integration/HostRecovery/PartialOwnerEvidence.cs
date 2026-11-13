using System;
using System.IO;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.MatchRecovery;
using RoundController=Ashfall.Integration.MatchRounds.RoundController;
namespace Ashfall.Integration.HostRecovery
{
    // Evidence for exactly two local owners. Never a common-root or full bundle capability.
    public sealed class PartialOwnerEvidence
    {
        public readonly string SessionDigest,HealthDigest,RetainedRecordsDigest;
        public readonly double SessionRecordedTime,HealthRecordedTime;
        public const string Unsupported="PARTIAL ONLY: equal recorded seconds do not establish a shared clock domain, Round/Director time, authenticated canonical root census, complete archive, global quiescence or coherent restore.";
        PartialOwnerEvidence(Session.CheckpointClockReceipt s,HealthService.CheckpointClockReceipt h)
        {SessionDigest=s.Digest;HealthDigest=h.Digest;RetainedRecordsDigest=h.RecordsDigest;SessionRecordedTime=s.RecordedTime;HealthRecordedTime=h.RecordedTime;}
        public static PartialOwnerEvidence Verify(Session session,Session.CheckpointClockReceipt s,byte[] sessionArchive,HealthService health,HealthService.CheckpointClockReceipt h,byte[] healthArchive,byte[] records,CommandContext host,CheckpointBinding binding)
        {
            RecoveryWire.Require(s!=null&&h!=null);
            s.Verify(session,binding,sessionArchive);h.Verify(health,host,binding,healthArchive,records);
            RecoveryWire.Require(s.RecordedTime>=0&&h.RecordedTime>=0&&!Double.IsNaN(s.RecordedTime)&&!Double.IsInfinity(s.RecordedTime)&&s.RecordedTime==h.RecordedTime);
            var a=s.CopyCanonicalMembers();var b=h.CopyCanonicalMembers();RecoveryWire.Require(a.Length==b.Length);
            for(int i=0;i<a.Length;i++)RecoveryWire.Require(a[i]==b[i]);
            RecoveryWire.Require(Array.IndexOf(a,binding.Owner)>=0);
            return new PartialOwnerEvidence(s,h);
        }
        // Optional strengthening of an already parsed legacy partial bundle. Only this
        // envelope is read here. Actual owner state is compared through owner APIs.
        public static PartialOwnerEvidence VerifyBundlePartial(ValidatedPartialBundle bundle,BundleBinding binding,Session session,Session.CheckpointClockReceipt s,RoundController rounds,HealthService health,HealthService.CheckpointClockReceipt h)
        {
            RecoveryWire.Require(bundle!=null&&binding!=null);
            var host=new CommandContext(binding.Owner.Session,"host","evidence",Int64.MaxValue,binding.Owner.Epoch,true);
            var proof=Verify(session,s,s.CopyArchive(),health,h,h.CopyArchive(),h.CopyRetainedRecords(),host,binding.Owner);
            using(var r=RecoveryWire.Reader(bundle.CopyOriginalBytes())) {
                RecoveryWire.Require(r.ReadInt32()==0x31425248&&r.ReadInt32()==1&&RecoveryWire.Text(r)==CheckpointBundle.Codec);
                binding.Check(r);RecoveryWire.Require(r.ReadInt32()==5);
                for(int i=0;i<5;i++) {
                    RecoveryWire.Text(r);string digest=RecoveryWire.Text(r);byte[] bytes=RecoveryWire.Blob(r);
                    RecoveryWire.Require(RecoveryWire.Hash(bytes)==digest);
                    if(i==0)RecoveryWire.Require(digest==h.Digest);
                    if(i==1)RecoveryWire.Require(digest==h.RecordsDigest);
                    if(i==2)RecoveryWire.Require(digest==RecoveryWire.Hash(MatchOwnerCheckpoint.Capture(session,rounds,binding.Match)));
                }
                RecoveryWire.End(r);
            }
            RecoveryWire.Require(binding.HealthSequence==h.EventSequence&&binding.LogicalTime==s.RecordedTime);
            return proof;
        }
        public void RestoreCoherentBundle(){throw new NotSupportedException(Unsupported);}
    }
}
