#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable {
 public sealed partial class R23PlayableLoop {
  internal void ResetLanLoadoutInput(bool warmup){
   if(WeaponWheel!=null)WeaponWheel.Cancel();queuedEquip=queuedDrop=selectedWeapon=wheelItemDrop=null;reloadRequested=false;StowPaddles();
   // Match entry resets these owners before binding Health. Warm-up has no round start.
   if(warmup){Gameplay.ResetRound();Consumables.ResetRound();if(Explosives!=null)Explosives.ResetRound();inventoryCapacity=3;RefreshSlotBudget();}
  }
  public bool ApplyLanResources(R24LanResourcesState state){if(LanSession==null||LanSession.Lobby==null||!LanSession.Lobby.OwnsResources(state)||Gameplay==null||Activities==null)return false;foreach(var entry in state.Reserves)if(!Gameplay.ReconcileNetworkReserve(entry.Key,entry.Value))return false;if(!Activities.ReconcileNetworkBalance(state.Builds))return false;inventoryCapacity=state.Capacity;RefreshSlotBudget();return true;}
 }
}
#endif
