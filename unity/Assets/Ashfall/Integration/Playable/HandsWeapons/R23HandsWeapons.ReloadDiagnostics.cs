#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23HandsWeapons {
  // Value copies only. Compare the last input actually supplied to the native
  // owner with today's raw hand in the same source logical (non-recoil) frame.
  public bool TryReadReloadControls(out HandlingInput sampled,out Vector3 currentSupportLocal,out Vector3 frameOrigin,out Quaternion frameRotation,out float frameScale){
   sampled=trackedControls;currentSupportLocal=frameOrigin=Vector3.zero;frameRotation=Quaternion.identity;frameScale=1;
   if(!Ready||weapon==null||SupportTransform==null)return false;
   frameOrigin=weapon.position;frameRotation=weapon.rotation;frameScale=LogicalReloadScale;
   currentSupportLocal=weapon.InverseTransformPoint(SupportTransform.position)/frameScale;
   return haveTrackedControls;
  }
 }
}
#endif
