using System;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.InventoryItems;
using Ashfall.Integration.ConstructionRuntime;
namespace Ashfall.Integration.ConstructionTransactions
{
    // Root must construct Accounts with THIS authority, and bind the same host
    // scheduler to Pickups.Advance. It never advances a clock to make a receipt fit.
    public sealed class PreparedSupplyGrant:IBuildGrantAuthority,IPickupSupplyLifetime
    {
        readonly PickupRules pickups;readonly IConstructionHostState host;
        PickupRules.PreparedSupplyRemoval active;
        public PreparedSupplyGrant(PickupRules pickups,IConstructionHostState host)
        {
            if(pickups==null||host==null)throw new ArgumentException("supply owners");
            this.pickups=pickups;this.host=host;
            if(!pickups.BindSupplyLifetime(this))throw new ArgumentException("supply lifetime already bound");
        }
        internal bool Binds(IConstructionHostState source){return Object.ReferenceEquals(host,source);}
        public bool TryRead(string actor,out PickupSupplyLife life)
        {
            life=null;HostBuilderStamp s;
            if(host.Tick!=pickups.Tick||!host.TryRead(actor,out s)||s==null||!s.Active||s.Actor!=actor)return false;
            life=new PickupSupplyLife(s.AuthorityLease,s.Life,s.Connection,s.RootRevision,s.PoseRevision,s.InputGeneration);return true;
        }
        public bool Validate(CommandContext c,string receipt,string kind,int amount)
        {
            HostBuilderStamp stamp;
            if(active==null||!host.TryRead(c.ActorId,out stamp)||!BuildChargePreparation.Current(stamp,c))return false;var r=active.Receipt;
            return receipt==r.Identity&&kind=="builds"&&r.Kind==kind&&amount==6&&r.Quantity==amount&&
                c.SessionId==r.Session&&c.ActorId==r.Actor&&c.CommandId==r.Command&&c.AuthorityEpoch==r.Epoch&&c.Tick==r.Tick&&c.IsAuthority&&active.Validate(host.Tick);
        }
        public RuleResult Collect(BuildChargeAccounts accounts,CommandContext c,string box)
        {
            if(accounts==null||active!=null||host.Tick!=pickups.Tick||host.Tick!=c.Tick)return RuleResult.Reject("supply scheduler/binding");
            // Expire within the current owner step; tick scale stays owned by Pickups.
            PickupRules.PreparedSupplyRemoval removal;
            var result=pickups.PrepareSupplyRemoval(c,box,pickups.Revision,c.Tick,out removal);
            if(!result.Accepted)return result;
            BuildChargeLedger.PreparedCharge grant=null;active=removal;
            try {
                var r=removal.Receipt;
                if(r.Kind!="builds"||r.Quantity!=6)return RuleResult.Reject("unsupported canonical supply quantity");
                result=accounts.ForActor(c.ActorId).PrepareGrant(c,r.Identity,r.Kind,r.Quantity,r.Expiry,out grant);
                if(!result.Accepted)return result;
                if(!removal.Validate(host.Tick)||!grant.Validate(host.Tick))return RuleResult.Reject("supply stale");
                removal.CommitValidated();grant.CommitValidated();return RuleResult.Accept();
            } finally {if(grant!=null)grant.Abort();removal.Abort();active=null;}
        }
    }
}
