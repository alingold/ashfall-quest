using System;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Original weapon_rarity_r6.json/.gd. Values belong to the individual instance.
    public sealed class R23WeaponInstanceInfo
    {
        public readonly string ActionId,InstanceId,ProfileId;
        public readonly int Stars,Capacity;
        public readonly float BodyDamage,HeadDamage,RecoilScale,SpreadScale;
        internal R23WeaponInstanceInfo(string action,string instance,string profile,int stars,R23RarityStats stats){ActionId=action;InstanceId=instance;ProfileId=profile;Stars=stars;Capacity=stats.Capacity;BodyDamage=stats.BodyDamage;HeadDamage=stats.HeadDamage;RecoilScale=stats.RecoilScale;SpreadScale=stats.SpreadScale;}
    }
    public struct R23RarityStats
    {
        public readonly int Capacity;public readonly float BodyDamage,HeadDamage,RecoilScale,SpreadScale;
        internal R23RarityStats(int capacity,float body,float head,float recoil,float spread){Capacity=capacity;BodyDamage=body;HeadDamage=head;RecoilScale=recoil;SpreadScale=spread;}
    }
    public static class R23WeaponRarity
    {
        public static bool TryRead(string profile,int stars,out R23RarityStats stats){stats=new R23RarityStats();if(stars<1||stars>5)return false;int i=stars-1;switch(profile){
            case "katana":case "excalibur":case "cyberblade":stats=new R23RarityStats(0,0,0,1,1);return true;
            case "dt11":stats=new R23RarityStats(2,96,96,1,new float[]{1,.92f,.84f,.76f,.68f}[i]);return true;
            case "knife":stats=new R23RarityStats(0,35,35,1,1);return true;
            case "smaw":stats=new R23RarityStats(1,new float[]{60,84,109,134,160}[i],new float[]{60,84,109,134,160}[i],1,1);return true;
            case "awp":stats=new R23RarityStats(3,new float[]{70,78,87,95,104}[i],new float[]{117,132,148,164,180}[i],1,1);return true;
            case "mk18":stats=new R23RarityStats((int)(new float[]{18f,24f,30f,36f,42f})[i],(new float[]{22f,22f,22f,22f,22f})[i],(new float[]{33f,33f,33f,33f,33f})[i],(new float[]{1f,0.85f,0.7f,0.55f,0.4f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "akm":stats=new R23RarityStats((int)(new float[]{20f,24f,28f,32f,36f})[i],(new float[]{20f,20f,20f,20f,20f})[i],(new float[]{30f,30f,30f,30f,30f})[i],(new float[]{1f,0.89f,0.78f,0.67f,0.56f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "spr":stats=new R23RarityStats((int)(new float[]{4f,4f,4f,4f,4f})[i],(new float[]{55f,59f,63f,67f,71f})[i],(new float[]{92f,106f,121f,137f,154f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "magnum":stats=new R23RarityStats((int)(new float[]{7f,7f,7f,7f,7f})[i],(new float[]{63f,67f,71f,75f,79f})[i],(new float[]{81f,93f,106f,119f,134f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "cx4":stats=new R23RarityStats((int)(new float[]{25f,30f,35f,40f,45f})[i],(new float[]{19f,19f,19f,19f,19f})[i],(new float[]{23f,23f,23f,23f,23f})[i],(new float[]{1f,0.85f,0.7f,0.55f,0.4f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "px4":stats=new R23RarityStats((int)(new float[]{16f,16f,16f,16f,16f})[i],(new float[]{30f,31f,33f,34f,36f})[i],(new float[]{45f,50f,56f,62f,68f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "m1014":stats=new R23RarityStats((int)(new float[]{6f,6f,6f,6f,6f})[i],(new float[]{77f,77f,77f,77f,77f})[i],(new float[]{77f,77f,77f,77f,77f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,0.9f,0.8f,0.7f,0.6f})[i]);return true;
            case "ump":stats=new R23RarityStats((int)(new float[]{28f,32f,36f,40f,44f})[i],(new float[]{19f,19f,19f,19f,19f})[i],(new float[]{23f,23f,23f,23f,23f})[i],(new float[]{1f,0.9f,0.8f,0.7f,0.6f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "p90":stats=new R23RarityStats((int)(new float[]{45f,50f,55f,60f,65f})[i],(new float[]{20f,20f,20f,20f,20f})[i],(new float[]{22f,22f,22f,22f,22f})[i],(new float[]{1f,0.89f,0.78f,0.67f,0.56f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "mp5":stats=new R23RarityStats((int)(new float[]{21f,24f,27f,30f,33f})[i],(new float[]{22f,22f,22f,22f,22f})[i],(new float[]{33f,33f,33f,33f,33f})[i],(new float[]{1f,0.88f,0.76f,0.64f,0.52f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "mp9":stats=new R23RarityStats((int)(new float[]{24f,28f,32f,36f,40f})[i],(new float[]{21f,21f,21f,21f,21f})[i],(new float[]{26f,26f,26f,26f,26f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "1911":stats=new R23RarityStats((int)(new float[]{8f,8f,8f,8f,8f})[i],(new float[]{44f,45f,47f,49f,51f})[i],(new float[]{61f,64f,68f,71f,75f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "tec9":stats=new R23RarityStats((int)(new float[]{12f,12f,12f,12f,12f})[i],(new float[]{25f,26f,27f,28f,29f})[i],(new float[]{30f,32f,35f,37f,40f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "rfb":stats=new R23RarityStats((int)(new float[]{25f,30f,35f,40f,45f})[i],(new float[]{19f,19f,19f,19f,19f})[i],(new float[]{23f,23f,23f,23f,23f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "fal":stats=new R23RarityStats((int)(new float[]{15f,18f,21f,24f,27f})[i],(new float[]{33f,33f,33f,33f,33f})[i],(new float[]{44f,44f,44f,44f,44f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            case "s85":stats=new R23RarityStats((int)(new float[]{5f,5f,5f,5f,5f})[i],(new float[]{65f,69f,73f,77f,81f})[i],(new float[]{105f,119f,133f,147f,161f})[i],(new float[]{1f,1f,1f,1f,1f})[i],(new float[]{1f,1f,1f,1f,1f})[i]);return true;
            default:return false;}}
    }
}
