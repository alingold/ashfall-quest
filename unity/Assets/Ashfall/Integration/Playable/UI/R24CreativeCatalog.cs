using System;
namespace Ashfall.Integration.Playable.UI
{
    // R6 weapon_rack ACTIVE_WEAPON_INDICES, with frag/zone handled by their
    // actual explosive owner. This catalog grants instances, never free ammo
    // to an existing carried instance.
    public static class R24CreativeCatalog
    {
        static readonly string[] weapons={"grapple","cx4","dt11","px4","mk18","awp","ump","magnum","m60","m1014","p90","uzis","matadors","spr","katana","excalibur","cyberblade","mp5","mp9","1911","tec9","akm","rfb","fal","s85","frag","smaw","zone","knife","flash","smoke"};
        static readonly string[] supplies={"pistol","rifle","shells","sniper","smg","builds"};
        static readonly string[] items={"banana","soda","shaker","backpack"};
        public static string[] Weapons(){return (string[])weapons.Clone();}
        public static string[] Supplies(){return (string[])supplies.Clone();}
        public static string[] Items(){return (string[])items.Clone();}
        public static bool Enabled(bool matchActive,bool matchAllowsCatalog){return !matchActive||matchAllowsCatalog;}
        public static bool Contains(string kind,string value)
        {return kind=="weapon"?Array.IndexOf(weapons,value)>=0:kind=="supply"?Array.IndexOf(supplies,value)>=0:kind=="item"?Array.IndexOf(items,value)>=0:kind=="utility"&&value=="revive";}
    }
    public sealed class R24CreativeRequest
    {
        public readonly string Kind,Value;public readonly int Hand,Stars;public readonly bool Drop;
        public R24CreativeRequest(string kind,string value,int hand,int stars,bool drop)
        {if(!R24CreativeCatalog.Contains(kind,value)||hand<0||hand>1||stars<1||stars>5)throw new ArgumentException("Actual catalog entry/hand/rarity required");Kind=kind;Value=value;Hand=hand;Stars=stars;Drop=drop;}
    }
}
