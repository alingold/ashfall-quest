#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23HandsWeapons {
  // Local menu/round cleanup must never replace custody which the LAN host retained.
  internal Func<bool> PreserveNetworkInventory;
  internal bool NetworkInventoryRetained {get{return PreserveNetworkInventory!=null&&PreserveNetworkInventory();}}
  internal bool RestoreNetworkWorldCopy(string id,Vector3 position){
   if(ownedWeapons.Contains(id)||droppedWeapons.ContainsKey(id))return false;R23WeaponInstanceInfo info;if(!round.TryReadWeaponInstance(id,out info))return false;int index=Array.IndexOf(weaponIds,info.ProfileId);if(index<0)return false;
   // Metadata can survive a fresh inventory preparation. Recreate the missing world
   // projection without minting a second native owner or granting local custody.
   var record=new WorldWeapon{Position=position,Rotation=Quaternion.identity,Visible=true};droppedWeapons.Add(id,record);if(WorldVisualNow(id,record))PresentWorldVisual(id,record);UpdateWeaponPresentation();return true;
  }
  internal bool WithdrawNetworkWorldCopy(string id){
   // Bare profile IDs are persistent presentation templates (e.g. PresentShotgun reads
   // m1014 while another gun is selected). Withdraw their prop, never retire the owner.
   if(id!=ResolveWeaponProfile(id))return DespawnWorldWeapon(id);WorldWeapon ground;
   if(droppedWeapons.TryGetValue(id,out ground)){if(ground.Visual!=null){ground.Visual.gameObject.SetActive(false);Destroy(ground.Visual.gameObject);}droppedWeapons.Remove(id);}starterWorldAtHome.Remove(id);
   if(id==activeActionId){starterPickup=false;if(!HasWeapon){holstered=true;weapon.gameObject.SetActive(false);}}UpdateWeaponPresentation();return true;
  }
  internal void RevokeNetworkOwned(string id){if(!ownedWeapons.Contains(id))return;if(id==activeActionId&&HasWeapon)HolsterCurrent();round.SuspendWeapon(id);ownedWeapons.Remove(id);ownedWeaponOrder.Remove(id);UpdateWeaponPresentation();}
  internal bool RestoreNetworkOwned(string id,string profile,int stars,int loaded,int leftAmmo,int rightAmmo,int reserve){
   R23WeaponInstanceInfo info;if(!round.TryReadWeaponInstance(id,out info)){if(!round.RegisterWeaponInstance(id,profile,stars))return false;}else if(info.ProfileId!=profile||info.Stars!=stars)return false;
   if(!round.ReconcileNetworkAmmo(id,loaded,leftAmmo,rightAmmo,reserve))return false;
   WorldWeapon ground;if(droppedWeapons.TryGetValue(id,out ground)){if(ground.Visual!=null){ground.Visual.gameObject.SetActive(false);Destroy(ground.Visual.gameObject);}droppedWeapons.Remove(id);}
   starterWorldAtHome.Remove(id);if(ownedWeapons.Add(id))ownedWeaponOrder.Add(id);UpdateWeaponPresentation();return true;
  }
 }
}
#endif
