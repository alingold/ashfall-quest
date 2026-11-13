#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  string inventorySession,inventoryMap;
  public bool PrepareLocalInventory(int[] startingCatalog,bool retain){if(peer==null||root==null||root.Gameplay==null)return false;var lobby=peer.Lobby;if(retain&&inventorySession==lobby.SessionId&&inventoryMap==lobby.MapId)return true;
   if(!root.Gameplay.ConfigureLocalMatchLoadout(startingCatalog))return false;inventorySession=lobby.SessionId;inventoryMap=lobby.MapId;lootAliases.Clear();return true;
  }
 }
}
#endif
