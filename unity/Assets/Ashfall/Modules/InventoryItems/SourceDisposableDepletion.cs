using Ashfall.Core;
using System;
using System.Collections.Generic;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Modules.InventoryItems
{
    public sealed partial class Inventory
    {
        // Candidate dependency InventoryItems -> WeaponHandling; no project graph adoption.
        public RuleResult PrepareSourceDisposableDepletion(WeaponLifetimes lifetimes,
            IPreparedSourceLaunch launch,long successorEquip,IWeaponFireBinding successorBinding,
            out PreparedSourceDisposableCommit prepared)
        {
            prepared=null;if(lifetimes==null||launch==null||launch.Descriptor==null)return RuleResult.Reject("missing launch owner");
            var d=launch.Descriptor;var c=d.Command;var member=Find(d.InstanceId);
            if(!ConsumablePreparationAllowed(c)||member==null||Holstered||SelectedInstanceId!=member.InstanceId||
                catalog.Get(member.DefinitionId).ProfileId!=d.Before.Profile)return RuleResult.Reject("not equipped source member");
            var afterItems=new List<ItemInstance>(items);afterItems.Remove(member);
            var afterGroups=new List<List<ItemInstance>>();string selected="";
            foreach(var g in groups){var copy=new List<ItemInstance>(g);if(copy.Remove(member)&&copy.Count>0)selected=copy[0].InstanceId;if(copy.Count>0)afterGroups.Add(copy);}
            if(selected==""&&afterItems.Count>0)selected=afterItems[0].InstanceId;
            // Consumable successors need no weapon reservation; unsupported until a
            // composed consumable equip owner exists. Do not invent a handler.
            if(selected!=""&&!catalog.Get(Find(selected).DefinitionId).IsWeapon)return RuleResult.Reject("consumable successor owner unsupported");
            WeaponLifetimes.SourceDispositionReservation mechanical;
            var result=lifetimes.PrepareSourceDisposableSelection(launch,selected,successorEquip,successorBinding,out mechanical);
            if(!result.Accepted)return result;
            preparedBusy=true;
            try{
                if(!boundary.Validate(c,InventoryOperation.Remove,null,new[]{member}).Accepted)return RuleResult.Reject("inventory eligibility");
                prepared=new PreparedSourceDisposableCommit(this,c,member,afterItems,afterGroups,selected,mechanical);return RuleResult.Accept();
            }finally{if(prepared==null){preparedBusy=false;mechanical.Abort();}}
        }
        public sealed class PreparedSourceDisposableCommit
        {
            readonly Inventory owner;readonly CommandContext context;readonly ItemInstance removed;
            readonly List<ItemInstance> items;readonly List<List<ItemInstance>> groups;
            readonly HashSet<string> commands;readonly Queue<KeyValuePair<string,long>> history;
            readonly string selected;readonly long revision,retired;readonly object lease;
            readonly WeaponLifetimes.SourceDispositionReservation mechanical;bool validated,done,aborted;
            internal PreparedSourceDisposableCommit(Inventory o,CommandContext c,ItemInstance removed,List<ItemInstance> items,List<List<ItemInstance>> groups,string selected,WeaponLifetimes.SourceDispositionReservation mechanical)
            {
                owner=o;context=c;this.removed=removed;this.items=items;this.groups=groups;this.selected=selected;this.mechanical=mechanical;revision=o.revision;
                lease=o.ReserveConsumable(c.Tick);var state=ConsumableReservation.States.GetOrCreateValue(o);state.Expires=mechanical.Launch.ExpiresTick+1;state.SourceDisposition=mechanical;
                commands=new HashSet<string>(o.commands,StringComparer.Ordinal);commands.Add(c.CommandId);
                history=new Queue<KeyValuePair<string,long>>(o.history);history.Enqueue(new KeyValuePair<string,long>(c.CommandId,c.Tick));retired=o.retiredTick;
                if(history.Count>256){var old=history.Dequeue();commands.Remove(old.Key);retired=Math.Max(retired,old.Value);}
            }
            bool Current(){return !done&&!aborted&&owner.preparedBusy&&!owner.retired&&!owner.executing&&owner.revision==revision&&owner.LiveConsumable(lease);}
            public bool Validate(long now)
            {
                validated=false;
                if(now>mechanical.Launch.ExpiresTick){Abort();return false;}
                if(now<context.Tick||!Current())return false;
                if(!owner.boundary.Validate(context,InventoryOperation.Remove,null,new[]{removed}).Accepted)return false;
                validated=mechanical.Validate(now).Accepted&&Current();return validated;
            }
            public bool Committed {get{return done;}}
            public void CommitValidated()
            {
                if(!validated||!Current()||!mechanical.ReadyToCommit)return;
                mechanical.CommitValidated();
                owner.items=items;owner.groups=groups;owner.commands=commands;owner.history=history;owner.retiredTick=retired;
                owner.SelectedInstanceId=selected;owner.Holstered=selected=="";owner.lastTick=context.Tick;owner.revision++;
                done=true;owner.ReleaseConsumable(lease);
            }
            public void Abort(){if(done||aborted)return;aborted=true;validated=false;mechanical.Abort();owner.ReleaseConsumable(lease);}
        }
        // Compatibility overload cannot attest mechanical depletion. Call the
        // concrete owner overload above with the actual prepared launch.
        public RuleResult PrepareSourceDisposableDepletion(CommandContext actor,
            string exactInstanceId, out PreparedSourceDisposableDepletion prepared)
        {
            prepared = null;
            return RuleResult.Reject("missing-owner-contract: WeaponLifetimes opaque exact source frag/zone exhausted-launch receipt and joint old/new selection reservation with life/connection/revision/expiry revocation");
        }

        // Intentionally unconstructible until the actual owning mechanics contract exists.
        // This is not a successful prepared operation and adds no checkpoint state.
        public sealed class PreparedSourceDisposableDepletion
        {
            private PreparedSourceDisposableDepletion() { }
            public bool Validate() { return false; }
            public void CommitValidated()
            { throw new System.InvalidOperationException("Source disposable owner composition unavailable"); }
            public void Abort() { }
        }
    }
}
