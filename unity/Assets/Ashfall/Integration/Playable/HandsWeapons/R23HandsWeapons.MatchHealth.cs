#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23HandsWeapons {
  public HealthSnapshot LocalMatchHealth{get{return round==null?null:round.PlayerHealthState;}}public bool LocalMatchHealthConfigured{get{return round!=null&&round.LocalMatchHealthConfigured;}}
  public void ConfigureLocalMatchHealth(string mode,int teams,int team,int health,int shield,double protection,double downed,double respawn,double clock,float damage){if(round==null)throw new InvalidOperationException("Actual round unavailable");round.ConfigureLocalMatchHealth(mode,teams,team,health,shield,protection,downed,respawn,clock,damage);foreach(var t in targets)t.Refresh(round);}
  public bool ResetMapVitals(){return round!=null&&round.ResetMapVitals();}
  public bool ApplyLocalMatchFall(){return round!=null&&round.ApplyLocalMatchFall();}
  public void AdvanceLocalMatchHealth(double clock){if(round!=null)round.AdvanceLocalMatchHealth(clock);}public bool TryRespawnLocalMatch(){return round!=null&&round.TryRespawnLocalMatch();}public void EndLocalMatchHealth(){if(round!=null)round.EndLocalMatchHealth();}public void SetLocalMatchInfiniteAmmo(bool enabled){if(round!=null)round.SetLocalMatchInfiniteAmmo(enabled);}public int ReadStoredAmmoReserveFamily(string family){return round==null?0:round.ReadStoredAmmoReserveFamily(family);}
  public bool ConfigureLocalMatchLoadout(int[] catalog){
   if(!Ready||catalog==null||catalog.Length==0||catalog.Length>3)return false;
   var profiles=new string[catalog.Length];
   for(int i=0;i<catalog.Length;i++){
    foreach(var profile in weaponIds)if(R23WeaponPickupRules.CatalogIndex(profile)==catalog[i])profiles[i]=profile;
    if(profiles[i]==null)return false;for(int j=0;j<i;j++)if(profiles[i]==profiles[j])return false;
   }
   HolsterCurrent();ownedWeapons.Clear();ownedWeaponOrder.Clear();
   // These are the offline range's legacy starter fixtures, not streamed cache
   // copies. Hiding their art is insufficient: dormant world records deliberately
   // remain claimable. Withdraw the records when the match owns the loadout.
   // Keep their native owners intact; neither selection nor this cleanup grants ammo.
   foreach(var id in starterWorldAtHome){
    WorldWeapon ground;if(!droppedWeapons.TryGetValue(id,out ground))continue;
    if(ground.Visual!=null){ground.Visual.gameObject.SetActive(false);Destroy(ground.Visual.gameObject);}
    droppedWeapons.Remove(id);
   }
   starterWorldAtHome.Clear();
   foreach(var profile in profiles){
    ownedWeapons.Add(profile);ownedWeaponOrder.Add(profile);
    WorldWeapon ground;if(droppedWeapons.TryGetValue(profile,out ground)){
     if(ground.Visual!=null){ground.Visual.gameObject.SetActive(false);Destroy(ground.Visual.gameObject);}
     droppedWeapons.Remove(profile);
    }
   }
   round.SelectWeapon(profiles[0]);activeActionId=profiles[0];selectedWeapon=Array.IndexOf(weaponIds,profiles[0]);starterPickup=false;holstered=true;HasWeapon=false;ApplyWeaponSelection();UpdateWeaponPresentation();return true;
  }
 }
}
#endif
