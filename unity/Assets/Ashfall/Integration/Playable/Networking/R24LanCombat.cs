using System;using System.Collections.Generic;using System.IO;using System.Text;using Ashfall.Core;using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Networking
{
    // A locally accepted volley: rays and stamp only. The host chooses victims,
    // rarity, damage and ammunition from its own custody ledger (source _shoot).
    public sealed class R24LanShot
    {
        public readonly string Profile,Instance;public readonly int Side;public readonly double Time;public readonly Float3 Origin;readonly Float3[] directions;
        public Float3[] Directions{get{return (Float3[])directions.Clone();}}
        public R24LanShot(string profile,int side,double time,Float3 origin,Float3[] rays,string instance=null){Instance=instance??profile;Profile=profile;Side=side;Time=time;Origin=origin;directions=rays==null?new Float3[0]:(Float3[])rays.Clone();}
        // net_session1000._shoot: 1..8 rays, |d| in [.9,1.1]; normalized before use.
        public bool Valid{get{R24LanBallisticProfile p;if(!Ashfall.Modules.Networking.Wire.IsText(Instance,128,false)||!R24LanBallisticProfile.TryRead(Profile,out p)||Side<0||Side>1||Double.IsNaN(Time)||Double.IsInfinity(Time)||Time<0||!Origin.IsFinite||Math.Abs(Origin.X)>100000||Math.Abs(Origin.Y)>100000||Math.Abs(Origin.Z)>100000||directions.Length<1||directions.Length>8||directions.Length>p.Pellets)return false;foreach(var d in directions){if(!d.IsFinite)return false;double n=Math.Sqrt((double)d.X*d.X+(double)d.Y*d.Y+(double)d.Z*d.Z);if(n<.9||n>1.1)return false;}return true;}}
        public byte[] Encode()
        {
            if(!Valid)throw new ArgumentException("Actual finite ballistic volley required");
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,new UTF8Encoding(false,true))){byte[] id=Encoding.ASCII.GetBytes(Profile);w.Write((byte)2);w.Write((byte)id.Length);w.Write(id);byte[] instance=Encoding.UTF8.GetBytes(Instance);w.Write((byte)instance.Length);w.Write(instance);w.Write((byte)Side);w.Write(Time);V(w,Origin);w.Write((byte)directions.Length);foreach(var d in directions)V(w,d);return s.ToArray();}
        }
        public static bool TryDecode(byte[] bytes,out R24LanShot shot)
        {
            shot=null;if(bytes==null||bytes.Length>300)return false;
            try{using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s)){if(r.ReadByte()!=2)return false;int n=r.ReadByte();if(n<1||n>16||n>s.Length-s.Position)return false;string profile=Encoding.ASCII.GetString(r.ReadBytes(n));int k=r.ReadByte();if(k<1||k>128||k>s.Length-s.Position)return false;string instance=new UTF8Encoding(false,true).GetString(r.ReadBytes(k));int side=r.ReadByte();double time=r.ReadDouble();var origin=V(r);int count=r.ReadByte();if(count<1||count>8)return false;var rays=new Float3[count];for(int i=0;i<count;i++)rays[i]=V(r);var value=new R24LanShot(profile,side,time,origin,rays,instance);if(s.Position!=s.Length||!value.Valid)return false;shot=value;return true;}}
            catch(IOException){return false;}catch(ArgumentException){return false;}
        }
        static void V(BinaryWriter w,Float3 v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}static Float3 V(BinaryReader r){return new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
    }
    public sealed class R24LanScoreRow
    {
        public readonly string Actor;public readonly int Team,Kills,Deaths,Score;
        internal R24LanScoreRow(string actor,int team,int kills,int deaths,int score){Actor=actor;Team=team;Kills=kills;Deaths=deaths;Score=score;}
    }
    // MP-HITMARKER: one host-confirmed loss the shooter caused (firearm, vehicle gun or
    // explosive; melee has its own playable.melee.hit/1 feedback). Actual pool losses,
    // never the requested amount. Point (when Located) is the victim head on the host's accepted pose.
    public sealed class R26HitConfirm
    {
        public readonly string Victim;public readonly float Shield,Health;public readonly bool Headshot,Kill,Located,BladeContact;public readonly Float3 Point;
        public R26HitConfirm(string victim,float shield,float health,bool headshot,bool kill,bool located,Float3 point,bool bladeContact=false){BladeContact=bladeContact;Victim=victim;Shield=shield;Health=health;Headshot=headshot;Kill=kill;Located=located;Point=point;}
        public float Total{get{return Shield+Health;}}
    }
    // Source _starting_inventory/_normalize_weapon custody data from the existing
    // Unity owners: catalog index, rarity capacity and ammunition family.
    public static class R24LanLoadout
    {
        static readonly string[] Profiles={"grapple","cx4","dt11","px4","mk18","awp","ump","magnum","m60","m1014","p90","uzis","matadors","spr","katana","excalibur","cyberblade","mp5","mp9","1911","tec9","akm","rfb","fal","s85","smaw","knife"};
        public static string Profile(int catalog){foreach(string id in Profiles)if(R23WeaponPickupRules.CatalogIndex(id)==catalog)return id;return null;}
        public static bool Melee(string profile){return profile=="katana"||profile=="excalibur"||profile=="cyberblade"||profile=="knife";}
        public static bool Paired(string profile){return profile=="uzis"||profile=="matadors";}
        public static string Family(string profile)
        {
            switch(profile){
                case "cx4":case "mk18":case "akm":case "m60":case "rfb":case "fal":return "rifle";
                case "px4":case "magnum":case "1911":case "tec9":return "pistol";
                case "ump":case "p90":case "mp5":case "mp9":case "uzis":return "smg";
                case "m1014":case "dt11":case "matadors":return "shells";
                case "awp":case "spr":case "s85":return "sniper";
                default:return null;
            }
        }
        // net_session1000._starting_inventory reserves.
        public static Dictionary<string,int> StartingReserves(){return new Dictionary<string,int>(StringComparer.Ordinal){{"rifle",2400},{"pistol",1200},{"shells",480},{"smg",1200},{"sniper",2400}};}
        public static int Capacity(string profile,int stars){R23RarityStats stats;return R23WeaponRarity.TryRead(profile,stars,out stats)?stats.Capacity:0;}
    }
    // Render receipts only. No client presentation field grants custody or damage authority.
    public sealed class R26RemoteEquipment
    {
        public readonly string Actor,Profile;public readonly int Hand;public readonly bool Suppressed,Blocked,Paddles;
        public R26RemoteEquipment(string actor,string profile,int hand,bool suppressed,bool blocked,bool paddles=false){Paddles=paddles;Actor=actor;Profile=profile;Hand=hand;Suppressed=suppressed;Blocked=blocked;}
    }
    public sealed class R26RemoteVolley
    {
        public readonly string Actor,Profile;public readonly int Hand;public readonly double Time;public readonly Float3 Origin;public readonly Float3[] Directions;public readonly long Id;
        public R26RemoteVolley(string actor,string profile,int hand,double time,Float3 origin,Float3[] directions,long id=1){Id=id;Actor=actor;Profile=profile;Hand=hand;Time=time;Origin=origin;Directions=directions;}
    }
    public static class R26RemoteWire
    {
        public const byte EquipmentLayout=3,VolleyLayout=10;const int MaximumActors=24;
        static bool Actor(string actor){return Ashfall.Modules.Networking.Wire.IsText(actor,64,false)&&Encoding.UTF8.GetByteCount(actor)<=64;}
        static bool Point(Float3 p){return p.IsFinite&&Math.Abs(p.X)<=100000&&Math.Abs(p.Y)<=100000&&Math.Abs(p.Z)<=100000;}
        static void Text(BinaryWriter w,string text){var b=Encoding.UTF8.GetBytes(text);w.Write((byte)b.Length);w.Write(b);}
        static string Text(BinaryReader r){int n=r.ReadByte();if(n<1||n>64)throw new InvalidDataException();byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new InvalidDataException();string text=new UTF8Encoding(false,true).GetString(b);if(!Actor(text))throw new InvalidDataException();return text;}
        static void V(BinaryWriter w,Float3 v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}static Float3 V(BinaryReader r){return new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
        public static byte[] Equipment(R26RemoteEquipment[] rows)
        {
            if(rows==null||rows.Length>MaximumActors)throw new ArgumentException("Bounded remote equipment");var seen=new HashSet<string>(StringComparer.Ordinal);
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(EquipmentLayout);w.Write((byte)rows.Length);foreach(var row in rows){int catalog=row.Profile==null?255:R23WeaponPickupRules.CatalogIndex(row.Profile);if(!Actor(row.Actor)||!seen.Add(row.Actor)||row.Hand<0||row.Hand>1||catalog>255)throw new ArgumentException("Remote equipment row");Text(w,row.Actor);w.Write((byte)catalog);w.Write((byte)(row.Hand|(row.Suppressed?2:0)|(row.Blocked?4:0)|(row.Paddles?8:0)));}return s.ToArray();}
        }
        public static bool TryEquipment(byte[] bytes,out R26RemoteEquipment[] rows)
        {
            rows=null;if(bytes==null||bytes.Length>1610)return false;
            try{using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s)){if(r.ReadByte()!=EquipmentLayout)return false;int n=r.ReadByte();if(n>MaximumActors)return false;var result=new R26RemoteEquipment[n];var seen=new HashSet<string>(StringComparer.Ordinal);for(int i=0;i<n;i++){string actor=Text(r);int catalog=r.ReadByte(),flags=r.ReadByte();string profile=catalog==255?null:R24LanLoadout.Profile(catalog);if(!seen.Add(actor)||catalog!=255&&profile==null||flags>15)return false;result[i]=new R26RemoteEquipment(actor,profile,flags&1,(flags&2)!=0,(flags&4)!=0,(flags&8)!=0);}if(s.Position!=s.Length)return false;rows=result;return true;}}
            catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}
        }
        public static byte[] Volley(R26RemoteVolley shot)
        {
            R24LanBallisticProfile p;if(shot==null||!Actor(shot.Actor)||!R24LanBallisticProfile.TryRead(shot.Profile,out p)||shot.Id<1||shot.Hand<0||shot.Hand>1||Double.IsNaN(shot.Time)||Double.IsInfinity(shot.Time)||shot.Time<0||!Point(shot.Origin)||shot.Directions==null||shot.Directions.Length>p.Pellets)throw new ArgumentException("Remote accepted volley");
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(VolleyLayout);Text(w,shot.Actor);w.Write(shot.Id);w.Write((byte)R23WeaponPickupRules.CatalogIndex(shot.Profile));w.Write((byte)shot.Hand);w.Write(shot.Time);V(w,shot.Origin);w.Write((byte)shot.Directions.Length);foreach(var d in shot.Directions){float norm=d.X*d.X+d.Y*d.Y+d.Z*d.Z;if(!d.IsFinite||Math.Abs(norm-1)>.001f)throw new ArgumentException("Remote direction");V(w,d);}return s.ToArray();}
        }
        public static bool TryVolley(byte[] bytes,out R26RemoteVolley shot)
        {
            shot=null;if(bytes==null||bytes.Length>200)return false;
            try{using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s)){if(r.ReadByte()!=VolleyLayout)return false;string actor=Text(r);long id=r.ReadInt64();string profile=R24LanLoadout.Profile(r.ReadByte());int hand=r.ReadByte();double time=r.ReadDouble();var origin=V(r);int n=r.ReadByte();if(n>8)return false;var ends=new Float3[n];for(int i=0;i<n;i++)ends[i]=V(r);if(s.Position!=s.Length)return false;var value=new R26RemoteVolley(actor,profile,hand,time,origin,ends,id);Volley(value);shot=value;return true;}}
            catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}
        }
    }
}
