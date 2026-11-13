using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Core;
using Ashfall.Modules.Persistence;
using Ashfall.Modules.Player;
using Ashfall.Modules.Presentation;

namespace Ashfall.Integration.PersonalSettings
{
    // Immutable typed view; no mutable PlayerTuning instance escapes this object.
    public sealed class EffectiveSettings
    {
        private readonly Dictionary<string,string> values;
        private readonly SettingsSource source;
        internal EffectiveSettings(Dictionary<string,string> data,SettingsSource source)
        { values=new Dictionary<string,string>(data,StringComparer.Ordinal);this.source=source; }
        public string Text(string key) { return values[key]; }
        public float Number(string key) { return float.Parse(values[key],CultureInfo.InvariantCulture); }
        public bool Flag(string key) { return values[key]=="true"; }
        public bool LeftHanded { get { return Text("dominant_hand")=="left"; } }
        public bool Vignette { get { return Flag("vignette_enabled"); } }
        public float WalkBaseMetersPerSecond { get { return Flag("comfort_speed")?2.6f:PlayerTuning.WalkSpeed; } }
        public float AudioLinear(string bus) { return Number(bus=="Master"?"master_volume":"audio_volumes."+bus); }
        public float AudioDecibels(string bus) { return (float)(20*Math.Log10(Math.Max(bus=="Master"?.0001f:.001f,AudioLinear(bus)))); }
        public bool AudioMuted(string bus) { return AudioLinear(bus)<=0; }
        // Maps only fields supported by the pinned Player public API. Comfort base,
        // handedness, forward reference, sprint and audio remain explicit typed ports.
        public PlayerTuning CreatePlayerTuning()
        {
            var result=source==SettingsSource.Quest?new PlayerTuning():PlayerTuning.SkinRuntime();
            result.WalkMultiplier=Number("walk_speed_multiplier");
            result.SnapRadians=Number("snap_degrees")*(float)(Math.PI/180);
            result.SmoothRadiansPerSecond=Number("smooth_turn_speed")*(float)(Math.PI/180);
            result.SmoothAnalog=Flag("smooth_analog");result.SmoothEaseIn=Flag("smooth_ease_in");
            result.Turning=Text("turning_style")=="snap"?TurnStyle.Snap:Text("turning_style")=="auto_snap"?TurnStyle.AutoSnap:TurnStyle.Smooth;
            return result;
        }
    }

