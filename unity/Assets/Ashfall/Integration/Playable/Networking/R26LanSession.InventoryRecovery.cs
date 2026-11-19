#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  readonly List<R26OwnedWeaponReceipt> recoveryWeapons=new List<R26OwnedWeaponReceipt>(6);readonly List<string> recoveryLocals=new List<string>(6);double inventoryMismatchAt=-1,nextInventoryRecovery;
  Func<bool> keepInventoryGuard;R24LanLobby inventoryBoundaryLobby;long inventoryBoundaryEpoch=-1;
  bool KeepNetworkInventory(){var lobby=peer==null?null:peer.Lobby;var map=lobby==null?null:lobby.SelectedMap;return map!=null&&!map.Cancelled&&!map.Home&&map.SourceRules!=null&&RetainsLocalInventory(map.SourceRules.RetainInventory);}
  void GuardNetworkInventory(){try{GuardNetworkInventoryCore();}catch(Exception error){Debug.LogException(error);throw;}}
  void GuardNetworkInventoryCore(){
   if(root!=null&&root.Gameplay!=null){if(keepInventoryGuard==null)keepInventoryGuard=KeepNetworkInventory;root.Gameplay.PreserveNetworkInventory=keepInventoryGuard;}
   var map=peer==null?null:peer.Lobby.SelectedMap;
   if(peer!=null&&map!=null&&(!ReferenceEquals(inventoryBoundaryLobby,peer.Lobby)||inventoryBoundaryEpoch!=peer.Lobby.Epoch)){inventoryBoundaryLobby=peer.Lobby;inventoryBoundaryEpoch=peer.Lobby.Epoch;
    if(!KeepNetworkInventory()){
     // A new host inventory must not rediscover free rows from the retired world.
     // In particular actor/cx4 would collide with the new cx4 starter's first drop.
     if(root!=null&&root.Gameplay!=null){if(root.WorldLoot!=null)root.WorldLoot.WithdrawMap();root.Gameplay.CopyWorldWeaponActionIds(weaponIdBuffer);foreach(string id in weaponIdBuffer)root.Gameplay.WithdrawNetworkWorldCopy(id);weaponIdBuffer.Clear();}
     inventorySession=inventoryMap=null;lootAliases.Clear();ResetPickupState();
    }
   }
   if(peer==null){inventoryBoundaryLobby=null;inventoryBoundaryEpoch=-1;}
   // HOME clears combat on the host even though session and selected map names survive.
   if(peer==null||map==null||map.Home||map.Cancelled){inventorySession=inventoryMap=null;inventoryMismatchAt=-1;}
  }
  void RecoverNetworkInventory(R24LanLobby lobby,double now){ReconcileHostOwnInventory(lobby,now);
   if(now<nextInventoryRecovery)return;nextInventoryRecovery=now+.25;
   if(!root.LanPickupActorReady||root.LanSession.MapEntryBlocked||!KeepNetworkInventory()||pendingDrops.Count>0||pendingCatalog.Count>0){inventoryMismatchAt=-1;return;}
   lobby.CopyOwnWeaponReceipts(recoveryWeapons);for(int i=recoveryWeapons.Count-1;i>=0;i--){PickupWatch watch;if(pickupWatches.TryGetValue(recoveryWeapons[i].Id,out watch)&&!root.Gameplay.OwnsWeapon(watch.Local))recoveryWeapons.RemoveAt(i);}if(recoveryWeapons.Count>6){inventoryMismatchAt=-1;return;}recoveryLocals.Clear();
   bool same=root.Gameplay.OwnedWeaponActionIds.Length==recoveryWeapons.Count;
   foreach(var receipt in recoveryWeapons){string local=receipt.Id;foreach(string owned in root.Gameplay.OwnedWeaponActionIds)if(CanonicalLoot(owned)==receipt.Id){local=owned;break;}
    R24LanLootState state;if(!root.Gameplay.OwnsWeapon(local)&&lobby.TryReadLoot(receipt.Id,out state))local=LocalSharedWeapon(state);
    R23WeaponInstanceInfo info;if(root.Gameplay.TryReadWeaponInstance(local,out info)&&(info.ProfileId!=receipt.Profile||info.Stars!=receipt.Stars))local=R24LanWorldIdentity.LocalId(lobby.SessionId,lobby.MapManifestHash,"weapon",receipt.Id,receipt.Profile,receipt.Stars);
    recoveryLocals.Add(local);same&=root.Gameplay.OwnsWeapon(local);
   }
   if(same){inventoryMismatchAt=-1;return;}if(inventoryMismatchAt<0){inventoryMismatchAt=now;return;}if(now-inventoryMismatchAt<2)return;
   // A sustained discrepancy is repaired from host custody, never from a local starter reset.
   // Uncollected grants are excluded above, so a released grip never becomes an automatic collect.
   // A refused/retried watch cannot suspend recovery of settled inventory.
   string held=root.Gameplay.HasWeapon?root.Gameplay.ActiveWeaponActionId:null;
   foreach(string local in root.Gameplay.OwnedWeaponActionIds)if(!recoveryLocals.Contains(local))root.Gameplay.RevokeNetworkOwned(local);
   for(int i=0;i<recoveryWeapons.Count;i++){var receipt=recoveryWeapons[i];string local=recoveryLocals[i];if(!root.Gameplay.RestoreNetworkOwned(local,receipt.Profile,receipt.Stars,receipt.Loaded,receipt.Left,receipt.Right,receipt.Reserve))continue;lootAliases[local]=receipt.Id;lootLocals[receipt.Id]=local;}
   if(held!=null&&!root.Gameplay.OwnsWeapon(held)&&recoveryLocals.Count>0)root.Gameplay.EquipOwned(recoveryLocals[0],root.Gameplay.PrimaryHand);
   inventoryMismatchAt=-1;Debug.Log("ASHFALL pickup: repaired local weapon custody from host receipts after 2s discrepancy");
  }
 }
}
#endif
