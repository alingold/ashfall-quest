using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.InventoryItems
{
    // Single simulation-thread aggregate. Instance identity never holds ammo or use state.
    public sealed partial class Inventory
    {
        public const int InitialCapacity=3, MaximumCapacity=6;
        private readonly ItemCatalog catalog;
        private readonly IInventoryBoundary boundary;
        private readonly string session, actor;
        private readonly long epoch;
        private Dictionary<string,ItemInstance> known=new Dictionary<string,ItemInstance>(StringComparer.Ordinal);
        private List<ItemInstance> items=new List<ItemInstance>();
        private List<List<ItemInstance>> groups=new List<List<ItemInstance>>();
        private HashSet<string> commands=new HashSet<string>(StringComparer.Ordinal);
        private Queue<KeyValuePair<string,long>> history=new Queue<KeyValuePair<string,long>>();
        private long lastTick=-1, retiredTick=-1;
        private bool executing, retired;
        public int Capacity { get; private set; }
        public string SelectedInstanceId { get; private set; }
        public bool Holstered { get; private set; }
        public int SlotCount { get { return groups.Count; } }
        public Inventory(ItemCatalog catalog,IInventoryBoundary boundary,string session,string actor,long epoch,bool starterLoadout)
        {
            if (catalog==null || boundary==null || String.IsNullOrEmpty(session) || String.IsNullOrEmpty(actor) || epoch<0) throw new ArgumentException("Invalid inventory binding");
            this.catalog=catalog; this.boundary=boundary; this.session=session; this.actor=actor; this.epoch=epoch; Capacity=3; SelectedInstanceId=""; Holstered=true;
            if(starterLoadout) foreach(int index in new int[]{0,2,9}) Add(new ItemInstance("weapon_"+index,catalog.Weapon(index).Id,1));
            RepairSelection(); if(starterLoadout) Holstered=false;
        }
        public ItemInstance[] Items { get { return items.ToArray(); } }
        public ItemInstance[][] Slots
        { get { var result=new List<ItemInstance[]>(); foreach(var group in OrderedGroups()) result.Add(group.ToArray()); return result.ToArray(); } }
        public RuleResult Apply(CommandContext context,InventoryOperation operation,ItemInstance incoming,string targetInstance)
        {
            if(preparedBusy) return RuleResult.Reject("prepared-busy");
            if(retired) return RuleResult.Reject("retired-inventory");
            if(executing) return RuleResult.Reject("reentrant-command");
            if(!context.IsAuthority || context.SessionId!=session || context.ActorId!=actor || context.AuthorityEpoch!=epoch || String.IsNullOrEmpty(context.CommandId) || context.Tick<0 || context.Tick<lastTick || context.Tick<=retiredTick || commands.Contains(context.CommandId)) return RuleResult.Reject("authority-or-replay");
            executing=true;
            try
            {
                if(incoming!=null && operation!=InventoryOperation.Admit && operation!=InventoryOperation.ReplaceSlot) return RuleResult.Reject("unexpected-incoming");
                ItemInstance target=Find(targetInstance); ItemInstance[] outgoing=new ItemInstance[0];
                if(operation==InventoryOperation.Admit || operation==InventoryOperation.ReplaceSlot)
                {
                    ItemDefinition d; ItemInstance prior;
                    if(incoming==null || String.IsNullOrEmpty(incoming.InstanceId) || Find(incoming.InstanceId)!=null || !catalog.TryGet(incoming.DefinitionId,out d) || !d.Active || (!d.IsWeapon && d.Id!="shaker" && d.Id!="soda" && d.Id!="banana") || incoming.Stars<1 || incoming.Stars>5 || (!d.Tiered && incoming.Stars!=1)) return RuleResult.Reject("invalid-instance");
                    if(known.TryGetValue(incoming.InstanceId,out prior) && (prior.DefinitionId!=incoming.DefinitionId || prior.Stars!=incoming.Stars)) return RuleResult.Reject("instance-identity-conflict");
                    bool room=HasStackRoom(d) || groups.Count<Capacity;
                    if(!room)
                    {
                        if(operation!=InventoryOperation.ReplaceSlot || target==null || target.InstanceId!=SelectedInstanceId) return RuleResult.Reject("capacity");
                        outgoing=Group(target).ToArray();
                    }
                }
                else if(operation==InventoryOperation.Remove || operation==InventoryOperation.Equip)
                { if(target==null) return RuleResult.Reject("not-owned"); outgoing=new ItemInstance[]{target}; }
                else if(operation==InventoryOperation.UpgradeCapacity)
                { if(Capacity>=MaximumCapacity) return RuleResult.Reject("capacity-max"); }
                else if(operation!=InventoryOperation.Holster) return RuleResult.Reject("invalid-operation");
                RuleResult allowed=boundary.Validate(context,operation,incoming,(ItemInstance[])outgoing.Clone());
                if(!allowed.Accepted) return allowed;
                var transactions=boundary as IInventoryTransactions;
                if(transactions==null) return RuleResult.Reject("staged-boundary-required");
                InventorySnapshot before=Capture();
                if(operation==InventoryOperation.Admit || operation==InventoryOperation.ReplaceSlot)
                { foreach(var old in outgoing) Remove(old); Add(incoming); }
                else if(operation==InventoryOperation.Remove) Remove(target);
                else if(operation==InventoryOperation.Equip) { SelectedInstanceId=target.InstanceId; Holstered=false; }
                else if(operation==InventoryOperation.Holster) Holstered=true;
                else if(operation==InventoryOperation.UpgradeCapacity) Capacity++;
                RepairSelection();
                InventorySnapshot after=Capture();
                Restore(before);
                // Stage sees old state; every exact outgoing slot member is in Removed.
                ItemInstance[] removed=(operation==InventoryOperation.Remove || operation==InventoryOperation.ReplaceSlot) ? outgoing : new ItemInstance[0];
                var change=new InventoryChange(operation,incoming,removed,before.SelectedInstanceId,
                    after.SelectedInstanceId,before.Holstered,after.Holstered,before.Capacity,after.Capacity);
                IInventoryReceipt receipt=null; bool committed=false;
                try
                {
                    RuleResult staged=transactions.Stage(context,change,out receipt);
                    if(!staged.Accepted) return staged;
                    if(receipt==null) return RuleResult.Reject("missing-staged-receipt");
                    RuleResult result=receipt.Commit();
                    if(!result.Accepted) return result;
                    Restore(after); committed=true;
                }
                finally { if(!committed && receipt!=null) receipt.Abort(); }
                revision++; commands.Add(context.CommandId); history.Enqueue(new KeyValuePair<string,long>(context.CommandId,context.Tick)); lastTick=context.Tick;
                if(history.Count>256) { var old=history.Dequeue(); commands.Remove(old.Key); retiredTick=Math.Max(retiredTick,old.Value); }
                return RuleResult.Accept();
            }
            finally { executing=false; }
        }
        // Handoff does not create/materialize weapons or initialize ammunition.
        public InventorySnapshot Capture()
        { return new InventorySnapshot(session,actor,epoch,Capacity,SelectedInstanceId,Holstered,items.ToArray(),groups,known,retired); }
        public InventorySnapshot RetireForHandoff()
        {
            if(preparedBusy || executing || retired) throw new InvalidOperationException("Inventory busy or retired");
            retired=true; return Capture();
        }
        public Inventory(ItemCatalog catalog,IInventoryBoundary boundary,string session,string actor,long epoch,InventorySnapshot snapshot)
            : this(catalog,boundary,session,actor,epoch,false)
        {
            if(snapshot==null || !snapshot.Handoff || snapshot.SessionId!=session || snapshot.ActorId!=actor || epoch<=snapshot.Epoch)
                throw new ArgumentException("Invalid epoch handoff");
            foreach(var item in snapshot.Known) {
                ItemDefinition d=catalog.Get(item.DefinitionId);
                if(!d.Active) throw new ArgumentException("Unavailable handoff definition");
                d.Stats(item.Stars);
            }
            Restore(snapshot);
        }
        private void Restore(InventorySnapshot snapshot)
        {
            Capacity=snapshot.Capacity; SelectedInstanceId=snapshot.SelectedInstanceId; Holstered=snapshot.Holstered;
            items.Clear(); items.AddRange(snapshot.Members); groups.Clear();
            foreach(var group in snapshot.Groups) groups.Add(new List<ItemInstance>(group));
            known.Clear(); foreach(var item in snapshot.Known) known.Add(item.InstanceId,item);
        }
        private ItemInstance Find(string id) { return items.Find(delegate(ItemInstance i){return i.InstanceId==id;}); }
        private List<ItemInstance> Group(ItemInstance item) { return groups.Find(delegate(List<ItemInstance> g){return g.Contains(item);}); }
        private bool HasStackRoom(ItemDefinition d)
        { return d.StackKey.Length>0 && groups.Exists(delegate(List<ItemInstance> g){return g.Count<2 && catalog.Get(g[0].DefinitionId).StackKey==d.StackKey;}); }
        private void Add(ItemInstance item)
        {
            known[item.InstanceId]=item;
            ItemDefinition d=catalog.Get(item.DefinitionId); List<ItemInstance> group=null;
            if(d.StackKey.Length>0) group=groups.Find(delegate(List<ItemInstance> g){return g.Count<2 && catalog.Get(g[0].DefinitionId).StackKey==d.StackKey;});
            if(group==null) {group=new List<ItemInstance>(); groups.Add(group);} group.Add(item); items.Add(item);
        }
        private void Remove(ItemInstance item)
        {
            var group=Group(item); group.Remove(item);
            if(SelectedInstanceId==item.InstanceId && group.Count>0) SelectedInstanceId=group[0].InstanceId;
            if(group.Count==0)groups.Remove(group); items.Remove(item);
        }
        private void RepairSelection()
        {
            if(Find(SelectedInstanceId)!=null)return;
            // Source prefers remaining same-stack member after disposable depletion.
            SelectedInstanceId=items.Count==0 ? "" : items[0].InstanceId;
            if(items.Count==0) Holstered=true;
        }
        private List<List<ItemInstance>> OrderedGroups()
        {
            var result=new List<List<ItemInstance>>(groups);
            result.Sort(delegate(List<ItemInstance> a,List<ItemInstance> b){return Compare(a[0],b[0]);}); return result;
        }
        private int Compare(ItemInstance a,ItemInstance b)
        {
            var da=catalog.Get(a.DefinitionId); var db=catalog.Get(b.DefinitionId);
            if(da.IsWeapon!=db.IsWeapon)return da.IsWeapon ? -1 : 1;
            if(!da.IsWeapon)return items.IndexOf(a).CompareTo(items.IndexOf(b));
            int c=da.Category.CompareTo(db.Category); if(c!=0)return c;
            var sa=da.Stats(a.Stars); var sb=db.Stats(b.Stars);
            // Explosives inherit weapon.damage=34; blast payload is not the inventory sort key.
            double ad=da.CatalogIndex>=26 && da.CatalogIndex<=28 ? 34 : sa.Body/sa.Pellets;
            double bd=db.CatalogIndex>=26 && db.CatalogIndex<=28 ? 34 : sb.Body/sb.Pellets;
            c=bd.CompareTo(ad); if(c!=0)return c;
            c=da.CatalogIndex.CompareTo(db.CatalogIndex); return c!=0 ? c : items.IndexOf(a).CompareTo(items.IndexOf(b));
        }
    }
}