    // Single bridge owns a Unity destination, never a Godot source file. Capture is
    // explicit disk reload; commits use an isolated store so rejected pending writes
    // cannot appear in personal/runtime snapshots. Accepted Persistence remains opaque.
    public sealed class PersonalSettingsAdapter : IPresentationHost, IPresentationSettingsHost, IPresentationCatalogHost
    {
        public const string ApiVersion="PersonalSettings/1";
        public bool SkinRuntimeSettings { get { return source==SettingsSource.SkinRuntime; } }
        private readonly object gate=new object();
        private readonly string path;
        private readonly IAtomicSettingsFiles files;
        private readonly ISettingsUiActions actions;
        private readonly SettingsSource source;
        private Dictionary<string,string> personal,host=new Dictionary<string,string>(),map=new Dictionary<string,string>();
        private HashSet<string> locked=new HashSet<string>();
        public PersonalSettingsAdapter(string unityDestination,SettingsSource source,ISettingsUiActions actions)
            : this(unityDestination,source,actions,new PhysicalSettingsFiles()) { }
        public PersonalSettingsAdapter(string unityDestination,SettingsSource source,ISettingsUiActions actions,IAtomicSettingsFiles files)
        {
            if(actions==null||files==null||!Enum.IsDefined(typeof(SettingsSource),source))throw new ArgumentException("settings ports/source");
            path=System.IO.Path.GetFullPath(unityDestination);
            if(!path.EndsWith(".ashfall-settings.json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Dedicated Unity settings destination required");
            this.source=source;this.actions=actions;this.files=files;
        }
        private SettingSpec Spec(string key)
        { foreach(var s in SettingsCatalog.Create())if(s.Key==key)return s;throw new ArgumentException("Unknown settings key"); }
        private string Normalize(string key,string value)
        {
            string result=Spec(key).Normalize(value);
            if(key=="walk_speed_multiplier")return Math.Min(source==SettingsSource.Quest?5:3,double.Parse(result,CultureInfo.InvariantCulture)).ToString("R",CultureInfo.InvariantCulture);
            return result;
        }
        // Context replacement is all-or-nothing and rejects invalid values; no user callback
        // can revoke permissions or change overrides in the middle of a commit.
        public void SetContext(IDictionary<string,string> hostValues,IDictionary<string,string> mapValues,IEnumerable<string> lockedKeys)
        {
            lock(gate) {
                var h=ValidateContext(hostValues);var m=ValidateContext(mapValues);var l=new HashSet<string>();
                if(lockedKeys!=null)foreach(var key in lockedKeys){Spec(key);l.Add(key);}
                // Validate the entire replacement against freshly read personal values
                // before publishing any override or permission changes.
                var composed=Read(Load());
                foreach(var p in m)composed[p.Key]=p.Value;
                foreach(var p in h)composed[p.Key]=p.Value;
                if(double.Parse(composed["sprint_fast_gain"],CultureInfo.InvariantCulture)<double.Parse(composed["sprint_slow_gain"],CultureInfo.InvariantCulture))
                    throw new ArgumentException("Conflicting composed sprint overrides");
                host=h;map=m;locked=l;
            }
        }
        private Dictionary<string,string> ValidateContext(IDictionary<string,string> data)
        {
            var result=new Dictionary<string,string>(StringComparer.Ordinal);
            if(data!=null)foreach(var p in data){if(p.Value==null||Normalize(p.Key,p.Value)!=p.Value)throw new ArgumentException("Invalid override");result.Add(p.Key,p.Value);}
            return result;
        }
        private static double Revision(AtomicSettingsStore store,string key,double fallback)
        {
            string value;double number;
            return store.TryRead(key,out value)&&double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&!double.IsNaN(number)&&!double.IsInfinity(number)?number:fallback;
        }
        private Dictionary<string,string> Read(AtomicSettingsStore store)
        {
            if(Revision(store,"movement_settings_version",1)>3 || Revision(store,"controls_tuning_revision",0)>1 || Revision(store,"audio_defaults_revision",0)>993)
                throw new InvalidOperationException("Unsupported source settings revision");
            // ImportGodotSettings preserves nested objects as opaque JSON strings.
            var nested=new Dictionary<string,Dictionary<string,string>>();
            foreach(string parent in new[]{"weapon_settings","audio_volumes"}) {
                string raw;
                nested[parent]=store.TryRead(parent,out raw)?RawSettingsJson.Object(raw):new Dictionary<string,string>();
            }
            var result=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var s in SettingsCatalog.Create()) {
                string value;int dot=s.Key.IndexOf('.');
                bool found;
                if(dot<0)found=store.TryRead(s.Key,out value);
                else {
                    found=nested[s.Key.Substring(0,dot)].TryGetValue(s.Key.Substring(dot+1),out value);
                    // Existing Unity dotted values are explicit and take precedence.
                    string dotted;if(store.TryRead(s.Key,out dotted)){found=true;value=dotted;}
                }
                if(found && value.StartsWith("\"",StringComparison.Ordinal))value=RawSettingsJson.String(value);
                result[s.Key]=Normalize(s.Key,found?value:s.Default);
            }
            // Source apply_settings: missing/partial saves use 1, even though Quest
            // player's pre-load field initializes to 2.5. No rewrite of raw personal save.
            string oldWalk;
            if(Revision(store,"movement_settings_version",1)<2 && store.TryRead("walk_speed_multiplier",out oldWalk)) {
                double n;if(double.TryParse(oldWalk,NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n))
                    result["walk_speed_multiplier"]=Normalize("walk_speed_multiplier",(n/1.2).ToString("R",CultureInfo.InvariantCulture));
            }
            if(Revision(store,"controls_tuning_revision",0)<1) {
                string raw;
                if(store.TryRead("glide_steering_gain",out raw))result["glide_steering_gain"]=Normalize("glide_steering_gain",(double.Parse(result["glide_steering_gain"],CultureInfo.InvariantCulture)*.5).ToString("R",CultureInfo.InvariantCulture));
                if(Math.Abs(double.Parse(result["sprint_fast_gain"],CultureInfo.InvariantCulture)-1.5)<.00001)result["sprint_fast_gain"]=Normalize("sprint_fast_gain",(9.5/6.5).ToString("R",CultureInfo.InvariantCulture));
            }
            if(Revision(store,"audio_defaults_revision",0)<993){result["audio_volumes.Music"]="0";result["audio_volumes.Ambience"]="0";}
            Pair(result);return result;
        }
        private static void Pair(Dictionary<string,string> values)
        {
            if(double.Parse(values["sprint_fast_gain"],CultureInfo.InvariantCulture)<double.Parse(values["sprint_slow_gain"],CultureInfo.InvariantCulture))
                values["sprint_fast_gain"]=values["sprint_slow_gain"];
        }
        private AtomicSettingsStore Load()
        {
            var store=new AtomicSettingsStore(path,files);
            if(!store.Load(false).Accepted)throw new InvalidOperationException("Settings unavailable");
            return store;
        }
        public SettingsSnapshot CaptureSettings()
        {
            lock(gate){var next=Read(Load());personal=next;return new SettingsSnapshot(personal,host,map);}
        }
        public bool TryReadSetting(string key,out string value)
        { lock(gate){if(personal==null)CaptureSettings();return personal.TryGetValue(key,out value);} }
        public bool CanEdit(string key) { lock(gate){return !locked.Contains(key)&&!host.ContainsKey(key)&&!map.ContainsKey(key);} }
        public EffectiveSettings Effective
        {
            get { lock(gate){if(personal==null)CaptureSettings();var result=new Dictionary<string,string>(personal);
                foreach(var p in map)result[p.Key]=p.Value;foreach(var p in host)result[p.Key]=p.Value;
                // Invalid composed sprint overrides fail explicitly, never mutate a locked rule.
                if(double.Parse(result["sprint_fast_gain"],CultureInfo.InvariantCulture)<double.Parse(result["sprint_slow_gain"],CultureInfo.InvariantCulture))throw new InvalidOperationException("Conflicting sprint overrides");
                return new EffectiveSettings(result,source);} }
        }
        public EffectiveSettings Personal
        { get { lock(gate){if(personal==null)CaptureSettings();return new EffectiveSettings(personal,source);} } }
        public RuleResult CommitSettings(IDictionary<string,string> changes)
        {
            lock(gate) { try {
                if(changes==null)return RuleResult.Reject("Missing batch");
                var batch=new Dictionary<string,string>(changes,StringComparer.Ordinal);
                foreach(var p in batch)if(!CanEdit(p.Key)||p.Value==null||Normalize(p.Key,p.Value)!=p.Value)return RuleResult.Reject("Invalid or locked setting");
                var store=Load();var next=Read(store);
                foreach(var p in batch)next[p.Key]=p.Value;
                if(double.Parse(next["sprint_fast_gain"],CultureInfo.InvariantCulture)<double.Parse(next["sprint_slow_gain"],CultureInfo.InvariantCulture))return RuleResult.Reject("Sprint conflict");
                var composed=new Dictionary<string,string>(next);
                foreach(var p in map)composed[p.Key]=p.Value;foreach(var p in host)composed[p.Key]=p.Value;
                if(double.Parse(composed["sprint_fast_gain"],CultureInfo.InvariantCulture)<double.Parse(composed["sprint_slow_gain"],CultureInfo.InvariantCulture))return RuleResult.Reject("Effective sprint conflict");
                // Materialize all interpreted values before stamping revisions. Otherwise
                // unrelated edits would stamp away legacy conversions on the next load.
                foreach(var p in next) {
                    int dot=p.Key.IndexOf('.');
                    if(dot<0)store.Write(p.Key,p.Value);
                }
                foreach(string parent in new[]{"audio_volumes","weapon_settings"}) {
                    string raw;var obj=store.TryRead(parent,out raw)?RawSettingsJson.Object(raw):new Dictionary<string,string>();
                    foreach(var p in next)if(p.Key.StartsWith(parent+".",StringComparison.Ordinal)) {
                        obj[p.Key.Substring(parent.Length+1)]=Spec(p.Key).Numeric?p.Value:(p.Value=="true"||p.Value=="false"?p.Value:RawSettingsJson.Quote(p.Value));
                        // Synchronize pre-existing dotted keys; preserve unrelated opaque data.
                        string old;if(store.TryRead(p.Key,out old))store.Write(p.Key,p.Value);
                    }
                    store.Write(parent,RawSettingsJson.Encode(obj));
                }
                store.Write("movement_settings_version","3");store.Write("controls_tuning_revision","1");store.Write("audio_defaults_revision","993");
                var flushed=store.Flush();if(!flushed.Accepted)return RuleResult.Reject("Settings commit rejected");
                personal=next;return RuleResult.Accept();
            } catch { return RuleResult.Reject("Settings unavailable"); } }
        }
        public void SetGameplayPaused(bool paused) { actions.SetPaused(paused); }
        public bool CanRequest(string action) { return actions.CanRequest(action); }
        public void Request(string action,long operation) { actions.Request(action,operation); }
    }
}
