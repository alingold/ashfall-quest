#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;using Ashfall.Core;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  double nextFoodEquipment;string publishedFood;int publishedFoodHand=-1;long publishedFoodEpoch=-1;
  sealed class RootFoodGeometry:IR24LanFoodGeometry {readonly R24LanSession owner;internal RootFoodGeometry(R24LanSession owner){this.owner=owner;}public bool TryFrame(string kind,int hand,R24LanRawTracking raw,out Ashfall.Modules.Pickups.ActorFrame frame){frame=null;return owner.root!=null&&owner.root.Consumables!=null&&owner.root.Consumables.TryHostFoodFrame(kind,hand,raw,out frame);}}
  void PublishFoodEquipment(){if(peer==null||root.Consumables==null)return;if(publishedFoodEpoch!=peer.Lobby.Epoch){publishedFood=null;publishedFoodHand=-1;publishedFoodEpoch=peer.Lobby.Epoch;}string id=root.Consumables.HeldId;int hand=root.Consumables.HeldHand-1;if(id==publishedFood&&hand==publishedFoodHand&&(id==null||UnityEngine.Time.realtimeSinceStartupAsDouble<nextFoodEquipment))return;nextFoodEquipment=UnityEngine.Time.realtimeSinceStartupAsDouble+.25;if(id==null){if(publishedFood!=null)peer.Lobby.RequestFood(publishedFood,System.Math.Max(0,publishedFoodHand),4,new Float3());publishedFood=null;publishedFoodHand=-1;}else if(peer.Lobby.RequestFood(id,System.Math.Max(0,hand),3,new Float3())){publishedFood=id;publishedFoodHand=hand;}}
  bool FoodRequest(string id,int hand,int action,Float3 at){string key="food/"+action+"/"+id;double now=Time.realtimeSinceStartupAsDouble,last;if(lootRequests.TryGetValue(key,out last)&&now-last<.5)return false;lootRequests[key]=now;PublishRawTracking();return peer.Lobby.RequestFood(id,hand,action,at);}
  public bool AllowFoodClaim(string objective,int hand){if(peer==null)return true;if(!objective.StartsWith("pickup:",StringComparison.Ordinal))return true;string id=objective.Substring(7);R24LanFoodState food;if(!peer.Lobby.TryReadFood(id,out food)||food.Consumed)return false;if(food.Owner==peer.Lobby.OwnActor)return true;if(food.Owner!=""||!root.LanWorldClaimIntent("food",id,hand-1))return false;FoodRequest(id,hand-1,0,food.Position);bool accepted=peer.Lobby.TryReadFood(id,out food)&&food.Owner==peer.Lobby.OwnActor;if(!accepted)TrackClaim("food",id,id,food.Kind,hand-1);return accepted;}
  bool ConsumeFood(string id){if(peer==null)return false;R24LanFoodState food;if(!peer.Lobby.TryReadFood(id,out food)||food.Owner!=peer.Lobby.OwnActor)return false;
   if(!food.Consumed)FoodRequest(id,Math.Max(0,root.Consumables.HeldHand-1),1,new Float3());
   if(!peer.Lobby.TryReadFood(id,out food)||!food.Consumed)return false;if(food.Kind=="banana")root.Rig.RefillStamina();return true;
  }
  bool DropFood(string id,Vector3 at){if(peer==null)return true;R24LanFoodState food;if(!peer.Lobby.TryReadFood(id,out food)||food.Consumed)return false;if(food.Owner==""){if(!completingConsumableDrop)pendingFoodDrops.Remove(id);return true;}if(food.Owner!=peer.Lobby.OwnActor)return false;bool sent=FoodRequest(id,Math.Max(0,root.Consumables.HeldHand-1),2,V(at));if(peer.Lobby.TryReadFood(id,out food)&&food.Owner=="")return true;if(sent)NoteConsumableDrop(pendingFoodDrops,id,id);return false;}
  void SyncFood(){if(root.Consumables==null)return;var lobby=peer.Lobby;BindFoodSync();BindFoodLifetime();CompleteFoodDrops(lobby);foodSync.Begin(lobby,lobby.Epoch,Time.realtimeSinceStartupAsDouble,root.Consumables.SyncWorldItemCount);if(foodSync.Discover)root.Consumables.CopySyncWorldItemIds(foodIdBuffer);
   if(foodSync.Discover&&lobby.State==R24LanLobbyState.Hosting)foreach(string id in foodIdBuffer){R24LanFoodState known;if(lobby.TryReadFood(id,out known))continue;var info=root.Consumables.InspectItem(id);Vector3 at;Quaternion rotation;bool owned;if(info!=null&&!info.Removed&&info.Owner==null&&root.Consumables.TryReadWorldItem(id,out at,out rotation,out owned))lobby.RegisterSharedFood(id,info.Kind,V(at));}
   lobby.ChangedFood(foodSync);foreach(var food in foodSync.Rows){if(food.Consumed&&food.Owner==lobby.OwnActor){if(root.Consumables.AcknowledgeSharedConsume(lobby,food)&&food.Kind=="banana")root.Rig.RefillStamina();continue;}var local=root.Consumables.InspectItem(food.Id);if(local==null){if(food.Owner!=""||food.Consumed)continue;root.Consumables.Spawn(food.Id,food.Kind,new Vector3(food.Position.X,food.Position.Y,food.Position.Z),false);}
    if(food.Owner==""&&local!=null&&local.Owner==null)root.Consumables.RepositionWorldItem(food.Id,new Vector3(food.Position.X,food.Position.Y,food.Position.Z));
    root.Consumables.SetWorldItemVisible(food.Id,food.Owner==lobby.OwnActor||food.Owner==""&&!food.Consumed);
   }
   if(foodSync.Discover)foreach(string id in foodIdBuffer){R24LanFoodState known;if(!lobby.TryReadFood(id,out known))root.Consumables.SetWorldItemVisible(id,false);}
  }
 }
}
#endif
