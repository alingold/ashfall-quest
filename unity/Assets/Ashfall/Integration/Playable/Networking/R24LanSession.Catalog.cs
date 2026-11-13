#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using UnityEngine;using Ashfall.Integration.Playable.UI;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  sealed class PendingCatalog {internal R24CreativeRequest Request;internal int Hand;internal double Until,Next;}
  readonly List<string> completedCatalog=new List<string>();
  long catalogEpoch=-1;readonly Dictionary<string,PendingCatalog> pendingCatalog=new Dictionary<string,PendingCatalog>();
  public bool QueueCatalog(R24CreativeRequest request,int hand,Vector3 at){if(peer==null||request==null)return false;if(catalogEpoch!=peer.Lobby.Epoch){catalogEpoch=peer.Lobby.Epoch;pendingCatalog.Clear();}PublishRawTracking();string id;if(!peer.Lobby.RequestCatalog(request.Kind,request.Value,request.Stars,hand,V(at),out id))return false;pendingCatalog[id]=new PendingCatalog{Request=request,Hand=hand,Until=Time.realtimeSinceStartupAsDouble+10};return true;}
  void SyncCatalog(){if(peer==null||root==null||!root.Ready)return;if(catalogEpoch!=peer.Lobby.Epoch){catalogEpoch=peer.Lobby.Epoch;pendingCatalog.Clear();}if(pendingCatalog.Count==0)return;var done=completedCatalog;done.Clear();double now=Time.realtimeSinceStartupAsDouble;foreach(var entry in pendingCatalog){var pending=entry.Value;var request=pending.Request;bool spawned=false;R24LanLootState weapon;R24LanSupplyState supply;R24LanFoodState food;R24LanGrenadeState grenade;spawned=peer.Lobby.TryReadLoot(entry.Key,out weapon)||peer.Lobby.TryReadSharedSupply(entry.Key,out supply)||peer.Lobby.TryReadFood(entry.Key,out food)||peer.Lobby.TryReadGrenade(entry.Key,out grenade);
    if(spawned&&(request.Drop||request.Kind=="supply")){root.UI.SetLoadoutStatus("PLACED "+request.Value.ToUpperInvariant());done.Add(entry.Key);}else if(spawned&&now>=pending.Next){pending.Next=now+.15;string local=entry.Key;if(request.Kind=="weapon"&&Ashfall.Integration.Playable.Explosives.R23GrenadeState.ValidKind(request.Value)){R24LanGrenadeState item;if(peer.Lobby.TryReadGrenade(entry.Key,out item))local=LocalSharedGrenade(item);}else if(request.Kind=="weapon"){R24LanLootState item;if(peer.Lobby.TryReadLoot(entry.Key,out item))local=LocalSharedWeapon(item);}if(root.CompleteLanCatalogPickup(local,request.Kind,request.Value,pending.Hand)){root.UI.SetLoadoutStatus("SELECTED "+request.Value.ToUpperInvariant());done.Add(entry.Key);}}
    if(now>pending.Until){root.UI.SetLoadoutStatus("CATALOG REQUEST NOT COMPLETED BY HOST");done.Add(entry.Key);}}
   foreach(string id in done)pendingCatalog.Remove(id);done.Clear();
  }
 }
}
#endif
