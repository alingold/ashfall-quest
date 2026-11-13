#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using UnityEngine;using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
namespace Ashfall.Integration.Playable.Consumables {
 public sealed partial class R24VRSupplies {
  public bool TryReadWorldItem(string id,out Vector3 position,out bool collected){position=Vector3.zero;collected=false;Box box;R24SupplyItem item;if(id==null||!boxes.TryGetValue(id,out box)||!state.TryRead(id,out item))return false;position=box.Position;collected=item.Collected;return true;}
  // Trusted layout lifetime only. Collected event identity/capacity is retained.
  public bool RetireUncollectedWorldItem(string id){Box box;if(id==null||!boxes.TryGetValue(id,out box)||!state.RemoveUncollected(id))return false;if(box.View!=null){box.View.SetActive(false);Destroy(box.View);}boxes.Remove(id);return true;}
  public void SettleWorldItem(string id,Func<bool> actualCollisionReady,Func<bool> sameLayout){if(actualCollisionReady==null||sameLayout==null)throw new ArgumentNullException();Box box;if(id!=null&&boxes.TryGetValue(id,out box))StartCoroutine(SettleCacheItem(id,box,actualCollisionReady,sameLayout));}
  IEnumerator SettleCacheItem(string id,Box box,Func<bool> ready,Func<bool> sameLayout){while(box.View==null&&sameLayout()&&AvailableSourceBox(id,box)&&Failure==null)yield return null;if(box.View==null||!sameLayout()||!AvailableSourceBox(id,box))yield break;yield return R24WorldPickupSettler.Defer(box.View.transform,actor,ready,delegate{return sameLayout()&&AvailableSourceBox(id,box);},delegate(Vector3 at){if(sameLayout()&&AvailableSourceBox(id,box))RepositionWorldItem(id,at);});}
 }
}
#endif
