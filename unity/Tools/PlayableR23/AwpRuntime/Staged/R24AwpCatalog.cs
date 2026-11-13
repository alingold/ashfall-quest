using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
 public sealed partial class SourceRifleDefinition {
  public static readonly SourceRifleDefinition Awp=new SourceRifleDefinition("awp",3,1,.95f,0,1,new Float3(0,-.1764f,.1061082762f),new Float3(0,-.18f,.1061082762f),new Float3(.072f,.045f,.07f),"AWP","sniper",true,70,117,180,boltAction:true,offerReach:.25f,insertReach:.25f,handleReach:.20f,handlePull:.115f,sourceManualControls:true,vrOpeningOnly:false);
 }
}
