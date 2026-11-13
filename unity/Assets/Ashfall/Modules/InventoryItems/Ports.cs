using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.InventoryItems
{
    // Coordinator supplies authority/alive/tracking/input/reload and spatial eligibility.
    // Validation must be side-effect-free. The module never spawns, consumes or reloads.
    public interface IInventoryBoundary
    {
        RuleResult Validate(CommandContext context, InventoryOperation operation,
            ItemInstance incoming, ItemInstance[] outgoing);
    }
    // Required in addition to pure eligibility validation; fail closed when absent.
    public interface IInventoryTransactions : IInventoryBoundary
    {
        RuleResult Stage(CommandContext context, InventoryChange change, out IInventoryReceipt receipt);
    }
    // Stage reserves incoming + ALL exact Removed IDs, equip and lifetime effects together.
    // Stage/Commit may reject or throw. Return a receipt before staging reversible work.
    // Abort must be nonthrowing, idempotent, and undo ALL reservations/partial effects,
    // including a failed Commit. Successful Commit publishes atomically and must not throw
    // after publication. Implementations must serialize access on the simulation thread.
    public interface IInventoryReceipt
    {
        RuleResult Commit();
        void Abort();
    }
    public sealed class InventoryChange
    {
        public readonly InventoryOperation Operation;
        public readonly ItemInstance Incoming;
        private readonly ItemInstance[] removed;
        public ItemInstance[] Removed { get { return (ItemInstance[])removed.Clone(); } }
        public readonly string PreviousSelectedId, SelectedId;
        public readonly bool PreviouslyHolstered, Holstered;
        public readonly int PreviousCapacity, Capacity;
        public bool SelectionChanged { get { return PreviousSelectedId!=SelectedId || PreviouslyHolstered!=Holstered; } }
        // Equip effects apply only to a real draw/selection transition. Central bridge stages
        // old/new gesture suspension, consumable/revive clearing, fresh trigger and delay.
        public bool RequireFreshTrigger { get { return SelectionChanged && !Holstered; } }
        public double DrawDelaySeconds { get { return RequireFreshTrigger ? 0.25 : 0; } }
        // Incoming/Removed notify entry/exit of carried ownership, NEVER weapon creation or
        // destruction. Preserve external per-instance ammo/reload/reserve and dual-hand state.
        internal InventoryChange(InventoryOperation operation,ItemInstance incoming,ItemInstance[] removed,
            string previous,string selected,bool wasHolstered,bool holstered,int oldCapacity,int capacity)
        { Operation=operation; Incoming=incoming; this.removed=(ItemInstance[])removed.Clone();
          PreviousSelectedId=previous; SelectedId=selected; PreviouslyHolstered=wasHolstered;
          Holstered=holstered; PreviousCapacity=oldCapacity; Capacity=capacity; }
    }
    public sealed class InventorySnapshot
    {
        public readonly string SessionId, ActorId, SelectedInstanceId;
        public readonly long Epoch;
        public readonly int Capacity;
        public readonly bool Holstered, Handoff;
        internal readonly ItemInstance[] Members, Known;
        internal readonly ItemInstance[][] Groups;
        public ItemInstance[] Items { get { return (ItemInstance[])Members.Clone(); } }
        internal InventorySnapshot(string session,string actor,long epoch,int capacity,string selected,bool holstered,
            ItemInstance[] items,List<List<ItemInstance>> groups,Dictionary<string,ItemInstance> known,bool handoff)
        {
            SessionId=session; ActorId=actor; Epoch=epoch; Capacity=capacity; SelectedInstanceId=selected; Holstered=holstered;
            Handoff=handoff; Members=items; Groups=new ItemInstance[groups.Count][];
            for(int n=0;n<groups.Count;n++) Groups[n]=groups[n].ToArray();
            Known=new List<ItemInstance>(known.Values).ToArray();
        }
    }
    public enum InventoryOperation { Admit, Remove, ReplaceSlot, Equip, Holster, UpgradeCapacity }
    public sealed class ItemInstance
    {
        public readonly string InstanceId, DefinitionId;
        public readonly int Stars;
        public ItemInstance(string instanceId, string definitionId, int stars)
        { InstanceId = instanceId; DefinitionId = definitionId; Stars = stars; }
    }
}
