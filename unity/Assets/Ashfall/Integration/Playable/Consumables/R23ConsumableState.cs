using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
namespace Ashfall.Integration.Playable.Consumables
{
    public struct R23ConsumableVitals
    {public float Health,Shield,Stamina,MaxStamina;public bool Alive;}
    public interface IR23ConsumableEffects
    {
        R23ConsumableVitals ReadVitals();
        // True means the real owner applied the capped positive gain/refill. False means zero mutation.
        bool TryApplyGain(float health,float shield,bool refillStamina);
    }
    public sealed class R23OwnedConsumable
    {
        public readonly string Id,Kind;public readonly int Slot;
        internal R23OwnedConsumable(string id,string kind,int slot){Id=id;Kind=kind;Slot=slot;}
    }
    // Small local port around the existing source PickupRules. Effects are always external real owners.
    public sealed class R23ConsumableState:IPickupActors,IPickupTransactions,IAuthorityGate,IPickupDropGroups,ISelectedPickupTransactions
    {
        const string Actor="player",Session="r23-consumables";
        public Func<string,bool> SharedConsume;
        PickupRules rules;readonly IR23ConsumableEffects effects;readonly IInteractionQuery query;
        sealed class Source {internal string Kind;internal Float3 Position;internal bool RestoreOnReset;}
        readonly Dictionary<string,Source> sources=new Dictionary<string,Source>(StringComparer.Ordinal);
        readonly List<R23OwnedConsumable> owned=new List<R23OwnedConsumable>();
        ActorFrame frame=new ActorFrame();Float3 dropPoint;long tick,serial;double clock;
        Float3 peelStart;bool peeling;
        int otherSlotsUsed,slotCapacity=3;
        public int SlotCapacity {get{return slotCapacity;}set{slotCapacity=Math.Max(3,Math.Min(6,value));}}
        public int Peels {get;private set;}
        public bool PeelActive {get{return peeling;}}
        public Float3 PeelStart {get{return peelStart;}}
        public string HeldId {get{return rules.HeldInstance(Actor);}}
        public Preparation Phase {get{return rules.Phase(Actor);}}
        public float PendingSoda {get{return rules.PendingSoda(Actor);}}
        public bool AcknowledgeSharedConsume(string id){if(SharedConsume==null||!owned.Exists(delegate(R23OwnedConsumable item){return item.Id==id;}))return false;var r=rules.AcknowledgeConsumed(new CommandContext(Session,Actor,"shared-"+(++serial),tick,0,true),id);if(!r.Accepted)return false;owned.RemoveAll(delegate(R23OwnedConsumable item){return item.Id==id;});return true;}
        public R23OwnedConsumable[] OwnedItems {get{return owned.ToArray();}}
        public R23ConsumableState(IR23ConsumableEffects effectPort,IInteractionQuery query)
        {if(effectPort==null||query==null)throw new ArgumentNullException();effects=effectPort;this.query=query;CreateRules();}
        void CreateRules(){rules=new PickupRules(Session,0,1000,this,this,this,query,8192,1,1024);}
        public bool Register(string id,string kind,Float3 position)
        {return Register(id,kind,position,true);}
        public bool Register(string id,string kind,Float3 position,bool restoreOnReset)
        {if(!rules.Register(id,kind,"consumable/"+id,position))return false;sources.Add(id,new Source{Kind=kind,Position=position,RestoreOnReset=restoreOnReset});return true;}
        public void Reset(bool restoreRegisteredSources=true)
        {
            owned.Clear();clock=0;tick=serial=0;peeling=false;Peels=0;CreateRules();
            if(restoreRegisteredSources){foreach(var id in new List<string>(sources.Keys)){var source=sources[id];if(source.RestoreOnReset)rules.Register(id,source.Kind,"consumable/"+id,source.Position);else sources.Remove(id);}}
            else sources.Clear();
        }
        public bool ResetActorLife(){var result=rules.CancelActorLife(new CommandContext(Session,Actor,"life-"+(++serial),tick,0,true));if(result.Accepted){Peels=0;peeling=false;}return result.Accepted;}
        public PickupSnapshot Inspect(string id){return rules.Inspect(id);}
        public bool RepositionWorldItem(string id,Float3 position)
        {if(!rules.TryRelocateLocalWorldItem(id,position))return false;sources[id].Position=position;return true;}
        public bool RetireUnownedWorldItem(string id)
        {if(!rules.TryRetireLocalWorldItem(id))return false;sources.Remove(id);return true;}
        public void SetOtherSlotsUsed(int count){otherSlotsUsed=Math.Max(0,Math.Min(6,count));}
        public bool CanStore(string kind){return FreeSlot(kind)>=0;}
        public void SetFrame(ActorFrame next)
        {
            frame=next??new ActorFrame();var actual=effects.ReadVitals();frame.Health=actual.Health;frame.Shield=actual.Shield;frame.Stamina=actual.Stamina;frame.MaxStamina=actual.MaxStamina;frame.Allowed&=actual.Alive;
        }
        public void Advance(float seconds)
        {
            if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<0||seconds>.1f)throw new ArgumentOutOfRangeException("seconds");
            clock+=seconds;tick=(long)Math.Floor(clock*1000+.000001);rules.Advance(tick);
            if(HeldId==null){Peels=0;peeling=false;}
        }
        public RuleResult Execute(PickupAction action,string id)
        {
            var result=rules.Execute(new CommandContext(Session,Actor,"local-"+(++serial),tick,0,true),action,id);
            if(result.Accepted)
            {
                if(action==PickupAction.Equip||action==PickupAction.Stow||action==PickupAction.Collect||action==PickupAction.Drop||action==PickupAction.Consume){Peels=0;peeling=false;}
                if(action==PickupAction.BeginPeel){peelStart=frame.OtherLocal;peeling=true;}
                if(action==PickupAction.SamplePeel&&peeling&&peelStart.Y-frame.OtherLocal.Y>=.10f){Peels=Math.Min(4,Peels+1);peeling=false;}
            }
            else if(action==PickupAction.SamplePeel)peeling=false;
            return result;
        }
        public RuleResult Drop(string id,Float3 position){dropPoint=position;return Execute(PickupAction.Drop,id);}
        public RuleResult Validate(CommandContext context){return context.IsAuthority&&context.SessionId==Session&&context.ActorId==Actor&&context.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("local_scope");}
        public bool TryRead(string actor,out ActorFrame value){value=frame;return actor==Actor;}
        int FreeSlot(string kind)
        {
            int available=SlotCapacity-otherSlotsUsed;
            var occupied=new HashSet<int>();foreach(var item in owned)occupied.Add(item.Slot);
            if(occupied.Count>available)return -1;
            if(kind!="banana")foreach(int slot in occupied)
            {int count=0;bool same=true;foreach(var item in owned)if(item.Slot==slot){count++;same&=item.Kind==kind;}if(count>0&&count<2&&same)return slot;}
            if(occupied.Count>=available)return -1;for(int slot=0;slot<SlotCapacity;slot++)if(!occupied.Contains(slot))return slot;return -1;
        }
        public RuleResult DropSlot(int slot,Float3[] positions)
        {
            var members=owned.FindAll(delegate(R23OwnedConsumable item){return item.Slot==slot;});
            if(positions==null||positions.Length!=members.Count||members.Count<1||members.Count>2)return RuleResult.Reject("slot-members");
            var drops=new EvictedInstance[members.Count];for(int i=0;i<drops.Length;i++)drops[i]=new EvictedInstance(members[i].Id,members[i].Kind,"consumable/"+members[i].Id,positions[i]);
            var result=rules.DropGroup(new CommandContext(Session,Actor,"local-"+(++serial),tick,0,true),drops);if(result.Accepted){Peels=0;peeling=false;}return result;
        }
        public bool TryStageDropGroup(string actor,EvictedInstance[] members,out IStagedPickupDropGroup stage)
        {
            stage=null;if(actor!=Actor||members==null||members.Length<1||members.Length>2)return false;
            int slot=-1;var ids=new HashSet<string>(StringComparer.Ordinal);foreach(var member in members){if(member==null||!ids.Add(member.ItemId)||!member.Position.IsFinite)return false;var found=owned.Find(delegate(R23OwnedConsumable item){return item.Id==member.ItemId&&item.Kind==member.Kind;});if(found==null||(slot>=0&&found.Slot!=slot))return false;slot=found.Slot;}
            int count=0;foreach(var item in owned)if(item.Slot==slot)count++;if(count!=members.Length)return false;
            stage=new GroupDrop(this,members,owned.ToArray());return true;
        }
        sealed class GroupDrop:IStagedPickupDropGroup
        {
            readonly R23ConsumableState owner;readonly EvictedInstance[] members;readonly R23OwnedConsumable[] before;bool done,aborted;
            internal GroupDrop(R23ConsumableState owner,EvictedInstance[] members,R23OwnedConsumable[] before){this.owner=owner;this.members=(EvictedInstance[])members.Clone();this.before=before;}
            public EvictedInstance[] Members {get{return(EvictedInstance[])members.Clone();}}
            public bool TryCommit(){if(done||aborted||owner.owned.Count!=before.Length)return false;for(int i=0;i<before.Length;i++)if(!Object.ReferenceEquals(owner.owned[i],before[i]))return false;owner.owned.RemoveAll(delegate(R23OwnedConsumable item){foreach(var member in members)if(item.Id==member.ItemId)return true;return false;});done=true;return true;}
            public void Abort(){if(!done)aborted=true;}
        }
        public bool TryStageStore(Transfer transfer,out IStagedStore stage)
        {
            stage=null;if(transfer.Operation!=TransferKind.Store||FreeSlot(transfer.Kind)<0)return false;
            foreach(var item in owned)if(item.Id==transfer.ItemId)return false;
            return TryStageStore(transfer,HeldId,out stage);
        }
        // Bind continuation to this exact ordered slot, not all same-kind items.
        // Source field_consumables consumes one and equips the first survivor.
        public bool TryStageStore(Transfer transfer,string currentHeld,out IStagedStore stage)
        {
            stage=null;int slot=FreeSlot(transfer.Kind);
            if(transfer.Operation!=TransferKind.Store||slot<0||currentHeld!=HeldId)return false;
            foreach(var item in owned)if(item.Id==transfer.ItemId)return false;
            var members=new List<string>();foreach(var item in owned)if(item.Slot==slot)members.Add(item.Id);members.Add(transfer.ItemId);
            stage=new Store(this,transfer,slot,new StackSelection(currentHeld,null,members.ToArray()));return true;
        }
        public bool TryStageConsume(Transfer transfer,string currentHeld,out IStagedConsume stage)
        {
            stage=null;if(transfer.Operation!=TransferKind.Consume||currentHeld!=HeldId||currentHeld!=transfer.ItemId)return false;
            var consumed=owned.Find(delegate(R23OwnedConsumable item){return item.Id==transfer.ItemId&&item.Kind==transfer.Kind;});if(consumed==null)return false;
            var members=new List<string>();string next=null;foreach(var item in owned)if(item.Slot==consumed.Slot){members.Add(item.Id);if(item.Id!=consumed.Id&&next==null)next=item.Id;}
            if(members.Count<1||members.Count>2)return false;
            stage=new Consume(this,transfer,new StackSelection(currentHeld,next,members.ToArray()));return true;
        }
        bool SameOwned(R23OwnedConsumable[] before,PickupRules beforeRules,string beforeHeld)
        {if(rules!=beforeRules||HeldId!=beforeHeld||owned.Count!=before.Length)return false;for(int i=0;i<before.Length;i++)if(!Object.ReferenceEquals(before[i],owned[i]))return false;return true;}
        sealed class Consume:IStagedConsume
        {
            readonly R23ConsumableState owner;readonly Transfer transfer;readonly R23OwnedConsumable[] before;readonly PickupRules beforeRules;readonly string held;bool done,aborted;
            public ConsumeReceipt Receipt {get;private set;}
            internal Consume(R23ConsumableState owner,Transfer transfer,StackSelection selection){this.owner=owner;this.transfer=transfer;before=owner.owned.ToArray();beforeRules=owner.rules;held=selection.BeforeHeld;Receipt=new ConsumeReceipt(transfer,selection);}
            public AtomicOutcome TryCommit(){if(done||aborted||!owner.SameOwned(before,beforeRules,held))return AtomicOutcome.Failed;if(!owner.TryCommit(transfer))return AtomicOutcome.Failed;done=true;return AtomicOutcome.Applied;}
            public void Abort(){if(!done)aborted=true;}
        }
        sealed class Store:IStagedStore
        {
            readonly R23ConsumableState owner;readonly Transfer transfer;readonly int slot;readonly R23OwnedConsumable[] before;readonly PickupRules beforeRules;readonly string held;bool done,aborted;
            public StoreReceipt Receipt {get;private set;}
            internal Store(R23ConsumableState owner,Transfer transfer,int slot,StackSelection selection){this.owner=owner;this.transfer=transfer;this.slot=slot;before=owner.owned.ToArray();beforeRules=owner.rules;held=selection.BeforeHeld;Receipt=new StoreReceipt(transfer,new EvictedInstance[0],selection);}
            public bool TryCommit(){if(done||aborted||!owner.SameOwned(before,beforeRules,held)||owner.FreeSlot(transfer.Kind)!=slot)return false;owner.owned.Add(new R23OwnedConsumable(transfer.ItemId,transfer.Kind,slot));done=true;return true;}
            public void Abort(){if(!done)aborted=true;}
        }
        public bool TryCommit(Transfer transfer)
        {
            int index=owned.FindIndex(delegate(R23OwnedConsumable item){return item.Id==transfer.ItemId&&item.Kind==transfer.Kind;});
            if(index<0)return false;
            if(transfer.Operation==TransferKind.Consume)
            {
                // Soda starts its actual source 50-point/10-second dose after PickupRules commits.
                if(SharedConsume!=null){if(!SharedConsume(transfer.ItemId))return false;}
                else if(transfer.Kind!="soda"&&!effects.TryApplyGain(transfer.Health,transfer.Shield,transfer.RefillStamina))return false;
            }
            else if(transfer.Operation!=TransferKind.Drop)return false;
            owned.RemoveAt(index);return true;
        }
        public bool TryPlaceDrop(string actor,string item,out Float3 position){position=dropPoint;return actor==Actor&&position.IsFinite;}
        public ShieldProcessing TryAddShield(string actor,float amount)
        {
            if(actor!=Actor)return ShieldProcessing.Failed;
            if(effects.ReadVitals().Shield>=100)return ShieldProcessing.CappedNoOp;
            return effects.TryApplyGain(0,amount,false)?ShieldProcessing.Applied:ShieldProcessing.Failed;
        }
    }
}
