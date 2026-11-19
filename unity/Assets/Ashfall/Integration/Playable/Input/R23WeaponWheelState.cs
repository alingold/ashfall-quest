using System;
namespace Ashfall.Integration.Playable.Input
{
    public static class R23InventoryPresentation
    {
        static readonly string[] RarityColors={"ffffff","4b9bff","bd69ff","ffc542","ff415c"};
        public static string Name(string profile)
        {switch(profile){case "grapple":return "GRAPPLE GUN";case "uzis":return "DUAL UZIS";case "matadors":return "DUAL MATADORS";case "mk18":return "MK18";case "spr":return "SPR";case "mp9":return "SILENCED MP9";case "knife":return "FIELD KNIFE";case "katana":return "ASH KATANA";case "excalibur":return "RIDGE EXCALIBUR";case "cyberblade":return "ARC CYBERBLADE";case "flash":return "FLASHBANG";case "smoke":return "SMOKE GRENADE";case "zone":return "ZONE GRENADE";case "banana":return "BANANA";case "soda":return "SODA";case "shaker":return "SHIELD SHAKER";default:return string.IsNullOrEmpty(profile)?null:profile.ToUpperInvariant();}}
        public static bool Tiered(string profile){return !string.IsNullOrEmpty(profile)&&profile!="flash"&&profile!="smoke"&&profile!="frag"&&profile!="zone"&&profile!="knife"&&profile!="katana"&&profile!="excalibur"&&profile!="cyberblade"&&profile!="banana"&&profile!="soda"&&profile!="shaker"&&profile!="backpack";}
        public static int Stars(string label,int stars){if(stars>0)return Math.Max(1,Math.Min(5,stars));int count=0;foreach(char c in label??"")if(c=='\u2605')count++;return Math.Max(1,Math.Min(5,count));}
        public static string Caption(string profile,string label,int stars=0)
        {if(string.IsNullOrEmpty(profile))return label??"";return Name(profile)+(Tiered(profile)?" "+new string('\u2605',Stars(label,stars)):"");}
        public static string RarityColor(int stars){return RarityColors[Math.Max(1,Math.Min(5,stars))-1];}
    }
    public sealed class R23WeaponWheelSlot
    {
        public readonly string Id,Label,WeaponId,Detail;public readonly bool CanEquip,CanDrop;public readonly int Stars,Count;
        public R23WeaponWheelSlot(string id,string label,string weaponId,string detail="",bool canEquip=true,int stars=0,int count=1,bool canDrop=true){CanDrop=canDrop;if(string.IsNullOrEmpty(id))throw new ArgumentException("Live owned instance ID required");if(count<1)throw new ArgumentOutOfRangeException("count");Count=count;Id=id;WeaponId=weaponId??"";Stars=R23InventoryPresentation.Stars(label,stars);Label=R23InventoryPresentation.Caption(WeaponId,label,Stars);Detail=detail??"";CanEquip=canEquip;}
    }
    public enum R23WeaponWheelAction { Equip, Backpack, QuickToggle, Revive, Drop }
    public sealed class R23WeaponWheelRequest
    {
        public readonly R23WeaponWheelAction Action;public readonly string Id;public readonly int Slot,Hand;
        internal R23WeaponWheelRequest(R23WeaponWheelAction action,string id,int slot,int hand){Action=action;Id=id;Slot=slot;Hand=hand;}
    }
    public static class R23WeaponWheelRules
    {
        public const float OuterRadius=.34f,InnerRadius=.102f,DisplayScale=1.2f,OpenDistance=.85f,QuickTapSeconds=.1f;
        public const int MaximumSlots=6;
        static readonly int[] ItemSectors={2,1,0,7,6,5}; // R6 weapon_wheel.gd ITEM_SLOTS.
        public static int SectorForSlot(int slot){if(slot<0||slot>=MaximumSlots)throw new ArgumentOutOfRangeException("slot");return ItemSectors[slot];}
        public static int SlotForSector(int sector){for(int slot=0;slot<ItemSectors.Length;slot++)if(ItemSectors[slot]==sector)return slot;return -1;}
        public static int Sector(float x,float y)
        {
            if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||(double)x*x+(double)y*y<InnerRadius*InnerRadius)return -1;
            double angle=Math.Atan2(y,x),nearest=double.PositiveInfinity;int result=-1;
            for(int i=0;i<8;i++){double difference=angle-(Math.PI*.5-i*Math.PI*.25);while(difference>Math.PI)difference-=Math.PI*2;while(difference<-Math.PI)difference+=Math.PI*2;difference=Math.Abs(difference);if(difference<nearest){nearest=difference;result=i;}}
            return result; // Source has no outer cutoff; only pointer display clamps.
        }
    }
    // Source weapon_rack.update_a_input hold/release/neutral policy; six source item sectors, capacity3 through6.
    public sealed partial class R23WeaponWheelState
    {
        readonly R23WeaponWheelSlot[] slots=new R23WeaponWheelSlot[R23WeaponWheelRules.MaximumSlots];bool blocked=true,previous;float elapsed;int hand=-1;
        public bool Active {get;private set;}public int HighlightedSector {get;private set;}public int Capacity {get;private set;}
        public R23WeaponWheelState(){HighlightedSector=-1;Capacity=3;}
        public R23WeaponWheelSlot Slot(int index){if(index<0||index>=R23WeaponWheelRules.MaximumSlots)throw new ArgumentOutOfRangeException("index");return slots[index];}
        public void SetSlots(R23WeaponWheelSlot[] values)
        {
            SetSlots(values,Math.Max(3,values==null?0:values.Length));
        }
        public void SetSlots(R23WeaponWheelSlot[] values,int capacity)
        {
            if(capacity<3||capacity>R23WeaponWheelRules.MaximumSlots)throw new ArgumentOutOfRangeException("capacity");
            if(values==null||values.Length>capacity)throw new ArgumentException("Live inventory slots exceed source capacity");
            for(int i=0;i<values.Length;i++)if(values[i]!=null)for(int j=0;j<i;j++)if(values[j]!=null&&values[j].Id==values[i].Id)throw new ArgumentException("Duplicate owned slot identity");
            bool changed=Capacity!=capacity;for(int i=0;i<slots.Length;i++){var next=i<values.Length?values[i]:null;var old=slots[i];if((old==null)!=(next==null)||(old!=null&&next!=null&&(old.Id!=next.Id||old.WeaponId!=next.WeaponId||old.CanEquip!=next.CanEquip||old.CanDrop!=next.CanDrop)))changed=true;slots[i]=next;}
            Capacity=capacity;if(changed)Cancel();
        }
        public void Cancel(){ResetDrop();Active=false;blocked=true;previous=false;elapsed=0;HighlightedSector=-1;}
        public R23WeaponWheelRequest Step(float dt,bool held,bool valid,int pointedSector,int primaryHand)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f||primaryHand<0||primaryHand>1)throw new ArgumentOutOfRangeException("wheel frame");
            if(!valid){Cancel();hand=primaryHand;return null;}
            if(hand!=primaryHand){Cancel();hand=primaryHand;}
            if(blocked){if(!held)blocked=false;return null;}
            if(held){if(!previous){elapsed=0;ResetDrop();}elapsed+=dt;if(elapsed>R23WeaponWheelRules.QuickTapSeconds+.0000001f)Active=true;HighlightedSector=Active&&pointedSector>=0&&pointedSector<8?pointedSector:-1;previous=true;return StepDrop(dt,primaryHand);}
            if(!previous)return null;
            R23WeaponWheelRequest request=null;
            if(!dropConsumed&&elapsed<=R23WeaponWheelRules.QuickTapSeconds+.0000001f)request=new R23WeaponWheelRequest(R23WeaponWheelAction.QuickToggle,null,-1,primaryHand);
            else if(Active&&!dropConsumed){int slot=R23WeaponWheelRules.SlotForSector(pointedSector);if(pointedSector==4)request=new R23WeaponWheelRequest(R23WeaponWheelAction.Backpack,null,-1,primaryHand);else if(pointedSector==3)request=new R23WeaponWheelRequest(R23WeaponWheelAction.Revive,"utility/revive",-1,primaryHand);else if(slot>=0&&slots[slot]!=null&&slots[slot].CanEquip)request=new R23WeaponWheelRequest(R23WeaponWheelAction.Equip,slots[slot].Id,slot,primaryHand);}
            ResetDrop();Active=false;previous=false;elapsed=0;HighlightedSector=-1;return request;
        }
    }
}
