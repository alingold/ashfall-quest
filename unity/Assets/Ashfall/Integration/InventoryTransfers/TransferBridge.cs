using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
namespace Ashfall.Integration.InventoryTransfers
{
    // All public reads and mutations share this lane. Never expose mutable aggregates.
    public sealed class TransferBridge : IPickupTransactions, IInventoryTransactions
    {
        // Concrete same-assembly adapter to the real registry; no grants/restores.
        public static bool PrepareConsumableWeapons(SessionWeapons registry,Inventory inventory,out System.Collections.Generic.List<Ashfall.Modules.WeaponHandling.IPreparedWeaponSuspension> tokens)
        {
            tokens=null;if(registry==null||inventory==null)return false;
            var snapshot=inventory.Capture();var catalog=SourceCatalog.Create();
            foreach(var item in inventory.Items)if(catalog.Get(item.DefinitionId).IsWeapon&&!registry.IsActive(item,snapshot.ActorId,snapshot.Epoch))return false;
            if(!registry.PrepareWorldSuspensions(snapshot.ActorId,new HashSet<string>(),out tokens))return false;
            foreach(var token in tokens)if(!token.Validate().Accepted){foreach(var prior in tokens)prior.Abort();tokens=null;return false;}
            return true;
        }
        public static void PublishConsumableHolster(SessionWeapons registry,string actor)
        {registry.PublishWorldHolster(actor);}
        sealed class Actor { public Inventory Inventory;public bool WeaponSuppressed; }
        sealed class Identity { public string Kind, Semantic; }
        readonly object lane=new object();
        readonly Dictionary<string,Actor> actors=new Dictionary<string,Actor>();
        readonly Dictionary<string,Identity> identities=new Dictionary<string,Identity>();
        readonly Queue<string> published=new Queue<string>();
        readonly ItemCatalog catalog; readonly PickupRules pickups;
        readonly ITransferEligibility eligibility; readonly IDropPlanner planner;
        readonly string session; readonly long epoch;
        CommandContext current; bool active, probing, equipping; InventoryChange proposal;
        StoreStage store; long now; readonly int hz; SessionWeapons weapons;
        public TransferBridge(string session,long epoch,int hz,IAuthorityGate gate,
            IPickupActors poses,IInteractionQuery query,ITransferEligibility eligibility,IDropPlanner planner)
        {
            if(eligibility==null||planner==null)throw new ArgumentNullException();
            this.session=session;this.epoch=epoch;this.hz=hz;this.eligibility=eligibility;this.planner=planner;
            catalog=SourceCatalog.Create();
            pickups=new PickupRules(session,epoch,hz,gate,poses,this,query,4096,64,4096);
        }
        public bool BindWeapons(SessionWeapons value) {lock(lane){if(active||weapons!=null||value==null||!value.BindActionOwner(lane,OwnsWeaponAction))return false;weapons=value;return true;}}
        bool OwnsWeaponAction(string actor,string id)
        {
            Actor a;if(String.IsNullOrEmpty(actor)||String.IsNullOrEmpty(id)||!actors.TryGetValue(actor,out a)||a.WeaponSuppressed||pickups.HeldInstance(actor)!=null)return false;
            InventorySnapshot snapshot=a.Inventory.Capture();return !snapshot.Holstered&&snapshot.SelectedInstanceId==id;
        }
        public RuleResult ApplyWeapon(CommandContext c,string id,Ashfall.Modules.WeaponHandling.HandlingAction action,Ashfall.Modules.WeaponHandling.HandlingInput input) {lock(lane){
            if(active||weapons==null||String.IsNullOrEmpty(c.ActorId)||!actors.ContainsKey(c.ActorId))return RuleResult.Reject("unbound-weapon");
            active=true;try {
                RuleResult allowed=eligibility.Validate(c);if(!allowed.Accepted)return allowed;
                if(!OwnsWeaponAction(c.ActorId,id))return RuleResult.Reject("not-action-owner");
                return weapons.Apply(actors[c.ActorId].Inventory.Capture(),c,id,action,input);
            }finally{active=false;}
        }}
        public bool AddActor(string actor) { lock(lane) {
            if(active||String.IsNullOrEmpty(actor)||actors.ContainsKey(actor)||actors.Count>=64)return false;
            actors.Add(actor,new Actor{Inventory=new Inventory(catalog,this,session,actor,epoch,false)});return true;
        } }
        public bool Register(string id,string kind,string semantic,Float3 position) { lock(lane) {
            if(active||identities.ContainsKey(id)||!pickups.Register(id,kind,semantic,position))return false;
            identities.Add(id,new Identity{Kind=kind,Semantic=semantic});return true;
        } }
        public void Advance(long tick) { lock(lane) { if(active)throw new InvalidOperationException();pickups.Advance(tick);now=tick; } }
        public bool TryInventorySnapshot(string actor,out InventorySnapshot snapshot)
        { lock(lane) { snapshot=null;Actor value;if(String.IsNullOrEmpty(actor)||!actors.TryGetValue(actor,out value))return false;snapshot=value.Inventory.Capture();return true; } }
        public InventorySnapshot Inventory(string actor) { lock(lane) {return actors[actor].Inventory.Capture();} }
        public PickupSnapshot Inspect(string id) { lock(lane) {return pickups.Inspect(id);} }
        public string HeldConsumable(string actor) {lock(lane){return pickups.HeldInstance(actor);}}
        public Preparation ConsumablePhase(string actor) {lock(lane){return pickups.Phase(actor);}}
        public string TakePublication() { lock(lane) {return published.Count==0?null:published.Dequeue();} }
        public RuleResult Execute(CommandContext c,PickupAction action,string id) { lock(lane) {
            if(active||!actors.ContainsKey(c.ActorId)||published.Count>=1024)return RuleResult.Reject("lane-or-capacity");
            current=c;active=true;
            try {
                RuleResult r=eligibility.Validate(c);if(!r.Accepted)return r;
                r=pickups.Execute(c,action,id);
                // Stowing a consumable does not implicitly draw the previously selected gun.
                if(r.Accepted&&action==PickupAction.Equip)actors[c.ActorId].WeaponSuppressed=true;
                if(r.Accepted&&weapons!=null)weapons.Selection(actors[c.ActorId].Inventory.Capture(),now,actors[c.ActorId].WeaponSuppressed,false);
                if(r.Accepted&&action==PickupAction.Collect)published.Enqueue(c.ActorId+":"+c.CommandId+":"+id);
                return r;
            } finally {active=false;store=null;proposal=null;probing=false;}
        } }
        public RuleResult AdmitExistingWeapon(CommandContext c,ItemInstance item,SessionWeapons weapons) { lock(lane) {
            if(active||!actors.ContainsKey(c.ActorId)||weapons==null||!Object.ReferenceEquals(this.weapons,weapons)||!weapons.IsActive(item,c.ActorId,c.AuthorityEpoch)||!catalog.Get(item.DefinitionId).IsWeapon)return RuleResult.Reject("unbound-lifetime");
            current=c;active=equipping=true;try {return actors[c.ActorId].Inventory.Apply(c,InventoryOperation.Admit,item,null);}finally{active=equipping=false;}
        } }
        public RuleResult Equip(CommandContext c,string id,bool holster) { lock(lane) {
            if(active||!actors.ContainsKey(c.ActorId))return RuleResult.Reject("lane");
            current=c;active=equipping=true;
            try {RuleResult r=actors[c.ActorId].Inventory.Apply(c,holster?InventoryOperation.Holster:InventoryOperation.Equip,null,id);if(r.Accepted){actors[c.ActorId].WeaponSuppressed=false;if(weapons!=null)weapons.Selection(actors[c.ActorId].Inventory.Capture(),c.Tick,false,true);}return r;}
            finally {active=equipping=false;}
        } }
        public RuleResult Validate(CommandContext c,InventoryOperation operation,ItemInstance incoming,ItemInstance[] outgoing)
        {
            if(!active||!Same(c,current))return RuleResult.Reject("unbound-context");
            return eligibility.Validate(c);
        }
        public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt)
        {
            receipt=null;
            if(probing) {proposal=change;return RuleResult.Reject("proposal-only");}
            if(equipping) {receipt=new EquipReceipt(this,c,change.Operation==InventoryOperation.Equip||change.Operation==InventoryOperation.Holster);return RuleResult.Accept();}
            if(store==null||!Equal(change,store.Change))return RuleResult.Reject("proposal-drift");
            receipt=store;return RuleResult.Accept();
        }
        sealed class EquipReceipt:IInventoryReceipt {
            readonly TransferBridge host;readonly CommandContext context;readonly bool clearPickup;bool committed;
            public EquipReceipt(TransferBridge host,CommandContext context,bool clearPickup){this.host=host;this.context=context;this.clearPickup=clearPickup;}
            public RuleResult Commit(){
                if(committed)return RuleResult.Reject("selection-receipt-used");
                // Inventory has validated its proposal and still exposes the old state.
                // Failed/invalid inventory intents never reach this receipt.
                if(clearPickup&&host.pickups.HeldInstance(context.ActorId)!=null){
                    RuleResult cleared=host.pickups.Execute(context,PickupAction.Stow,null);if(!cleared.Accepted)return cleared;
                }
                committed=true;return RuleResult.Accept();
            }
            public void Abort(){}
        }
        bool IPickupTransactions.TryStageStore(Transfer transfer,out IStagedStore result)
        {
            result=null;
            if(!active||transfer.ActorId!=current.ActorId||transfer.Operation!=TransferKind.Store)return false;
            Inventory inventory=actors[current.ActorId].Inventory;
            proposal=null;probing=true;
            try {inventory.Apply(current,InventoryOperation.ReplaceSlot,new ItemInstance(transfer.ItemId,transfer.Kind,1),inventory.SelectedInstanceId);}
            finally {probing=false;}
            if(proposal==null)return false;
            // Weapon lifetime retirement cannot be compensated with the consumed API.
            foreach(ItemInstance member in proposal.Removed)if(catalog.Get(member.DefinitionId).IsWeapon)return false;
            IDropBatch batch=null;
            try {
                if(!planner.Prepare(current,proposal.Removed,out batch)||batch==null)return false;
                EvictedInstance[] placements=batch.Placements;
                if(placements==null||placements.Length!=proposal.Removed.Length)return false;
                var ids=new HashSet<string>();
                foreach(EvictedInstance p in placements) {
                    Identity identity; ItemInstance member=Array.Find(proposal.Removed,delegate(ItemInstance i){return p!=null&&i.InstanceId==p.ItemId;});
                    if(p==null||member==null||!ids.Add(p.ItemId)||!identities.TryGetValue(p.ItemId,out identity)||identity.Kind!=p.Kind||identity.Semantic!=p.SemanticOwnerId||!p.Position.IsFinite)return false;
                }
                store=new StoreStage(this,inventory,proposal,batch,new StoreReceipt(transfer,placements),now);
                result=store;batch=null;return true;
            } finally {if(batch!=null)batch.Abort();}
        }
        sealed class StoreStage:IStagedStore,IInventoryReceipt
        {
            readonly TransferBridge host;readonly Inventory inventory;readonly IDropBatch batch;
            public readonly InventoryChange Change; readonly long preparedTick;
            public StoreReceipt Receipt {get;private set;} bool committed,aborted;
            public StoreStage(TransferBridge h,Inventory i,InventoryChange c,IDropBatch b,StoreReceipt r,long tick)
            {host=h;inventory=i;Change=c;batch=b;Receipt=r;preparedTick=tick;}
            public bool TryCommit() {
                if(committed||aborted||host.now!=preparedTick||host.current.Tick>host.now||host.now-host.current.Tick>=host.hz||!batch.StillValid)return false;
                return inventory.Apply(host.current,Change.Operation,Change.Incoming,Change.PreviousSelectedId).Accepted;
            }
            public RuleResult Commit() {
                if(committed||aborted||!batch.StillValid||!host.eligibility.Validate(host.current).Accepted)return RuleResult.Reject("stale-reservations");
                if(!batch.Commit())return RuleResult.Reject("batch-commit");
                committed=true;return RuleResult.Accept();
            }
            public void Abort(){if(!aborted){aborted=true;batch.Abort();}}
        }
        static bool Same(CommandContext a,CommandContext b){return a.SessionId==b.SessionId&&a.ActorId==b.ActorId&&a.AuthorityEpoch==b.AuthorityEpoch&&a.CommandId==b.CommandId&&a.Tick==b.Tick&&a.IsAuthority==b.IsAuthority;}
        static bool Equal(InventoryChange a,InventoryChange b) {
            if(a.Operation!=b.Operation||a.Incoming.InstanceId!=b.Incoming.InstanceId||a.Incoming.DefinitionId!=b.Incoming.DefinitionId||a.Incoming.Stars!=b.Incoming.Stars||a.SelectedId!=b.SelectedId||a.PreviousSelectedId!=b.PreviousSelectedId||a.Capacity!=b.Capacity||a.Holstered!=b.Holstered)return false;
            ItemInstance[] x=a.Removed,y=b.Removed;if(x.Length!=y.Length)return false;
            for(int n=0;n<x.Length;n++)if(x[n].InstanceId!=y[n].InstanceId||x[n].DefinitionId!=y[n].DefinitionId||x[n].Stars!=y[n].Stars)return false;return true;
        }
        // Fail closed for effects for which no actual module adapter is available.
        bool IPickupTransactions.TryCommit(Transfer t){return false;}
        bool IPickupTransactions.TryPlaceDrop(string actor,string id,out Float3 p){p=new Float3();return false;}
        ShieldProcessing IPickupTransactions.TryAddShield(string actor,float amount){return ShieldProcessing.Failed;}
    }
}




