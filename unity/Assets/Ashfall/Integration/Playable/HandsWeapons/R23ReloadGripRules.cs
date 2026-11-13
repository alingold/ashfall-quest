using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons {
 // Source weapon.gd reload_claims_hand/get_reload_zones. These are proximity
 // reservations BEFORE a fresh grip, independently of held-part ownership.
 public static class R23ReloadGripRules {
  static bool Near(Float3 point,Float3 center,float radius,float scale){return WeaponHandling.SourceRifleReach(point,center,radius,scale);}
  public static bool InZone(string profile,HandlingSnapshot state,Float3 point,bool leftHanded=false,R24M60Stage m60Stage=R24M60Stage.Ready){
   if(!point.IsFinite)return false;
   var phase=state.Phase;
   if(profile=="m1014"){
    if(phase!=ReloadPhase.NeedsMagazine&&phase!=ReloadPhase.HeldMagazine&&phase!=ReloadPhase.NeedsRack)return false;
    var center=phase==ReloadPhase.NeedsRack?new Float3(leftHanded?-.06f:.06f,.045f,.10f):R23M1014Definition.Offer;
    return Near(point,center,.115f,1);
   }
   if(profile=="magnum")return phase==ReloadPhase.CylinderOpen?Near(point,R23MagnumRuntime.Offer(leftHanded),.18f,R23MagnumRuntime.ModelScale):phase==ReloadPhase.CylinderLoaded&&Near(point,R23MagnumRuntime.OpenPoint(leftHanded),.18f,R23MagnumRuntime.ModelScale);
   if(profile=="dt11")return phase==ReloadPhase.NeedsMagazine?Near(point,R24Dt11Definition.Offer(state.BoltTravel),.16f,R24Dt11Definition.Scale):phase==ReloadPhase.NeedsRack&&Near(point,R24Dt11Definition.CloseGrip(state.BoltTravel),.24f,R24Dt11Definition.Scale);
   if(profile=="awp")return phase==ReloadPhase.NeedsBolt?WeaponHandling.AwpBoltReach(point):(phase==ReloadPhase.NeedsMagazine||phase==ReloadPhase.HeldMagazine)&&(Near(point,R24AwpDefinition.Offer,.25f,1)||Near(point,R24AwpDefinition.Insert,.25f,1));
   if(profile=="m60")return m60Stage!=R24M60Stage.Ready&&Near(point,R24M60Definition.Grab(m60Stage,leftHanded),.105f,1);
   var definition=SourceRifleDefinition.ForId(profile);if(definition==null)return false;
   if(phase==ReloadPhase.NeedsMagazine)return Near(point,definition.Offer,definition.TopFeed?.25f:definition.OfferReach,definition.ReloadScale);
   if(phase==ReloadPhase.NeedsRack)return Near(point,definition.ChargingPoint(leftHanded),definition.TopFeed?.12f:definition.HandleReach,definition.ReloadScale);
   if(phase!=ReloadPhase.HeldMagazine)return false;
   if(definition.S85Insertion){float x=(point.X-definition.Offer.X)*definition.ReloadScale,y=(point.Y-definition.Offer.Y)*definition.ReloadScale,z=(point.Z-definition.Offer.Z)*definition.ReloadScale;return y>=.045f&&y<=.40f&&x*x+z*z<=.16f*.16f;}
   return Near(point,definition.Insert,definition.TopFeed?.25f:definition.InsertReach,definition.ReloadScale);
  }
 }
}
