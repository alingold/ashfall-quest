#if UNITY_6000_0_OR_NEWER
using System;
namespace Ashfall.Integration.Playable.Consumables {
 public sealed partial class R23VRConsumables {
  internal Action<string> SharedRetire;
  // Lifetime/availability is separate from distance culling: a guest can reach
  // a live cache whose art is culled on the host. Hidden local aliases cannot claim.
  internal bool CanClaimWorldFood(string id){WorldItem item;if(state==null||id==null||!worldVisible||!items.TryGetValue(id,out item)||item.View==null||!item.WorldVisible)return false;var snapshot=state.Inspect(id);return snapshot!=null&&!snapshot.Removed&&snapshot.Owner==null;}
  void RetireSharedFood(string id){if(SharedRetire!=null)SharedRetire(id);}
 }
}
#endif
