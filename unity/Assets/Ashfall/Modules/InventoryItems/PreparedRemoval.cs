using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.InventoryItems
{
    public sealed partial class Inventory
    {
        private long revision;
        private bool preparedBusy;
        public long Revision { get { return revision; } }
        public bool PrepareRemoval(CommandContext c,string id,out PreparedRemoval token)
        {return PrepareRemoval(c,id,false,out token);}
        public bool PrepareDropRemoval(CommandContext c,string id,out PreparedRemoval token)
        {return PrepareRemoval(c,id,true,out token);}
        private bool PrepareRemoval(CommandContext c,string id,bool drop,out PreparedRemoval token)
        {
            token=null;if(!ConsumablePreparationAllowed(c))return false;ItemInstance item=Find(id);
            if(preparedBusy||executing||retired||item==null||catalog.Get(item.DefinitionId).IsWeapon||!c.IsAuthority||c.SessionId!=session||c.ActorId!=actor||c.AuthorityEpoch!=epoch||String.IsNullOrEmpty(c.CommandId)||c.Tick<lastTick||c.Tick<=retiredTick||commands.Contains(c.CommandId))return false;
            if(!boundary.Validate(c,InventoryOperation.Remove,null,new ItemInstance[]{item}).Accepted)return false;
            var members=Group(item).ToArray();string next=null;foreach(var member in members)if(member.InstanceId!=id){next=member.InstanceId;break;}
            token=new PreparedRemoval(this,c,item,members,next,revision,drop);preparedBusy=true;return true;
        }
        public sealed class PreparedRemoval
        {
            private readonly Inventory owner;private readonly CommandContext context;private readonly ItemInstance item;
            private readonly object lease;private readonly long revision;private readonly bool holstered;private bool done,aborted,validated;
            private readonly string[] members;
            private readonly List<ItemInstance> afterItems;
            private readonly List<List<ItemInstance>> afterGroups;
            private readonly HashSet<string> afterCommands;
            private readonly Queue<KeyValuePair<string,long>> afterHistory;
            private readonly long afterRetired;
            public readonly string Selected;
            public string[] Members { get { return (string[])members.Clone(); } }
            internal PreparedRemoval(Inventory o,CommandContext c,ItemInstance i,ItemInstance[] group,string next,long rev,bool drop)
            {owner=o;context=c;item=i;revision=rev;lease=o.ReserveConsumable(c.Tick);Selected=drop&&o.SelectedInstanceId!=i.InstanceId?o.SelectedInstanceId:next;holstered=drop?(o.SelectedInstanceId==i.InstanceId||o.Holstered):Selected==null;members=Array.ConvertAll(group,delegate(ItemInstance m){return m.InstanceId;});
                afterItems=new List<ItemInstance>(o.items);afterItems.Remove(i);afterGroups=new List<List<ItemInstance>>();
                foreach(var original in o.groups){var copy=new List<ItemInstance>(original);copy.Remove(i);if(copy.Count>0)afterGroups.Add(copy);}
                afterCommands=new HashSet<string>(o.commands,StringComparer.Ordinal);afterCommands.Add(c.CommandId);
                afterHistory=new Queue<KeyValuePair<string,long>>(o.history);afterHistory.Enqueue(new KeyValuePair<string,long>(c.CommandId,c.Tick));afterRetired=o.retiredTick;
                if(afterHistory.Count>256){var old=afterHistory.Dequeue();afterCommands.Remove(old.Key);afterRetired=Math.Max(afterRetired,old.Value);}
            }
            public bool Validate() {return validated=!done&&!aborted&&owner.preparedBusy&&!owner.retired&&owner.revision==revision&&owner.Find(item.InstanceId)==item&&owner.LiveConsumable(lease)&&owner.boundary.Validate(context,InventoryOperation.Remove,null,new ItemInstance[]{item}).Accepted;}
            public void CommitValidated()
            {
                if(done)return;if(!validated||aborted||!owner.LiveConsumable(lease)||owner.revision!=revision)throw new InvalidOperationException("Invalid removal");
                owner.items=afterItems;owner.groups=afterGroups;owner.commands=afterCommands;owner.history=afterHistory;owner.retiredTick=afterRetired;
                owner.SelectedInstanceId=Selected??"";owner.Holstered=holstered;owner.revision++;owner.lastTick=context.Tick;
                done=true;owner.ReleaseConsumable(lease);
            }
            public void Abort(){if(done||aborted)return;aborted=true;owner.ReleaseConsumable(lease);}
        }
    }
}


