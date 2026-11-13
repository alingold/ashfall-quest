using System;
using System.Collections.Generic;

namespace Ashfall.Integration.Playable.UI
{
    public enum R23UiPage { Loading, Ready, Playing, Pause, Settings, Weapons, Appearance, Maps, Loadout, Dead, Error, SpawnChoice, MatchRules, Credits }
    public enum R23Turning { Smooth, Snap }
    public enum R23Forward { Head, LeftController }
    public sealed class R23MapOption
    {
        public readonly string Id,Label,ThumbnailResource;
        public R23MapOption(string id,string label,string thumbnailResource=null)
        {
            if(string.IsNullOrEmpty(id))throw new ArgumentException("Map ID required");
            Id=id;Label=label??id;
            ThumbnailResource=thumbnailResource??SourceThumbnail(id);
        }
        static string SourceThumbnail(string id)
        {
            // Original map_catalog1000.image / archive_catalog1003.image.
            // Only exact known IDs bind source images; no guessed screenshots.
            switch(id)
            {
                case "valley":case "range":case "cyber_bird":return "R23MapThumbnails/"+id;
                case "archive:battlefield-paintball-gods-pitdevil":return "R23MapThumbnails/battlefield-paintball-gods-pitdevil";
                case "archive:alfa-aglae-souza":return "R23MapThumbnails/alfa-aglae-souza";
                case "archive:classic-bigbox-vr":return "R23MapThumbnails/classic-bigbox-vr";
                default:return id!=null&&id.StartsWith("archive:",StringComparison.Ordinal)&&!id.Contains("..")?"R24MapThumbnails/"+id.Substring(8):null;
            }
        }
    }

