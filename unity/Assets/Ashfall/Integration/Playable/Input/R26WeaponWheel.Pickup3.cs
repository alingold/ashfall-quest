#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Input {
 public sealed partial class R23WeaponWheel {
  internal string Pickup3State {get{return Active+":"+HighlightedSector+" ring="+state.DropFraction+" pending="+(pendingDropId??"none");}}
  internal Vector3 Pickup3SlotPoint(int slot){return root.TransformPoint(At(R23WeaponWheelRules.SectorForSlot(slot),.215f));}
 }
}
#endif
