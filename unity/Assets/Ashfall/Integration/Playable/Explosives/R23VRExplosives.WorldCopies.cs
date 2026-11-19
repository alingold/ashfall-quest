#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
namespace Ashfall.Integration.Playable.Explosives {
 public sealed partial class R23VRExplosives {
  public Func<bool> WorldCollisionReady;
  void QueueWorldSettle(Item item){
   if(item.View==null||!Ready||!isActiveAndEnabled)return;int revision=item.Revision;var view=item.View.transform;
   Func<bool> available=delegate{Item current;R23GrenadeItemState value;return this!=null&&view!=null&&items.TryGetValue(item.Id,out current)&&System.Object.ReferenceEquals(current,item)&&item.Revision==revision&&item.Visible&&state.TryRead(item.Id,out value)&&!value.Owned&&!value.Spent;};
   StartCoroutine(R24WorldPickupSettler.Defer(view,actor,delegate{return worldVisible&&(WorldCollisionReady==null?Ready:WorldCollisionReady());},available,delegate(Vector3 at){if(!available())return;item.Position=at;view.position=at;if(WorldItemMoved!=null)WorldItemMoved(item.Id,at);}));
  }
  // Source cache rolls still consume/retain their stars draw. Source R6 explicitly
  // excludes Frag26/Zone28 from tiered rarity: effective level1, fixed blast rules.
  public bool SpawnWorldItem(string id,string kind,int sourceStars,Vector3 position,Quaternion rotation,bool visible=true){
   if(!Allowed(kind)||actor==null||!Finite(position)||!Finite(rotation)||items.ContainsKey(id??"")||!state.RegisterWorldCopy(id,kind,sourceStars))return false;
   var item=new Item{Id=id,Kind=kind,Position=position,InitialPosition=position,Rotation=rotation.normalized,InitialRotation=rotation.normalized,Visible=visible,Culled=!Ashfall.Integration.Playable.World.Loot.R26PickupCulling.Within(position,Ashfall.Integration.Playable.World.Loot.R26PickupCulling.ShowRange)};items.Add(id,item);if(Ready&&visible)CreateView(item);return true;
  }
  public bool RetireUnownedWorldItem(string id){
   Item item;if(id==null||!items.TryGetValue(id,out item)||!state.RetireUnowned(id))return false;
   if(item.View!=null){item.View.SetActive(false);Destroy(item.View);}items.Remove(id);return true;
  }
  public bool IsWorldItemVisible(string id){Item item;R23GrenadeItemState value;return id!=null&&items.TryGetValue(id,out item)&&state.TryRead(id,out value)&&!value.Owned&&!value.Spent&&worldVisible&&item.Visible&&item.View!=null&&item.View.activeSelf;}
 }
}
#endif
