#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  // A queued guest command is not a custody receipt. Keep the local item until the
  // host releases it, then finish the native drop without another input edge.
  struct ConsumableDrop {internal string Local;internal long Epoch;internal double Until;}
  const int ConsumableDropLimit=32;
  readonly Dictionary<string,ConsumableDrop> pendingGrenadeDrops=new Dictionary<string,ConsumableDrop>(ConsumableDropLimit,StringComparer.Ordinal),pendingFoodDrops=new Dictionary<string,ConsumableDrop>(ConsumableDropLimit,StringComparer.Ordinal);
  readonly List<string> consumableDropDone=new List<string>(ConsumableDropLimit),consumableDropAct=new List<string>(ConsumableDropLimit);
  R24LanLobby consumableDropLobby;long consumableDropEpoch=-1;bool completingConsumableDrop;
  void ResetConsumableDrops(){pendingGrenadeDrops.Clear();pendingFoodDrops.Clear();consumableDropDone.Clear();consumableDropAct.Clear();consumableDropLobby=null;consumableDropEpoch=-1;}
  void CheckConsumableDropSession(R24LanLobby lobby){if(ReferenceEquals(consumableDropLobby,lobby)&&consumableDropEpoch==lobby.Epoch)return;ResetConsumableDrops();consumableDropLobby=lobby;consumableDropEpoch=lobby.Epoch;}
  void NoteConsumableDrop(Dictionary<string,ConsumableDrop> pending,string local,string global){var lobby=peer.Lobby;CheckConsumableDropSession(lobby);if(lobby.State!=R24LanLobbyState.Connected||pending.Count>=ConsumableDropLimit&&!pending.ContainsKey(global))return;
   pending[global]=new ConsumableDrop{Local=local,Epoch=lobby.Epoch,Until=Time.realtimeSinceStartupAsDouble+3};
  }
  void CompleteGrenadeDrops(R24LanLobby lobby){CheckConsumableDropSession(lobby);if(pendingGrenadeDrops.Count==0)return;double now=Time.realtimeSinceStartupAsDouble;var done=consumableDropDone;var finish=consumableDropAct;done.Clear();finish.Clear();
   foreach(var pair in pendingGrenadeDrops){var p=pair.Value;R24LanGrenadeState row;if(p.Epoch!=lobby.Epoch||now>p.Until||lobby.State!=R24LanLobbyState.Connected){done.Add(pair.Key);continue;}if(!lobby.TryReadGrenade(pair.Key,out row))continue;if(row.Spent||row.Owner!=""&&row.Owner!=lobby.OwnActor){done.Add(pair.Key);continue;}if(row.Owner=="")finish.Add(pair.Key);}
   foreach(string id in done)pendingGrenadeDrops.Remove(id);done.Clear();
   // Run before ReconcileNetworkItem: that projection can itself revoke native custody.
   completingConsumableDrop=true;try{foreach(string id in finish){ConsumableDrop p;R24LanGrenadeState row;if(!pendingGrenadeDrops.TryGetValue(id,out p)||!lobby.TryReadGrenade(id,out row))continue;if(root.Explosives.Drop(p.Local,new Vector3(row.Position.X,row.Position.Y,row.Position.Z),Quaternion.identity))pendingGrenadeDrops.Remove(id);}}finally{completingConsumableDrop=false;finish.Clear();}
  }
  void CompleteFoodDrops(R24LanLobby lobby){CheckConsumableDropSession(lobby);if(pendingFoodDrops.Count==0)return;double now=Time.realtimeSinceStartupAsDouble;var done=consumableDropDone;var finish=consumableDropAct;done.Clear();finish.Clear();
   foreach(var pair in pendingFoodDrops){var p=pair.Value;R24LanFoodState row;if(p.Epoch!=lobby.Epoch||now>p.Until||lobby.State!=R24LanLobbyState.Connected){done.Add(pair.Key);continue;}if(!lobby.TryReadFood(pair.Key,out row))continue;if(row.Consumed||row.Owner!=""&&row.Owner!=lobby.OwnActor){done.Add(pair.Key);continue;}if(row.Owner=="")finish.Add(pair.Key);}
   foreach(string id in done)pendingFoodDrops.Remove(id);done.Clear();
   completingConsumableDrop=true;try{foreach(string id in finish){ConsumableDrop p;R24LanFoodState row;if(!pendingFoodDrops.TryGetValue(id,out p)||!lobby.TryReadFood(id,out row))continue;if(root.Consumables.Drop(p.Local,new Vector3(row.Position.X,row.Position.Y,row.Position.Z),Quaternion.identity))pendingFoodDrops.Remove(id);}}finally{completingConsumableDrop=false;finish.Clear();}
  }
 }
}
#endif
