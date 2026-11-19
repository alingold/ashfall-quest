#if UNITY_6000_0_OR_NEWER
using System.Text;
namespace Ashfall.Integration.Playable {
 public sealed partial class R23PlayableLoop {
  // PICKUP4 diagnostics and opt-in evidence seams (bot only, XR refused).
  static bool Pickup4Bot{get{return R25NetBot.Arg("-ashfallNetBotPickup4")=="true"&&!UnityEngine.XR.XRSettings.isDeviceActive;}}
  internal int Pickup4Used{get{return Gameplay.OwnedWeaponActionIds.Length+ConsumableSlotCount+ExplosiveSlotCount;}}
  internal bool Pickup4Live{get{return Ready&&roundStarted&&!switchingMap&&ActualMatchRules!=null&&MatchActive&&LanRoundBound&&Gameplay.Health>0;}}
  internal string Pickup4State{get{var s=new StringBuilder();s.Append("cap=").Append(InventoryCapacity).Append(" w=").Append(Gameplay.OwnedWeaponActionIds.Length).Append(" f=").Append(ConsumableSlotCount).Append(" g=").Append(ExplosiveSlotCount).Append(" live=").Append(Pickup4Live).Append(" page=").Append(UI.Page).Append(" weapons=").Append(System.String.Join(",",Gameplay.OwnedWeaponActionIds)).Append(" foods=");
   foreach(var item in Consumables.OwnedItems)s.Append(item.Id).Append(':').Append(item.Kind).Append('@').Append(item.Slot).Append(',');s.Append(" grenades=");if(Explosives!=null)foreach(var item in Explosives.OwnedItems)s.Append(item.Id).Append('@').Append(item.Slot).Append(',');s.Append(" room=").Append(CanCollectWeapon());return s.ToString();}}
  internal string Pickup4Objective{get{return "objective="+selectedObjective.Kind+":"+selectedObjective.Id+"@"+selectedObjective.Hand+" route="+routeObjectiveInput;}}
  internal void Pickup4OpenBackpack(){if(Pickup4Bot)OpenBackpack();}
  internal bool Pickup4BagGesture(string id,bool grip){return Pickup4Bot&&BackpackOpen&&UI.Pickup4Gesture(id,grip);}
 }
}
#endif
