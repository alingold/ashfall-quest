using System;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Integration.InventoryTransfers;
namespace Ashfall.Integration.WorldWeaponTransfers
{
    // Explicit adoption for roots that already own the actual legacy inventories.
    // TransferBridge's private resolver/lane are intentionally not guessed or copied.
    public sealed class SessionWeaponWorldAdapter
    {
        readonly object lane;readonly Func<string,Inventory> resolve;readonly WorldWeaponCoordinator coordinator;
        public SessionWeaponWorldAdapter(object hostLane,Func<string,Inventory> actualInventories,WorldWeaponCoordinator coordinator)
        {if(hostLane==null||actualInventories==null||coordinator==null||!coordinator.OwnsLane(hostLane))throw new ArgumentException();lane=hostLane;resolve=actualInventories;this.coordinator=coordinator;}
        public bool PrepareDrop(CommandContext context,IWeaponPrincipal principal,long deadline,string[] slot,out WorldWeaponCoordinator.Operation token)
        {lock(lane)return coordinator.PrepareDrop(context,principal,deadline,resolve(context.ActorId),slot,out token);}
        public bool PrepareCollect(CommandContext context,IWeaponPrincipal principal,long deadline,WorldWeaponRecord[] records,out WorldWeaponCoordinator.Operation token)
        {lock(lane)return coordinator.PrepareCollect(context,principal,deadline,resolve(context.ActorId),records,out token);}
    }
}
