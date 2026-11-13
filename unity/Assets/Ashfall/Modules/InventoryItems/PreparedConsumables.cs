using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ashfall.Core;
namespace Ashfall.Modules.InventoryItems
{
    // Ephemeral reservations are not durable Inventory state. The owner clock must
    // advance on the trusted host lane, including while input is paused.
    internal sealed class ConsumableReservation
    {
        public ConsumableReservation() {}
        internal long Tick=-1, Expires; internal object Active;
        internal Ashfall.Modules.WeaponHandling.WeaponLifetimes.SourceDispositionReservation SourceDisposition;
        internal static readonly ConditionalWeakTable<Inventory,ConsumableReservation> States=new ConditionalWeakTable<Inventory,ConsumableReservation>();
    }
    public sealed partial class Inventory
    {
        public bool AdvanceConsumableReservations(CommandContext host)
        {
            var state=ConsumableReservation.States.GetOrCreateValue(this);
            if(executing||!host.IsAuthority||host.SessionId!=session||host.ActorId!=actor||host.AuthorityEpoch!=epoch||host.Tick<0||host.Tick<state.Tick)return false;
            state.Tick=host.Tick;
            if(state.Active!=null&&state.Tick>=state.Expires){if(state.SourceDisposition!=null)state.SourceDisposition.Abort();state.SourceDisposition=null;state.Active=null;preparedBusy=false;}
            return true;
        }
        private object ReserveConsumable(long tick)
        {
            var state=ConsumableReservation.States.GetOrCreateValue(this);
            if(tick<state.Tick||tick>long.MaxValue-120)return null;
            state.Tick=tick;state.Expires=tick+120;state.SourceDisposition=null;state.Active=new object();return state.Active;
        }
        private bool LiveConsumable(object lease)
        {var s=ConsumableReservation.States.GetOrCreateValue(this);return lease!=null&&Object.ReferenceEquals(s.Active,lease)&&s.Tick<s.Expires;}
        private void ReleaseConsumable(object lease)
        {var s=ConsumableReservation.States.GetOrCreateValue(this);if(Object.ReferenceEquals(s.Active,lease)){s.Active=null;s.SourceDisposition=null;preparedBusy=false;}}
        // Trusted host safety cancellation, reserved before PickupRules reads actor state.
        // It never grants an action, spends a command, or calls gameplay eligibility.
        public bool PrepareSafetyHolster(string expectedHeld,out PreparedSafetyHolster token)
        {
            token=null;
            if(preparedBusy||executing||retired||revision==long.MaxValue||String.IsNullOrEmpty(expectedHeld)||SelectedInstanceId!=expectedHeld||Holstered)return false;
            var item=Find(expectedHeld);if(item==null||catalog.Get(item.DefinitionId).IsWeapon)return false;
            token=new PreparedSafetyHolster(this,revision,expectedHeld);preparedBusy=true;return true;
        }
        public sealed class PreparedSafetyHolster
        {
            private readonly Inventory owner;private readonly long revision;private readonly string held;private readonly object lease;private bool done,aborted;
            internal PreparedSafetyHolster(Inventory o,long r,string id){owner=o;revision=r;held=id;lease=o.ReserveConsumable(Math.Max(o.lastTick,ConsumableReservation.States.GetOrCreateValue(o).Tick)<0?0:Math.Max(o.lastTick,ConsumableReservation.States.GetOrCreateValue(o).Tick));}
            public bool Validate(){return !done&&!aborted&&owner.preparedBusy&&!owner.retired&&owner.revision==revision&&owner.SelectedInstanceId==held&&!owner.Holstered&&owner.LiveConsumable(lease);}
            // Only the reserving host lane calls this after validate-all. No callbacks,
            // allocation, eligibility or other fallible operation after PickupRules stows.
            public void CommitValidated(){if(done)return;if(!Validate())throw new InvalidOperationException("Invalid safety reservation");owner.Holstered=true;owner.revision++;owner.ReleaseConsumable(lease);done=true;}
            public void Abort(){if(done||aborted)return;aborted=true;owner.ReleaseConsumable(lease);}
        }
        // Inventory projection only: mixed callers must reserve real weapon suspension
        // on the same host lane. Weapon eviction is deliberately unsupported.
        private bool ConsumablePreparationAllowed(CommandContext c)
        {
            if(preparedBusy||executing||retired||!c.IsAuthority||c.SessionId!=session||c.ActorId!=actor||c.AuthorityEpoch!=epoch||String.IsNullOrEmpty(c.CommandId)||c.Tick<0||c.Tick<lastTick||c.Tick<=retiredTick||commands.Contains(c.CommandId))return false;
            if(c.Tick>long.MaxValue-120||c.Tick<ConsumableReservation.States.GetOrCreateValue(this).Tick||revision==long.MaxValue)return false;
            return true;
        }
        public bool PrepareConsumableSelection(CommandContext c,string selected,out PreparedConsumables token)
        { return PrepareConsumables(c,null,selected,false,out token); }
        public bool PrepareConsumableAdmission(CommandContext c,ItemInstance incoming,out PreparedConsumables token)
        { return PrepareConsumableAdmission(c,incoming,false,out token); }
        public bool PrepareConsumableAdmission(CommandContext c,ItemInstance incoming,bool replaceSelected,out PreparedConsumables token)
        { if(incoming==null){token=null;return false;}return PrepareConsumables(c,incoming,null,replaceSelected,out token); }
        private bool PrepareConsumables(CommandContext c,ItemInstance incoming,string selected,bool replaceSelected,out PreparedConsumables token)
        {
            token=null;if(!ConsumablePreparationAllowed(c))return false;
            ItemInstance[] removed=new ItemInstance[0];ItemInstance target=Find(selected);
            if(incoming==null&&selected!=null&&(target==null||catalog.Get(target.DefinitionId).IsWeapon))return false;
            if(incoming!=null) {
                ItemDefinition d;ItemInstance prior;
                if(String.IsNullOrEmpty(incoming.InstanceId)||Find(incoming.InstanceId)!=null||!catalog.TryGet(incoming.DefinitionId,out d)||!d.Active||d.IsWeapon||(d.Id!="banana"&&d.Id!="shaker"&&d.Id!="soda")||incoming.Stars!=1)return false;
                if(!HasStackRoom(d)&&groups.Count>=Capacity) {var old=Find(SelectedInstanceId);if(!replaceSelected||old==null)return false;removed=Group(old).ToArray();foreach(var member in removed)if(catalog.Get(member.DefinitionId).IsWeapon)return false;}
                if(known.TryGetValue(incoming.InstanceId,out prior)&&(prior.DefinitionId!=incoming.DefinitionId||prior.Stars!=incoming.Stars))return false;
            }
            preparedBusy=true;
            try {
                var op=incoming!=null?(removed.Length>0?InventoryOperation.ReplaceSlot:InventoryOperation.Admit):(selected==null?InventoryOperation.Holster:InventoryOperation.Equip);
                if(!boundary.Validate(c,op,incoming,incoming!=null?removed:(target==null?new ItemInstance[0]:new ItemInstance[]{target})).Accepted)return false;
                token=new PreparedConsumables(this,c,incoming,selected,removed);return true;
            } finally {if(token==null)preparedBusy=false;}
        }
        public sealed class PreparedConsumables
        {
            private readonly Inventory owner;private readonly CommandContext context;private readonly long revision;private readonly object lease;
            private readonly List<ItemInstance> afterItems;private readonly List<List<ItemInstance>> afterGroups;
            private readonly Dictionary<string,ItemInstance> afterKnown;private readonly HashSet<string> afterCommands;
            private readonly Queue<KeyValuePair<string,long>> afterHistory;private readonly long afterRetired;
            private readonly string selected;private readonly bool holstered;private bool committed,aborted,validated;
            private readonly ItemInstance[] removed;private readonly ItemInstance incoming;private readonly InventoryOperation operation;private readonly ItemInstance[] boundaryMembers;
            public ItemInstance[] Removed {get{return (ItemInstance[])removed.Clone();}}
            internal PreparedConsumables(Inventory o,CommandContext c,ItemInstance incoming,string selection,ItemInstance[] evicted)
            {
                removed=(ItemInstance[])evicted.Clone();this.incoming=incoming;operation=incoming!=null?(removed.Length>0?InventoryOperation.ReplaceSlot:InventoryOperation.Admit):(selection==null?InventoryOperation.Holster:InventoryOperation.Equip);
                boundaryMembers=incoming!=null?removed:(selection==null?new ItemInstance[0]:new ItemInstance[]{o.Find(selection)});owner=o;context=c;revision=o.revision;lease=o.ReserveConsumable(c.Tick);selected=selection??o.SelectedInstanceId;holstered=selection==null;
                afterItems=new List<ItemInstance>(o.items);afterGroups=new List<List<ItemInstance>>();foreach(var group in o.groups)afterGroups.Add(new List<ItemInstance>(group));
                foreach(var old in removed){afterItems.Remove(old);foreach(var group in afterGroups)group.Remove(old);}afterGroups.RemoveAll(delegate(List<ItemInstance> g){return g.Count==0;});
                afterKnown=new Dictionary<string,ItemInstance>(o.known,StringComparer.Ordinal);
                if(incoming!=null) {
                    selected=incoming.InstanceId;afterItems.Add(incoming);afterKnown[incoming.InstanceId]=incoming;List<ItemInstance> destination=null;
                    var d=o.catalog.Get(incoming.DefinitionId);
                    if(d.StackKey.Length>0)foreach(var group in afterGroups)if(group.Count<2&&o.catalog.Get(group[0].DefinitionId).StackKey==d.StackKey){destination=group;break;}
                    if(destination==null){destination=new List<ItemInstance>();afterGroups.Add(destination);}destination.Add(incoming);
                    if(selected=="")selected=incoming.InstanceId;
                }
                afterCommands=new HashSet<string>(o.commands,StringComparer.Ordinal);afterCommands.Add(c.CommandId);
                afterHistory=new Queue<KeyValuePair<string,long>>(o.history);afterHistory.Enqueue(new KeyValuePair<string,long>(c.CommandId,c.Tick));afterRetired=o.retiredTick;
                if(afterHistory.Count>256){var old=afterHistory.Dequeue();afterCommands.Remove(old.Key);afterRetired=Math.Max(afterRetired,old.Value);}
            }
            public bool Validate(){return validated=!committed&&!aborted&&owner.preparedBusy&&!owner.retired&&owner.revision==revision&&owner.LiveConsumable(lease)&&owner.boundary.Validate(context,operation,incoming,(ItemInstance[])boundaryMembers.Clone()).Accepted;}
            // Immediately after validate-all under the shared host lane; reference swaps only.
            public void CommitValidated()
            {
                if(committed)return;if(!validated||aborted||!owner.LiveConsumable(lease)||owner.revision!=revision)throw new InvalidOperationException("Invalid consumable projection");
                owner.items=afterItems;owner.groups=afterGroups;owner.known=afterKnown;owner.commands=afterCommands;owner.history=afterHistory;owner.retiredTick=afterRetired;
                owner.SelectedInstanceId=selected;owner.Holstered=holstered;owner.lastTick=context.Tick;owner.revision++;committed=true;owner.ReleaseConsumable(lease);
            }
            public void Abort(){if(committed||aborted)return;aborted=true;owner.ReleaseConsumable(lease);}
        }
    }
}
