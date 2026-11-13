using System;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Original weapon_inventory.get_aimed_pickup. LOS is sampled along head->hand
    // and hand->actual pickup bounds centre; never an independent gaze/aim ray.
    public static class R23WeaponPickupRules
    {
        static float WorldGrip(R23HandInput input){return input.Grip==0&&input.GripPressed?1:input.Grip;}
        public static bool GripNeutral(R23HandInput input){return WorldGrip(input)<=.2f;}
        public static bool GripClaim(R23HandInput input){return WorldGrip(input)>=.65f;}
        public const float Reach=2,Proximity=.35f,AimDot=.94f;
        public static bool TryScore(float distance,float alignment,bool headClear,bool handClear,out float score){score=float.NegativeInfinity;if(!headClear||!handClear||float.IsNaN(distance)||float.IsInfinity(distance)||float.IsNaN(alignment)||float.IsInfinity(alignment)||distance<0||distance>Reach||distance>Proximity&&alignment<=AimDot)return false;score=distance<=Proximity?2+(Proximity-distance):alignment-distance*.025f;return true;}
        public static int CatalogIndex(string id){switch(id){case "katana":return 14;case "excalibur":return 15;case "cyberblade":return 16;case "knife":return 29;case "smaw":return 27;case "1911":return 19;case "tec9":return 20;case "rfb":return 23;case "fal":return 24;case "s85":return 25;case "ump":return 6;case "p90":return 10;case "mp5":return 17;case "mp9":return 18;case "m1014":return 9;case "cx4":return 0;case "px4":return 2;case "akm":return 21;case "spr":return 13;case "magnum":return 7;case "mk18":return 3;default:return int.MaxValue;}}
    }
}
