using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Modes;
using Ashfall.RecoveryContracts;
using RoundController=Ashfall.Integration.MatchRounds.RoundController;
namespace Ashfall.Integration.MatchRecovery
{
    // Partial participant image only. No Director, producer or authority adoption capability.
    // Expected digest/freshness and the actual producer digest originate at the trusted root.
    public sealed class MatchOwnerBinding
    {
        public readonly CheckpointBinding Checkpoint;
        public readonly string HealthPublicationDigest;
        public readonly long HealthSequence,AcknowledgedThrough;
        public readonly double LogicalTime;
        public MatchOwnerBinding(CheckpointBinding checkpoint,string healthDigest,long sequence,long ack,double time)
        {RecoveryWire.Require(checkpoint!=null&&healthDigest!=null&&healthDigest.Length==64&&sequence>=0&&ack>=0&&ack<=sequence&&!Double.IsNaN(time)&&!Double.IsInfinity(time)&&time>=0);foreach(char c in healthDigest)RecoveryWire.Require(c>='0'&&c<='9'||c>='a'&&c<='f');Checkpoint=checkpoint;HealthPublicationDigest=healthDigest;HealthSequence=sequence;AcknowledgedThrough=ack;LogicalTime=time;}
        internal void Write(BinaryWriter w){Checkpoint.Write(w);RecoveryWire.Text(w,HealthPublicationDigest);w.Write(HealthSequence);w.Write(AcknowledgedThrough);w.Write(LogicalTime);}
        internal void Check(BinaryReader r){Checkpoint.Check(r);RecoveryWire.Require(RecoveryWire.Text(r)==HealthPublicationDigest&&r.ReadInt64()==HealthSequence&&r.ReadInt64()==AcknowledgedThrough&&r.ReadDouble()==LogicalTime);}
    }
    public static class MatchOwnerCheckpoint
    {
        public const string Codec="PartialSessionRounds/1";
        // Caller already holds the serialized host lane. This helper does no IO or publication.
        public static byte[] Capture(Session session,RoundController round,MatchOwnerBinding binding)
        {
            byte[] s=session.CaptureCheckpoint(binding.Checkpoint),r=round.CaptureCheckpoint(binding.Checkpoint);
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){RecoveryWire.Text(w,Codec);binding.Write(w);RecoveryWire.Blob(w,s);RecoveryWire.Blob(w,r);w.Flush();byte[] b=m.ToArray();RecoveryWire.Require(b.Length<=RecoveryWire.Limit);return b;}
        }
        public static void Reconstruct(byte[] image,string trustedDigest,MatchOwnerBinding expected,ModeRules rules,IModeRoster roster,string director,int capacity,IAuthorityGate gate,ISessionClock clock,IReconnectTokens tokens,ISpawnWorld world,ISessionManagement management,out Session session,out RoundController round)
        {
            session=null;round=null;RecoveryWire.Require(image!=null&&image.Length<=RecoveryWire.Limit&&trustedDigest!=null&&RecoveryWire.Hash(image)==trustedDigest);
            byte[] s,r;using(var reader=RecoveryWire.Reader(image)){RecoveryWire.Require(RecoveryWire.Text(reader)==Codec);expected.Check(reader);s=RecoveryWire.Blob(reader);r=RecoveryWire.Blob(reader);RecoveryWire.End(reader);}
            Session nextSession=Session.RestoreCheckpoint(s,expected.Checkpoint,gate,clock,tokens,world,management);
            RoundController nextRound=RoundController.RestoreCheckpoint(r,expected.Checkpoint,rules,roster,director,gate,capacity);
            // Outputs appear together only after both actual owners have validated.
            session=nextSession;round=nextRound;
        }
    }
}
