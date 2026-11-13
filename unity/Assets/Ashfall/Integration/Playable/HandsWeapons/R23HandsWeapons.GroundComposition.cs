#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23HandsWeapons {
  public event Action<string,Vector3> WorldWeaponMoved;
  internal bool RepositionNetworkWorldWeapon(string id,Vector3 at){WorldWeapon record;if(!To(at).IsFinite||ownedWeapons.Contains(id)||!droppedWeapons.TryGetValue(id,out record))return false;if((record.Position-at).sqrMagnitude<.0000001f)return true;record.Position=at;record.CenterSet=false;if(record.Visual!=null)record.Visual.position=at;return true;}
  bool sourceGroundLayoutManaged;public Func<bool> WorldPickupCollisionReady;
  public bool SourceGroundLayoutManaged{get{return sourceGroundLayoutManaged;}}
  public string[] LegacyStarterWorldActionIds{get{var ids=new List<string>(starterWorldAtHome);if(weapon!=null&&starterPickup&&!holstered&&!HasWeapon)ids.Add(activeActionId);return ids.ToArray();}}
  public int LegacyStarterWorldCount{get{return LegacyStarterWorldActionIds.Length;}}
  public bool MarkWorldWeaponWallMounted(string id){WorldWeapon record;if(id==null||!droppedWeapons.TryGetValue(id,out record))return false;record.WallMounted=true;record.SettleOutcome=R24PickupSettleOutcome.WallMounted;return true;}
  public bool IsWorldWeaponWallMounted(string id){WorldWeapon record;return id!=null&&droppedWeapons.TryGetValue(id,out record)&&record.WallMounted;}
  // Actual WorldLoot owns the one source review grid/scatter. Keep default native
  // owners (including the carried3) but withdraw the obsolete pilot props.
  public void BindSourceGroundLayout(){
   sourceGroundLayoutManaged=true;
   foreach(var id in starterWorldAtHome){WorldWeapon record;if(!droppedWeapons.TryGetValue(id,out record))continue;if(record.Visual!=null){record.Visual.gameObject.SetActive(false);Destroy(record.Visual.gameObject);}droppedWeapons.Remove(id);}
   starterWorldAtHome.Clear();
   if(starterPickup&&!HasWeapon){starterPickup=false;holstered=true;if(weapon!=null)weapon.gameObject.SetActive(false);}
  }
  public bool TryReadWorldWeaponSettle(string id,out R24PickupSettleOutcome outcome,out Vector3 position){outcome=R24PickupSettleOutcome.Pending;position=Vector3.zero;WorldWeapon record;if(id==null||!droppedWeapons.TryGetValue(id,out record))return false;outcome=record.SettleOutcome;position=record.Position;return true;}
  void QueueWorldWeaponSettle(string id){
   WorldWeapon record;if(!sourceGroundLayoutManaged||!Ready||!isActiveAndEnabled||!droppedWeapons.TryGetValue(id,out record)||record.Visual==null||record.WallMounted)return;
   var view=record.Visual;record.SettleOutcome=R24PickupSettleOutcome.Pending;
   Func<bool> available=delegate{WorldWeapon current;return this!=null&&view!=null&&droppedWeapons.TryGetValue(id,out current)&&System.Object.ReferenceEquals(current,record)&&record.Visual==view&&record.Visible&&!record.WallMounted&&!ownedWeapons.Contains(id);};
   StartCoroutine(R24WorldPickupSettler.Defer(view,player,delegate{return WorldPickupCollisionReady==null?Ready:WorldPickupCollisionReady();},available,delegate(Vector3 at){if(!available())return;record.Position=at;view.position=at;if(WorldWeaponMoved!=null)WorldWeaponMoved(id,at);},false,delegate(R24PickupSettleOutcome outcome){if(available())record.SettleOutcome=outcome;}));
  }
 }
}
#endif
