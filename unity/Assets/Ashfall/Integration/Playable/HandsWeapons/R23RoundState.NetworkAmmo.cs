using System;using Ashfall.Modules.WeaponHandling;using Ashfall.Integration.DesktopPlaytest.Combat;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23RoundState {
  internal bool ReconcileNetworkReserve(string family,int value){IRevisionedAmmoReserve actual;if(value<0||family==null||!reserves.TryGetValue(family,out actual))return false;var policy=actual as R24MatchAmmoReserve;if(policy!=null)return policy.ReconcileNetworkReserve(value);int current=actual.Available;if(current==value)return true;return (current<value?actual.Refund(value-current):actual.Take(current-value))==Math.Abs((long)current-value);}
  // Only authenticated LAN snapshot composition calls this internal projection.
  // It updates retained owners; it never registers, clones or retires a weapon.
  internal bool ReconcileNetworkAmmo(string id,int loaded,int left,int right,int reserve){R23WeaponInstanceInfo info;if(retiredRound||!instances.TryGetValue(id,out info)||loaded<0||loaded>info.Capacity||reserve<0)return false;bool done=false;string profile=info.ProfileId;if(profile=="grapple")return loaded==1&&reserve==0;R24SmawRuntime rocket;if(smaws.TryGetValue(id,out rocket))return rocket.ReconcileNetworkAmmo(loaded,reserve);if(knives.ContainsKey(id))return loaded==0;
   LocalRifleSandbox rifle;R23MagnumRuntime magnum;R23M1014Owner shotgun;R24Dt11Owner dt;R24AwpOwner awp;R24M60Owner m60;R24DualOwner dual;
   if(rifles.TryGetValue(id,out rifle))done=rifle.ReconcileNetworkAmmo(loaded);else if(magnums.TryGetValue(id,out magnum))done=magnum.ReconcileNetworkAmmo(loaded);else if(shotguns.TryGetValue(id,out shotgun))done=shotgun.ReconcileNetworkAmmo(loaded);else if(dt11s.TryGetValue(id,out dt))done=dt.ReconcileNetworkAmmo(loaded);else if(awps.TryGetValue(id,out awp))done=awp.ReconcileNetworkAmmo(loaded);else if(m60s.TryGetValue(id,out m60))done=m60.ReconcileNetworkAmmo(loaded);else if(duals.TryGetValue(id,out dual)&&left>=0&&right>=0&&left+right==loaded)done=dual.ReconcileNetworkAmmo(left,right);
   if(!done)return false;string family=profile=="magnum"?"pistol":profile=="dt11"||profile=="m1014"||profile=="matadors"?"shells":profile=="uzis"?"smg":SourceRifleDefinition.ForId(profile).AmmoFamily;IRevisionedAmmoReserve actual;
   if(!reserves.TryGetValue(family,out actual))return false;var policy=actual as R24MatchAmmoReserve;if(policy!=null)return policy.ReconcileNetworkReserve(reserve);int current=actual.Available;if(current==reserve)return true;return (current<reserve?actual.Refund(reserve-current):actual.Take(current-reserve))==Math.Abs((long)current-reserve);
  }
 }
}
