#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  double nextEquipment;long equipmentEpoch=-1;string publishedWeapon;int publishedHand=-1;bool publishedSuppressed,publishedBlocked;
  void PublishEquipment(){if(peer==null||root==null||root.Gameplay==null)return;var game=root.Gameplay;string id=game.HasWeapon?CanonicalLoot(game.ActiveWeaponActionId):null;int hand=game.PrimaryHand;bool suppressed=game.NetworkCombatSuppressed,blocked=game.NetworkHandsBlocked;double now=Time.realtimeSinceStartupAsDouble;if(equipmentEpoch==peer.Lobby.Epoch&&publishedWeapon==id&&publishedHand==hand&&publishedSuppressed==suppressed&&publishedBlocked==blocked&&now<nextEquipment)return;if(peer.Lobby.RequestEquipment(id,hand,suppressed,blocked)){equipmentEpoch=peer.Lobby.Epoch;publishedWeapon=id;publishedHand=hand;publishedSuppressed=suppressed;publishedBlocked=blocked;nextEquipment=now+.1;}}
 }
}
#endif
