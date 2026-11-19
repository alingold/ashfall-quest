#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.HandsWeapons;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  readonly Dictionary<string,string> lootAliases=new Dictionary<string,string>(StringComparer.Ordinal);
  readonly Dictionary<string,double> lootRequests=new Dictionary<string,double>(StringComparer.Ordinal);long lootEpoch=-1;
  string CanonicalLoot(string local){string id;return lootAliases.TryGetValue(local,out id)?id:local;}
  bool RequestLootOnce(string id,int hand,bool drop,Float3 at){double now=Time.realtimeSinceStartupAsDouble,last;string key=(drop?"drop/":"get/")+id;if(lootRequests.TryGetValue(key,out last)&&now-last<.5)return false;lootRequests[key]=now;PublishRawTracking();return peer.Lobby.RequestLoot(id,hand,drop,at);}
  public bool AllowWeaponClaim(string local,int hand){if(peer==null)return true;var lobby=peer.Lobby;if(lobby.State==R24LanLobbyState.Connected&&!lootAliases.ContainsKey(local))return Pickup3Refused(local,"unmapped");if(lobby.SelectedMap==null||lobby.SelectedMap.SourceRules==null)return Pickup3Refused(local,"no-rules");string id=CanonicalLoot(local);R24LanLootState state;if(!lobby.TryReadLoot(id,out state))return Pickup3Refused(local,"unknown");if(heldSwap!=null&&!(heldSwap.DropApplied&&heldSwap.Local==local))return false;if(state.Owner==lobby.OwnActor)return true;if(state.Owner!="")return Pickup3Refused(local,"claimed:"+state.Owner);if(!root.LanWorldClaimIntent("weapon",local,hand))return false;if(UseHeldWeaponSwap())return StartHeldWeaponSwap(local,state,hand);if(!PrepareWeaponRoom(id,local,hand))return Pickup3Refused(local,"no-room:"+lobby.LastPickupRejection);RequestLootOnce(id,hand,false,state.Position);WatchPickup(local,id,hand);bool accepted=lobby.TryReadLoot(id,out state)&&state.Owner==lobby.OwnActor;if(!accepted){TrackClaim("weapon",local,id,state.Profile,hand);Pickup3Refused(local,"awaiting-host-receipt");}return accepted;}
  bool AllowWeaponDrop(string local,Vector3 at){if(peer==null||local==confirmedSwapDrop)return true;string id=CanonicalLoot(local);var lobby=peer.Lobby;R24LanLootState state;
   // Initial loadout action IDs are actor-scoped; host chooses a global drop ID.
   string global=id;if(!lobby.TryReadLoot(id,out state))global=lobby.StarterDropId(lobby.OwnActor,id);
   PendingDrop pending;int loaded,reserve;bool knownAlias=lootAliases.ContainsKey(local),waiting=pendingDrops.TryGetValue(global,out pending);long beforeRevision=lobby.TryReadLoot(global,out state)?state.Revision:-1;
   if(state!=null&&state.Owner==""&&R26DropReceipt.Fresh(lobby.State==R24LanLobbyState.Hosting,lobby.State==R24LanLobbyState.Hosting&&lobby.TryReadAmmo(lobby.OwnActor,id,out loaded,out reserve),knownAlias,waiting,waiting?pending.BeforeRevision:-1,state.Revision)&&(new Vector3(state.Position.X,state.Position.Y,state.Position.Z)-at).sqrMagnitude<1){BindDroppedAlias(local,global);return true;}
   bool requested=RequestLootOnce(id,root.Gameplay.PrimaryHand,true,V(at));if(lobby.State==R24LanLobbyState.Hosting&&requested&&lobby.TryReadLoot(global,out state)&&state.Owner==""){BindDroppedAlias(local,global);return true;}NotePendingDrop(local,global,waiting?pending.BeforeRevision:beforeRevision);return Pickup3Refused(local,lobby.State==R24LanLobbyState.Hosting?lobby.LastPickupRejection:"awaiting-drop-receipt");
  }
  public bool AuthorizeSupply(string receipt,string family,int amount){if(peer==null)return true;if(!receipt.StartsWith("pickup089:",StringComparison.Ordinal))return false;string id=receipt.Substring(10);R24LanSupplyState item;
   if(!peer.Lobby.TryReadSharedSupply(id,out item)||item.Family!=family||item.Amount!=amount)return false;if(item.Collector==peer.Lobby.OwnActor)return true;if(item.Collector!="")return false;
   double now=Time.realtimeSinceStartupAsDouble,last;string key="supply/"+id;if(lootRequests.TryGetValue(key,out last)&&now-last<.5)return false;lootRequests[key]=now;PublishRawTracking();
   int hand=Vector3.Distance(root.Rig.Left.position,new Vector3(item.Position.X,item.Position.Y,item.Position.Z))<Vector3.Distance(root.Rig.Right.position,new Vector3(item.Position.X,item.Position.Y,item.Position.Z))?0:1;peer.Lobby.RequestSupply(id,hand);WatchSupply(id,hand);
   return peer.Lobby.TryReadSharedSupply(id,out item)&&item.Collector==peer.Lobby.OwnActor;
  }
  void SyncSupplies(){if(root.Supplies==null)return;var lobby=peer.Lobby;double now=Time.realtimeSinceStartupAsDouble;supplySync.Begin(lobby,lobby.Epoch,now,root.Supplies.SyncWorldItemCount);
   if(supplySync.Discover&&lobby.State==R24LanLobbyState.Hosting){root.Supplies.CopyWorldItemIds(supplyIdBuffer);foreach(string id in supplyIdBuffer){R24LanSupplyState known;if(lobby.TryReadSharedSupply(id,out known))continue;Ashfall.Integration.Playable.Consumables.R24SupplyItem item;Vector3 at;Quaternion rotation;bool collected;if(root.Supplies.TryReadItem(id,out item)&&!item.Collected&&root.Supplies.TryReadWorldPose(id,out at,out rotation,out collected))lobby.RegisterSharedSupply(id,item.Family,item.Amount,V(at));}}
   lobby.ChangedSupplies(supplySync);
   foreach(var item in supplySync.Rows){Ashfall.Integration.Playable.Consumables.R24SupplyItem local;if(!root.Supplies.TryReadItem(item.Id,out local))root.Supplies.Spawn(item.Id,item.Family,new Vector3(item.Position.X,item.Position.Y,item.Position.Z),item.Amount);if(item.Collector=="")root.Supplies.RepositionWorldItemIfMoved(item.Id,new Vector3(item.Position.X,item.Position.Y,item.Position.Z));
    // A host receipt retires the box at once: the collector needs no second grab (SyncResources projects the gain).
    bool open=item.Collector=="";root.Supplies.SetWorldItemVisible(item.Id,open);}
   if(supplySync.Discover){root.Supplies.CopyWorldItemIds(supplyIdBuffer);foreach(string id in supplyIdBuffer){R24LanSupplyState known;if(!lobby.TryReadSharedSupply(id,out known))root.Supplies.SetWorldItemVisible(id,false);}}
   SyncSupplyDims(lobby,now);SyncSupplyWatches(lobby,now);
  }
  string LocalSharedWeapon(R24LanLootState state){string local,global;PendingDrop drop;bool guest=peer.Lobby.State==R24LanLobbyState.Connected;
   if(heldSwap!=null&&heldSwap.Released==state.Id&&InstanceMatches(heldSwap.Offered,state))return heldSwap.Offered;
   if(pendingDrops.Count>0&&pendingDrops.TryGetValue(state.Id,out drop)&&InstanceMatches(drop.Local,state))return drop.Local;
   if(lootLocals.TryGetValue(state.Id,out local)&&lootAliases.TryGetValue(local,out global)&&global==state.Id&&(guest||InstanceMatches(local,state)))return local;
   foreach(var pair in lootAliases)if(pair.Value==state.Id&&InstanceMatches(pair.Key,state)){lootLocals[state.Id]=pair.Key;return pair.Key;}if(!guest)return state.Id;
   local=R24LanWorldIdentity.LocalId(peer.Lobby.SessionId,peer.Lobby.MapManifestHash,"weapon",state.Id,state.Profile,state.Stars);lootAliases[local]=state.Id;lootLocals[state.Id]=local;return local;}
  void SyncLoot(){GuardNetworkInventory();if(peer==null||root==null||!root.Ready||root.Gameplay==null||peer.Lobby.SelectedMap==null||!peer.Lobby.SelectedMap.Active||peer.Lobby.SelectedMap.SourceRules==null)return;var lobby=peer.Lobby;
   if(!ReferenceEquals(pickupLobby,lobby)){pickupLobby=lobby;ResetPickupState();}
   if(lootEpoch!=lobby.Epoch){lootEpoch=lobby.Epoch;lootRequests.Clear();ResetPickupState();}
   BindPickupDiagnostics(lobby);SyncFood();SyncSupplies();var gameplay=root.Gameplay;double now=Time.realtimeSinceStartupAsDouble;weaponSync.Begin(lobby,lobby.Epoch,now,gameplay.SyncWorldWeaponCount);
   if(weaponSync.Discover&&lobby.State==R24LanLobbyState.Hosting){gameplay.CopyWorldWeaponActionIds(weaponIdBuffer);foreach(string id in weaponIdBuffer){R24LanLootState known;if(lootAliases.ContainsKey(id)||lobby.TryReadLoot(id,out known))continue;string instance;Vector3 center,position;Quaternion rotation;R23WeaponInstanceInfo info;if(gameplay.TryReadWorldWeapon(id,out instance,out center,out position,out rotation)&&gameplay.TryReadWeaponInstance(id,out info))lobby.RegisterWorldWeapon(id,info.ProfileId,info.Stars,V(position));}}
   SyncHeldWeaponSwap(lobby);CompletePendingDrops(lobby,gameplay);lobby.ChangedLoot(weaponSync);
   foreach(var state in weaponSync.Rows){string local=LocalSharedWeapon(state);R23WeaponInstanceInfo info;
    if(!gameplay.TryReadWeaponInstance(local,out info)){if(state.Owner!="")continue;gameplay.RegisterWorldWeapon(local,state.Profile,state.Stars,new Vector3(state.Position.X,state.Position.Y,state.Position.Z),Quaternion.identity);}
    if(state.Owner==""){Vector3 world=new Vector3(state.Position.X,state.Position.Y,state.Position.Z),old;Quaternion rotation;if(!gameplay.TryGetDroppedPose(local,out old,out rotation))gameplay.RestoreNetworkWorldCopy(local,world);gameplay.RepositionNetworkWorldWeapon(local,world);}
    bool owned=gameplay.OwnsWeapon(local);
    if(!owned){bool visible=state.Owner==""||state.Owner==lobby.OwnActor;gameplay.SetWorldWeaponVisible(local,visible);}
   }
   // A guest's independent loot generation is presentation only; unknown props cannot be claimed.
   if(weaponSync.Discover){gameplay.CopyWorldWeaponActionIds(weaponIdBuffer);foreach(string local in weaponIdBuffer){R24LanLootState known;if(lobby.State==R24LanLobbyState.Connected&&!lootAliases.ContainsKey(local)||!lobby.TryReadLoot(CanonicalLoot(local),out known))gameplay.SetWorldWeaponVisible(local,false);}}
   SyncWeaponDims(lobby,now);SyncPickupWatches(lobby,gameplay,now);RecoverNetworkInventory(lobby,now);
  }
 }
}
#endif
