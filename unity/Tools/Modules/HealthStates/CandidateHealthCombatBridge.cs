using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;

// STAGED TOOLING ONLY. All world/evidence/query callbacks must be pure stable host reads.
// The service monitor excludes every competing Health mutation through evaluation/application.
public sealed class CandidateHealthCombatBridge : ICombatEvidence
{
    readonly HealthService health; readonly CombatRules combat; readonly ICombatEvidence source;
    readonly Dictionary<string,BridgeReceipt> completed=new Dictionary<string,BridgeReceipt>();
    readonly Dictionary<string,string> commandHits=new Dictionary<string,string>();
    HealthSnapshot evaluated;HitEvidence prepared;long serial;
    readonly Dictionary<string,WeaponLease> leases=new Dictionary<string,WeaponLease>();
    sealed class Apply:ICombatApplication {readonly Func<DamageDecision,RuleResult> operation;internal Apply(Func<DamageDecision,RuleResult> operation){this.operation=operation;}public RuleResult TryApply(DamageDecision decision){return operation(decision);}}
    // This remains a host-side TOOL fixture. Current Combat requires host-minted
    // weapon/event/contact capabilities before its pure evidence callback runs.
    void PrepareEvidence(CommandContext context,string hitId,long serverTick){prepared=null;evaluated=null;HitEvidence e;if(!source.TryResolve(context,hitId,out e)||e==null)return;var target=health.Read(e.TargetId);var actor=health.Read(context.ActorId);if(target==null||actor==null)return;var world=source as IHealthWorld;if(world==null)throw new InvalidOperationException("Tool fixture needs a trusted host clock");
        var host=new CommandContext(context.SessionId,"host","fixture-event/"+(++serial),serverTick,context.AuthorityEpoch,true);string key=context.ActorId+":"+e.WeaponId;WeaponLease lease;if(!leases.TryGetValue(key,out lease)){lease=combat.Equip(host,key,context.ActorId,e.WeaponId,1,serverTick);leases.Add(key,lease);}var ev=combat.PublishEvent(host,serial,serverTick+256,DamageSource.CurrentActorIntent,e.Kind,lease,serverTick);e.Identity=combat.PublishHit(host,ev,hitId,e.TargetId,e.ContactEntry,serverTick);e.HealthRevision=target.Revision;e.Health=target.Health;e.Shield=target.Shield;e.TargetAlive=target.Connected&&target.Life==LifeState.Alive;e.ActorAlive=actor.Connected&&actor.Life==LifeState.Alive;e.Protected=world.Now<target.ProtectionUntil;evaluated=target;prepared=e;
    }
    public CandidateHealthCombatBridge(HealthService h,string session,long epoch,IAuthorityGate gate,ICombatEvidence evidence,IInteractionQuery query)
    {health=h;source=evidence;combat=new CombatRules(session,epoch,256,4096,gate,this,query);}
    public sealed class BridgeReceipt
    {
        public readonly string HitId; public readonly RuleResult Result; public readonly DamageReceipt Damage;
        private readonly HealthTransition[] events;
        public HealthTransition[] Events {get{return (HealthTransition[])events.Clone();}}
        internal BridgeReceipt(string hit,RuleResult result,DamageReceipt damage,HealthTransition[] events)
        {HitId=hit;Result=result;Damage=damage;this.events=(HealthTransition[])events.Clone();}
    }
    public BridgeReceipt Hit(CommandContext context,string hitId,long serverTick)
    {
        return health.Serialize(delegate {
            // Authenticate even terminal retries; no cross-session/epoch cache lookup.
            RuleResult admission=health.Authorize(context);
            if(!admission.Accepted)return new BridgeReceipt(hitId,admission,null,health.DrainTransitions());
            string command=context.ActorId.Length+":"+context.ActorId+context.CommandId;
            string key=context.ActorId.Length+":"+context.ActorId+hitId;
            string bound;
            if(commandHits.TryGetValue(command,out bound)&&bound!=hitId)
                return new BridgeReceipt(hitId,RuleResult.Reject("identity-conflict"),null,health.DrainTransitions());
            BridgeReceipt previous;
            if(completed.TryGetValue(key,out previous)) {
                // Event IDs remain stable on retry; append any newly expired transitions.
                var merged=new List<HealthTransition>(previous.Events);merged.AddRange(health.DrainTransitions());
                return new BridgeReceipt(hitId,previous.Result,previous.Damage,merged.ToArray());
            }
            admission=health.PrepareDamage(context);
            if(!admission.Accepted)return new BridgeReceipt(hitId,admission,null,health.DrainTransitions());
            if(completed.Count>=8192)return new BridgeReceipt(hitId,RuleResult.Reject("capacity"),null,health.DrainTransitions());
            PrepareEvidence(context,hitId,serverTick);
            DamageDecision decision=combat.Evaluate(context,hitId,serverTick);
            DamageReceipt applied=null;
            RuleResult result=RuleResult.Reject(decision.Reason);
            if(decision.Accepted) {
                if(evaluated==null||decision.TargetId!=evaluated.Id)throw new InvalidOperationException("Combat target drift");
                result=combat.Commit(decision,new Apply(delegate(DamageDecision d){applied=health.ApplyDamage(context,hitId,d.TargetId,evaluated.Revision,d.HealthLoss,d.ShieldLoss,true);return applied.Result;}),serverTick);
                if(applied.Retryable)throw new InvalidOperationException("Reentrant host changed health during transaction");
            }
            var receipt=new BridgeReceipt(hitId,result,applied,health.DrainTransitions());
            completed.Add(key,receipt);commandHits.Add(command,hitId);return receipt;
        });
    }
    public bool TryResolve(CommandContext context,string hitId,out HitEvidence evidence)
    {
        evidence=prepared;return evidence!=null;
    }
}
