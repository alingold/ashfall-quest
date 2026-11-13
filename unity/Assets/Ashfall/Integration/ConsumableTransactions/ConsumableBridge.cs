using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Sessions;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Player;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.InventoryTransfers;
namespace Ashfall.Integration.ConsumableTransactions
{
    // Host must route ALL inventory, health, actor and locomotion access through Lane.
    // External geometry and canonical reserve bindings are trusted host-only ports.
    // Mixed inventories require the actual session registry; weapon eviction remains closed.
    public sealed class ConsumableBridge : IPickupTransactions,ISelectedPickupTransactions,IAtomicPickupEffects,IPreparedPickupSelection
    {
        public readonly HealthService Lane;
        private readonly object weaponLane=new object();
        private SessionWeapons weapons;
        private Session sessionBinding;
        private readonly HashSet<object> outstanding=new HashSet<object>();
        private readonly Dictionary<string,Lifetime> lifetimes=new Dictionary<string,Lifetime>(StringComparer.Ordinal);
        public bool BindSession(Session session)
        {return Serialize(delegate{if(active||sessionBinding!=null||session==null||lifetimes.Count!=0)return false;sessionBinding=session;return true;});}
        private sealed class Lifetime
        {
            internal ConsumableBridge Bridge;internal string Actor,Session;internal long Epoch,Life,Connection;internal object Identity;
            internal bool Valid(){var h=Bridge.Lane.Read(Actor);var s=Bridge.sessionBinding;return h!=null&&h.Connected&&h.Life==LifeState.Alive&&h.LifeGeneration==Life&&h.ConnectionGeneration==Connection&&(s==null||(s.Id==Session&&s.Epoch==Epoch&&Identity!=null&&Object.ReferenceEquals(Identity,s.GetConnectionIdentity(Actor))));}
        }
        private Lifetime CaptureLifetime(string actor)
        {var h=Lane.Read(actor);if(h==null||!h.Connected||h.Life!=LifeState.Alive)return null;var s=sessionBinding;var token=new Lifetime{Bridge=this,Actor=actor,Session=current.SessionId,Epoch=current.AuthorityEpoch,Life=h.LifeGeneration,Connection=h.ConnectionGeneration,Identity=s==null?null:s.GetConnectionIdentity(actor)};return token.Valid()?token:null;}
        private bool CurrentLifetime(string actor){Lifetime token;return lifetimes.TryGetValue(actor,out token)&&token.Valid();}
        private Lifetime PreparedLifetime(string actor){Lifetime token;return lifetimes.TryGetValue(actor,out token)&&token.Valid()?token:null;}
        public byte[] CapturePickup(CheckpointBinding binding)
        {return Serialize(delegate{if(active||outstanding.Count!=0||safety.Count!=0||pendingDrop!=null||pickups==null)throw new InvalidOperationException("Outstanding consumable preparation");return PickupCheckpointCodec.Capture(pickups,binding);});}

        private T Serialize<T>(Func<T> action){return Lane.Serialize(delegate{lock(weaponLane){return action();}});}
        public bool BindWeapons(SessionWeapons registry)
        {return Serialize(delegate{if(active||weapons!=null||registry==null||!registry.BindPreparedWorldActionOwner(weaponLane,delegate(string actor){Inventory i;return inventories.TryGetValue(actor,out i)?i:null;}))return false;weapons=registry;return true;});}
        public RuleResult ApplyWeapon(CommandContext c,string id,HandlingAction action,HandlingInput input)
        {return Serialize(delegate{Inventory i;if(active||weapons==null||!inventories.TryGetValue(c.ActorId,out i)||!Lane.Authorize(c).Accepted)return RuleResult.Reject("weapon-binding");return weapons.Apply(i.Capture(),c,id,action,input);});}
        private bool PrepareWeapons(string actor,out List<IPreparedWeaponSuspension> tokens)
        {
            tokens=null;Inventory i;if(!inventories.TryGetValue(actor,out i))return false;
            bool mixed=false;var catalog=SourceCatalog.Create();foreach(var item in i.Items)if(catalog.Get(item.DefinitionId).IsWeapon)mixed=true;
            return !mixed||TransferBridge.PrepareConsumableWeapons(weapons,i,out tokens);
        }

