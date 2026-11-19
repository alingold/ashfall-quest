#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  readonly R26LanItemSync<R24LanLootState> weaponSync=new R26LanItemSync<R24LanLootState>(row=>row.Revision);
  readonly R26LanItemSync<R24LanSupplyState> supplySync=new R26LanItemSync<R24LanSupplyState>(row=>row.Revision);
  readonly R26LanItemSync<R24LanFoodState> foodSync=new R26LanItemSync<R24LanFoodState>(row=>row.Revision);
  readonly R26LanItemSync<R24LanGrenadeState> grenadeSync=new R26LanItemSync<R24LanGrenadeState>(row=>row.Revision);
  readonly List<string> foodIdBuffer=new List<string>(4096),grenadeIdBuffer=new List<string>(4096);
  Action<string> dimWeapon,dimSupply;Func<string,bool> sharedConsume;Func<string,Vector3,bool> sharedFoodDrop;
  R24LanLobby dimLobby;bool dimBlocked;double dimNow;
  void ResetItemSync(){weaponSync.Reset();supplySync.Reset();foodSync.Reset();grenadeSync.Reset();foodIdBuffer.Clear();grenadeIdBuffer.Clear();dimLobby=null;}
  void BindFoodSync(){if(sharedConsume==null){sharedConsume=ConsumeFood;sharedFoodDrop=DropFood;}root.Consumables.SetSharedConsume(sharedConsume);root.Consumables.SharedDrop=sharedFoodDrop;}
  // Dimming depends on local readiness, refusal timers, capacity and newly rebuilt art,
  // not just host revisions. Visit only presented copies for the expensive state reads.
  void SyncWeaponDims(R24LanLobby lobby,double now){dimLobby=lobby;dimNow=now;dimBlocked=PickupsBlocked(lobby);if(dimWeapon==null)dimWeapon=DimWeapon;root.Gameplay.VisitPresentedWorldWeapons(dimWeapon);}
  void DimWeapon(string local){R24LanLootState state;if(dimLobby.TryReadLoot(CanonicalLoot(local),out state)&&!root.Gameplay.OwnsWeapon(local)&&(state.Owner==""||state.Owner==dimLobby.OwnActor))root.Gameplay.SetWorldWeaponDimmed(local,state.Owner==""&&!WeaponPickupOpen(dimLobby,state.Id,dimBlocked,dimNow));}
  void SyncSupplyDims(R24LanLobby lobby,double now){dimLobby=lobby;dimNow=now;dimBlocked=PickupsBlocked(lobby);if(dimSupply==null)dimSupply=DimSupply;root.Supplies.VisitPresentedWorldSupplies(dimSupply);}
  void DimSupply(string id){R24LanSupplyState item;if(dimLobby.TryReadSharedSupply(id,out item)&&item.Collector=="")root.Supplies.SetWorldItemDimmed(id,!SupplyPickupOpen(dimLobby,item,dimBlocked,dimNow));}
 }
}
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23HandsWeapons {
  internal int SyncWorldWeaponCount{get{return droppedWeapons.Count+(starterPickup&&!holstered?1:0);}}
  internal void VisitPresentedWorldWeapons(Action<string> visit){foreach(var pair in droppedWeapons)if(pair.Value.Visual!=null)visit(pair.Key);}
 }
}
namespace Ashfall.Integration.Playable.Consumables {
 public sealed partial class R24VRSupplies {
  internal int SyncWorldItemCount{get{return boxes.Count;}}
  internal void VisitPresentedWorldSupplies(Action<string> visit){foreach(var pair in boxes)if(pair.Value.View!=null&&pair.Value.View.activeSelf)visit(pair.Key);}
 }
 public sealed partial class R23VRConsumables {
  internal int SyncWorldItemCount{get{return items.Count;}}
  internal void CopySyncWorldItemIds(List<string> into){into.Clear();foreach(string id in items.Keys)into.Add(id);}
 }
}
namespace Ashfall.Integration.Playable.Explosives {
 public sealed partial class R23VRExplosives {
  internal int SyncWorldItemCount{get{return items.Count;}}
  internal void CopySyncWorldItemIds(List<string> into){into.Clear();foreach(string id in items.Keys)into.Add(id);}
 }
}
#endif
