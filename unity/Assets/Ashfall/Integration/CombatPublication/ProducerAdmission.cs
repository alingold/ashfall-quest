using CheckpointWire = Ashfall.Integration.CheckpointWire;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.CombatPublication
{
    public sealed partial class HealthPublicationLane
    {
        public const string AdmissionApiVersion="CombatPublicationAdmission/1";
        bool admissionClosed;
        long closeNextEpoch;
        string closeNextBinding;
        // Local routed-producer closure only. This is NEVER global retirement authority.
        public sealed class ProducerAdmissionClose
        {
            public readonly string SessionId,NextSourceBinding;
            public readonly long Epoch,NextEpoch;
            public readonly CheckpointReceipt Checkpoint;
            internal ProducerAdmissionClose(string session,long epoch,long next,string binding,CheckpointReceipt receipt)
            {SessionId=session;Epoch=epoch;NextEpoch=next;NextSourceBinding=binding;Checkpoint=receipt;}
        }
        public bool ProducerAdmissionClosed {get{return health.Serialize(()=>admissionClosed);}}
        public bool IsBoundTo(HealthService service,string sessionId,long authorityEpoch,string sourceBinding)
        {return object.ReferenceEquals(health,service)&&session==sessionId&&epoch==authorityEpoch&&checkpointBinding==sourceBinding;}
        public RuleResult CloseProducerAdmission(CommandContext c,long nextEpoch,string nextBinding,out ProducerAdmissionClose closure)
        {
            ProducerAdmissionClose value=null;
            RuleResult result=health.Serialize(()=>{
                if(busy||!Host(c)||RecoveryRequired||checkpointBinding==null||nextEpoch<=epoch||!CheckpointWire.Binding(nextBinding))return RuleResult.Reject("producer-close-identity-or-recovery");
                if(admissionClosed&&(closeNextEpoch!=nextEpoch||closeNextBinding!=nextBinding))return RuleResult.Reject("producer-close-conflict");
                // Freeze admission before capturing. A failed capture/flush leaves live admission
                // closed; no capability is returned until the coherent journal is durable.
                admissionClosed=true;closeNextEpoch=nextEpoch;closeNextBinding=nextBinding;checkpointContext=c;
                try {
                    health.CreateCheckpoint(c,checkpointBinding); // Reservations must be disposed by their owner.
                    if(!Capture(c))return RuleResult.Reject("producer-close-checkpoint");
                }catch(System.InvalidOperationException){return RuleResult.Reject("producer-close-reservation");}
                value=new ProducerAdmissionClose(session,epoch,nextEpoch,nextBinding,SuccessfulCheckpoint);
                return RuleResult.Accept();
            });
            closure=value;return result;
        }
        public RuleResult CheckGlobalRetirement(CommandContext c,long deliveredThrough)
        {
            return health.Serialize(()=>{
                if(!Host(c)||RecoveryRequired||!admissionClosed)return RuleResult.Reject("producer-close-required");
                if(pending.Count!=0||ack!=deliveredThrough||MatchCheckpointRecords.Sequence(health)!=deliveredThrough||health.PendingTransitionCount!=0)return RuleResult.Reject("producer-undelivered");
                // No root concrete registry receipt exists in this pinned composition. A local
                // close must not authorize teardown in the presence of direct references or tokens.
                return RuleResult.Reject("root-producer-quiescence-required");
            });
        }
    }
}

