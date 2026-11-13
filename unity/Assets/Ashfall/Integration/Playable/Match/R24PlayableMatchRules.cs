using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ashfall.Integration.Playable.Match
{
    // Frozen R6 net_rules1000.gd; personal VR settings are deliberately separate.
    // No network/host ownership is implied by this local immutable value.
    public sealed class R24PlayableMatchRules
    {
        public readonly string MapId;
        public readonly string Mode;
        public readonly int MaxPlayers;
        public readonly int Teams;
        public readonly bool FriendlyFire;
        public readonly float Damage;
        public readonly float NpcDamage;
        public readonly float BuildDamage;
        public readonly float Headshots;
        public readonly float Walk;
        public readonly float Run;
        public readonly float Flight;
        public readonly float Fling;
        public readonly float Stamina;
        public readonly int StartingHealth;
        public readonly int StartingShield;
        public readonly int StartingBuilds;
        public readonly bool BuildPieces;
        public readonly bool Builds;
        public readonly bool InfiniteBuilds;
        public readonly bool InfiniteAmmo;
        public readonly bool Loot;
        public readonly bool Catalog;
        public readonly float RespawnDelay;
        public readonly float Protection;
        public readonly bool RetainInventory;
        public readonly float DownedSeconds;
        public readonly int TimeLimit;
        public readonly int ScoreLimit;
        public readonly int Rounds;
        public readonly bool LateJoin;
        public readonly bool Spectators;
        public readonly bool Locked;
        public readonly string Loadout;
        public readonly bool ZoneEnabled;
        // flashsmoke kill switches (default ON): when OFF the kind is not looted,
        // granted by the catalogue, registered or thrown.
        public readonly bool Flashbangs;
        public readonly bool SmokeGrenades;
        public readonly bool Vehicles;
        public readonly bool Tanks,Helicopters;
        public readonly bool Jetpack,GrappleGun,Fists,Parry;
        public bool WebSlingers {get{return GrappleGun;}} // source compatibility only
        public readonly bool GrapplePull;
        public readonly bool GrapplePullFriends;
        public bool TeamRespawn {get{return Mode=="respawn"&&Teams>0;}}
        public readonly float ZoneDamage;
        public readonly float ZoneSpeed;
        public readonly float ZoneWait;
        readonly Dictionary<string,object> values;
        R24PlayableMatchRules(Dictionary<string,object> validated)
        {
            values=new Dictionary<string,object>(validated,StringComparer.Ordinal);
            MapId=(string)values["map"];
            Mode=(string)values["mode"];
            MaxPlayers=(int)values["max_players"];
            Teams=(int)values["teams"];
            FriendlyFire=(bool)values["friendly_fire"];
            Damage=(float)values["damage"];
            NpcDamage=(float)values["npc_damage"];
            BuildDamage=(float)values["build_damage"];
            Headshots=(float)values["headshots"];
            Walk=(float)values["walk"];
            Run=(float)values["run"];
            Flight=(float)values["flight"];
            Fling=(float)values["fling"];
            Stamina=(float)values["stamina"];
            StartingHealth=(int)values["starting_health"];
            StartingShield=(int)values["starting_shield"];
            StartingBuilds=(int)values["starting_builds"];
            BuildPieces=(bool)values["build_pieces"];
            Builds=(bool)values["builds"];
            InfiniteBuilds=(bool)values["infinite_builds"];
            InfiniteAmmo=(bool)values["infinite_ammo"];
            Loot=(bool)values["loot"];
            Catalog=(bool)values["catalog"];
            RespawnDelay=(float)values["respawn_delay"];
            Protection=(float)values["protection"];
            RetainInventory=(bool)values["retain_inventory"];
            DownedSeconds=(float)values["downed_seconds"];
            TimeLimit=(int)values["time_limit"];
            ScoreLimit=(int)values["score_limit"];
            Rounds=(int)values["rounds"];
            LateJoin=(bool)values["late_join"];
            Spectators=(bool)values["spectators"];
            Locked=(bool)values["locked"];
            Loadout=(string)values["loadout"];
            // Current Unity design: team deathmatch is timer-driven, without a zone.
            ZoneEnabled=!(Mode=="respawn"&&Teams>0)&&(bool)values["zone_enabled"];
            values["zone_enabled"]=ZoneEnabled;
            ZoneDamage=(float)values["zone_damage"];
            ZoneSpeed=(float)values["zone_speed"];
            ZoneWait=(float)values["zone_wait"];
            Flashbangs=(bool)values["flashbangs"];
            SmokeGrenades=(bool)values["smoke_grenades"];
            GrapplePullFriends=(bool)values["grapple_pull_friends"];GrapplePull=(bool)values["grapple_pull"];
            Tanks=(bool)values["tanks"];Helicopters=(bool)values["helicopters"];
            Vehicles=(bool)values["vehicles"];Fists=(bool)values["fists"];Parry=(bool)values["parry"];Jetpack=(bool)values["jetpack"];GrappleGun=(bool)values["grapple_gun"];
        }
        static readonly Dictionary<string,object> Defaults=new Dictionary<string,object>(StringComparer.Ordinal)
        {
            {"map","range"},
            {"mode","revive"},
            {"max_players",8},
            {"teams",2},
            {"friendly_fire",false},
            {"damage",1.0f},
            {"npc_damage",1.0f},
            {"build_damage",1.0f},
            {"headshots",1.0f},
            {"walk",2.5f},
            {"run",1.0f},
            {"flight",1.0f},
            {"fling",3.0f},
            {"stamina",1.0f},
            {"starting_health",100},
            {"starting_shield",100},
            {"starting_builds",999},
            {"build_pieces",true},
            {"builds",true},
            {"infinite_builds",false},
            {"infinite_ammo",false},
            {"loot",true},
            {"catalog",false},
            {"respawn_delay",5.0f},
            {"protection",3.0f},
            {"retain_inventory",true},
            {"downed_seconds",60.0f},
            {"time_limit",600},
            {"score_limit",25},
            {"rounds",1},
            {"late_join",true},
            {"spectators",true},
            {"locked",false},
            {"loadout","standard"},
            {"zone_enabled",true},
            {"zone_damage",5.0f},
            {"zone_speed",2.0f},
            {"zone_wait",30.0f},
            {"flashbangs",true},
            {"smoke_grenades",true},
            {"tanks",true},{"helicopters",true},{"vehicles",true},{"fists",true},{"parry",true},{"jetpack",true},{"grapple_gun",true},{"grapple_pull",true},{"grapple_pull_friends",false},
        };
        static readonly Dictionary<string,double[]> Limits=new Dictionary<string,double[]>(StringComparer.Ordinal)
        {
            {"max_players",new double[]{2,8}},
            {"teams",new double[]{0,8}},
            {"damage",new double[]{0.0,3.0}},
            {"npc_damage",new double[]{0.0,3.0}},
            {"build_damage",new double[]{0.0,3.0}},
            {"headshots",new double[]{0.0,3.0}},
            {"walk",new double[]{0.25,5.0}},
            {"run",new double[]{0.25,3.0}},
            {"flight",new double[]{0.25,3.0}},
            {"fling",new double[]{0.0,3.0}},
            {"stamina",new double[]{0.25,3.0}},
            {"starting_health",new double[]{1,100}},
            {"starting_shield",new double[]{0,100}},
            {"starting_builds",new double[]{0,100000}},
            {"respawn_delay",new double[]{1.0,30.0}},
            {"protection",new double[]{0.0,10.0}},
            {"downed_seconds",new double[]{10.0,180.0}},
            {"time_limit",new double[]{0,3600}},
            {"score_limit",new double[]{0,500}},
            {"rounds",new double[]{1,10}},
            {"zone_damage",new double[]{0.0,50.0}},
            {"zone_speed",new double[]{0.1,20.0}},
            {"zone_wait",new double[]{0.0,300.0}},
        };
        static readonly Dictionary<string,string[]> SourceChoices=new Dictionary<string,string[]>(StringComparer.Ordinal)
        {
            {"map",new string[]{"valley","range","cyber_bird","community_local"}},
            {"mode",new string[]{"revive","respawn"}},
            {"loadout",new string[]{"standard","pistols","swords"}},
        };
        public static R24PlayableMatchRules FromSourceValues(IDictionary<string,object> input,Func<string,bool> playableMap=null)
        {
            var result=new Dictionary<string,object>(Defaults,StringComparer.Ordinal);
            object legacy;if(input!=null&&!input.ContainsKey("grapple_gun")&&input.TryGetValue("web_slingers",out legacy)&&legacy is bool)result["grapple_gun"]=legacy;
            if(input!=null)foreach(var row in Defaults)
            {
                object v;if(!input.TryGetValue(row.Key,out v))continue;
                string[] options;
                if(SourceChoices.TryGetValue(row.Key,out options))
                {
                    var s=v as string;if(s==null)continue;
                    if(row.Key=="map")
                    {
                        if(s=="valley"||s=="range"||s=="cyber_bird"||((s=="community_local"||s.StartsWith("archive:",StringComparison.Ordinal))&&playableMap!=null&&playableMap(s)))result[row.Key]=s;
                    }
                    else if(Array.IndexOf(options,s)>=0)result[row.Key]=s;
                }
                else if(row.Value is bool){if(v is bool)result[row.Key]=v;}
                else
                {
                    double n;if(!Number(v,out n))continue;double[] bounds=Limits[row.Key];
                    n=Math.Max(bounds[0],Math.Min(bounds[1],n));
                    // Assign separately: a conditional int/float expression would box every int as float.
                    if(row.Value is int)result[row.Key]=(int)n;else result[row.Key]=(float)n;
                }
            }
            return new R24PlayableMatchRules(result);
        }
        static bool Number(object value,out double number)
        {
            number=0;
            if(!(value is int||value is long||value is float||value is double))return false;
            number=Convert.ToDouble(value,CultureInfo.InvariantCulture);return !double.IsNaN(number)&&!double.IsInfinity(number);
        }
        public static R24PlayableMatchRules ForPreset(string id,string mapId="range")
        {
            var patch=new Dictionary<string,object>();
            switch(id)
            {
                case "team_revive":break;
                case "team_respawn":patch["mode"]="respawn";break;
                case "free_for_all":patch["mode"]="respawn";patch["teams"]=0;break;
                case "practice":
                    patch["mode"]="respawn";patch["damage"]=0f;patch["catalog"]=true;
                    patch["infinite_ammo"]=true;patch["infinite_builds"]=true;
                    patch["score_limit"]=0;patch["time_limit"]=0;patch["zone_enabled"]=false;break;
                default:throw new ArgumentException("Unknown source match preset","id");
            }
            return FromSourceValues(patch).WithMap(mapId);
        }
        // The UI/Root map catalog owns installed/archive membership. This method receives
        // its selected ID, never resolves or loads geometry and never installs community maps.
        public R24PlayableMatchRules WithMap(string selectedCatalogMapId)
        {
            if(String.IsNullOrEmpty(selectedCatalogMapId)||selectedCatalogMapId.Length>180||selectedCatalogMapId.IndexOfAny(new[]{'\r','\n','\0'})>=0)
                throw new ArgumentException("A selected installed map ID is required","selectedCatalogMapId");
            var next=new Dictionary<string,object>(values,StringComparer.Ordinal);next["map"]=selectedCatalogMapId;
            if(R24ArchiveRoundPolicy.FiveLevels(selectedCatalogMapId)&&PresetId!="practice"){next["rounds"]=5;if(MapId!=selectedCatalogMapId)next["time_limit"]=300;}
            return new R24PlayableMatchRules(next);
        }
        public R24PlayableMatchRules With(string sourceKey,object value)
        {
            if(sourceKey=="web_slingers")sourceKey="grapple_gun";
            if(sourceKey==null||!Defaults.ContainsKey(sourceKey))throw new ArgumentException("Unknown source rule","sourceKey");
            if(sourceKey=="map")throw new ArgumentException("Use WithMap with an installed catalog selection","sourceKey");
            var next=new Dictionary<string,object>(values,StringComparer.Ordinal);next[sourceKey]=value;
            return FromSourceValues(next,delegate(string id){return id==MapId;});
        }
        public object ReadSourceValue(string key)
        {object value;if(!values.TryGetValue(key,out value))throw new ArgumentException("Unknown source rule","key");return value;}
        public static string[] SourceKeys{get{var keys=new string[Defaults.Count];Defaults.Keys.CopyTo(keys,0);Array.Sort(keys,StringComparer.Ordinal);return keys;}}
        public static bool TryGetRange(string key,out float min,out float max)
        {double[] v;if(Limits.TryGetValue(key,out v)){min=(float)v[0];max=(float)v[1];return true;}min=max=0;return false;}
        public static string[] Choices(string key)
        {string[] choices;return SourceChoices.TryGetValue(key,out choices)?(string[])choices.Clone():new string[0];}
        public string PresetId
        {
            get
            {
                // front_menu1000._preset_selected_r5: derived state, including edited presets.
                if(Mode=="respawn"&&Damage==0&&Catalog&&InfiniteAmmo&&!ZoneEnabled)return "practice";
                if(Mode=="revive")return "team_revive";
                return Teams==0?"free_for_all":"team_respawn";
            }
        }
        public int[] StartingCatalog
        {
            get
            {
                if(MapId=="archive:magnum-chaosvr"||MapId=="archive:magnum-3-chaosvr")return new[]{7};
                return Loadout=="pistols"?new[]{2,7,19}:Loadout=="swords"?new[]{14,15,16}:new[]{0,2,9};
            }
        }
        public static bool Editable(string stage,string key)
        {
            if(!Defaults.ContainsKey(key))return false;
            if(stage=="lobby"||stage=="countdown")return true;
            if(stage!="match")return false;
            return Array.IndexOf(new[]{"walk","run","flight","fling","stamina","damage","npc_damage","build_damage","headshots","zone_damage","time_limit","score_limit"},key)>=0;
        }
        public string Pretty(string key)
        {

            object value=ReadSourceValue(key);if(value is bool)return (bool)value?"ON":"OFF";
            if(Array.IndexOf(new[]{"damage","npc_damage","build_damage","headshots","walk","run","flight","fling","stamina"},key)>=0)
                return Math.Floor(Convert.ToDouble(value,CultureInfo.InvariantCulture)*100+.5).ToString(CultureInfo.InvariantCulture)+"%";
            if(key=="mode")return Mode=="respawn"?"DEATHMATCH":"TEAM REVIVE";
            if(key=="teams"&&Teams==0)return "FREE-FOR-ALL";
            if(key=="time_limit")return TimeLimit==0?"UNLIMITED":Math.Floor(TimeLimit/60d+.5).ToString(CultureInfo.InvariantCulture)+" MIN";
            if(key=="score_limit"&&ScoreLimit==0)return "UNLIMITED";
            if(key=="zone_damage")return ZoneDamage.ToString("F1",CultureInfo.InvariantCulture)+" HP / s";
            if(key=="zone_speed")return ZoneSpeed.ToString("F1",CultureInfo.InvariantCulture)+" m / s";
            if(key=="zone_wait")return ZoneWait.ToString("F0",CultureInfo.InvariantCulture)+" s";
            if(key=="respawn_delay"||key=="protection"||key=="downed_seconds")return Convert.ToSingle(value,CultureInfo.InvariantCulture).ToString("F1",CultureInfo.InvariantCulture)+" s";
            return Convert.ToString(value,CultureInfo.InvariantCulture).Replace('_',' ').ToUpperInvariant();
        }
    }
}
