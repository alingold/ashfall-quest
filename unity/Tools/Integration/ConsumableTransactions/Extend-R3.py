from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall')
p=R/'unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableBridge.cs';s=p.read_text().replace('using Ashfall.Modules.Player;', 'using Ashfall.Modules.Player;\nusing Ashfall.Modules.WeaponHandling;\nusing Ashfall.Integration.InventoryTransfers;')
s=s.replace('private PickupRules pickups;', '''private sealed class AmmoBinding {internal FiniteAmmoReserve Reserve;internal int Accepted;}
        private readonly Dictionary<string,Dictionary<string,AmmoBinding>> ammo=new Dictionary<string,Dictionary<string,AmmoBinding>>(StringComparer.Ordinal);
        private IDropPlanner dropPlanner;
        public void BindDrops(IDropPlanner planner){Lane.Serialize(delegate{if(active||dropPlanner!=null||planner==null)throw new InvalidOperationException("drop binding");dropPlanner=planner;return true;});}
        // Trusted host maps the actual canonical family reserve, never a newly minted pool.
        // All reserve users must share Lane; lifecycle/weapon registry binding is external.
        public bool BindAmmoReserve(string actor,string family,FiniteAmmoReserve reserve)
        {return Lane.Serialize(delegate{if(active||reserve==null||!inventories.ContainsKey(actor)||family=="builds"||PickupRules.SupplyAmount(family)==0)return false;Dictionary<string,AmmoBinding> map;if(!ammo.TryGetValue(actor,out map)){map=new Dictionary<string,AmmoBinding>(StringComparer.Ordinal);ammo.Add(actor,map);}if(map.ContainsKey(family))return false;foreach(var existing in ammo.Values)foreach(var binding in existing.Values)if(Object.ReferenceEquals(binding.Reserve,reserve))return false;map.Add(family,new AmmoBinding{Reserve=reserve});return true;});}
        public int LastAcceptedSupply(string actor,string family)
        {return Lane.Serialize(delegate{Dictionary<string,AmmoBinding> map;AmmoBinding binding;return ammo.TryGetValue(actor,out map)&&map.TryGetValue(family,out binding)?binding.Accepted:0;});}
        private PickupRules pickups;''')
s=s.replace('// Replacement needs prepared world placement AND weapon lifetime APIs. No eviction here.', '// Freeze source slot order; only consumables may be evicted by this owner.')
s=s.replace('inventory.PrepareConsumableAdmission(current,new ItemInstance(request.ItemId,request.Kind,1),out admission)', 'inventory.PrepareConsumableAdmission(current,new ItemInstance(request.ItemId,request.Kind,1),dropPlanner!=null,out admission)')
s=s.replace('try {stage=new Store(request,held,admission,members.ToArray());return true;}catch{admission.Abort();throw;}', '''IDropBatch batch=null;
            try {
                var removed=admission.Removed;
                if(removed.Length>0) {
                    if(!dropPlanner.Prepare(current,removed,out batch)||batch==null)return false;
                    var placements=batch.Placements;if(placements==null||placements.Length!=removed.Length)return false;
                    var ids=new HashSet<string>(StringComparer.Ordinal);
                    foreach(var placement in placements)if(placement==null||!placement.Position.IsFinite||!ids.Add(placement.ItemId)||!Array.Exists(removed,delegate(ItemInstance m){return m.InstanceId==placement.ItemId&&m.DefinitionId==placement.Kind;}))return false;
                }
                stage=new Store(request,held,admission,members.ToArray(),batch);batch=null;return true;
            } finally {if(stage==null)admission.Abort();if(batch!=null)batch.Abort();}''')
