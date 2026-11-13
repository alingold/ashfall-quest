using CheckpointWire = Ashfall.Integration.CheckpointWire;
using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Persistence;
namespace Ashfall.Integration.CombatPublication
{
    public sealed partial class HealthPublicationLane
    {
        // Receipt (digest, source binding, minimum sequence) is a trusted coordinator input.
        // Returned objects are all cold instances. Existing objects are never restored/rolled back.
        public static bool TryRestoreCold(CommandContext host,string binding,string expectedDigest,long minimumSequence,
            AtomicSettingsStore persistence,int teams,bool respawn,IAuthorityGate authority,IHealthWorld world,
            IInteractionQuery query,ICombatEvidence facts,out HealthService health,out CombatHealthBridge bridge,
            out HealthPublicationLane lane,double downed=60,double delay=5,double protection=3,int eventCapacity=2048,long minimumGeneration=0,bool allowBackupRecovery=false)
        {
            health=null;bridge=null;lane=null;
            if(!CheckpointWire.Binding(binding)||minimumSequence<0||minimumGeneration<0||persistence==null)return false;
            try {
                if(!persistence.Load(allowBackupRecovery).Accepted)return false;
                string saved,journal;
                if(!persistence.TryRead("combat-checkpoint/2",out saved)||saved.Length>CheckpointWire.MaximumBytes*2||!persistence.TryRead("combat-publication/1",out journal))return false;
                byte[] bytes=Convert.FromBase64String(saved);HealthService s;CombatHealthBridge b;long generation;
                if(!CombatHealthBridge.TryColdCheckpoint(host,binding,expectedDigest,bytes,journal,teams,respawn,authority,world,query,facts,out s,out b,out generation,downed,delay,protection)||MatchCheckpointRecords.Sequence(s)<minimumSequence||generation<minimumGeneration)return false;
                // Decode and validate publication state before exposing ANY reconstructed object.
                var result=new HealthPublicationLane(s,b,persistence,host.SessionId,host.AuthorityEpoch,host.ActorId,eventCapacity,binding,true);
                result.Decode(journal);
                if(result.high>MatchCheckpointRecords.Sequence(s)||result.ack>MatchCheckpointRecords.Sequence(s))return false;
                result.reopened=false;result.dirty=false;result.checkpointContext=host;result.SuccessfulCheckpoint=new CheckpointReceipt(binding,expectedDigest,generation,MatchCheckpointRecords.Sequence(s),result.ack);
                health=s;bridge=b;lane=result;return true;
            }catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}
        }
        public RuleResult Respawn(CommandContext host,CommandContext actor,string target,long revision,long life,long connection,double expiresAt)
        {
            return Run(host,()=>{
                HealthService.PreparedRespawn token;
                if(!health.PrepareRespawn(actor,target,revision,life,connection,expiresAt,out token))return RuleResult.Reject("respawn-prepare");
                if(!token.Validate()){token.Abort();return RuleResult.Reject("respawn-stale");}
                token.CommitValidated();return RuleResult.Accept();
            });
        }
    }
}


