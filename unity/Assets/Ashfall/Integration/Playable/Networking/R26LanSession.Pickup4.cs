#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using Ashfall.Core;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  // PICKUP4 evidence seam: the host bot opens warmup through the normal host map path.
  internal bool Pickup4OpenWarmup(string id){if(R25NetBot.Arg("-ashfallNetBotPickup4")!="true"||UnityEngine.XR.XRSettings.isDeviceActive||!Hosting||mapWorker!=null)return false;var choice=World.R23MapCatalog.Find(id);if(choice==null)return false;preparingMap=true;mapWorker=StartCoroutine(PrepareHostMap(choice,peer));return true;}
  internal void PublishPickup4Tracking(){if(R25NetBot.Arg("-ashfallNetBotPickup4")=="true")PublishRawTracking();}
  // PICKUP4 (r15 P0): host custody of the host's own actor never exceeds what its local player holds.
  // An own grant whose item still lies unowned in the local world (the local collect refused after the
  // host admitted it) is released after a short grace. The PICKUP3 watchdog could not repair this: it
  // restores locally, which RestoreNetworkOwned refuses while the world copy exists.
  const double OwnGhostGrace=.75;readonly Dictionary<string,double> ownGhostSince=new Dictionary<string,double>(System.StringComparer.Ordinal);readonly HashSet<string> ownGhostSeen=new HashSet<string>(System.StringComparer.Ordinal);
  readonly List<R26OwnedWeaponReceipt> ownGhostWeapons=new List<R26OwnedWeaponReceipt>();readonly List<string> ownGhostIds=new List<string>(),ownGhostDone=new List<string>();double nextOwnGhost;
  public int OwnGhostReleases{get;private set;}
  void ReconcileHostOwnInventory(R24LanLobby lobby,double now){
   if(lobby.State!=R24LanLobbyState.Hosting||!root.LanPickupActorReady||root.LanSession.MapEntryBlocked){ownGhostSince.Clear();return;}if(now<nextOwnGhost)return;nextOwnGhost=now+.25;ownGhostSeen.Clear();
   lobby.CopyOwnWeaponReceipts(ownGhostWeapons);
   foreach(var receipt in ownGhostWeapons){bool held=false;foreach(string owned in root.Gameplay.OwnedWeaponActionIds)if(owned==receipt.Id||CanonicalLoot(owned)==receipt.Id){held=true;break;}if(held)continue;
    string local;if(!lootLocals.TryGetValue(receipt.Id,out local))local=receipt.Id;string instance;Vector3 center,p;Quaternion q;if(!root.Gameplay.TryReadWorldWeapon(local,out instance,out center,out p,out q)&&!(local!=receipt.Id&&root.Gameplay.TryReadWorldWeapon(local=receipt.Id,out instance,out center,out p,out q)))continue;
    OwnGhost("weapon/"+receipt.Id,now,delegate{return lobby.ReleaseOwnUncollectedWeapon(receipt.Id,V(p));});}
   if(root.Consumables!=null){lobby.CopyOwnFoodIds(ownGhostIds);foreach(string id in ownGhostIds){var info=root.Consumables.InspectItem(id);Vector3 p;Quaternion q;bool owned;if(info==null||info.Removed||info.Owner!=null||!root.Consumables.TryReadWorldItem(id,out p,out q,out owned)||owned)continue;string food=id;Vector3 at=p;OwnGhost("food/"+id,now,delegate{return lobby.ReleaseOwnUncollectedFood(food,V(at));});}}
   if(root.Explosives!=null){lobby.CopyOwnGrenadeIds(ownGhostIds);foreach(string id in ownGhostIds){bool mine=false;foreach(var item in root.Explosives.OwnedItems)if(item.Id==id){mine=true;break;}Vector3 p;Quaternion q;bool owned;if(mine||!root.Explosives.TryReadWorldItem(id,out p,out q,out owned)||owned)continue;string grenade=id;Vector3 at=p;OwnGhost("grenade/"+id,now,delegate{return lobby.ReleaseOwnUncollectedGrenade(grenade,V(at));});}}
   ownGhostDone.Clear();foreach(string key in ownGhostSince.Keys)if(!ownGhostSeen.Contains(key))ownGhostDone.Add(key);foreach(string key in ownGhostDone)ownGhostSince.Remove(key);
  }
  void OwnGhost(string key,double now,System.Func<bool> release){ownGhostSeen.Add(key);double since;if(!ownGhostSince.TryGetValue(key,out since)){ownGhostSince[key]=now;return;}if(now-since<OwnGhostGrace)return;ownGhostSince.Remove(key);if(!release())return;OwnGhostReleases++;Debug.Log("ASHFALL pickup4: host released its own uncollected custody of '"+key+"' (local item still on the ground)");}
 }
}
#endif
