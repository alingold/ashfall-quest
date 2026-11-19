using System;using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public enum R24M60Stage {Ready,CoverOpen,BoxRemoved,HoldingBox,NeedsFeed,BoxSeated,NeedsCharge}
 public static class R24M60Definition {
  public const string Id="m60",Family="rifle";public const float Scale=1,MaximumRps=7.2f,Range=Ashfall.Modules.WeaponHandling.SourceRifleDefinition.FirearmRange;
  public static readonly Float3 Muzzle=new Float3(0,.04f,.91f),Foregrip=new Float3(0,-.025f,.36f),CoverHome=new Float3(.0015411414f,.0199894716f,.07853131694f),CoverGrab=new Float3(-.0003907146f,.0980003095f,-.2963777456f),BoxHome=new Float3(-.04931006848f,.01512533089f,.04920581269f),BoxOffer=new Float3(-.04931006848f,-.2348746691f,.04920581269f),HandleHome=new Float3(.0448353235f,.00426057515f,.09120431243f),BeltGrab=new Float3(-.06235352715f,.05377213027f,.02618776733f),PrimaryGrip=new Float3(-.00011193297f,-.08511693818f,-.00940797724f);
  public static int Capacity(int stars){if(stars<1||stars>5)throw new ArgumentOutOfRangeException("stars");return 60+15*stars;}
  public static Float3 Mirror(Float3 p,bool left){return new Float3(left?-p.X:p.X,p.Y,p.Z);}
  public static Float3 Seat(bool left){return Mirror(BoxHome,left);}public static Float3 Offer(bool left){return Mirror(BoxOffer,left);}public static Float3 Handle(bool left){return Mirror(HandleHome,left);}
  public static Float3 Grab(R24M60Stage stage,bool left){if(stage==R24M60Stage.CoverOpen||stage==R24M60Stage.BoxRemoved){var p=stage==R24M60Stage.CoverOpen?BoxHome:BoxOffer;return Mirror(new Float3(p.X-.075f,p.Y,p.Z),left);}if(stage==R24M60Stage.HoldingBox)return Seat(left);if(stage==R24M60Stage.NeedsFeed)return Mirror(BeltGrab,left);if(stage==R24M60Stage.NeedsCharge)return Handle(left);float a=stage==R24M60Stage.Ready||stage==R24M60Stage.NeedsCharge?0:1.35f,c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Float3(CoverHome.X+CoverGrab.X,CoverHome.Y+CoverGrab.Y*c-CoverGrab.Z*s,CoverHome.Z+CoverGrab.Y*s+CoverGrab.Z*c);}
  public static bool Reach(Float3 p,Float3 at,float radius){return Ashfall.Modules.WeaponHandling.WeaponHandling.Reach(p,at,radius);}
 }
}
namespace Ashfall.Modules.WeaponHandling {
 public sealed partial class SourceRifleDefinition {
  // Metadata only. Real production dispatch is R24M60Owner/ApplyM60, never ApplySourceRifle.
  public static readonly SourceRifleDefinition M60=new SourceRifleDefinition("m60",75,1,1f/7.2f,0,1,new Float3(-.04931006848f,-.2348746691f,.04920581269f),new Float3(-.04931006848f,.01512533089f,.04920581269f),new Float3(.0448353235f,.00426057515f,.09120431243f),"M60","rifle",false,27,33,sourceManualControls:true,vrOpeningOnly:false);
 }
}
