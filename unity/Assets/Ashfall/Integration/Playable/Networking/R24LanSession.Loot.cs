#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.HandsWeapons;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  readonly Dictionary<string,string> lootAliases=new Dictionary<string,string>(StringComparer.Ordinal);
  readonly Dictionary<string,double> lootRequests=new Dictionary<string,double>(StringComparer.Ordinal);long lootEpoch=-1;
  string CanonicalLoot(string local){string id;return lootAliases.TryGetValue(local,out id)?id:local;}
  bool RequestLootOnce(string id,int hand,bool drop,Float3 at){double now=Time.realtimeSinceStartupAsDouble,last;string key=(drop?"drop/":"get/")+id;if(lootRequests.TryGetValue(key,out last)&&now-last<.5)return false;lootRequests[key]=now;PublishRawTracking();return peer.Lobby.RequestLoot(id,hand,drop,at);}
  public bool AllowWeaponClaim(string local,int hand){if(peer==null)return true;var lobby=peer.Lobby;if(lobby.State==R24LanLobbyState.Connected&&!lootAliases.ContainsKey(local))return false;if(lobby.SelectedMap==null||lobby.SelectedMap.SourceRules==null)return false;string id=CanonicalLoot(local);R24LanLootState state;if(!lobby.TryReadLoot(id,out state))return false;if(state.Owner==lobby.OwnActor)return true;if(state.Owner!=""||!root.LanWorldClaimIntent("weapon",local,hand))return false;RequestLootOnce(id,hand,false,state.Position);bool accepted=lobby.TryReadLoot(id,out state)&&state.Owner==lobby.OwnActor;if(!accepted)TrackClaim("weapon",local,id,state.Profile,hand);return accepted;}
  bool AllowWeaponDrop(string local,Vector3 at){if(peer==null)return true;string id=CanonicalLoot(local);var lobby=peer.Lobby;R24LanLootState state;
   // Initial loadout action IDs are actor-scoped; host chooses a global drop ID.
   string global=id;if(!lobby.TryReadLoot(id,out state))global=lobby.OwnActor+"/"+id;
   if(lobby.TryReadLoot(global,out state)&&state.Owner==""&&(new Vector3(state.Position.X,state.Position.Y,state.Position.Z)-at).sqrMagnitude<1){lootAliases[local]=global;return true;}
   RequestLootOnce(id,root.Gameplay.PrimaryHand,true,V(at));if(lobby.TryReadLoot(global,out state)&&state.Owner==""){lootAliases[local]=global;return true;}return false;
  }
  public bool AuthorizeSupply(string receipt,string family,int amount){if(peer==null)return true;if(!receipt.StartsWith("pickup089:",StringComparison.Ordinal))return false;string id=receipt.Substring(10);R24LanSupplyState item;
   if(!peer.Lobby.TryReadSharedSupply(id,out item)||item.Family!=family||item.Amount!=amount)return false;if(item.Collector==peer.Lobby.OwnActor)return true;if(item.Collector!="")return false;
   double now=Time.realtimeSinceStartupAsDouble,last;string key="supply/"+id;if(lootRequests.TryGetValue(key,out last)&&now-last<.5)return false;lootRequests[key]=now;PublishRawTracking();
   int hand=Vector3.Distance(root.Rig.Left.position,new Vector3(item.Position.X,item.Position.Y,item.Position.Z))<Vector3.Distance(root.Rig.Right.position,new Vector3(item.Position.X,item.Position.Y,item.Position.Z))?0:1;peer.Lobby.RequestSupply(id,hand);
   return peer.Lobby.TryReadSharedSupply(id,out item)&&item.Collector==peer.Lobby.OwnActor;
  }
  void SyncSupplies(){if(root.Supplies==null)return;var lobby=peer.Lobby;
   if(lobby.State==R24LanLobbyState.Hosting)foreach(string id in root.Supplies.WorldItemIds){R24LanSupplyState known;if(lobby.TryReadSharedSupply(id,out known))continue;Ashfall.Integration.Playable.Consumables.R24SupplyItem item;Vector3 at;Quaternion rotation;bool collected;if(root.Supplies.TryReadItem(id,out item)&&!item.Collected&&root.Supplies.TryReadWorldPose(id,out at,out rotation,out collected))lobby.RegisterSharedSupply(id,item.Family,item.Amount,V(at));}
   foreach(var item in lobby.SharedSupplies){Ashfall.Integration.Playable.Consumables.R24SupplyItem local;if(!root.Supplies.TryReadItem(item.Id,out local))root.Supplies.Spawn(item.Id,item.Family,new Vector3(item.Position.X,item.Position.Y,item.Position.Z),item.Amount);if(item.Collector=="")root.Supplies.RepositionWorldItem(item.Id,new Vector3(item.Position.X,item.Position.Y,item.Position.Z));root.Supplies.SetWorldItemVisible(item.Id,item.Collector==""||item.Collector==lobby.OwnActor);}
   foreach(string id in root.Supplies.WorldItemIds){R24LanSupplyState known;if(!lobby.TryReadSharedSupply(id,out known))root.Supplies.SetWorldItemVisible(id,false);}
  }
  string LocalSharedWeapon(R24LanLootState state){R23WeaponInstanceInfo info;foreach(var pair in lootAliases)if(pair.Value==state.Id&&root.Gameplay.TryReadWeaponInstance(pair.Key,out info)&&info.ProfileId==state.Profile&&info.Stars==state.Stars)return pair.Key;if(peer.Lobby.State==R24LanLobbyState.Hosting&&root.Gameplay.TryReadWeaponInstance(state.Id,out info)&&info.ProfileId==state.Profile&&info.Stars==state.Stars)return state.Id;if(peer.Lobby.State==R24LanLobbyState.Hosting)return state.Id;string local=R24LanWorldIdentity.LocalId(peer.Lobby.SessionId,peer.Lobby.MapManifestHash,"weapon",state.Id,state.Profile,state.Stars);lootAliases[local]=state.Id;return local;}
  void SyncLoot(){if(peer==null||root==null||!root.Ready||root.Gameplay==null||peer.Lobby.SelectedMap==null||!peer.Lobby.SelectedMap.Active||peer.Lobby.SelectedMap.SourceRules==null)return;var lobby=peer.Lobby;
   if(lootEpoch!=lobby.Epoch){lootEpoch=lobby.Epoch;lootRequests.Clear();pendingRevives.Clear();}
   SyncFood();SyncSupplies();var gameplay=root.Gameplay;
   if(lobby.State==R24LanLobbyState.Hosting)foreach(string id in gameplay.WorldWeaponActionIds){R24LanLootState known;if(lootAliases.ContainsKey(id)||lobby.TryReadLoot(id,out known))continue;string instance;Vector3 center,position;Quaternion rotation;R23WeaponInstanceInfo info;if(gameplay.TryReadWorldWeapon(id,out instance,out center,out position,out rotation)&&gameplay.TryReadWeaponInstance(id,out info))lobby.RegisterWorldWeapon(id,info.ProfileId,info.Stars,V(position));}
   foreach(var state in lobby.LootStates){string local=LocalSharedWeapon(state);R23WeaponInstanceInfo info;
    if(!gameplay.TryReadWeaponInstance(local,out info)){if(state.Owner!="")continue;gameplay.RegisterWorldWeapon(local,state.Profile,state.Stars,new Vector3(state.Position.X,state.Position.Y,state.Position.Z),Quaternion.identity);}
    if(state.Owner=="")gameplay.RepositionNetworkWorldWeapon(local,new Vector3(state.Position.X,state.Position.Y,state.Position.Z));
    bool owned=gameplay.OwnsWeapon(local);
    if(!owned)gameplay.SetWorldWeaponVisible(local,state.Owner==""||state.Owner==lobby.OwnActor);
   }
   // A guest's independent loot generation is presentation only; unknown props cannot be claimed.
   foreach(string local in gameplay.WorldWeaponActionIds){R24LanLootState known;if(lobby.State==R24LanLobbyState.Connected&&!lootAliases.ContainsKey(local)||!lobby.TryReadLoot(CanonicalLoot(local),out known))gameplay.SetWorldWeaponVisible(local,false);}
  }
 }
}
#endif
