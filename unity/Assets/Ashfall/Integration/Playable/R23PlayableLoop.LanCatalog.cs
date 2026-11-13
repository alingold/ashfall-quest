#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable {
 public sealed partial class R23PlayableLoop {
  internal bool LanCatalogPickup{get{return catalogPickup;}}
  internal bool LanWorldClaimIntent(string kind,string id,int hand){if(catalogPickup)return true;if(!routeObjectiveInput||selectedObjective.Hand!=hand||selectedObjective.Id!=id)return false;return kind=="weapon"?selectedObjective.Kind==Input.R23GripObjectiveKind.Weapon:kind=="grenade"?selectedObjective.Kind==Input.R23GripObjectiveKind.Explosive:selectedObjective.Kind==Input.R23GripObjectiveKind.Consumable;}
  internal bool LanClaimHandHeld(int hand){var sample=hand==0?Rig.LeftInput:Rig.RightInput;return Ready&&roundStarted&&!switchingMap&&Gameplay.Health>0&&Rig.SessionFocused&&sample.Tracked&&sample.Grip>=.65f&&!PageBlocksGameplay&&!BackpackOpen&&!(hand==0?Climbing.LeftAnchored:Climbing.RightAnchored);}

  // Reuses the normal native custody/slot/hand transaction after host registration.
  internal bool CompleteLanCatalogPickup(string id,string kind,string value,int hand){if(!Ready||!roundStarted||switchingMap||Gameplay.Health<=0||!Rig.Tracking||!Rig.SessionFocused||PageBlocksGameplay||!(hand==0?Rig.LeftInput.Tracked:Rig.RightInput.Tracked))return false;
   if(kind=="item"&&value=="backpack"){Supplies.UpdateInputAvailability(SupplyInput(true,Rig.LeftInput,Rig.RightInput));return Supplies.TryCollect(id,hand+1)==1;}
   catalogPickup=true;try{bool grenade=kind=="weapon"&&Ashfall.Integration.Playable.Explosives.R23GrenadeState.ValidKind(value);if(grenade&&OwnsExplosive(id))return TryEquipOwnedItem(id,hand);if(!MakeRoomForPickup(value,kind=="weapon"&&!grenade,grenade))return false;StowPaddles();Consumables.Stow();if(Explosives!=null)Explosives.Stow();if(Gameplay.HasWeapon)Gameplay.HolsterCurrent();Gameplay.UpdateInputAvailability(AvailabilityFrame());
   if(grenade)return OwnsExplosive(id)?Explosives.TryEquip(id,hand+1,true):Explosives.TryPickup(id,hand+1,true);if(kind=="weapon")return Gameplay.CollectCatalogWorldWeapon(id,hand);SampleConsumableAvailability();return Consumables.TryPickup(id,hand+1);}finally{catalogPickup=false;}
  }
 }
}
#endif
