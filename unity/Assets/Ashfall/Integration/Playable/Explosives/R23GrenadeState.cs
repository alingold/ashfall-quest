using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Explosives
{
    public sealed class R23OwnedGrenade
    {
        public readonly string Id,Kind;public readonly int Slot,SourceStars;public int Stars{get{return 1;}}
        internal R23OwnedGrenade(string id,string kind,int slot,int sourceStars=1){Id=id;Kind=kind;Slot=slot;SourceStars=sourceStars;}
        public string Label {get{return R23GrenadeState.KindLabel(Kind);}}
    }
    public struct R23GrenadeItemState
    {public string Id,Kind;public int Slot,Stars,SourceStars;public bool Owned,Spent;}
    // Each registered source id is ONE physical payload (arsenal_data071), not the older
    // explosive_weapon.setup's two-ammo default. Equip/drop never create new payloads.
    public sealed class R23GrenadeState
    {
        sealed class Item{internal string Id,Kind;internal int Slot=-1,SourceStars=1;internal bool Owned,Spent,RestoreOnReset=true;internal float Cooldown;}
        readonly Dictionary<string,Item> items=new Dictionary<string,Item>(StringComparer.Ordinal);
        readonly HashSet<string> registered=new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> order=new List<string>();int otherSlots,slotCapacity=3;bool previous=true,release=true;
        public int SlotCapacity {get{return slotCapacity;}set{slotCapacity=Math.Max(3,Math.Min(6,value));}}
        public string HeldId {get;private set;}public int HeldHand {get;private set;}
        public float Cooldown {get{return HeldId==null?0:items[HeldId].Cooldown;}}
        public static bool ValidKind(string kind){return kind=="frag"||kind=="zone"||Ashfall.Integration.Playable.FlashSmoke.R25Throwables.IsFlashOrSmoke(kind);}
        // flashsmoke: explicit label per kind (new kinds never inherit the zone label).
        public static string KindLabel(string kind){return kind=="frag"?"FRAG":kind=="zone"?"ZONE GRENADE":Ashfall.Integration.Playable.FlashSmoke.R25Throwables.Label(kind);}
        public R23OwnedGrenade[] OwnedItems {get{var result=new List<R23OwnedGrenade>();foreach(string id in order){var i=items[id];if(i.Owned&&!i.Spent)result.Add(new R23OwnedGrenade(i.Id,i.Kind,i.Slot,i.SourceStars));}return result.ToArray();}}
        public bool Register(string id,string kind)
        {return Register(id,kind,1,true);}
        public bool RegisterWorldCopy(string id,string kind,int sourceStars)
        {return Register(id,kind,sourceStars,false);}
        bool Register(string id,string kind,int sourceStars,bool restoreOnReset)
        {if(String.IsNullOrEmpty(id)||id.Length>128||!ValidKind(kind)||sourceStars<1||sourceStars>5||registered.Contains(id))return false;items.Add(id,new Item{Id=id,Kind=kind,SourceStars=sourceStars,RestoreOnReset=restoreOnReset});registered.Add(id);order.Add(id);return true;}
        // A map can withdraw ground/spent copies, never somebody's owned payload.
        // Its identity stays used so retirement cannot mint a second charge.
        public bool RetireUnowned(string id){Item i;if(id==null||!items.TryGetValue(id,out i)||i.Owned||HeldId==id)return false;items.Remove(id);order.Remove(id);return true;}
        public bool TryRead(string id,out R23GrenadeItemState value)
        {value=new R23GrenadeItemState();Item i;if(id==null||!items.TryGetValue(id,out i))return false;value=new R23GrenadeItemState{Id=i.Id,Kind=i.Kind,Slot=i.Slot,Owned=i.Owned,Spent=i.Spent,Stars=1,SourceStars=i.SourceStars};return true;}
        public void SetOtherSlotsUsed(int count){otherSlots=Math.Max(0,Math.Min(6,count));}
        public bool CanStore(string kind){return ValidKind(kind)&&FreeSlot(kind)>=0;}
        int FreeSlot(string kind)
        {
            var groups=new Dictionary<int,List<Item>>();foreach(var i in items.Values)if(i.Owned&&!i.Spent){if(!groups.ContainsKey(i.Slot))groups.Add(i.Slot,new List<Item>());groups[i.Slot].Add(i);}
            if(groups.Count>SlotCapacity-otherSlots)return -1;
            if(kind=="frag")for(int slot=0;slot<SlotCapacity;slot++){List<Item> group;if(groups.TryGetValue(slot,out group)&&group.Count==1&&group[0].Kind=="frag")return slot;}
            if(groups.Count>=SlotCapacity-otherSlots)return -1;for(int slot=0;slot<SlotCapacity;slot++)if(!groups.ContainsKey(slot))return slot;return -1;
        }
        public bool Pickup(string id,int hand,bool available)
        {Item i;if(!available||hand<1||hand>2||id==null||!items.TryGetValue(id,out i)||i.Owned||i.Spent)return false;int slot=FreeSlot(i.Kind);if(slot<0)return false;i.Owned=true;i.Slot=slot;return Equip(id,hand,true);}
        public bool Equip(string id,int hand,bool available)
        {Item i;if(!available||hand<1||hand>2||id==null||!items.TryGetValue(id,out i)||!i.Owned||i.Spent)return false;HeldId=id;HeldHand=hand;i.Cooldown=Math.Max(i.Cooldown,.25f);Suspend();return true;}
        public void Stow(){HeldId=null;HeldHand=0;Suspend();}
        public bool Drop(string id)
        {Item i;if(id==null||!items.TryGetValue(id,out i)||!i.Owned||i.Spent)return false;i.Owned=false;i.Slot=-1;if(HeldId==id)Stow();return true;}
        public bool DropSlot(int slot)
        {var members=new List<Item>();foreach(string id in order){var i=items[id];if(i.Owned&&!i.Spent&&i.Slot==slot)members.Add(i);}if(members.Count<1||members.Count>2)return false;foreach(var i in members){i.Owned=false;i.Slot=-1;}foreach(var i in members)if(HeldId==i.Id){Stow();break;}return true;}
        public void Advance(float seconds)
        {if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<0||seconds>.25f)throw new ArgumentOutOfRangeException("seconds");if(HeldId!=null)items[HeldId].Cooldown=Math.Max(0,items[HeldId].Cooldown-seconds);}
        public bool TryThrow(bool down,bool eligible,bool launchClear,out R23OwnedGrenade consumed){return TryThrow(down,eligible,launchClear,null,out consumed);}
        public bool TryThrow(bool down,bool eligible,bool launchClear,Func<R23OwnedGrenade,bool> authorize,out R23OwnedGrenade consumed)
        {
            consumed=null;if(!eligible){Suspend();return false;}
            if(!down){previous=release=false;return false;}bool fresh=!previous&&!release;previous=true;
            Item item;if(!fresh||Cooldown>0||!launchClear||HeldId==null||!items.TryGetValue(HeldId,out item)||!item.Owned||item.Spent)return false;
            var proposed=new R23OwnedGrenade(item.Id,item.Kind,item.Slot,item.SourceStars);if(authorize!=null&&!authorize(proposed))return false;consumed=proposed;item.Spent=true;item.Owned=false;item.Cooldown=.35f;release=true;
            int hand=HeldHand;HeldId=null;HeldHand=0;
            // Source consume_disposable selects the remaining member of the same frag stack.
            foreach(string id in order){var next=items[id];if(next.Owned&&!next.Spent&&next.Slot==item.Slot){HeldId=next.Id;HeldHand=hand;next.Cooldown=Math.Max(next.Cooldown,.25f);break;}}
            return true;
        }
        internal bool ReconcileNetworkItem(string id,bool owned,bool spent){Item item;if(id==null||!items.TryGetValue(id,out item)||owned&&spent)return false;if(spent){item.Spent=true;item.Owned=false;item.Slot=-1;if(HeldId==id)Stow();return true;}if(item.Spent)return !owned;if(owned&&!item.Owned){string held=HeldId;int hand=HeldHand;if(!Pickup(id,1,true))return false;if(held==null)Stow();else Equip(held,hand,true);}else if(!owned&&item.Owned)Drop(id);return true;}
        public void Suspend(){previous=release=true;}
        public void ResetRound(){HeldId=null;HeldHand=0;Suspend();foreach(string id in order.ToArray()){var item=items[id];if(!item.RestoreOnReset){items.Remove(id);order.Remove(id);continue;}item.Owned=item.Spent=false;item.Slot=-1;item.Cooldown=0;}}
    }
}
