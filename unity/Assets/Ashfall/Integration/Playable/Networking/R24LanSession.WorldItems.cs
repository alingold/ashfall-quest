#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  void WorldWeaponMoved(string id,Vector3 at){if(peer!=null)peer.Lobby.RelocateHostWorldItem("weapon",CanonicalLoot(id),V(at));}
  void WorldFoodMoved(string id,Vector3 at){if(peer!=null)peer.Lobby.RelocateHostWorldItem("food",id,V(at));}
  void WorldSupplyMoved(string id,Vector3 at){if(peer!=null)peer.Lobby.RelocateHostWorldItem("supply",id,V(at));}
  void WorldGrenadeMoved(string id,Vector3 at){if(peer!=null)peer.Lobby.RelocateHostWorldItem("grenade",CanonicalGrenade(id),V(at));}
  void ClearImportedWorldItems(){if(root==null)return;if(root.Gameplay!=null)foreach(var pair in lootAliases)if(pair.Key.StartsWith("lan/",System.StringComparison.Ordinal)&&!root.Gameplay.OwnsWeapon(pair.Key))root.Gameplay.DespawnWorldWeapon(pair.Key);if(root.Explosives!=null)foreach(var pair in grenadeAliases)if(pair.Key.StartsWith("lan/",System.StringComparison.Ordinal))root.Explosives.RetireUnownedWorldItem(pair.Key);}
  void BindWorldItemMotion(){root.Gameplay.WorldWeaponMoved+=WorldWeaponMoved;root.Consumables.WorldItemMoved+=WorldFoodMoved;root.Supplies.WorldItemMoved+=WorldSupplyMoved;root.Explosives.WorldItemMoved+=WorldGrenadeMoved;}
  void UnbindWorldItemMotion(){if(root==null)return;if(root.Gameplay!=null)root.Gameplay.WorldWeaponMoved-=WorldWeaponMoved;if(root.Consumables!=null)root.Consumables.WorldItemMoved-=WorldFoodMoved;if(root.Supplies!=null)root.Supplies.WorldItemMoved-=WorldSupplyMoved;if(root.Explosives!=null)root.Explosives.WorldItemMoved-=WorldGrenadeMoved;}
 }
}
#endif
