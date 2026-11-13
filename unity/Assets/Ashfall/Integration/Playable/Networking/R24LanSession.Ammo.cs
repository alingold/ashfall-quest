#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  readonly Dictionary<string,R24LanAmmoState> appliedAmmo=new Dictionary<string,R24LanAmmoState>();long ammoEpoch=-1;
  void SyncAmmo(){if(peer==null||root==null||root.Gameplay==null||!root.Ready)return;var lobby=peer.Lobby;if(ammoEpoch!=lobby.Epoch){ammoEpoch=lobby.Epoch;appliedAmmo.Clear();}
   foreach(string local in root.Gameplay.OwnedWeaponActionIds){R24LanAmmoState state,prior;if(!lobby.TryReadOwnAmmo(CanonicalLoot(local),out state)||!lobby.OwnsAmmo(state)||state.Profile!=root.Gameplay.ResolveWeaponProfile(local))continue;
    if(appliedAmmo.TryGetValue(local,out prior)&&prior.Epoch==state.Epoch&&prior.Acknowledged>=lobby.AmmoPredictionSequence&&prior.Loaded==state.Loaded&&prior.Left==state.Left&&prior.Right==state.Right&&prior.Reserve==state.Reserve)continue;
    if(root.Gameplay.ReconcileNetworkAmmo(local,state.Loaded,state.Left,state.Right,state.Reserve)){appliedAmmo[local]=state;if(state.Profile=="smaw"&&state.Loaded==0&&state.Reserve==0)root.Gameplay.RetireNetworkSmaw(local);}
   }
  }
 }
}
#endif