        private readonly Dictionary<string,Inventory> inventories=new Dictionary<string,Inventory>(StringComparer.Ordinal);
        private readonly Dictionary<string,Locomotion> movement=new Dictionary<string,Locomotion>(StringComparer.Ordinal);
        private readonly Dictionary<string,Inventory.PreparedSafetyHolster> safety=new Dictionary<string,Inventory.PreparedSafetyHolster>(StringComparer.Ordinal);
        private bool PrepareSafety(string actor)
        {
            string held=pickups.HeldInstance(actor);if(held==null)return true;
            Inventory inventory;Inventory.PreparedSafetyHolster token;
            if(!inventories.TryGetValue(actor,out inventory)||!inventory.PrepareSafetyHolster(held,out token))return false;
            safety.Add(actor,token);return token.Validate();
        }
        private void ReleaseSafety(string actor)
        {Inventory.PreparedSafetyHolster token;if(safety.TryGetValue(actor,out token)){token.Abort();safety.Remove(actor);}}
        private void FinishSafety()
        {
            foreach(var pair in safety) {if(pickups.HeldInstance(pair.Key)==null)pair.Value.CommitValidated();else pair.Value.Abort();}
            safety.Clear();
        }
        private void AbortSafety(){foreach(var pair in safety)pair.Value.Abort();safety.Clear();}
        private BuildChargeAccounts builds;
        public bool BindBuilds(BuildChargeAccounts accounts)
        {return Serialize(delegate{if(active||builds!=null||accounts==null)return false;builds=accounts;return true;});}
        private sealed class AmmoBinding {internal FiniteAmmoReserve Reserve;internal int Accepted;}
        private readonly Dictionary<string,Dictionary<string,AmmoBinding>> ammo=new Dictionary<string,Dictionary<string,AmmoBinding>>(StringComparer.Ordinal);
        private IDropPlanner dropPlanner;private IDropBatch pendingDrop;private EvictedInstance pendingPlacement;
        private void AbortDrop(){if(pendingDrop!=null)pendingDrop.Abort();pendingDrop=null;pendingPlacement=null;}
        public void BindDrops(IDropPlanner planner){Serialize(delegate{if(active||dropPlanner!=null||planner==null)throw new InvalidOperationException("drop binding");dropPlanner=planner;return true;});}
        // Trusted host maps the actual canonical family reserve, never a newly minted pool.
        // All reserve users must share Lane; lifecycle/weapon registry binding is external.
        public bool BindAmmoReserve(string actor,string family,FiniteAmmoReserve reserve)
        {return Serialize(delegate{if(active||reserve==null||String.IsNullOrEmpty(actor)||!inventories.ContainsKey(actor)||family=="builds"||PickupRules.SupplyAmount(family)==0)return false;Dictionary<string,AmmoBinding> map;if(!ammo.TryGetValue(actor,out map)){map=new Dictionary<string,AmmoBinding>(StringComparer.Ordinal);ammo.Add(actor,map);}if(map.ContainsKey(family))return false;foreach(var existing in ammo.Values)foreach(var binding in existing.Values)if(Object.ReferenceEquals(binding.Reserve,reserve))return false;map.Add(family,new AmmoBinding{Reserve=reserve});return true;});}
        public int LastAcceptedSupply(string actor,string family)
        {return Serialize(delegate{Dictionary<string,AmmoBinding> map;AmmoBinding binding;return ammo.TryGetValue(actor,out map)&&map.TryGetValue(family,out binding)?binding.Accepted:0;});}
        private PickupRules pickups;private CommandContext current;private bool active;
        public ConsumableBridge(HealthService health){if(health==null)throw new ArgumentNullException("health");Lane=health;}
        public void Bind(PickupRules rules){if(pickups!=null||rules==null)throw new InvalidOperationException("binding");pickups=rules;}
        public void AddActor(string actor,Inventory inventory,Locomotion locomotion)
        {Serialize(delegate{if(active||inventories.ContainsKey(actor)||inventory==null||locomotion==null||inventory.Capture().ActorId!=actor)throw new ArgumentException("actor binding");inventories.Add(actor,inventory);movement.Add(actor,locomotion);return true;});}
        public RuleResult Execute(CommandContext c,PickupAction action,string item)
        {return Serialize(delegate{if(active||pickups==null||!Lane.Authorize(c).Accepted)return RuleResult.Reject("context-or-reentry");active=true;current=c;List<IPreparedWeaponSuspension> tokens=null;try{
                Lifetime previous;var lifetime=CaptureLifetime(c.ActorId);if(lifetime==null)return RuleResult.Reject("connection-life");
                bool changed=lifetimes.TryGetValue(c.ActorId,out previous)&&!previous.Valid();lifetimes[c.ActorId]=lifetime;
                Inventory inventory;if(!inventories.TryGetValue(c.ActorId,out inventory)||!inventory.AdvanceConsumableReservations(c)||(action!=PickupAction.Reserve&&action!=PickupAction.Release&&!PrepareWeapons(c.ActorId,out tokens)))return RuleResult.Reject("inventory-or-registry");
                if(!PrepareSafety(c.ActorId))return RuleResult.Reject("safety-reservation");
                var result=pickups.Execute(c,changed?PickupAction.Stow:action,changed?null:item);FinishSafety();if(changed)return RuleResult.Reject("connection-life-changed-preparation-reset");
                if(result.Accepted&&action!=PickupAction.Reserve&&action!=PickupAction.Release&&tokens!=null){foreach(var token in tokens)token.CommitValidated();TransferBridge.PublishConsumableHolster(weapons,c.ActorId);}
                return result;
            }finally{if(tokens!=null)foreach(var token in tokens)token.Abort();AbortDrop();AbortSafety();active=false;}});}
        public void Advance(CommandContext c)
        {Serialize(delegate{if(active||pickups==null||!Lane.Authorize(c).Accepted)throw new InvalidOperationException("context-or-reentry");active=true;current=c;try{foreach(var pair in inventories)if(!pair.Value.AdvanceConsumableReservations(new CommandContext(c.SessionId,pair.Key,c.CommandId,c.Tick,c.AuthorityEpoch,c.IsAuthority))||!PrepareSafety(pair.Key))throw new InvalidOperationException("safety-reservation");pickups.Advance(c.Tick);FinishSafety();}finally{AbortDrop();AbortSafety();active=false;}return true;});}
        public bool PublishSelection(string actor,string beforeHeld,string selected)
        {
            Inventory inventory;Inventory.PreparedConsumables token;
            if(active&&current.ActorId==actor)ReleaseSafety(actor);
            if(!active||current.ActorId!=actor||pickups.HeldInstance(actor)!=beforeHeld||!inventories.TryGetValue(actor,out inventory)||!inventory.PrepareConsumableSelection(current,selected,out token))return false;
            try {if(!token.Validate()||!CurrentLifetime(actor))return false;token.CommitValidated();return true;}finally{token.Abort();}
        }
        public bool TryStageConsume(Transfer request,string held,out IStagedConsume stage)
        {
            stage=null;Inventory inventory;Locomotion locomotion;
            if(active&&request!=null&&request.ActorId==current.ActorId)ReleaseSafety(request.ActorId);
            if(!active||request==null||request.Operation!=TransferKind.Consume||request.ActorId!=current.ActorId||request.ItemId!=held||!inventories.TryGetValue(request.ActorId,out inventory)||!movement.TryGetValue(request.ActorId,out locomotion))return false;
            string kind=null;foreach(var member in inventory.Items)if(member.InstanceId==request.ItemId)kind=member.DefinitionId;
            if(kind!=request.Kind||!PickupRules.IsConsumable(kind)||pickups.HeldInstance(request.ActorId)!=held||inventory.Holstered||inventory.SelectedInstanceId!=held)return false;
            var state=Lane.Read(current.ActorId);if(state==null)return false;
            HealthService.PreparedGain gain;
            if(!Lane.PrepareGain(current,"consume:"+request.ItemId,current.ActorId,state.Revision,state.LifeGeneration,state.ConnectionGeneration,kind=="banana"?100:0,kind=="shaker"?50:0,out gain))return false;
            Inventory.PreparedRemoval removal;
            if(!inventory.PrepareRemoval(current,request.ItemId,out removal)){gain.Abort();return false;}
            var lifetime=PreparedLifetime(request.ActorId);if(lifetime==null){removal.Abort();gain.Abort();return false;}stage=new Consumption(this,lifetime,request,held,removal,gain,locomotion);outstanding.Add(stage);return true;
        }
        private sealed class Consumption : IStagedConsume
        {
            public ConsumeReceipt Receipt {get;private set;}
            private readonly Inventory.PreparedRemoval removal;private readonly HealthService.PreparedGain gain;private readonly Locomotion locomotion;
            private bool aborted,committed;private AtomicOutcome result;
            private readonly ConsumableBridge bridge;private readonly Lifetime lifetime;
            internal Consumption(ConsumableBridge owner,Lifetime identity,Transfer t,string held,Inventory.PreparedRemoval r,HealthService.PreparedGain g,Locomotion l)
            {bridge=owner;lifetime=identity;removal=r;gain=g;locomotion=l;Receipt=new ConsumeReceipt(t,new StackSelection(held,r.Selected,r.Members));}
            public AtomicOutcome TryCommit()
            {
                if(committed)return result;if(aborted||!bridge.active||!removal.Validate()||!gain.Validate()||!lifetime.Valid())return AtomicOutcome.Failed;
                bool banana=Receipt.Request.Kind=="banana";
                if(gain.Proposed.Outcome==GainOutcome.CappedNoOp&&!banana&&Receipt.Request.Kind!="soda")return AtomicOutcome.CappedNoOp;
                if(banana&&gain.Proposed.Outcome==GainOutcome.CappedNoOp&&locomotion.SprintStamina>=6)return AtomicOutcome.CappedNoOp;
                // All fallible validation precedes this boundary. Real privileged port is bounded.
                gain.CommitValidated();if(banana)locomotion.RestoreStamina(6);removal.CommitValidated();committed=true;bridge.outstanding.Remove(this);result=AtomicOutcome.Applied;return result;
            }
            public void Abort(){if(committed)return;aborted=true;gain.Abort();removal.Abort();bridge.outstanding.Remove(this);}
        }
        public bool TryStageStore(Transfer request,out IStagedStore stage){return TryStageStore(request,null,out stage);}
        public bool TryStageStore(Transfer request,string held,out IStagedStore stage)
        {
            stage=null;Inventory inventory;
            if(active&&request!=null&&request.ActorId==current.ActorId)ReleaseSafety(request.ActorId);
            if(!active||request==null||request.Operation!=TransferKind.Store||request.ActorId!=current.ActorId||!PickupRules.IsConsumable(request.Kind)||!inventories.TryGetValue(request.ActorId,out inventory))return false;
            // Freeze source slot order; only consumables may be evicted by this owner.
            var members=new List<string>();foreach(var slot in inventory.Slots)if(request.Kind!="banana"&&slot.Length==1&&slot[0].DefinitionId==request.Kind){members.Add(slot[0].InstanceId);break;}
            members.Add(request.ItemId);Inventory.PreparedConsumables admission;
            if(!inventory.PrepareConsumableAdmission(current,new ItemInstance(request.ItemId,request.Kind,1),dropPlanner!=null,out admission))return false;
            IDropBatch batch=null;EvictedInstance[] placements=new EvictedInstance[0];
            try {
                var removed=admission.Removed;
                if(removed.Length>0) {
                    if(!dropPlanner.Prepare(current,removed,out batch)||batch==null)return false;
                    placements=batch.Placements;if(placements==null||placements.Length!=removed.Length)return false;
                    var ids=new HashSet<string>(StringComparer.Ordinal);
                    foreach(var placement in placements)if(placement==null||!placement.Position.IsFinite||!ids.Add(placement.ItemId)||!Array.Exists(removed,delegate(ItemInstance m){return m.InstanceId==placement.ItemId&&m.DefinitionId==placement.Kind;}))return false;
                }
                var lifetime=PreparedLifetime(request.ActorId);if(lifetime==null)return false;stage=new Store(this,lifetime,request,held,admission,members.ToArray(),batch,placements);outstanding.Add(stage);batch=null;return true;
            } finally {if(stage==null)admission.Abort();if(batch!=null)batch.Abort();}
        }
        private sealed class Store : IStagedStore
        {
            public StoreReceipt Receipt {get;private set;}
            private readonly Inventory.PreparedConsumables admission;private readonly IDropBatch batch;private bool done,aborted;
            private readonly ConsumableBridge bridge;private readonly Lifetime lifetime;
            internal Store(ConsumableBridge owner,Lifetime identity,Transfer t,string held,Inventory.PreparedConsumables prepared,string[] members,IDropBatch drops,EvictedInstance[] placements){bridge=owner;lifetime=identity;admission=prepared;batch=drops;Receipt=new StoreReceipt(t,placements,new StackSelection(held,null,members));}
            public bool TryCommit(){if(done)return true;if(aborted||!bridge.active||!admission.Validate()||(batch!=null&&!batch.StillValid)||!lifetime.Valid())return false;if(batch!=null&&!batch.Commit())return false;admission.CommitValidated();done=true;bridge.outstanding.Remove(this);return true;}
            public void Abort(){if(!done){aborted=true;admission.Abort();if(batch!=null)batch.Abort();bridge.outstanding.Remove(this);}}
        }
        public AtomicOutcome CommitEffect(Transfer request)
        {
            Inventory inventory;
            if(active&&request!=null&&request.ActorId==current.ActorId)ReleaseSafety(request.ActorId);
            if(!active||request==null||!CurrentLifetime(request.ActorId)||request.ActorId!=current.ActorId||!inventories.TryGetValue(request.ActorId,out inventory))return AtomicOutcome.Failed;
            if(request.Operation==TransferKind.Drop) {
                if(pendingDrop==null||pendingPlacement==null||pendingPlacement.ItemId!=request.ItemId||pendingPlacement.Kind!=request.Kind||pendingPlacement.Position.X!=request.Position.X||pendingPlacement.Position.Y!=request.Position.Y||pendingPlacement.Position.Z!=request.Position.Z)return AtomicOutcome.Failed;
                Inventory.PreparedRemoval removal;if(!inventory.PrepareDropRemoval(current,request.ItemId,out removal))return AtomicOutcome.Failed;
                try{if(!removal.Validate()||!pendingDrop.StillValid||!CurrentLifetime(request.ActorId)||!pendingDrop.Commit())return AtomicOutcome.Failed;removal.CommitValidated();return AtomicOutcome.Applied;}finally{removal.Abort();}
            }
            if(request.Operation==TransferKind.Supply) {
                if(request.Kind=="builds") {
                    if(builds==null||request.Amount!=6)return AtomicOutcome.Failed;
                    BuildChargeLedger.PreparedCharge charge;
                    if(!builds.ForActor(request.ActorId).PrepareGrant(current,request.ItemId,"builds",request.Amount,current.Tick,out charge).Accepted)return AtomicOutcome.Failed;
                    try{if(!charge.Validate(current.Tick)||!CurrentLifetime(request.ActorId))return AtomicOutcome.Failed;charge.CommitValidated();return AtomicOutcome.Applied;}finally{charge.Abort();}
                }
                Dictionary<string,AmmoBinding> map;AmmoBinding binding;
                if(request.Amount<=0||!ammo.TryGetValue(request.ActorId,out map)||!map.TryGetValue(request.Kind,out binding))return AtomicOutcome.Failed;
                // Refund is the real bounded reserve's single atomic mutation. Pickups
                // has already validated source ownership/range/replay and removes the
                // whole box with no fallible operation after this return.
                int accepted=binding.Reserve.Refund(request.Amount);binding.Accepted=accepted;
                return accepted>0?AtomicOutcome.Applied:AtomicOutcome.CappedNoOp;
            }
            if(request.Operation==TransferKind.UpgradeBackpack)return inventory.Apply(current,InventoryOperation.UpgradeCapacity,null,null).Accepted?AtomicOutcome.Applied:AtomicOutcome.Failed;
            return AtomicOutcome.Failed;
        }
        public bool TryCommit(Transfer request){return CommitEffect(request)==AtomicOutcome.Applied;}
        public bool TryPlaceDrop(string actor,string item,out Float3 position)
        {
            position=new Float3();Inventory inventory;
            if(!active||actor!=current.ActorId||dropPlanner==null||pendingDrop!=null||!inventories.TryGetValue(actor,out inventory))return false;
            ItemInstance member=Array.Find(inventory.Items,delegate(ItemInstance m){return m.InstanceId==item&&PickupRules.IsConsumable(m.DefinitionId);});if(member==null)return false;
            IDropBatch batch=null;
            try{if(!dropPlanner.Prepare(current,new ItemInstance[]{member},out batch)||batch==null)return false;
                var placements=batch.Placements;if(placements==null||placements.Length!=1||placements[0]==null||placements[0].ItemId!=item||placements[0].Kind!=member.DefinitionId||!placements[0].Position.IsFinite)return false;
                CanonicalPickupSource source;if(!pickups.TryReadCanonicalSource(item,out source)||source.Removed||source.Owner!=actor||source.Kind!=placements[0].Kind||source.SemanticOwnerId!=placements[0].SemanticOwnerId)return false;
                pendingPlacement=placements[0];pendingDrop=batch;batch=null;position=pendingPlacement.Position;return true;
            }finally{if(batch!=null)batch.Abort();}
        }
        public ShieldProcessing TryAddShield(string actor,float amount)
        {
            if(!active)return ShieldProcessing.Failed;var lifetime=CaptureLifetime(actor);if(lifetime==null)return ShieldProcessing.Failed;
            var state=Lane.Read(actor);if(state==null)return ShieldProcessing.Failed;
            var c=new CommandContext(current.SessionId,actor,"soda:"+state.LifeGeneration+":"+state.ConnectionGeneration+":"+current.Tick,current.Tick,current.AuthorityEpoch,current.IsAuthority);
            HealthService.PreparedGain gain;
            if(state==null||!Lane.PrepareGain(c,c.CommandId,actor,state.Revision,state.LifeGeneration,state.ConnectionGeneration,0,Math.Min(100,amount),out gain))return ShieldProcessing.Failed;
            try{if(!gain.Validate()||!lifetime.Valid())return ShieldProcessing.Failed;return gain.CommitValidated().Outcome==GainOutcome.Applied?ShieldProcessing.Applied:ShieldProcessing.CappedNoOp;}finally{gain.Abort();}
        }
    }
}

