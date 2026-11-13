using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.CombatPublication;
using Ashfall.RecoveryContracts;
namespace Ashfall.Integration.MatchDirector
{
    public sealed partial class HealthProducerDrain
    {
        internal HealthTransition ArchivePending {get{return pending;}}
        internal void WriteArchive(BinaryWriter w,List<HealthTransition> events)
        {
            RecoveryWire.Text(w,binding);RecoveryWire.Text(w,host.SessionId);RecoveryWire.Text(w,host.ActorId);RecoveryWire.Text(w,host.CommandId);w.Write(host.Tick);w.Write(host.AuthorityEpoch);w.Write(host.IsAuthority);
            w.Write(pending!=null);if(pending!=null)w.Write(events.IndexOf(pending));
            w.Write(publication!=null);if(publication!=null)
            {
                var receipt=publication.SuccessfulCheckpoint;RecoveryWire.Require(receipt!=null&&!publication.RecoveryRequired);
                RecoveryWire.Text(w,receipt.SourceBinding);RecoveryWire.Text(w,receipt.Digest);w.Write(receipt.Generation);w.Write(receipt.HealthSequence);w.Write(receipt.AcknowledgedThrough);w.Write(publication.ProducerAdmissionClosed);
            }
        }
        internal static HealthProducerDrain ReadArchive(BinaryReader r,HealthTransition[] events,HealthService health,HealthPublicationLane publication)
        {
            string binding=RecoveryWire.Text(r),session=RecoveryWire.Text(r),actor=RecoveryWire.Text(r),command=RecoveryWire.Text(r);long tick=r.ReadInt64(),epoch=r.ReadInt64();bool authority=RecoveryWire.Bool(r);
            RecoveryWire.Require(authority&&tick>=0&&epoch>=0);
            var host=new CommandContext(session,actor,command,tick,epoch,authority);
            HealthTransition pending=RecoveryWire.Bool(r)?events[RecoveryWire.Count(r,events.Length-1)]:null;
            bool durable=RecoveryWire.Bool(r);RecoveryWire.Require(durable==(publication!=null));
            if(durable)
            {
                var receipt=publication.SuccessfulCheckpoint;RecoveryWire.Require(receipt!=null&&publication.IsBoundTo(health,session,epoch,binding)&&!publication.RecoveryRequired);
                RecoveryWire.Require(RecoveryWire.Text(r)==receipt.SourceBinding&&RecoveryWire.Text(r)==receipt.Digest&&r.ReadInt64()==receipt.Generation&&r.ReadInt64()==receipt.HealthSequence&&r.ReadInt64()==receipt.AcknowledgedThrough&&RecoveryWire.Bool(r)==publication.ProducerAdmissionClosed);
            }
            var x=new HealthProducerDrain(health,host,binding,publication);x.pending=pending;return x;
        }
    }
}
