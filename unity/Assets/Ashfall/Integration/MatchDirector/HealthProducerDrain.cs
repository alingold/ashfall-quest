using System;
using System.IO;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.CombatPublication;
namespace Ashfall.Integration.MatchDirector
{
    // Trusted host composition only. Owns prefix transfer; never accepts a guessed watermark.
    // The host must also serialize Combat, Session and membership on this same lane.
    public sealed partial class HealthProducerDrain
    {
        private readonly HealthService health;
        private readonly CommandContext host;
        private readonly string binding;
        private HealthTransition pending;
        private readonly HealthPublicationLane publication;
        internal HealthProducerDrain(HealthService health,CommandContext host,string binding,HealthPublicationLane publication)
        {this.health=health;this.host=host;this.binding=binding;this.publication=publication;}
        internal T Serialize<T>(Func<T> work) {return health.Serialize(work);}
        internal RuleResult Check(string session,long epoch,long delivered)
        {
            if(host.SessionId!=session||host.AuthorityEpoch!=epoch)return RuleResult.Reject("producer-scope");
            try
            {
                // Actual Health checkpoint rejects outstanding publication reservations and
                // authenticates the host. Read only its versioned identity/prefix header.
                byte[] bytes=health.CreateCheckpoint(host,binding);
                using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Encoding.UTF8))
                {
                    int version=r.ReadInt32();
                    if((version!=1&&version!=2)||r.ReadString()!=binding||r.ReadString()!=session||r.ReadInt64()!=epoch)return RuleResult.Reject("producer-schema");
                    r.ReadInt32();r.ReadBoolean();r.ReadDouble();r.ReadDouble();r.ReadDouble();
                    long published=r.ReadInt64();
                    if(pending!=null||health.PendingTransitionCount!=0||published!=delivered)return RuleResult.Reject("producer-undelivered");
                    if(publication!=null&&(publication.RecoveryRequired||publication.Offer(host).Length!=0))return RuleResult.Reject("producer-undelivered");
                }
                return RuleResult.Accept();
            }
            catch(InvalidDataException){return RuleResult.Reject("producer-journal");}
            catch(IOException){return RuleResult.Reject("producer-journal");}
            catch(InvalidOperationException){return RuleResult.Reject("producer-not-quiescent");}
        }
        internal RuleResult Retirement(string session,long epoch,long delivered)
        {
            RuleResult settled=Check(session,epoch,delivered);if(!settled.Accepted)return settled;
            if(publication==null)return RuleResult.Reject("root-producer-quiescence-required");
            HealthPublicationLane.ProducerAdmissionClose closure;
            RuleResult r=publication.CloseProducerAdmission(host,epoch+1,binding,out closure);
            if(!r.Accepted)return r;
            // A lane-owned tombstone closes only routed calls. Retained Health/Combat
            // and validated owner tokens remain outside its coverage (HOST-EPOCH-GATE/1).
            return publication.CheckGlobalRetirement(host,delivered);
        }
        // A refused event stays owned here; later Health events stay in the real outbox.
        // Prefix capacity is one, independent of match score/replay capacity.
        internal RuleResult Drain(Director director)
        {
            try {return Serialize(delegate {
                if(host.AuthorityEpoch!=director.Epoch)return RuleResult.Reject("producer-scope");
                while(true)
                {
                    if(pending==null)
                    {
                        if(publication==null)
                        {
                            var batch=health.DrainTransitionPrefix(1);
                            if(batch.Length==0)return RuleResult.Accept();
                            pending=batch[0];
                        }
                        else
                        {
                            if(publication.RecoveryRequired)return RuleResult.Reject("producer-recovery-required");
                            var packets=publication.Offer(host);
                            if(packets.Length==0)return RuleResult.Accept();
                            pending=Decode(packets[0]);
                        }
                    }
                    RuleResult result=director.Deliver(pending);
                    if(!result.Accepted)return result;
                    if(publication!=null)
                    {result=publication.Acknowledge(host,pending.Sequence);if(!result.Accepted)return result;}
                    pending=null;
                }
            });}
            catch(InvalidDataException){return RuleResult.Reject("producer-journal");}
            catch(IOException){return RuleResult.Reject("producer-journal");}
            catch(InvalidOperationException){return RuleResult.Reject("producer-not-quiescent");}
        }
        // Only called on a packet offered by the bound authenticated producer instance.
        private HealthTransition Decode(HealthPublicationLane.Packet packet)
        {
            if(packet.Payload==null||packet.Payload.Length>21848)throw new InvalidDataException("publication-size");
            try {return MatchCheckpointRecords.RestorePublication(Convert.FromBase64String(packet.Payload),host.SessionId,host.AuthorityEpoch,packet.Sequence);}
            catch(FormatException){throw new InvalidDataException("publication-base64");}
            catch(ArgumentException){throw new InvalidDataException("publication-encoding");}
        }
    }
}
