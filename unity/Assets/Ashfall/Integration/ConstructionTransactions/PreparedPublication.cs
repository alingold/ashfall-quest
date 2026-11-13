using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Integration.ConstructionRuntime;
namespace Ashfall.Integration.ConstructionTransactions
{
    // Proposed coordinator boundary. No implementation is inferred for accepted Construction/2.
    // Prepare preallocates panel IDs, support/collapse state, replay and immutable output.
    // All methods run under the SAME external host lane as Inventory and Combat.
    public interface IPreparedStructure
    {
        ConstructionPublication Publication {get;}
        bool Validate();
        void CommitValidated();
        void Abort();
    }
    internal interface IPreparedChargeBinding {bool Accepts(IPreparedBuildCharge charge);}
    public interface IStructurePreparation
    {
        RuleResult PreparePlace(ConstructionCommand command,ConstructionLease lease,long expiry,out IPreparedStructure prepared);
        RuleResult PrepareDamage(CommandContext context,string body,float damage,long expectedRevision,long expiry,out IPreparedStructure prepared);
        RuleResult PrepareAdvance(float seconds,bool paused,long expiry,out IPreparedStructure prepared);
    }
    // Requires an actual Inventory build-charge reservation, not a shadow balance.
    public interface IPreparedBuildCharge
    { bool Validate();void CommitValidated();void Abort(); }
    public interface IBuildChargePreparation
    { RuleResult Prepare(CommandContext context,int cost,long expiry,out IPreparedBuildCharge prepared); }
    // Fixed capacity pull/ack journal. Deliveries retain the same immutable object on retry.
    // No IO, callbacks or allocations between validate-all and publication.
    public sealed class PreparedPublisher
    {
        readonly ConstructionPublication[] ring;int start,count;bool busy;
        readonly IConstructionHostState clock;
        long observed=-1,lastSerial;bool clockFailed;string domain;
        public PreparedPublisher(IConstructionHostState clock,int capacity)
        {if(clock==null||capacity<1||capacity>1024)throw new ArgumentException("publisher binding");this.clock=clock;ring=new ConstructionPublication[capacity];}
        bool Time(long expiry)
        {long now=clock.Tick;if(now<0||now<observed)clockFailed=true;observed=now;return !clockFailed&&now<=expiry;}
        public ConstructionPublication Pending {get{return count==0?null:ring[start];}}
        public bool Acknowledge(ConstructionPublication publication)
        {if(busy||publication==null||!Object.ReferenceEquals(Pending,publication))return false;ring[start]=null;start=(start+1)%ring.Length;count--;return true;}
        public RuleResult Publish(IPreparedStructure structure,IPreparedBuildCharge charge,long expiry)
        {
            if(busy)return RuleResult.Reject("reentrant publication");
            busy=true;
            try {
                if(structure==null)return RuleResult.Reject("prepared_structure_api_unbound");
                if(count==ring.Length||!Time(expiry))return RuleResult.Reject("publication capacity/time");
                var publication=structure.Publication;
                if(publication==null||lastSerial==long.MaxValue||publication.Serial!=lastSerial+1)return RuleResult.Reject("immutable publication sequence required");
                string nextDomain=BodyIdentity.ConstructedScope(publication.Session,publication.Epoch,publication.HostInstance)+BodyIdentity.Part(publication.MapHash);
                if(domain!=null&&domain!=nextDomain)return RuleResult.Reject("publication domain mismatch");
                // All externally fallible reads and validation finish before either commit.
                var binding=structure as IPreparedChargeBinding;
                if(binding!=null&&!binding.Accepts(charge))return RuleResult.Reject("concrete charge binding mismatch");
                if(!structure.Validate()||(charge!=null&&!charge.Validate())||!Time(expiry))return RuleResult.Reject("prepared participant stale");
                structure.CommitValidated();
                if(charge!=null)charge.CommitValidated();
                ring[(start+count)%ring.Length]=publication;count++;lastSerial=publication.Serial;domain=nextDomain;
                return RuleResult.Accept();
            } finally {if(charge!=null)charge.Abort();if(structure!=null)structure.Abort();busy=false;}
        }
        public RuleResult Place(IStructurePreparation structures,IBuildChargePreparation charges,ConstructionCommand command,ConstructionLease lease,long expiry)
        {
            // Do not call legacy Place, debit a substitute counter, or mutate admission.
            if(structures==null||charges==null)return RuleResult.Reject("prepared_construction_inventory_api_unbound");
            IPreparedStructure structure;var r=structures.PreparePlace(command,lease,expiry,out structure);
            if(!r.Accepted){if(structure!=null)structure.Abort();return r;}
            if(structure==null)return RuleResult.Reject("missing structure reservation");
            IPreparedBuildCharge charge=null;
            try {
                var p=structure.Publication;if(p==null)return RuleResult.Reject("missing publication");
                // Cost must be returned by the prepared backend, never derived from a ghost.
                var priced=structure as IPreparedPlacement;
                if(priced==null||priced.Cost<0||priced.Cost>2)return RuleResult.Reject("prepared cost missing/bounded");
                r=charges.Prepare(command.Context,priced.Cost,expiry,out charge);
                if(!r.Accepted)return r;
                if(charge==null)return RuleResult.Reject("missing charge reservation");
                return Publish(structure,charge,expiry);
            } finally {if(charge!=null)charge.Abort();structure.Abort();}
        }
        public RuleResult Advance(IStructurePreparation structures,float seconds,bool paused,long expiry)
        {
            if(structures==null)return RuleResult.Reject("prepared_structure_advance_api_unbound");
            if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<0)return RuleResult.Reject("advance interval");
            IPreparedStructure prepared;var r=structures.PrepareAdvance(seconds,paused,expiry,out prepared);
            if(!r.Accepted){if(prepared!=null)prepared.Abort();return r;}
            return Publish(prepared,null,expiry);
        }
    }
    public interface IPreparedPlacement : IPreparedStructure {int Cost {get;}}
}
