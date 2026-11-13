#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  long appliedFoodReset=-1,foodResetEpoch=-1;
  void SyncFoodReset(){if(peer==null||root==null||root.Consumables==null)return;var lobby=peer.Lobby;if(foodResetEpoch!=lobby.Epoch){foodResetEpoch=lobby.Epoch;appliedFoodReset=-1;}R24LanFoodReset row;if(lobby.TryReadOwnFoodReset(out row)&&row.Sequence>appliedFoodReset&&root.Consumables.ReconcileNetworkPreparationReset(row.Id))appliedFoodReset=row.Sequence;}
 }
}
#endif
