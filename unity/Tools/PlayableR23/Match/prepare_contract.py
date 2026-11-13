import json,re,hashlib,pathlib,uuid
ROOT=pathlib.Path(r'C:/Users/aling/Documents/ashfall'); SRC=pathlib.Path(r'D:/ashfall-r6-staging/frozen-source-r6-v001/source'); HERE=pathlib.Path(__file__).resolve().parent
mf=SRC.parent/'SOURCE-MANIFEST.json'; assert hashlib.sha256(mf.read_bytes()).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
manifest=json.loads(mf.read_text(encoding='utf-8')); rows={r['path']:r for r in manifest['files']}
pins={}
for name in ['scripts/net_rules1000.gd','scripts/front_menu1000.gd','scripts/match_hud_r3.gd']:
 p=SRC/name; h=hashlib.sha256(p.read_bytes()).hexdigest(); assert h==rows[name]['sha256'],name; pins[name]=h
text=(SRC/'scripts/net_rules1000.gd').read_text(encoding='utf-8')
def read(name):
 raw=re.search(r'const '+name+r'\s*:?=\s*(\{[^\n]+\})',text).group(1)
 raw=re.sub(r'(?<=\[)\.(\d)',r'0.\1',raw)
 return json.loads(raw)
defaults=read('DEFAULTS'); limits=read('LIMITS'); choices=read('CHOICES')
def pname(k):return 'MapId' if k=='map' else ''.join(x.title() for x in k.split('_'))
def cstype(v):return 'bool' if isinstance(v,bool) else 'int' if isinstance(v,int) else 'float' if isinstance(v,float) else 'string'
def lit(v):
 if isinstance(v,bool):return str(v).lower()
 if isinstance(v,float):return repr(v)+'f'
 return json.dumps(v)
lines=['using System;','using System.Collections.Generic;','using System.Globalization;','','namespace Ashfall.Integration.Playable.Match','{','    // Frozen R6 net_rules1000.gd; personal VR settings are deliberately separate.','    // No network/host ownership is implied by this local immutable value.','    public sealed class R24PlayableMatchRules','    {']
for k,v in defaults.items():lines.append('        public readonly '+cstype(v)+' '+pname(k)+';')
lines+=['        readonly Dictionary<string,object> values;','        R24PlayableMatchRules(Dictionary<string,object> validated)','        {','            values=new Dictionary<string,object>(validated,StringComparer.Ordinal);']
for k,v in defaults.items():lines.append('            '+pname(k)+'=('+cstype(v)+')values["'+k+'"];')
lines+=['        }','        static readonly Dictionary<string,object> Defaults=new Dictionary<string,object>(StringComparer.Ordinal)','        {']
for k,v in defaults.items():lines.append('            {"'+k+'",'+lit(v)+'},')
lines+=['        };','        static readonly Dictionary<string,double[]> Limits=new Dictionary<string,double[]>(StringComparer.Ordinal)','        {']
for k,v in limits.items():lines.append('            {"'+k+'",new double[]{'+str(v[0])+','+str(v[1])+'}},')
lines+=['        };','        static readonly Dictionary<string,string[]> SourceChoices=new Dictionary<string,string[]>(StringComparer.Ordinal)','        {']
for k,v in choices.items():lines.append('            {"'+k+'",new string[]{'+','.join(json.dumps(x) for x in v)+'}},')
lines+=['        };', '''        public static R24PlayableMatchRules FromSourceValues(IDictionary<string,object> input,Func<string,bool> playableMap=null)
        {
            var result=new Dictionary<string,object>(Defaults,StringComparer.Ordinal);
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
            if(String.IsNullOrEmpty(selectedCatalogMapId)||selectedCatalogMapId.Length>180||selectedCatalogMapId.IndexOfAny(new[]{'\\r','\\n','\\0'})>=0)
                throw new ArgumentException("A selected installed map ID is required","selectedCatalogMapId");
            var next=new Dictionary<string,object>(values,StringComparer.Ordinal);next["map"]=selectedCatalogMapId;
            return new R24PlayableMatchRules(next);
        }
        public R24PlayableMatchRules With(string sourceKey,object value)
        {
            if(sourceKey==null||!Defaults.ContainsKey(sourceKey))throw new ArgumentException("Unknown source rule","sourceKey");
            if(sourceKey=="map")throw new ArgumentException("Use WithMap with an installed catalog selection","sourceKey");
            var next=new Dictionary<string,object>(values,StringComparer.Ordinal);next[sourceKey]=value;
            return FromSourceValues(next,delegate(string id){return id==MapId;});
        }
        public object ReadSourceValue(string key)
        {object value;if(!values.TryGetValue(key,out value))throw new ArgumentException("Unknown source rule","key");return value;}
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
}''']
asset=ROOT/'unity/Assets/Ashfall/Integration/Playable/Match/R24PlayableMatchRules.cs';asset.parent.mkdir(parents=True,exist_ok=True);asset.write_text('\n'.join(lines)+'\n',encoding='utf-8')
if not asset.with_suffix('.cs.meta').exists():asset.with_suffix('.cs.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
(HERE/'source-pins.json').write_text(json.dumps({'sourceContent':manifest['contentId'],'manifestSha256':hashlib.sha256(mf.read_bytes()).hexdigest(),'files':pins},indent=2)+'\n')
print('Contract written; verified source',len(defaults),'fields',len(limits),'ranges')
