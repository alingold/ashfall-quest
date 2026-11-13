using System;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Integration.ConstructionRuntime;
namespace Ashfall.Integration.ConstructionTransactions
{
    // Actual Combat Commit invokes this application only for its registered pending decision.
    // Target must be exact typed body identity, never a semantic owner or caller hit claim.
    // No actual structure backend exists yet; missing preparation fails before mutation.
    public sealed class CombatStructures
    {
        sealed class Application:ICombatApplication
        {
            internal IStructurePreparation Structures;internal PreparedPublisher Publisher;
            internal CommandContext Context;internal string Body;internal long Expiry;
            public RuleResult TryApply(DamageDecision decision)
            {
                if(decision==null||!decision.Accepted||decision.TargetId!=Body||decision.SessionId!=Context.SessionId||decision.AuthorityEpoch!=Context.AuthorityEpoch)
                    return RuleResult.Reject("structure damage identity");
                IPreparedStructure prepared;var r=Structures.PrepareDamage(Context,Body,decision.Requested,decision.ExpectedRevision,Expiry,out prepared);
                if(!r.Accepted){if(prepared!=null)prepared.Abort();return r;}
                return Publisher.Publish(prepared,null,Expiry);
            }
        }
        readonly CombatRules combat;readonly IStructurePreparation structures;readonly PreparedPublisher publisher;readonly IConstructionHostState clock;
        public CombatStructures(CombatRules combat,IStructurePreparation structures,PreparedPublisher publisher,IConstructionHostState clock)
        {if(combat==null||publisher==null||clock==null)throw new ArgumentException("combat structure binding");this.combat=combat;this.structures=structures;this.publisher=publisher;this.clock=clock;}
        public RuleResult Apply(CommandContext context,string registeredHit,string body,long expiry)
        {
            if(structures==null)return RuleResult.Reject("prepared_structure_damage_api_unbound");
            if(String.IsNullOrEmpty(body)||!body.StartsWith("constructed/",StringComparison.Ordinal))return RuleResult.Reject("typed construction body required");
            long tick=clock.Tick;if(tick<0||tick>expiry)return RuleResult.Reject("damage expiry");
            var decision=combat.Evaluate(context,registeredHit,tick);
            if(!decision.Accepted)return RuleResult.Reject(decision.Reason);
            if(decision.TargetId!=body)return RuleResult.Reject("body mismatch");
            return combat.Commit(decision,new Application{Structures=structures,Publisher=publisher,Context=context,Body=body,Expiry=expiry},tick);
        }
    }
}
