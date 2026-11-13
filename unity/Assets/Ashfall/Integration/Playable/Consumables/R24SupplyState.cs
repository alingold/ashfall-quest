using System;
using System.Collections.Generic;
namespace Ashfall.Integration.Playable.Consumables
{
    public sealed class R24SupplyItem
    {
        public readonly string Id,Family;public readonly int Amount;public readonly bool Collected;
        internal R24SupplyItem(string id,string family,int amount,bool collected){Id=id;Family=family;Amount=amount;Collected=collected;}
    }
    public sealed class R24SupplyState
    {
        sealed class Box{internal string Id,Family;internal int Amount;internal bool Collected;}
        readonly Dictionary<string,Box> boxes=new Dictionary<string,Box>(StringComparer.Ordinal);readonly Func<string,string,int,int> grant;bool busy;
        public R24SupplyState(Func<string,string,int,int> realGrant){if(realGrant==null)throw new ArgumentNullException("realGrant");grant=realGrant;}
        public static string[] Families {get{return new[]{"rifle","pistol","shells","smg","sniper","builds","backpack"};}}
        public static int DefaultAmount(string family){switch(family){case "pistol":return 16;case "smg":return 20;case "rifle":return 32;case "shells":case "builds":return 6;case "sniper":return 5;case "backpack":return 1;default:return 0;}}
        public static string Label(string family){return family=="backpack"?"BACKPACK +1 SLOT":family=="rifle"?"ASSAULT":family=="shells"?"SHOTGUN":family=="builds"?"BUILDS":family=="pistol"?"PISTOL":family=="smg"?"SMG":"SNIPER";}
        public bool Register(string id,string family,int amount=-1){if(String.IsNullOrEmpty(id)||id.Length>128||DefaultAmount(family)==0||boxes.ContainsKey(id))return false;boxes.Add(id,new Box{Id=id,Family=family,Amount=amount<0?DefaultAmount(family):amount});return true;}
        public bool TryRead(string id,out R24SupplyItem item){item=null;Box box;if(id==null||!boxes.TryGetValue(id,out box))return false;item=new R24SupplyItem(box.Id,box.Family,box.Amount,box.Collected);return true;}
        public int Collect(string id)
        {
            Box box;if(busy||id==null||!boxes.TryGetValue(id,out box)||box.Collected||box.Amount<=0)return 0;
            busy=true;try{int accepted=grant("pickup089:"+box.Id,box.Family,box.Amount);if(accepted<=0)return 0;box.Collected=true;return accepted;}finally{busy=false;}
        }
        // Trusted world-layout teardown only; collected event tombstones are retained.
        public bool RemoveUncollected(string id)
        {Box box;if(busy||id==null||!boxes.TryGetValue(id,out box)||box.Collected)return false;return boxes.Remove(id);}
        public void ResetRound(){foreach(var box in boxes.Values)box.Collected=false;}
    }
}
