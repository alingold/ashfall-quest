#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  string inventorySession,inventoryMap;long inventoryEpoch=-1,inventoryDeathEpoch=-1;
  internal void ObserveInventoryLife(long epoch,bool dead){inventoryDeathEpoch=dead?epoch:-1;}
  public bool RetainsLocalInventory(bool retain){if(peer==null)return false;var lobby=peer.Lobby;var map=lobby.SelectedMap;
   return map!=null&&R26InventorySlots.RetainsLocal(true,lobby.SessionId,lobby.MapId,inventorySession,inventoryMap)
    &&(inventoryEpoch==lobby.Epoch||retain&&map.Rules!=null&&map.Round>1&&inventoryEpoch==lobby.Epoch-1&&inventoryDeathEpoch!=inventoryEpoch);}
  internal void RestoreRespawnLoadout(Match.R24PlayableMatchRules rules){
   ResetPickupState();ResetConsumableDrops();pendingClaims.Clear();pendingCatalog.Clear();lootRequests.Clear();
   foreach(int index in rules.StartingCatalog)lootAliases.Remove(R24LanLoadout.Profile(index));
   root.ResetRespawnLoadout(rules);
  }
  public bool PrepareLocalInventory(int[] startingCatalog,bool retain){if(peer==null||root==null||root.Gameplay==null)return false;var lobby=peer.Lobby;if(RetainsLocalInventory(retain)){inventoryEpoch=lobby.Epoch;return true;}
   root.ResetLanLoadoutInput(lobby.SelectedMap.Rules==null);
   if(!root.Gameplay.ConfigureLocalMatchLoadout(startingCatalog))return false;inventoryEpoch=lobby.Epoch;inventorySession=lobby.SessionId;inventoryMap=lobby.MapId;lootAliases.Clear();return true;
  }
 }
}
#endif
