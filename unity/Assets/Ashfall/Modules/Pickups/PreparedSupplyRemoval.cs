using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.Pickups
{
    // Trusted host owner port; no client supplied lifetime numbers are accepted.
    public interface IPickupSupplyLifetime
    {
        bool TryRead(string actor,out PickupSupplyLife life);
    }
    public sealed class PickupSupplyLife
    {
        public readonly string Authority;
        public readonly long Life,Connection,RootRevision,PoseRevision,InputGeneration;
        public PickupSupplyLife(string authority,long life,long connection,long root,long pose,long input)
        {Authority=authority;Life=life;Connection=connection;RootRevision=root;PoseRevision=pose;InputGeneration=input;}
        internal bool Same(PickupSupplyLife b)
        {return b!=null&&Authority==b.Authority&&Life==b.Life&&Connection==b.Connection&&RootRevision==b.RootRevision&&PoseRevision==b.PoseRevision&&InputGeneration==b.InputGeneration;}
        internal bool Valid {get{return !String.IsNullOrEmpty(Authority)&&Life>0&&Connection>0&&RootRevision>0&&PoseRevision>=0&&InputGeneration>0;}}
    }
    public sealed class SupplyRemovalReceipt
    {
        public readonly string Identity,Box,Kind,SemanticBody,Session,Actor,Command;
        public readonly int Quantity;
        public readonly long Revision,Epoch,Tick,Expiry;
        public readonly Float3 Position;
        public readonly PickupSupplyLife Lifetime;
        internal SupplyRemovalReceipt(CommandContext c,CanonicalPickupSource s,long expiry,PickupSupplyLife life)
        {Identity=Guid.NewGuid().ToString("N");Box=s.InstanceId;Kind=s.Kind;SemanticBody=s.SemanticOwnerId;Quantity=s.Amount;Revision=s.Revision;Position=s.Position;Session=c.SessionId;Actor=c.ActorId;Command=c.CommandId;Epoch=c.AuthorityEpoch;Tick=c.Tick;Expiry=expiry;Lifetime=life;}
    }
    public sealed partial class PickupRules
    {
        // Runtime-only binding is intentionally not serialized. Restored owners are
        // quarantined and unbound. Capture refuses every outstanding reservation.
        private IPickupSupplyLifetime supplyLifetime;
        private PreparedSupplyRemoval pendingSupply;
        public long Tick {get{lock(sync)return now;}}
        public long Revision {get{lock(sync)return revision;}}
        public int PendingSupplyCount {get{lock(sync)return pendingSupply==null?0:1;}}
        public bool BindSupplyLifetime(IPickupSupplyLifetime lifetime)
        {lock(sync){if(lifetime==null||supplyLifetime!=null||quarantined||executing||pendingSupply!=null)return false;supplyLifetime=lifetime;return true;}}
        public RuleResult PrepareSupplyRemoval(CommandContext c,string box,long expectedRevision,long expiry,out PreparedSupplyRemoval prepared)
        {
            lock(sync){
                prepared=null;
                if(quarantined||executing||pendingSupply!=null||supplyLifetime==null||revision==long.MaxValue||expectedRevision!=revision||
                    !c.IsAuthority||c.SessionId!=session||c.AuthorityEpoch!=epoch||!Id(c.ActorId)||!Id(c.CommandId)||c.Tick!=now||expiry<now||expiry-now>lease)return No("supply-binding");
                executing=true;
                try {
                    Item i;ActorFrame f;PickupSupplyLife life;Dictionary<string,long> history;
                    if(!gate.Validate(c).Accepted||!supplyLifetime.TryRead(c.ActorId,out life)||life==null||!life.Valid||!actors.TryRead(c.ActorId,out f)||!Valid(f)||!f.Allowed||!f.HandEligible||!f.TransferSafe)return No("supply-lifetime");
                    if(box==null||!items.TryGetValue(box,out i)||i.Removed||i.Owner!=null||SupplyAmount(i.Kind)==0||i.Amount<=0||i.Reserved!=c.ActorId||i.Until<=now||expiry>=i.Until||!Reach(c.ActorId,f,i))return No("supply-source");
                    if(!seen.TryGetValue(c.ActorId,out history)||history.ContainsKey(c.CommandId)||replayCount>=replayCapacity)return No("supply-replay");
                    var next=new Dictionary<string,Dictionary<string,long>>(seen,StringComparer.Ordinal);
                    var commands=new Dictionary<string,long>(history,StringComparer.Ordinal);commands.Add(c.CommandId,now);next[c.ActorId]=commands;
                    var receipt=new SupplyRemovalReceipt(c,new CanonicalPickupSource(i.Id,i.Kind,i.Semantic,i.Owner,i.Reserved,i.Amount,i.Position,i.Removed,i.Until,revision),expiry,life);
                    prepared=new PreparedSupplyRemoval(this,c,receipt,next);pendingSupply=prepared;return Yes();
                } finally {executing=false;}
            }
        }
        public sealed class PreparedSupplyRemoval
        {
            readonly PickupRules owner;readonly CommandContext context;readonly Item item;
            readonly Dictionary<string,Dictionary<string,long>> commands;bool committed,aborted;
            public SupplyRemovalReceipt Receipt {get;private set;}
            internal PreparedSupplyRemoval(PickupRules owner,CommandContext context,SupplyRemovalReceipt receipt,Dictionary<string,Dictionary<string,long>> commands)
            {this.owner=owner;this.context=context;this.item=owner.items[receipt.Box];Receipt=receipt;this.commands=commands;}
            public bool Validate(long hostTick)
            {
                lock(owner.sync){
                    if(committed||aborted||owner.executing||owner.quarantined||!Object.ReferenceEquals(owner.pendingSupply,this)||owner.revision!=Receipt.Revision||hostTick!=owner.now||hostTick<Receipt.Tick||hostTick>Receipt.Expiry||item.Removed||item.Owner!=null||item.Reserved!=Receipt.Actor||item.Until<=hostTick)return false;
                    owner.executing=true;
                    try {PickupSupplyLife life;ActorFrame f;return owner.gate.Validate(context).Accepted&&owner.supplyLifetime.TryRead(Receipt.Actor,out life)&&Receipt.Lifetime.Same(life)&&owner.actors.TryRead(Receipt.Actor,out f)&&Valid(f)&&f.Allowed&&f.HandEligible&&f.TransferSafe&&owner.Reach(Receipt.Actor,f,item);}
                    finally {owner.executing=false;}
                }
            }
            // Only after validate-all, on the shared host lane. Assignment-only:
            // no ports, allocation, parsing, IO or authorization in this phase.
            public void CommitValidated()
            {
                if(committed||aborted)return;
                item.Removed=true;item.Reserved=null;item.Until=0;
                owner.seen=commands;owner.replayCount++;owner.revision=Receipt.Revision+1;
                owner.pendingSupply=null;committed=true;
            }
            public void Abort(){if(committed||aborted)return;aborted=true;if(Object.ReferenceEquals(owner.pendingSupply,this))owner.pendingSupply=null;}
        }
    }
}
