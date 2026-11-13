using Ashfall.Core;

namespace Ashfall.Modules.Pickups
{
    // Implementations are host-only. Pose/eligibility comes from accepted server input,
    // never directly from an interaction packet. Coordinates are Unity-neutral SI.
    public interface IPickupActors
    {
        bool TryRead(string actorId, out ActorFrame frame);
    }
    public sealed class ActorFrame
    {
        public bool Allowed, HandEligible, TransferSafe, Desktop, OtherHandEligible, OtherGrip;
        public Float3 Head, Hand, Forward, LocalHand, OtherLocal, Anchor, HeadLocalAnchor;
        public float ItemUpDot, Health, Shield, Stamina, MaxStamina;
    }
    public enum TransferKind { Store, Drop, Consume, UpgradeBackpack, Supply }
    public sealed class Transfer
    {
        public readonly TransferKind Operation;
        public readonly string ActorId, ItemId, Kind;
        public readonly Float3 Position;
        public readonly float Health, Shield;
        public readonly bool RefillStamina;
        public readonly int Amount;
        public Transfer(TransferKind operation, string actor, string item, string kind,
            Float3 position, float health, float shield, bool refillStamina, int amount)
        { Operation=operation; ActorId=actor; ItemId=item; Kind=kind; Position=position;
          Health=health; Shield=shield; RefillStamina=refillStamina; Amount=amount; }
    }
    public sealed class EvictedInstance
    {
        public readonly string ItemId, Kind, SemanticOwnerId;
        public readonly Float3 Position;
        public EvictedInstance(string itemId,string kind,string semanticOwnerId,Float3 position)
        { ItemId=itemId;Kind=kind;SemanticOwnerId=semanticOwnerId;Position=position; }
    }
    public sealed class StoreReceipt
    {
        public readonly Transfer Incoming;
        public readonly StackSelection Selection;
        private readonly EvictedInstance[] evicted;
        public StoreReceipt(Transfer incoming,EvictedInstance[] instances)
            : this(incoming,instances,null) { }
        public StoreReceipt(Transfer incoming,EvictedInstance[] instances,StackSelection selection)
        { Incoming=incoming;evicted=instances==null?null:(EvictedInstance[])instances.Clone();Selection=selection; }
        public int Count { get { return evicted==null?-1:evicted.Length; } }
        public EvictedInstance At(int index) { return evicted[index]; }
    }
    public interface IStagedStore
    {
        StoreReceipt Receipt { get; }
        // Commit exactly this frozen receipt, once, or return false with zero mutation.
        // Hold inventory revision and ALL placement/occupancy reservations until commit
        // or Abort. Competing inventory/geometry changes must cause commit to fail.
        bool TryCommit();
        // Idempotent, nonthrowing release; after success must not undo committed work.
        void Abort();
    }
    public enum ShieldProcessing { Failed, Applied, CappedNoOp }
    // Ordered authoritative slot membership, not all items with the same kind.
    // BeforeHeld binds selection to the current exact Pickups member. Stage must
    // also bind inventory revision/selection; commit rejects intervening changes.
    public sealed class StackSelection
    {
        public readonly string BeforeHeld, Selected;
        private readonly string[] members;
        public StackSelection(string beforeHeld,string selected,string[] orderedMembers)
        { BeforeHeld=beforeHeld;Selected=selected;members=orderedMembers==null?null:(string[])orderedMembers.Clone(); }
        public int Count { get { return members==null?-1:members.Length; } }
        public string At(int index) { return members[index]; }
    }
    public enum AtomicOutcome { Failed, Applied, CappedNoOp }
    public sealed class ConsumeReceipt
    {
        public readonly Transfer Request;
        public readonly StackSelection Selection;
        public ConsumeReceipt(Transfer request,StackSelection selection) { Request=request;Selection=selection; }
    }
    public interface IStagedConsume
    {
        ConsumeReceipt Receipt { get; }
        // Applied atomically removes exact instance + applies all requested effects.
        // Failed/CappedNoOp perform ZERO mutation, including inventory and selection.
        AtomicOutcome TryCommit();
        void Abort();
    }
    public interface ISelectedPickupTransactions
    {
        bool TryStageStore(Transfer request,string currentHeld,out IStagedStore stage);
        // Stage has no visible effects. Freeze consumed slot BEFORE removal, choose
        // its first surviving member in source order, or null. Never another slot.
        bool TryStageConsume(Transfer request,string currentHeld,out IStagedConsume stage);
    }
    public interface IAtomicPickupEffects
    {
        // Non-Store, non-Consume operations only. Applied means full source removal
        // plus upgrade/drop or positive accepted supply, even if partial supply fit.
        // Failed/CappedNoOp mean zero mutation; no Spawn/respawn used for restoration.
        AtomicOutcome CommitEffect(Transfer request);
    }
    public interface IPickupTransactions
    {
        // Atomic, nonthrowing, no callbacks into PickupRules. False means NO mutation.
        // Store MUST use TryStageStore, never this legacy non-Store operation port.
        // Upgrade adds one slot up to 6. Supply accepts >0 reserve/builds, then removes
        // the entire source box, including when only part of its amount fits.
        // Consume removes this exact instance AND applies capped health/shield/stamina.
        bool TryCommit(Transfer transfer);
        // No visible mutations. Stage source make-room/stack selection and every
        // evicted exact instance (weapons too), semantic owner and settled position.
        // Normally false returns null; caller also aborts any partial non-null stage
        // returned on failure. Every non-null stage is released on every path.
        // All ports are nonthrowing and must not call back or publish to observers.
        // Coordinator must serialize inventory/world readers with the host transaction
        // lane through completion of Execute, so commit+world publication is atomic.
        bool TryStageStore(Transfer transfer,out IStagedStore stage);
        // Host geometry must return a reachable, settled position, preserving walls.
        bool TryPlaceDrop(string actorId, string itemId, out Float3 position);
        // Applied includes partial gain; CappedNoOp is SUCCESS at cap. Both drain
        // soda duration. Failed means zero mutation and retries on later active time.
        ShieldProcessing TryAddShield(string actorId, float amount);
    }
}
