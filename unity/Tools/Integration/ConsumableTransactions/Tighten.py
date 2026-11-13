from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall')
p=R/'unity/Assets/Ashfall/Modules/HealthStates/HealthService.cs';s=p.read_text();s=s.replace('new Queue<HealthTransition>()','new Queue<HealthTransition>(1024)').replace('new Dictionary<string,long>(StringComparer.Ordinal)','new Dictionary<string,long>(4096,StringComparer.Ordinal)');p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/HealthStates/PreparedGains.cs';s=p.read_text().replace('HashSet<string> gainCommands=new HashSet<string>(StringComparer.Ordinal)','Dictionary<string,bool> gainCommands=new Dictionary<string,bool>(4096,StringComparer.Ordinal)').replace('gainCommands.Contains(','gainCommands.ContainsKey(').replace('new Dictionary<string,GainReceipt>(StringComparer.Ordinal)','new Dictionary<string,GainReceipt>(4096,StringComparer.Ordinal)').replace('owner.gainCommands.Add(Key(context.ActorId,context.CommandId));','owner.gainCommands.Add(Key(context.ActorId,context.CommandId),true);')
s=s.replace('private bool aborted;private GainReceipt receipt;','private readonly long sequence;private readonly HealthTransition publication;private bool aborted;private GainReceipt receipt;')
s=s.replace('key=Key(c.ActorId,logical);receipt=', 'key=Key(c.ActorId,logical);sequence=service.eventSequence;receipt=')
s=s.replace('health,shield); }','health,shield);if(receipt.Outcome==GainOutcome.Applied){var after=new HealthSnapshot(player.Id,player.Team,player.Deaths,revision+1,player.Life,player.Health+health,player.Shield+shield,player.DownedUntil,player.RespawnAt,player.ProtectionUntil,player.ReadyUntil,player.Connected,player.Charge,life,connection);publication=new HealthTransition(service.session,service.epoch,sequence+1,"gain",revision,after);} }')
s=s.replace('return !aborted&&!committed&&','return !aborted&&!committed&&owner.eventSequence==sequence&&')
s=s.replace('owner.Publish(player,revision,"gain");','owner.eventSequence++;owner.transitions.Enqueue(publication);')
p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/InventoryItems/Inventory.cs';s=p.read_text();
for name in ['List<ItemInstance> items','List<List<ItemInstance>> groups','HashSet<string> commands','Queue<KeyValuePair<string,long>> history']:s=s.replace('private readonly '+name,'private '+name)
p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/InventoryItems/PreparedRemoval.cs';s=p.read_text().replace('using System;','using System;\nusing System.Collections.Generic;')
s=s.replace('private readonly string[] members;', '''private readonly string[] members;
            private readonly List<ItemInstance> afterItems;
            private readonly List<List<ItemInstance>> afterGroups;
            private readonly HashSet<string> afterCommands;
            private readonly Queue<KeyValuePair<string,long>> afterHistory;
            private readonly long afterRetired;''')
s=s.replace('members=Array.ConvertAll(group,delegate(ItemInstance m){return m.InstanceId;});}', '''members=Array.ConvertAll(group,delegate(ItemInstance m){return m.InstanceId;});
                afterItems=new List<ItemInstance>(o.items);afterItems.Remove(i);afterGroups=new List<List<ItemInstance>>();
                foreach(var original in o.groups){var copy=new List<ItemInstance>(original);copy.Remove(i);if(copy.Count>0)afterGroups.Add(copy);}
                afterCommands=new HashSet<string>(o.commands,StringComparer.Ordinal);afterCommands.Add(c.CommandId);
                afterHistory=new Queue<KeyValuePair<string,long>>(o.history);afterHistory.Enqueue(new KeyValuePair<string,long>(c.CommandId,c.Tick));afterRetired=o.retiredTick;
                if(afterHistory.Count>256){var old=afterHistory.Dequeue();afterCommands.Remove(old.Key);afterRetired=Math.Max(afterRetired,old.Value);}
            }''')
start=s.index('                owner.Remove(item);');end=s.index('                done=true;',start)
s=s[:start]+'''                owner.items=afterItems;owner.groups=afterGroups;owner.commands=afterCommands;owner.history=afterHistory;owner.retiredTick=afterRetired;
                owner.SelectedInstanceId=Selected??"";owner.Holstered=Selected==null;owner.revision++;owner.lastTick=context.Tick;
'''+s[end:];p.write_text(s)