    // A new immutable value is published on each settings change. Consumers own application.
    public sealed class R23UiSettings
    {
        public readonly R23Turning Turning;
        public readonly R23Forward Forward;
        public readonly float SnapDegrees, SmoothDegreesPerSecond, WalkMultiplier;
        public readonly float MasterVolume, SfxVolume, MusicVolume, AmbienceVolume, VoiceVolume;
        public readonly float FlingPower,MotionDamping,OffhandInfluence,BotDifficulty;
        public readonly bool RetiredMaps,RetiredSkins,RetiredSounds;
        public readonly bool GripToggle;public readonly int DominantHand;
        public R23UiSettings(R23Turning turning=R23Turning.Smooth, R23Forward forward=R23Forward.Head,
            float snapDegrees=30, float smoothDegreesPerSecond=360, float walkMultiplier=2.5f,
            float masterVolume=1, float sfxVolume=1, float musicVolume=0, float ambienceVolume=0, float voiceVolume=1,
            float flingPower=3,float motionDamping=1,float offhandInfluence=0,bool gripToggle=false,int dominantHand=1,float botDifficulty=.5f,bool retiredMaps=false,bool retiredSkins=false,bool retiredSounds=false)
        {
            RetiredMaps=retiredMaps;RetiredSkins=retiredSkins;RetiredSounds=retiredSounds;
            Turning=turning==R23Turning.Snap?R23Turning.Snap:R23Turning.Smooth;
            Forward=forward==R23Forward.LeftController?R23Forward.LeftController:R23Forward.Head;
            SnapDegrees=Clamp(snapDegrees,15,180);
            SmoothDegreesPerSecond=Clamp(smoothDegreesPerSecond,30,720);WalkMultiplier=Clamp(walkMultiplier,.25f,5);
            MasterVolume=Clamp(masterVolume,0,1);SfxVolume=Clamp(sfxVolume,0,1);MusicVolume=Clamp(musicVolume,0,1);
            AmbienceVolume=Clamp(ambienceVolume,0,1);VoiceVolume=Clamp(voiceVolume,0,1);
            FlingPower=Clamp(flingPower,0,5);MotionDamping=Clamp(motionDamping,0,2);OffhandInfluence=Clamp(offhandInfluence,0,1);GripToggle=gripToggle;DominantHand=dominantHand==0?0:1;BotDifficulty=Clamp(botDifficulty,0,1);
        }
        public R23UiSettings With(R23Turning? turning=null,R23Forward? forward=null,float? snap=null,float? smooth=null,float? walk=null,float? master=null,float? sfx=null,float? music=null,float? ambience=null,float? voice=null,float? fling=null,float? damping=null,float? offhand=null,bool? gripToggle=null,int? dominantHand=null,float? botDifficulty=null,bool? retiredMaps=null,bool? retiredSkins=null,bool? retiredSounds=null)
        {return new R23UiSettings(turning??Turning,forward??Forward,snap??SnapDegrees,smooth??SmoothDegreesPerSecond,walk??WalkMultiplier,master??MasterVolume,sfx??SfxVolume,music??MusicVolume,ambience??AmbienceVolume,voice??VoiceVolume,fling??FlingPower,damping??MotionDamping,offhand??OffhandInfluence,gripToggle??GripToggle,dominantHand??DominantHand,botDifficulty??BotDifficulty,retiredMaps??RetiredMaps,retiredSkins??RetiredSkins,retiredSounds??RetiredSounds);}
        static float Clamp(float v,float min,float max) { if(float.IsNaN(v)||float.IsInfinity(v))throw new ArgumentOutOfRangeException("v");return Math.Max(min,Math.Min(max,v)); }
    }
    public sealed class R23WeaponOption
    {
        public readonly string Id, Label;
        public readonly bool Available;
        public R23WeaponOption(string id,string label,bool available=true) { if(string.IsNullOrEmpty(id))throw new ArgumentException("Weapon ID required");Id=id;Label=label??id;Available=available; }
    }
    public sealed class R23AppearanceOption
    {
        public readonly string Id, Label;
        public readonly bool Available;
        public readonly int SourceIndex;
        public R23AppearanceOption(string id,string label,bool available=true,int sourceIndex=-1) { if(string.IsNullOrEmpty(id))throw new ArgumentException("Appearance ID required");Id=id;Label=label??id;Available=available;SourceIndex=sourceIndex; }
    }
    public sealed class R23LoadoutOption
    {
        public readonly string Id,Label,Detail,ProfileId;
        public readonly int Stars;
        public readonly int Count,EquippedHand;
        public readonly bool Equipped,CanEquip,CanDrop;
        public R23LoadoutOption(string id,string label,string detail="",int count=1,bool equipped=false,int equippedHand=1,bool canEquip=true,bool canDrop=true,string profileId="",int stars=1)
        {
            if(string.IsNullOrEmpty(id))throw new ArgumentException("Owned item ID required");
            if(count<1)throw new ArgumentOutOfRangeException("count");
            if(equippedHand<(equipped?0:-1)||equippedHand>1)throw new ArgumentOutOfRangeException("equippedHand");
            Id=id;Label=label??id;Detail=detail??"";ProfileId=profileId??"";Stars=Math.Max(1,Math.Min(5,stars));Count=count;Equipped=equipped;EquippedHand=equipped?equippedHand:-1;CanEquip=canEquip;CanDrop=canDrop;
        }
    }
    public sealed class R23OwnedLoadout
    {
        readonly R23LoadoutOption[] slots;
        public readonly int Capacity;
        public int Count {get{return slots.Length;}}
        public R23LoadoutOption[] Options {get{return (R23LoadoutOption[])slots.Clone();}}
        public R23OwnedLoadout(IEnumerable<R23LoadoutOption> values,int capacity=3)
        {
            if(capacity<3||capacity>6)throw new ArgumentOutOfRangeException("capacity");
            var copy=new List<R23LoadoutOption>();var ids=new HashSet<string>(StringComparer.Ordinal);
            if(values!=null)foreach(var option in values)
            {if(option==null||!ids.Add(option.Id))throw new ArgumentException("Owned slots must have unique, nonnull item IDs");copy.Add(option);}
            if(copy.Count>capacity)throw new ArgumentException("Owned slots exceed host-supplied capacity");
            slots=copy.ToArray();Capacity=capacity;
        }
        public R23LoadoutOption Slot(int index)
        {if(index<0||index>=Capacity)throw new ArgumentOutOfRangeException("index");return index<slots.Length?slots[index]:null;}
        public bool SameIdentity(R23OwnedLoadout other)
        {
            if(other==null||Capacity!=other.Capacity||Count!=other.Count)return false;
            for(int i=0;i<slots.Length;i++)if(slots[i].Id!=other.slots[i].Id||slots[i].ProfileId!=other.slots[i].ProfileId||slots[i].Stars!=other.slots[i].Stars||slots[i].CanEquip!=other.slots[i].CanEquip||slots[i].CanDrop!=other.slots[i].CanDrop)return false;
            return true;
        }
    }
    public sealed class R23BackpackResource
    {
        public readonly string Family;public readonly int Count;public readonly bool CanDrop;
        public R23BackpackResource(string family,int count,bool canDrop=false){Family=family??"";Count=Math.Max(0,count);CanDrop=canDrop&&count>0;}
    }
    public sealed class R23HudSnapshot
    {
        public readonly float Health, MaxHealth;
        public readonly int Ammo, Reserve, Hits;
        public readonly bool HasWeapon, Reloading;
        public readonly string Weapon;
        public readonly float Shield,MaxShield,Stamina,MaxStamina;
        public readonly int BuildCharges;public readonly string Movement,Hint;
        public R23HudSnapshot(float health,float maxHealth,int ammo,int reserve,bool hasWeapon,bool reloading,string weapon,int hits=0,float shield=-1,float maxShield=100,float stamina=-1,float maxStamina=6,int buildCharges=-1,string movement="",string hint="")
        { Health=health;MaxHealth=maxHealth;Ammo=ammo;Reserve=reserve;HasWeapon=hasWeapon;Reloading=reloading;Weapon=weapon??"WEAPON";Hits=hits;Shield=shield;MaxShield=maxShield;Stamina=stamina;MaxStamina=maxStamina;BuildCharges=buildCharges;Movement=movement??"";Hint=hint??""; }
    }
}
