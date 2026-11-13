#if UNITY_6000_0_OR_NEWER
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23HandsWeapons {
  public bool ReloadClaimsHand(int hand){
   // Source never takes an existing climb/objective claim, nor reserves an
   // offhand merely because a gun is waiting for reload somewhere else.
   if(!Ready||!HasWeapon||holstered||presentationSuppressed||gameplayPaused||!hadFocus||Health<=0||!isActiveAndEnabled||hand!=SupportHand||!HandAvailable(hand)||IsDual||IsMelee)return false;
   if(haveInputSnapshot&&lastInput.Hand(hand).Grip>.2f&&ReloadHandOccupied)return true;
   var point=To(weapon.InverseTransformPoint(HandTransform(hand).position)/LogicalReloadScale);
   if(IsSmaw){
    if(smawView==null||!smawView.Ready)return false;var phase=round.ActiveSnapshot.Phase;
    if(phase!=ReloadPhase.NeedsMagazine&&phase!=ReloadPhase.HeldMagazine)return false;
    var center=To(weapon.InverseTransformPoint(phase==ReloadPhase.NeedsMagazine?smawView.ReloadOfferPosition:smawView.ReloadInsertionPosition)/LogicalReloadScale);
    if(WeaponHandling.SourceRifleReach(point,center,.18f,LogicalReloadScale))return true;
    return phase==ReloadPhase.NeedsMagazine&&WeaponHandling.SourceRifleReach(point,new Ashfall.Core.Float3(center.X,center.Y,center.Z-.06f/LogicalReloadScale),.18f,LogicalReloadScale);
   }
   return R23ReloadGripRules.InZone(ActiveWeaponId,round.ActiveSnapshot,point,primaryHand==0,IsM60?round.M60.Stage:R24M60Stage.Ready);
  }
 }
}
#endif
