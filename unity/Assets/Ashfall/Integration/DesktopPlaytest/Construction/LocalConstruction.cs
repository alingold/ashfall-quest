using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Modules.InventoryItems;
namespace Ashfall.Integration.DesktopPlaytest.Construction
{
    // Explicitly local test entitlement. Never connected to network ingress or pickups.
    public sealed class LocalConstruction
    {
        sealed class LocalGate : IAuthorityGate, IBuildGrantAuthority
        {
            public RuleResult Validate(CommandContext c)
            { return c.IsAuthority && c.SessionId=="desktop-synthetic" && c.AuthorityEpoch==0 && c.ActorId=="local-builder" && c.Tick>=0 ? RuleResult.Accept():RuleResult.Reject("local_context"); }
            public bool Validate(CommandContext c,string receipt,string kind,int amount)
            { return Validate(c).Accepted && receipt=="local-test-initial999" && kind=="source-initial999" && amount==999; }
        }
        readonly CandidateService service;
        readonly BuildChargeLedger ledger;
        long serial;
        public int Balance { get { return ledger.Balance; } }
        public PlacedPanel[] Snapshot() { return service.Snapshot(); }
        public LocalConstruction(IConstructionWorld world)
        {
            var gate=new LocalGate();
            service=new CandidateService("desktop-synthetic",0,gate,world,false);
            ledger=new BuildChargeAccounts("desktop-synthetic",0,gate,gate).ForActor("local-builder");
            BuildChargeLedger.PreparedCharge grant;
            if(!ledger.PrepareGrant(Context(0),"local-test-initial999","source-initial999",999,0,out grant).Accepted || !grant.Validate(0))throw new InvalidOperationException("local_seed");
            grant.CommitValidated();
        }
        CommandContext Context(long tick)
        { return new CommandContext("desktop-synthetic","local-builder","local-"+(++serial),tick,0,true); }
        // Preview exercises the same actual placement service then aborts; it spends nothing.
        public RuleResult TryPlace(BuilderState pose,long tick,bool commit,out PlacedPanel[] panels,out int cost)
        {
            panels=new PlacedPanel[0];cost=0;
            if(tick<0 || serial==long.MaxValue)return RuleResult.Reject("tick/serial");
            var command=Context(tick); CandidateService.PreparedChange structure;
            var result=service.PreparePlace(new ConstructionCommand(command,serial),pose,out structure);
            if(!result.Accepted)return result;
            cost=structure.Cost;panels=structure.Panels;
            BuildChargeLedger.PreparedCharge debit;
            result=ledger.PrepareDebit(command,cost,tick,out debit);
            if(!result.Accepted){structure.Abort();return result;}
            if(!structure.Validate() || !debit.Validate(tick))
            {structure.Abort();debit.Abort();return RuleResult.Reject("validation");}
            if(commit){structure.CommitValidated();debit.CommitValidated();}
            else {structure.Abort();debit.Abort();}
            return RuleResult.Accept();
        }
    }
}