s=s.replace('private readonly Inventory.PreparedConsumables admission;private bool done,aborted;', 'private readonly Inventory.PreparedConsumables admission;private readonly IDropBatch batch;private bool done,aborted;')
s=s.replace('internal Store(Transfer t,string held,Inventory.PreparedConsumables prepared,string[] members){admission=prepared;Receipt=new StoreReceipt(t,new EvictedInstance[0],new StackSelection(held,null,members));}', 'internal Store(Transfer t,string held,Inventory.PreparedConsumables prepared,string[] members,IDropBatch drops){admission=prepared;batch=drops;Receipt=new StoreReceipt(t,drops==null?new EvictedInstance[0]:drops.Placements,new StackSelection(held,null,members));}')
s=s.replace('if(aborted||!admission.Validate())return false;admission.CommitValidated();', 'if(aborted||!admission.Validate()||(batch!=null&&!batch.StillValid))return false;if(batch!=null&&!batch.Commit())return false;admission.CommitValidated();')
s=s.replace('aborted=true;admission.Abort();', 'aborted=true;admission.Abort();if(batch!=null)batch.Abort();')
s=s.replace('if(request.Operation==TransferKind.UpgradeBackpack)', '''if(request.Operation==TransferKind.Supply) {
                Dictionary<string,AmmoBinding> map;AmmoBinding binding;
                if(request.Amount<=0||!ammo.TryGetValue(request.ActorId,out map)||!map.TryGetValue(request.Kind,out binding))return AtomicOutcome.Failed;
                // Refund is the real bounded reserve's single atomic mutation. Pickups
                // has already validated source ownership/range/replay and removes the
                // whole box with no fallible operation after this return.
                int accepted=binding.Reserve.Refund(request.Amount);binding.Accepted=accepted;
                return accepted>0?AtomicOutcome.Applied:AtomicOutcome.CappedNoOp;
            }
            if(request.Operation==TransferKind.UpgradeBackpack)''')
p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/InventoryItems/PreparedConsumables.cs';s=p.read_text().replace('if(done)return;owner.Holstered=true;', 'if(done)return;if(!Validate())throw new InvalidOperationException("Invalid safety reservation");owner.Holstered=true;')
s=s.replace('return PrepareConsumables(c,null,selected,out token);', 'return PrepareConsumables(c,null,selected,false,out token);')
s=s.replace('{ if(incoming==null){token=null;return false;}return PrepareConsumables(c,incoming,null,out token); }', '''{ return PrepareConsumableAdmission(c,incoming,false,out token); }
        public bool PrepareConsumableAdmission(CommandContext c,ItemInstance incoming,bool replaceSelected,out PreparedConsumables token)
        { if(incoming==null){token=null;return false;}return PrepareConsumables(c,incoming,null,replaceSelected,out token); }''')
s=s.replace('string selected,out PreparedConsumables token)\n        {\n            token=null;', 'string selected,bool replaceSelected,out PreparedConsumables token)\n        {\n            token=null;')
s=s.replace('ItemInstance target=Find(selected);', 'ItemInstance[] removed=new ItemInstance[0];ItemInstance target=Find(selected);')
s=s.replace('||(!HasStackRoom(d)&&groups.Count>=Capacity)', '')
s=s.replace('if(known.TryGetValue(incoming.InstanceId,out prior)', '''if(!HasStackRoom(d)&&groups.Count>=Capacity) {var old=Find(SelectedInstanceId);if(!replaceSelected||old==null)return false;removed=Group(old).ToArray();}
                if(known.TryGetValue(incoming.InstanceId,out prior)''')
s=s.replace('var op=incoming!=null?InventoryOperation.Admit:', 'var op=incoming!=null?(removed.Length>0?InventoryOperation.ReplaceSlot:InventoryOperation.Admit):')
s=s.replace('target==null?new ItemInstance[0]:new ItemInstance[]{target}', 'incoming!=null?removed:(target==null?new ItemInstance[0]:new ItemInstance[]{target})')
s=s.replace('new PreparedConsumables(this,c,incoming,selected)', 'new PreparedConsumables(this,c,incoming,selected,removed)')
s=s.replace('internal PreparedConsumables(Inventory o,CommandContext c,ItemInstance incoming,string selection)', '''private readonly ItemInstance[] removed;
            public ItemInstance[] Removed {get{return (ItemInstance[])removed.Clone();}}
            internal PreparedConsumables(Inventory o,CommandContext c,ItemInstance incoming,string selection,ItemInstance[] evicted)''')
s=s.replace('owner=o;context=c;revision=o.revision;selected=', 'removed=(ItemInstance[])evicted.Clone();owner=o;context=c;revision=o.revision;selected=')
s=s.replace('afterKnown=new Dictionary', '''foreach(var old in removed){afterItems.Remove(old);foreach(var group in afterGroups)group.Remove(old);}afterGroups.RemoveAll(delegate(List<ItemInstance> g){return g.Count==0;});
                afterKnown=new Dictionary''')
p.write_text(s)
print('Consumable replacement uses real prepared Inventory plus existing IDropPlanner; supply uses actual FiniteAmmoReserve')
