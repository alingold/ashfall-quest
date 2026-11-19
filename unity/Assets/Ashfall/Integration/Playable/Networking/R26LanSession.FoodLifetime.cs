#if UNITY_6000_0_OR_NEWER
using System;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  Action<string> retireFood;Func<string,bool> foodAvailable;
  void BindFoodLifetime(){if(retireFood==null){retireFood=RetireFood;foodAvailable=FoodAvailable;}root.Consumables.SharedRetire=retireFood;peer.Lobby.BindFoodAvailability(foodAvailable);}
  bool FoodAvailable(string id){return root!=null&&root.Consumables!=null&&root.Consumables.CanClaimWorldFood(id);}
  void RetireFood(string id){if(peer!=null&&peer.Lobby.State==R24LanLobbyState.Hosting)peer.Lobby.RetireWorldFood(id);}
 }
}
#endif
