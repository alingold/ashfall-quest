using Ashfall.Core;
using Ashfall.Modules.Combat;
namespace Ashfall.Integration.CombatPublication
{
    public sealed partial class HealthPublicationLane
    {
        // Trusted host simulation routes capability creation through the same durable lane.
        // Outputs are exposed only after the complete post-operation checkpoint succeeds.
        public RuleResult Equip(CommandContext host,string instance,string owner,string archetype,long generation,long tick,out WeaponLease weapon)
        {
            WeaponLease value=null;
            var result=Run(host,()=>{value=bridge.Equip(host,instance,owner,archetype,generation,tick);return value==null?RuleResult.Reject("equip"):RuleResult.Accept();});
            weapon=result.Accepted?value:null;return result;
        }
        public RuleResult PublishEvent(CommandContext host,long serial,long expires,DamageSource source,DamageKind kind,WeaponLease weapon,long tick,out HostEvent published)
        {
            HostEvent value=null;
            var result=Run(host,()=>{value=bridge.PublishEvent(host,serial,expires,source,kind,weapon,tick);return value==null?RuleResult.Reject("event"):RuleResult.Accept();});
            published=result.Accepted?value:null;return result;
        }
        public RuleResult PublishSourceExplosion(CommandContext host,long serial,long expires,WeaponLease weapon,
            SourceRadialProvenance provenance,Float3 origin,float radius,float power,SourceRadialFalloff falloff,long tick,out HostEvent published)
        {
            HostEvent value=null;
            var result=Run(host,()=>{value=bridge.PublishSourceExplosion(host,serial,expires,weapon,provenance,origin,radius,power,falloff,tick);return value==null?RuleResult.Reject("explosion"):RuleResult.Accept();});
            published=result.Accepted?value:null;return result;
        }
        public RuleResult PublishHit(CommandContext host,string id,HostEvent ev,string subhit,string target,long contact,long tick,out HostHit published)
        {
            HostHit value=null;
            var result=Run(host,()=>{value=bridge.PublishHit(host,id,ev,subhit,target,contact,tick);return value==null?RuleResult.Reject("hit"):RuleResult.Accept();});
            published=result.Accepted?value:null;return result;
        }
        // Local closure is persisted first, but is not an all-producer retirement receipt.
        public RuleResult PrepareEpochRetirement(CommandContext host,long nextEpoch,string nextBinding,out CheckpointReceipt checkpoint)
        {
            checkpoint=null;
            ProducerAdmissionClose closure;
            RuleResult closed=CloseProducerAdmission(host,nextEpoch,nextBinding,out closure);
            if(!closed.Accepted)return closed;
            return CheckGlobalRetirement(host,closure.Checkpoint.HealthSequence);
        }
    }
}
