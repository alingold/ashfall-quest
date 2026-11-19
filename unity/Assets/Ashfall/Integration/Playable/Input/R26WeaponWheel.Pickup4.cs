#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Input {
 public sealed partial class R23WeaponWheel {
  // PICKUP4 diagnostics: the wheel slot presenting an owned instance id or a consumable/grenade kind.
  internal string Pickup4Slot(int i){if(i<0||i>=R23WeaponWheelRules.MaximumSlots)return "none";var s=state.Slot(i);return s==null?"empty":s.Id+" canDrop="+s.CanDrop+" dropSlot="+state.DropSlot+" ring="+state.DropFraction.ToString("F2");}
  internal int Pickup4SlotOf(string idOrKind){for(int i=0;i<R23WeaponWheelRules.MaximumSlots;i++){var s=state.Slot(i);if(s!=null&&(s.Id==idOrKind||s.WeaponId==idOrKind))return i;}return -1;}
 }
}
#endif
