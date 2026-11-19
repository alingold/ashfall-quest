#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable {
 public sealed partial class R23PlayableLoop {
  // MP-PICKUP: a shared-world collect with a full inventory swaps like single-player.
  // The session runs the same make_room before asking the host, so the ordered command
  // lane carries the held item's drop ahead of the collect (the host requires a free slot).
  internal bool LanWeaponFits(){if(!Ready||Gameplay==null)return true;RefreshSlotBudget();return CanCollectWeapon();}
  internal bool LanMakeRoomForWeapon(string incoming){return MakeRoomForPickup(incoming,true);}
  // Local presentation gate for greyed-out pickups; admission stays host-owned.
  internal bool LanPickupActorReady{get{return Ready&&roundStarted&&!switchingMap&&Gameplay!=null&&Gameplay.Health>0&&(Gameplay.LocalMatchHealth==null||Gameplay.LocalMatchHealth.Life==Ashfall.Modules.HealthStates.LifeState.Alive);}}
 }
}
#endif
