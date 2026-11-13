using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.InventoryItems
{
    // The root provisions one Accounts registry per authority domain on its host lane.
    // This port validates authenticated explicit source receipts; client strings are not grants.
    public interface IBuildGrantAuthority
    {bool Validate(CommandContext context,string sourceReceipt,string sourceKind,int amount);}
    public sealed class BuildChargeAccounts
    {
        readonly string session;readonly long epoch;readonly IAuthorityGate authority;readonly IBuildGrantAuthority grants;
        readonly Dictionary<string,BuildChargeLedger> actors=new Dictionary<string,BuildChargeLedger>(StringComparer.Ordinal);
        public BuildChargeAccounts(string session,long epoch,IAuthorityGate authority,IBuildGrantAuthority grants)
        {if(String.IsNullOrEmpty(session)||epoch<0||authority==null||grants==null)throw new ArgumentException("build accounts");this.session=session;this.epoch=epoch;this.authority=authority;this.grants=grants;}
        public BuildChargeLedger ForActor(string actor)
        {if(String.IsNullOrEmpty(actor)||actor.Length>128)throw new ArgumentException("actor");BuildChargeLedger ledger;if(!actors.TryGetValue(actor,out ledger)){ledger=new BuildChargeLedger(session,epoch,actor,authority,grants);actors.Add(actor,ledger);}return ledger;}
    }
    public sealed class BuildChargeLedger
    {
        public const string ApiVersion="BuildChargeLedger/1";
        readonly string session,actor;readonly long epoch;readonly IAuthorityGate authority;readonly IBuildGrantAuthority grants;
        HashSet<string> commands=new HashSet<string>(StringComparer.Ordinal),receipts=new HashSet<string>(StringComparer.Ordinal);
        int balance;long revision,lastTick=-1;bool seeded;PreparedCharge pending;
        internal BuildChargeLedger(string session,long epoch,string actor,IAuthorityGate authority,IBuildGrantAuthority grants)
        {this.session=session;this.epoch=epoch;this.actor=actor;this.authority=authority;this.grants=grants;}
        internal bool ReconcileNetworkBalance(int value){if(value<0||pending!=null||revision==long.MaxValue)return false;if(balance!=value){balance=value;revision++;}return true;}
        public int Balance {get{return balance;}}public long Revision {get{return revision;}}
        bool Gate(CommandContext c){return c.IsAuthority&&c.SessionId==session&&c.ActorId==actor&&c.AuthorityEpoch==epoch&&c.Tick>=0&&c.Tick>=lastTick&&!String.IsNullOrEmpty(c.CommandId)&&c.CommandId.Length<=128&&!commands.Contains(c.CommandId)&&commands.Count<4096&&revision<long.MaxValue&&authority.Validate(c).Accepted;}
        public sealed class PreparedCharge
        {
            readonly BuildChargeLedger owner;readonly long revision;readonly CommandContext context;readonly long expiry;
            readonly int after,grantAmount;readonly string receipt,kind;readonly bool seeded;readonly HashSet<string> commands,receipts;bool committed,aborted;
            internal PreparedCharge(BuildChargeLedger owner,CommandContext context,int after,long expiry,string receipt,bool seed,string kind=null,int grantAmount=0)
            {this.receipt=receipt;this.kind=kind;this.grantAmount=grantAmount;this.owner=owner;this.context=context;revision=owner.revision;this.after=after;this.expiry=expiry;seeded=owner.seeded||seed;commands=new HashSet<string>(owner.commands,StringComparer.Ordinal);commands.Add(context.CommandId);receipts=new HashSet<string>(owner.receipts,StringComparer.Ordinal);if(receipt!=null)receipts.Add(receipt);}
            public int BalanceAfter {get{return after;}}
            public bool Validate(long now){return !committed&&!aborted&&now>=context.Tick&&now<=expiry&&owner.revision==revision&&Object.ReferenceEquals(owner.pending,this)&&owner.authority.Validate(context).Accepted&&(receipt==null||owner.grants.Validate(context,receipt,kind,grantAmount));}
            public void CommitValidated(){if(committed||aborted)return;owner.balance=after;owner.revision=revision+1;owner.commands=commands;owner.receipts=receipts;owner.seeded=seeded;owner.lastTick=context.Tick;owner.pending=null;committed=true;}
            public void Abort(){if(committed||aborted)return;aborted=true;if(Object.ReferenceEquals(owner.pending,this))owner.pending=null;}
        }
        public RuleResult PrepareDebit(CommandContext context,int cost,long expiry,out PreparedCharge prepared)
        {prepared=null;if(pending!=null||!Gate(context)||cost<0||cost>4||cost>balance||expiry<context.Tick)return RuleResult.Reject("build_debit");prepared=new PreparedCharge(this,context,balance-cost,expiry,null,false);pending=prepared;return RuleResult.Accept();}
        // Backpack supply drops debit their whole finite box, independently of
        // the bounded four-charge panel/support placement limit above.
        public RuleResult PrepareSupplyWithdrawal(CommandContext context,int amount,long expiry,out PreparedCharge prepared)
        {prepared=null;if(pending!=null||!Gate(context)||amount<1||amount>6||amount>balance||expiry<context.Tick)return RuleResult.Reject("build_supply_withdrawal");prepared=new PreparedCharge(this,context,balance-amount,expiry,null,false);pending=prepared;return RuleResult.Accept();}
        public RuleResult PrepareGrant(CommandContext context,string receipt,string kind,int amount,long expiry,out PreparedCharge prepared)
        {
            prepared=null;bool seed=kind=="source-initial999";
            if(pending!=null||!Gate(context)||String.IsNullOrEmpty(receipt)||receipt.Length>256||receipts.Contains(receipt)||receipts.Count>=4096||expiry<context.Tick||
                (seed?(seeded||amount!=999):(kind!="builds"||amount<1||amount>6))||amount>Int32.MaxValue-balance||!grants.Validate(context,receipt,kind,amount))return RuleResult.Reject("build_grant");
            prepared=new PreparedCharge(this,context,balance+amount,expiry,receipt,seed,kind,amount);pending=prepared;return RuleResult.Accept();
        }
    }
}
