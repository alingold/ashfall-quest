using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
    public sealed partial class SourceRifleDefinition
    {
        // Original one_star050 cadence/capacity +0991 first-rarity listed damage. Each inherits
        // catalog_firearm.gd, including base forward support:1911 is not pistol.gd/PX4.
        public static readonly SourceRifleDefinition M1911=new SourceRifleDefinition("1911",8,1,1f/5f,0,.55f,
            new Float3(0,-.278f,.03945997842f),new Float3(0,-.18f,.03945997842f),new Float3(.00009799562f,.04938651152f,.2075922251f),
            "1911","pistol",true,44,61,sourceManualControls:true,vrOpeningOnly:true);
        public static readonly SourceRifleDefinition Tec9=new SourceRifleDefinition("tec9",12,1,1f/14.4f,0,.72f,
            new Float3(0,-.278f,.20857775698f),new Float3(0,-.18f,.20857775698f),new Float3(-.02756504908f,.0667179746f,.19203866124f),
            "TEC9","pistol",false,25,30,sourceManualControls:true,vrOpeningOnly:true);
        public static readonly SourceRifleDefinition Rfb=new SourceRifleDefinition("rfb",25,1,1f/9.5f,0,.8f,
            new Float3(0,-.28f,-.21889231405f),new Float3(0,-.10f,-.21889231405f),new Float3(.065f,.055f,.10f),
            "RFB","rifle",false,19,23,offerReach:.14f,insertReach:.14f,handleReach:.13f,handlePull:.08f,sourceManualControls:false,vrOpeningOnly:true);
        public static readonly SourceRifleDefinition Fal=new SourceRifleDefinition("fal",15,1,1f/5f,0,1.037875f,
            new Float3(0,-.278f,.16448644208f),new Float3(0,-.18f,.16448644208f),new Float3(.00015203089f,.06701772309f,.15670422316f),
            "FAL","rifle",true,33,44,sourceManualControls:true,vrOpeningOnly:true);
        // Source S85 current M1 Carbine geometry, ZERO bolt rest and current AABB center.
        // Offer raised .1524 physical metres; insertion is its special raised offer-relative zone.
        public static readonly SourceRifleDefinition S85=new SourceRifleDefinition("s85",5,1,1f/1.4f,0,1.12f,
            new Float3(0,-.278f+.1524f/1.12f,.13077066646f),new Float3(0,-.18f,.13077066646f),new Float3(.01008660719f,.0540779233f,.18385013938f),
            "S85","sniper",true,65,105,boltAction:true,s85Insertion:true,sourceManualControls:true,vrOpeningOnly:true);
        static partial void ResolveCatalogProfile(string id,ref SourceRifleDefinition result)
        {result=id=="1911"?M1911:id=="tec9"?Tec9:id=="rfb"?Rfb:id=="fal"?Fal:id=="s85"?S85:null;}
        static partial void AppendCatalogProfiles(List<SourceRifleDefinition> profiles)
        {profiles.AddRange(new[]{M1911,Tec9,Rfb,Fal,S85});}
    }
}
