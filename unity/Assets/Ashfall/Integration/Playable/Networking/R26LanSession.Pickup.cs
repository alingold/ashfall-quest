#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.HandsWeapons;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 // MP-PICKUP (playtest 2026-11-15). The host collects only into a free slot, but the root's
 // single-player make_room swap ran only after the host had granted custody, so the default
 // three-weapon loadout (3 slots) refused every gun. Here the swap's drop is queued ahead of
 // the collect on the ordered command lane; claims are retried while the grip is held,
 // abandoned grants are released, host-confirmed drops finish without a second press, and
 // pickups the local player cannot take right now are greyed out (presentation only).
 public sealed partial class R24LanSession {
  sealed class PickupWatch {internal string Local;internal int Hand;internal long Epoch;internal double Started,Next;internal bool Released,Refused;}
  sealed class PendingDrop {internal string Local;internal long Epoch,BeforeRevision;internal double Until;}
  const double ClaimWindow=3,ReleaseAfter=4.5,ReleaseGiveUp=9,RefusedDim=2;
  readonly Dictionary<string,PickupWatch> pickupWatches=new Dictionary<string,PickupWatch>(StringComparer.Ordinal),supplyWatches=new Dictionary<string,PickupWatch>(StringComparer.Ordinal);
  readonly Dictionary<string,PendingDrop> pendingDrops=new Dictionary<string,PendingDrop>(StringComparer.Ordinal);
  readonly Dictionary<string,double> refusedWeapons=new Dictionary<string,double>(StringComparer.Ordinal),refusedSupplies=new Dictionary<string,double>(StringComparer.Ordinal);
  readonly Dictionary<string,string> lootLocals=new Dictionary<string,string>(StringComparer.Ordinal);
  readonly List<string> weaponIdBuffer=new List<string>(),supplyIdBuffer=new List<string>(),pickupDone=new List<string>(),pickupAct=new List<string>();
  R24LanLobby pickupLobby;
  void ResetPickupState(){heldSwap=null;ResetItemSync();pickupWatches.Clear();supplyWatches.Clear();pendingDrops.Clear();refusedWeapons.Clear();refusedSupplies.Clear();lootLocals.Clear();}
  // Session close: no greyed-out copy may survive into single-player.
  void ClearPickupPresentation(){ResetPickupState();pickupLobby=null;if(root==null)return;if(root.Gameplay!=null)root.Gameplay.ClearWorldWeaponDims();if(root.Supplies!=null)root.Supplies.ClearWorldItemDims();}
  void BindPickupDiagnostics(R24LanLobby lobby){
   if(Debug.isDebugBuild){if(lobby.State==R24LanLobbyState.Hosting&&lobby.PickupRejectionLog==null)lobby.PickupRejectionLog=LogPickupRejection;}
  }
  static void LogPickupRejection(string line){Debug.Log(line);}
  // ---------- full-inventory swap ----------
  bool PrepareWeaponRoom(string id,string local,int hand){if(root.LanWeaponFits())return true;var lobby=peer.Lobby;string why;double now=Time.realtimeSinceStartupAsDouble,last;
   if(lobby.State==R24LanLobbyState.Hosting){if(!lobby.PreviewOwnLoot(id,hand,true,out why)){NotePickupRefused(id,false,why,now);return false;}if(root.LanMakeRoomForWeapon(local)||root.LanWeaponFits())return true;NotePickupRefused(id,false,"no-room",now);return false;}
   if(lootRequests.TryGetValue("get/"+id,out last)&&now-last<.5)return true; // this grip already queued its swap
   long before=lobby.CommandSequence;if(root.LanMakeRoomForWeapon(local)||lobby.CommandSequence!=before)return true; // drop queued ahead of the collect
   NotePickupRefused(id,false,"no-room",now);return false;}
  // ---------- claim watches: bounded retry, refusal feedback, abandoned-grant release ----------
  void WatchPickup(string local,string canonical,int hand){Watch(pickupWatches,canonical,local,hand);refusedWeapons.Remove(canonical);}
  void WatchSupply(string id,int hand){Watch(supplyWatches,id,id,hand);refusedSupplies.Remove(id);}
  void Watch(Dictionary<string,PickupWatch> watches,string key,string local,int hand){double now=Time.realtimeSinceStartupAsDouble;PickupWatch w;if(watches.TryGetValue(key,out w)){if(w.Epoch==peer.Lobby.Epoch&&w.Local==local&&w.Hand==hand)return;}else{if(watches.Count>=64)return;w=new PickupWatch();watches.Add(key,w);}w.Local=local;w.Hand=hand;w.Epoch=peer.Lobby.Epoch;w.Started=now;w.Next=now+.5;w.Released=w.Refused=false;}
  void SyncPickupWatches(R24LanLobby lobby,R23HandsWeapons gameplay,double now){
   Prune(refusedWeapons,now);if(pickupWatches.Count==0)return;var done=pickupDone;done.Clear();
   foreach(var pair in pickupWatches){var w=pair.Value;R24LanLootState state;if(w.Epoch!=lobby.Epoch||!lobby.TryReadLoot(pair.Key,out state)){done.Add(pair.Key);continue;}double age=now-w.Started;
    if(state.Owner==lobby.OwnActor){
     if(gameplay.OwnsWeapon(w.Local)){done.Add(pair.Key);continue;}
     if(age>ReleaseGiveUp){done.Add(pair.Key);continue;}if(age<ReleaseAfter||now<w.Next)continue;
     // Granted but never taken into the hand (grip released before the receipt or the local
     // collect failed): hand custody back so the slot and the item free up for everyone.
     w.Next=now+1;w.Released=true;ReleaseAbandoned(pair.Key,w,state);continue;}
    if(state.Owner!=""||w.Released){done.Add(pair.Key);continue;}
    if(age<ClaimWindow){if(lobby.State==R24LanLobbyState.Connected&&now>=w.Next&&root.LanClaimHandHeld(w.Hand)){w.Next=now+.5;RequestLootOnce(pair.Key,w.Hand,false,state.Position);}continue;} // PICKUP4: a host retry grants with no local collect
    // Still watched after the refusal so a grant that lands late (lag) is released too.
    if(!w.Refused){w.Refused=true;NotePickupRefused(pair.Key,false,lobby.State==R24LanLobbyState.Hosting?lobby.LastPickupRejection:null,now);}if(age>ReleaseGiveUp)done.Add(pair.Key);}
   foreach(string id in done)pickupWatches.Remove(id);done.Clear();
  }
  void SyncSupplyWatches(R24LanLobby lobby,double now){
   Prune(refusedSupplies,now);if(supplyWatches.Count==0)return;var done=pickupDone;done.Clear();
   foreach(var pair in supplyWatches){var w=pair.Value;R24LanSupplyState item;if(w.Epoch!=lobby.Epoch||!lobby.TryReadSharedSupply(pair.Key,out item)){done.Add(pair.Key);continue;}
    if(item.Collector==lobby.OwnActor){done.Add(pair.Key);if(lobby.State==R24LanLobbyState.Connected)root.UI.SetLoadoutStatus("COLLECTED");continue;}
    if(item.Collector!=""){done.Add(pair.Key);continue;}
    if(now-w.Started<ClaimWindow){if(now>=w.Next&&root.LanClaimHandHeld(w.Hand)){w.Next=now+.5;PublishRawTracking();lobby.RequestSupply(pair.Key,w.Hand);}continue;}
    done.Add(pair.Key);NotePickupRefused(pair.Key,true,lobby.State==R24LanLobbyState.Hosting?lobby.LastPickupRejection:null,now);}
   foreach(string id in done)supplyWatches.Remove(id);done.Clear();
  }
  void ReleaseAbandoned(string id,PickupWatch w,R24LanLootState state){Vector3 item=new Vector3(state.Position.X,state.Position.Y,state.Position.Z),feet=root.Rig.transform.position;
   // Back to its own point when the host's 3 m drop reach allows; otherwise at the claimant's feet.
   Vector3 at=(item-feet).sqrMagnitude<=2.7f*2.7f?item:feet+Vector3.up*.3f;RequestLootOnce(id,w.Hand,true,V(at));
   if(Debug.isDebugBuild){Debug.Log("ASHFALL pickup: releasing unclaimed custody of '"+id+"'");}
  }
  void NotePickupRefused(string id,bool supply,string reason,double now){var refused=supply?refusedSupplies:refusedWeapons;if(refused.Count>=256)refused.Clear();refused[id]=now+RefusedDim;if(root!=null&&root.UI!=null)root.UI.SetLoadoutStatus(RefusalText(reason));
   if(Debug.isDebugBuild){Debug.Log("ASHFALL pickup: "+(supply?"supply":"weapon")+" '"+id+"' not granted ("+(reason??"no host receipt")+")");}
  }
  static string RefusalText(string reason){switch(reason){case "slots-full":return "INVENTORY FULL / DROP AN ITEM FIRST";case "no-room":return "NO ROOM / HELD ITEM COULD NOT BE DROPPED";case "capacity-max":return "BACKPACK LIMIT REACHED";case "inventory-closed":return "PICKUPS OPEN WHEN THE ROUND STARTS";case "rules":return "PICKUP DISABLED BY MATCH RULES";case "tracking":return "PICKUP REFUSED / TRACKING LOST";case "body-reach":case "hand-reach":return "PICKUP REFUSED / TOO FAR";case "line-of-sight":return "PICKUP REFUSED / VIEW BLOCKED";default:return "PICKUP NOT CONFIRMED BY HOST";}}
  void Prune(Dictionary<string,double> until,double now){if(until.Count==0)return;var expired=pickupAct;expired.Clear();foreach(var pair in until)if(pair.Value<=now)expired.Add(pair.Key);foreach(string id in expired)until.Remove(id);expired.Clear();}
  // ---------- host-confirmed guest drops ----------
  void NotePendingDrop(string local,string global,long beforeRevision){if(peer.Lobby.State!=R24LanLobbyState.Connected)return;PendingDrop p;if(!pendingDrops.TryGetValue(global,out p)){if(pendingDrops.Count>=32)return;p=new PendingDrop();pendingDrops.Add(global,p);}p.Local=local;p.Epoch=peer.Lobby.Epoch;p.BeforeRevision=beforeRevision;p.Until=Time.realtimeSinceStartupAsDouble+3;}
  void BindDroppedAlias(string local,string global){lootAliases[local]=global;lootLocals[global]=local;pendingDrops.Remove(global);}
  void CompletePendingDrops(R24LanLobby lobby,R23HandsWeapons gameplay){if(pendingDrops.Count==0)return;double now=Time.realtimeSinceStartupAsDouble;var done=pickupDone;var finish=pickupAct;done.Clear();finish.Clear();
   foreach(var pair in pendingDrops){var p=pair.Value;R24LanLootState state;if(p.Epoch!=lobby.Epoch||now>p.Until){done.Add(pair.Key);continue;}if(!lobby.TryReadLoot(pair.Key,out state)||state.Owner!=""||state.Revision<=p.BeforeRevision)continue;if(!gameplay.OwnsWeapon(p.Local)){done.Add(pair.Key);continue;}finish.Add(pair.Key);}
   foreach(string id in done)pendingDrops.Remove(id);done.Clear();
   // The host accepted the drop: finish it locally at the host point, without a second press.
   foreach(string global in finish){PendingDrop p;R24LanLootState state;if(!pendingDrops.TryGetValue(global,out p)||!lobby.TryReadLoot(global,out state))continue;if(gameplay.DropOwned(p.Local,new Vector3(state.Position.X,state.Position.Y,state.Position.Z),Quaternion.identity))root.UI.SetLoadoutStatus("DROPPED WITH AMMO PRESERVED");}
   finish.Clear();
  }
  bool InstanceMatches(string local,R24LanLootState state){R23WeaponInstanceInfo info;return root.Gameplay.TryReadWeaponInstance(local,out info)&&info.ProfileId==state.Profile&&info.Stars==state.Stars;}
  // ---------- greyed-out presentation (local estimate; the host still decides) ----------
  bool PickupsBlocked(R24LanLobby lobby){var map=lobby.SelectedMap;return !root.LanPickupActorReady||map==null||!map.Active||map.Cancelled||map.Results||lobby.RoundCountdown>0;}
  static bool RulesAllowPickup(R24LanLobby lobby,string id){var map=lobby.SelectedMap;var rules=map==null?null:map.SourceRules;return rules!=null&&(rules.Loot||id.IndexOf("/catalog/",StringComparison.Ordinal)>=0&&(map.Rules==null||rules.Catalog));}
  bool WeaponPickupOpen(R24LanLobby lobby,string id,bool blocked,double now){double until;return !blocked&&RulesAllowPickup(lobby,id)&&!(refusedWeapons.Count>0&&refusedWeapons.TryGetValue(id,out until)&&until>now);}
  bool SupplyPickupOpen(R24LanLobby lobby,R24LanSupplyState item,bool blocked,double now){double until;return !blocked&&RulesAllowPickup(lobby,item.Id)&&!(item.Family=="backpack"&&root.InventoryCapacity>=6)&&!(refusedSupplies.Count>0&&refusedSupplies.TryGetValue(item.Id,out until)&&until>now);}
 }
}
#endif
