using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.InventoryItems
{
    public sealed partial class Inventory
    {
        // Trusted composition only: the caller reserves actual mechanics and geometry too.
        public bool PrepareWeaponHandoff(CommandContext c,string[] outgoing,ItemInstance[] incoming,out PreparedWeaponHandoff token)
        {
            token=null;
            if(outgoing==null||incoming==null||outgoing.Length+incoming.Length==0||preparedBusy||executing||retired||revision==long.MaxValue||!c.IsAuthority||c.SessionId!=session||c.ActorId!=actor||c.AuthorityEpoch!=epoch||String.IsNullOrEmpty(c.CommandId)||c.Tick<0||c.Tick<lastTick||c.Tick<=retiredTick||commands.Contains(c.CommandId))return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);var removed=new List<ItemInstance>();
            foreach(string id in outgoing){var item=Find(id);if(item==null||!catalog.Get(item.DefinitionId).IsWeapon||!ids.Add(id))return false;removed.Add(item);}
            foreach(var item in removed)foreach(var member in Group(item))if(!ids.Contains(member.InstanceId))return false;
            var afterItems=new List<ItemInstance>(items);var afterGroups=new List<List<ItemInstance>>();
            foreach(var g in groups){var copy=new List<ItemInstance>();foreach(var i in g)if(!ids.Contains(i.InstanceId))copy.Add(i);if(copy.Count>0)afterGroups.Add(copy);}
            afterItems.RemoveAll(delegate(ItemInstance i){return ids.Contains(i.InstanceId);});
            var afterKnown=new Dictionary<string,ItemInstance>(known,StringComparer.Ordinal);
            var admitted=new HashSet<string>(StringComparer.Ordinal);
            foreach(var i in incoming){ItemDefinition d;ItemInstance prior;
                if(i==null||String.IsNullOrEmpty(i.InstanceId)||ids.Contains(i.InstanceId)||Find(i.InstanceId)!=null||!admitted.Add(i.InstanceId)||!catalog.TryGet(i.DefinitionId,out d)||!d.Active||!d.IsWeapon||i.Stars<1||i.Stars>5||(!d.Tiered&&i.Stars!=1))return false;
                if(known.TryGetValue(i.InstanceId,out prior)&&(prior.DefinitionId!=i.DefinitionId||prior.Stars!=i.Stars))return false;
                List<ItemInstance> destination=null;if(d.StackKey.Length>0)foreach(var g in afterGroups)if(g.Count<2&&catalog.Get(g[0].DefinitionId).StackKey==d.StackKey){destination=g;break;}
                if(destination==null){if(afterGroups.Count>=Capacity)return false;destination=new List<ItemInstance>();afterGroups.Add(destination);}destination.Add(i);afterItems.Add(i);afterKnown[i.InstanceId]=i;
            }
            preparedBusy=true;
            try {
                if(removed.Count>0&&!boundary.Validate(c,InventoryOperation.Remove,null,removed.ToArray()).Accepted)return false;
                foreach(var i in incoming)if(!boundary.Validate(c,InventoryOperation.Admit,i,new ItemInstance[0]).Accepted)return false;
                token=new PreparedWeaponHandoff(this,c,afterItems,afterGroups,afterKnown,removed.ToArray(),(ItemInstance[])incoming.Clone());return true;
            }finally{if(token==null)preparedBusy=false;}
        }
        public sealed class PreparedWeaponHandoff
        {
            readonly Inventory owner;readonly CommandContext context;readonly long version,retired;
            readonly List<ItemInstance> items;readonly List<List<ItemInstance>> groups;readonly Dictionary<string,ItemInstance> known;
            readonly HashSet<string> commands;readonly Queue<KeyValuePair<string,long>> history;readonly string selected;
            readonly ItemInstance[] removed,admitted;
            bool validated,done,aborted;
            internal PreparedWeaponHandoff(Inventory o,CommandContext c,List<ItemInstance> i,List<List<ItemInstance>> g,Dictionary<string,ItemInstance> k,ItemInstance[] removed,ItemInstance[] admitted)
            {owner=o;context=c;version=o.revision;items=i;groups=g;known=k;selected=i.Exists(delegate(ItemInstance m){return m.InstanceId==o.SelectedInstanceId;})?o.SelectedInstanceId:(i.Count==0?"":i[0].InstanceId);
                this.removed=removed;this.admitted=admitted;
                commands=new HashSet<string>(o.commands,StringComparer.Ordinal);commands.Add(c.CommandId);history=new Queue<KeyValuePair<string,long>>(o.history);history.Enqueue(new KeyValuePair<string,long>(c.CommandId,c.Tick));retired=o.retiredTick;
                if(history.Count>256){var old=history.Dequeue();commands.Remove(old.Key);retired=Math.Max(retired,old.Value);}}
            public bool Validate(){
                validated=false;
                if(done||aborted||!owner.preparedBusy||owner.executing||owner.retired||owner.revision!=version)return false;
                // Recheck the exact prepared request while every participant is still
                // reversible. The trusted boundary is pure/stable under the host lane.
                if(removed.Length>0&&!owner.boundary.Validate(context,InventoryOperation.Remove,null,(ItemInstance[])removed.Clone()).Accepted)return false;
                foreach(var item in admitted)if(!owner.boundary.Validate(context,InventoryOperation.Admit,item,new ItemInstance[0]).Accepted)return false;
                validated=!done&&!aborted&&owner.preparedBusy&&!owner.executing&&!owner.retired&&owner.revision==version;return validated;
            }
            public void CommitValidated(){if(done)return;if(!validated||aborted||owner.revision!=version)return;owner.items=items;owner.groups=groups;owner.known=known;owner.commands=commands;owner.history=history;owner.retiredTick=retired;owner.SelectedInstanceId=selected;owner.Holstered=true;owner.lastTick=context.Tick;owner.revision++;done=true;owner.preparedBusy=false;}
            public void Abort(){if(done||aborted)return;aborted=true;validated=false;owner.preparedBusy=false;}
        }
    }
}
