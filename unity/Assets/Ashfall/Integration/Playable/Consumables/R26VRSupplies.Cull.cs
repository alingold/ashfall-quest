#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.World.Loot;using UnityEngine;
namespace Ashfall.Integration.Playable.Consumables {
 public sealed partial class R24VRSupplies {
  // MP-PERF: supply boxes and backpacks are shown only near the local viewer (presentation only).
  R26CullSweep<Box> worldViewSweep;
  internal void CullWorldViews(int visits)
  {
   if(state==null||!Ready)return;if(worldViewSweep==null)worldViewSweep=new R26CullSweep<Box>(boxes);
   string id;Box box;worldViewSweep.Begin(visits);
   while(worldViewSweep.Next(out id,out box)){bool culled=!R26PickupCulling.Wanted(box.Position,!box.Culled);if(culled==box.Culled)continue;box.Culled=culled;Refresh(box);}
  }
 }
}
#endif
