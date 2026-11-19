#if UNITY_6000_0_OR_NEWER
using System;using Ashfall.Integration.Playable.World.Loot;using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23HandsWeapons {
  // MP-PERF: `Visible` is eligibility (loot streaming, host custody). The world visual is
  // presentation: it exists only while the copy is eligible AND near the local viewer.
  R26CullSweep<WorldWeapon> worldVisualSweep;
  // Copies that stay presented at any distance (the review kit, displays). Presentation only.
  public Func<string,bool> WorldVisualExempt;
  bool WorldVisualAlways(string id){return WorldVisualExempt!=null&&WorldVisualExempt(id);}
  void PresentWorldVisual(string id,WorldWeapon record){record.Visual=CreateWorldWeaponVisual(id,Array.IndexOf(weaponIds,ResolveWeaponProfile(id)),record.Position,record.Rotation);QueueWorldWeaponSettle(id);}
  void WithdrawWorldVisual(WorldWeapon record){WorldPickupCenter(record);record.Visual.gameObject.SetActive(false);Destroy(record.Visual.gameObject);record.Visual=null;}
  // A newly eligible copy within arm's reach is presented at once; farther ones wait for the sweep's budget.
  bool WorldVisualNow(string id,WorldWeapon record){return WorldVisualAlways(id)||R26PickupCulling.Within(record.Position,R26PickupCulling.UrgentRange);}
  // One bounded slice per frame: reconciles eligible copies against viewer distance and
  // builds at most `builds` visuals (copies within arm's reach are never deferred).
  internal void CullWorldVisuals(int visits,int builds)
  {
   if(!Ready||round==null)return;if(worldVisualSweep==null)worldVisualSweep=new R26CullSweep<WorldWeapon>(droppedWeapons);
   string id;WorldWeapon record;bool changed=false;worldVisualSweep.Begin(visits);
   while(worldVisualSweep.Next(out id,out record))
   {
    if(!record.Visible)continue;bool wanted=WorldVisualAlways(id)||R26PickupCulling.Wanted(record.Position,record.Visual!=null);
    if(wanted&&record.Visual==null){if(builds<=0&&!R26PickupCulling.Within(record.Position,R26PickupCulling.UrgentRange))continue;PresentWorldVisual(id,record);builds--;changed=true;}
    else if(!wanted&&record.Visual!=null){WithdrawWorldVisual(record);changed=true;}
   }
   if(changed)UpdateWeaponPresentation();
  }
 }
}
#endif
