#if UNITY_6000_0_OR_NEWER
using System;using Ashfall.Integration.Playable.World.Loot;using UnityEngine;
namespace Ashfall.Integration.Playable.Explosives {
 public sealed partial class R23VRExplosives {
  // MP-PERF: unowned grenade art is shown only near the local viewer. `Visible` stays the
  // eligibility set by loot streaming and host custody; `Culled` is presentation only.
  R26CullSweep<Item> worldViewSweep;
  public Func<string,bool> WorldViewExempt;
  bool CulledNow(Item item){return !(WorldViewExempt!=null&&WorldViewExempt(item.Id))&&!R26PickupCulling.Wanted(item.Position,!item.Culled);}
  // One item's art state; the per-frame pass and every setter share it.
  void RefreshView(Item item)
  {
   R23GrenadeItemState s;state.TryRead(item.Id,out s);bool held=HeldId==item.Id;bool show=!s.Spent&&(held?SimulationEnabled&&HandReady(HeldHand):!s.Owned&&worldVisible&&item.Visible&&!item.Culled);
   if(item.View==null&&show&&Ready)CreateView(item);if(item.View==null)return;if(item.View.activeSelf!=show)item.View.SetActive(show);if(!show)return;
   if(held&&Hand(HeldHand)!=null){var h=Hand(HeldHand);var q=Grip(h);item.View.transform.SetPositionAndRotation(h.position+q*new Vector3(0,-.015f,.03f),q);}
   else if(!s.Owned)item.View.transform.SetPositionAndRotation(item.Position,item.Rotation);
  }
  internal void CullWorldViews(int visits)
  {
   if(!Ready)return;if(worldViewSweep==null)worldViewSweep=new R26CullSweep<Item>(items);
   string id;Item item;worldViewSweep.Begin(visits);
   while(worldViewSweep.Next(out id,out item)){R23GrenadeItemState s;if(!state.TryRead(id,out s)||s.Owned||s.Spent){item.Culled=false;continue;}bool culled=CulledNow(item);if(culled==item.Culled)continue;item.Culled=culled;RefreshView(item);}
  }
 }
}
#endif
