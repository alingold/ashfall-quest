#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.World.Loot;using UnityEngine;
namespace Ashfall.Integration.Playable.Consumables {
 public sealed partial class R23VRConsumables {
  // MP-PERF: unowned food art is shown only near the local viewer (presentation only).
  R26CullSweep<WorldItem> worldViewSweep;
  // One item's art state; SyncViews and every per-item setter share it.
  void SyncView(WorldItem item)
  {
   if(item.View==null)return;bool held=item.Id==HeldId;if(held)item.RestPosed=false;
   // A culled prop is unowned and already hidden: nothing to inspect or pose.
   if(item.Culled&&!held&&!item.View.activeSelf)return;
   var snapshot=state.Inspect(item.Id);bool visible=snapshot!=null&&!snapshot.Removed&&(snapshot.Owner==null?worldVisible&&item.WorldVisible&&!item.Culled:held);
   if(item.View.activeSelf!=visible)item.View.SetActive(visible);if(held||!visible||item.RestPosed)return;
   // Rest pose once per showing: the held-item parts only change while the item is held.
   item.RestPosed=true;foreach(Transform part in item.View.transform)if(part.name=="Cap")part.gameObject.SetActive(true);if(item.Ring!=null)item.Ring.gameObject.SetActive(false);if(item.Peels!=null)item.Peels.gameObject.SetActive(false);var label=item.View.GetComponentInChildren<TextMesh>(true);if(label!=null)label.gameObject.SetActive(false);
  }
  internal void CullWorldViews(int visits)
  {
   if(state==null||!Ready)return;if(worldViewSweep==null)worldViewSweep=new R26CullSweep<WorldItem>(items);
   string id;WorldItem item;worldViewSweep.Begin(visits);
   while(worldViewSweep.Next(out id,out item))
   {
    // Only a shown world prop is culled; held and stowed items are never touched. No allocation.
    if(item.View==null)continue;bool wanted=R26PickupCulling.Wanted(item.View.transform.position,!item.Culled);
    if(item.Culled){if(!wanted)continue;item.Culled=false;SyncView(item);}
    else if(!wanted&&id!=HeldId&&item.View.activeSelf){item.Culled=true;SyncView(item);}
   }
  }
 }
}
#endif
