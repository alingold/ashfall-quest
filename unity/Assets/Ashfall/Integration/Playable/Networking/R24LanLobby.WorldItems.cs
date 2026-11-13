using Ashfall.Core;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanLobby {
  // Host-native settling only. There is no wire command exposing this port.
  internal bool RelocateHostWorldItem(string kind,string id,Float3 at){if(State!=R24LanLobbyState.Hosting||SelectedMap==null||!SelectedMap.Active||SelectedMap.Cancelled||!at.IsFinite)return false;
   if(kind=="weapon"){LootEntry item;if(!loot.TryGetValue(id,out item)||item.Owner!="")return false;if(Distance2(item.At,at)<.0000001)return true;item.At=at;PublishLoot(item);return true;}
   if(kind=="food"){R24LanFoodState item;if(!foods.TryGetValue(id,out item)||item.Owner!=""||item.Consumed)return false;if(Distance2(item.Position,at)<.0000001)return true;PublishFood(new R24LanFoodState(id,item.Kind,"",at,false,checked(++foodSequence)));return true;}
   if(kind=="supply"){R24LanSupplyState item;if(!sharedSupplies.TryGetValue(id,out item)||item.Collector!="")return false;if(Distance2(item.Position,at)<.0000001)return true;PublishSupply(new R24LanSupplyState(id,item.Family,item.Amount,at,"",checked(++supplySequence)));return true;}
   if(kind=="grenade"){R24LanGrenadeState item;if(!grenades.TryGetValue(id,out item)||item.Owner!=""||item.Spent)return false;if(Distance2(item.Position,at)<.0000001)return true;PublishGrenade(id,item.Kind,"",at,false);return true;}return false;
  }
 }
}
