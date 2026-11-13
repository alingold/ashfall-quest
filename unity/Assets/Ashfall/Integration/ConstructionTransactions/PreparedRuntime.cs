using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.InventoryItems;
using Ashfall.Integration.ConstructionRuntime;
namespace Ashfall.Integration.ConstructionTransactions
{
    // Explicit host-lane composition. Native mirrors consume Pending after commit;
    // they are not participants in the authoritative in-memory publication.
    public sealed class PreparedRuntime
    {
        readonly CanonicalBuilder source;readonly RuntimeHost ingress;
        readonly PreparedConstruction structures;readonly BuildChargePreparation charges;
        readonly PreparedPublisher publisher;
        readonly BuildChargeAccounts supplyAccounts;readonly PreparedSupplyGrant supply;
        public PreparedRuntime(CanonicalBuilder source,RuntimeHost ingress,PreparedConstruction structures,
            BuildChargeAccounts accounts,int capacity)
        {
            if(source==null||ingress==null||structures==null||accounts==null)throw new ArgumentException("prepared runtime binding");
            this.source=source;this.ingress=ingress;this.structures=structures;supplyAccounts=accounts;
            charges=new BuildChargePreparation(accounts,source);publisher=new PreparedPublisher(source,capacity);
        }
        public bool Claim(string hand,long expiry,out ConstructionLease lease)
        {return source.Claim(ingress,hand,expiry,out lease);}
        public RuleResult Place(ConstructionCommand command,ConstructionLease lease,long expiry)
        {return publisher.Place(structures,charges,command,lease,expiry);}
        public ConstructionPublication Pending {get{return publisher.Pending;}}
        public bool Acknowledge(ConstructionPublication exact){return publisher.Acknowledge(exact);}
        public PlacedPanel[] Snapshot(){return structures.Snapshot();}
        public PreparedRuntime(CanonicalBuilder source,RuntimeHost ingress,PreparedConstruction structures,
            BuildChargeAccounts accounts,int capacity,PreparedSupplyGrant supply)
            :this(source,ingress,structures,accounts,capacity)
        {if(supply==null||!supply.Binds(source))throw new ArgumentException("supply binding");this.supply=supply;}
        public RuleResult CollectBuildSupply(CommandContext command,string sourceBox)
        {return supply==null?RuleResult.Reject("pickup_prepared_source_removal_receipt_api_unbound"):supply.Collect(supplyAccounts,command,sourceBox);}
    }
}
