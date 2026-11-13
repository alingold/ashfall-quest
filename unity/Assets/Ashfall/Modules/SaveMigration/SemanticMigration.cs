using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Persistence;

namespace Ashfall.Modules.SaveMigration
{
    public sealed class PhysicalMigrationSources : IMigrationSources
    {
        public byte[] ReadIfPresent(string path, int maxBytes)
        {
            try { return new PhysicalSettingsFiles().Read(path, maxBytes); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
    }
    // Plans contain immutable snapshots. Commit imports the snapshot, never rereads or writes Godot data.
    public sealed class SemanticMigration
    {
        public const string CampaignId = "ashfall-expanded-v1";
        private static readonly string[] Missions = { "relay", "turbine", "uplink", "dam_crown", "spillway", "extraction", "complete" };
        private static readonly string[] Legacy = { "relay", "turbine", "uplink", "extraction", "complete" };
        private static readonly string[] Suffixes = { "", ".pending", ".backup", ".backup.pending" };
        private readonly IMigrationSources sources;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public SemanticMigration(IMigrationSources sources) { if (sources == null) throw new ArgumentNullException("sources"); this.sources = sources; }
        private sealed class Snapshot
        {
            internal string Path, Raw;
            internal Dictionary<string, string> Data;
            internal MigrationState State;
        }
        private Snapshot Read(string path, bool campaign, bool expanded, Dictionary<string,string> originals)
        {
            var s = new Snapshot { Path = System.IO.Path.GetFullPath(path), State = MigrationState.Missing };
            try
            {
                byte[] bytes = sources.ReadIfPresent(s.Path, AtomicSettingsStore.MaxDocumentBytes);
                originals[s.Path] = "null";
                if (bytes == null) return s;
                if (bytes.Length > AtomicSettingsStore.MaxDocumentBytes) throw new IOException("Source exceeds limit");
                originals[s.Path] = RawJson.Quote(Convert.ToBase64String(bytes));
                s.State = MigrationState.Invalid;
                s.Raw = Utf8.GetString(bytes); if (s.Raw.StartsWith("\ufeff", StringComparison.Ordinal)) s.Raw = s.Raw.Substring(1);
                s.Data = RawJson.Object(s.Raw);
                string version;
                if (s.Data.ContainsKey("schema")) { s.State = MigrationState.Blocked; return s; }
                if (!campaign) { s.State = s.Data.ContainsKey("version") ? MigrationState.Blocked : MigrationState.Ready; return s; }
                if (expanded && (Text(s.Data,"campaign_id") != CampaignId || Array.IndexOf(Missions,Text(s.Data,"mission_id")) < 0))
                { s.State = MigrationState.Blocked; return s; }
                if (!s.Data.TryGetValue("version", out version)) return s;
                // Decimal cannot round an unfamiliar long coefficient down to schema one.
                if (!One(version)) { s.State = MigrationState.Blocked; return s; }
                double chapter, kills, time;
                if (!Number(s.Data,"chapter",out chapter) || !Whole(chapter, (expanded ? Missions : Legacy).Length-1) ||
                    !Number(s.Data,"kills",out kills) || !Whole(kills,1000000) || !Number(s.Data,"play_time",out time) || time < 0 || time > 31536000) return s;
                string mission = (expanded ? Missions : Legacy)[(int)chapter];
                if (expanded && Text(s.Data,"mission_id") != mission) return s;
                s.Data = new Dictionary<string,string>(s.Data, StringComparer.Ordinal);
                s.Data["campaign_id"] = RawJson.Quote(CampaignId); s.Data["mission_id"] = RawJson.Quote(mission);
                s.Data["chapter"] = Array.IndexOf(Missions,mission).ToString(CultureInfo.InvariantCulture);
                s.State = MigrationState.Ready; return s;
            }
            catch (FormatException) { s.State = MigrationState.Invalid; return s; }
            catch (Exception ex) { s.State = MigrationState.Blocked; s.Raw = ex.Message; return s; }
        }
        private static bool One(string t)
        {
            int e=t.IndexOfAny(new[]{'e','E'}); string c=e<0?t:t.Substring(0,e); long exp=0;
            if(e>=0&&!Int64.TryParse(t.Substring(e+1),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out exp))return false;
            int dot=c.IndexOf('.'), fraction=dot<0?0:c.Length-dot-1;
            string digits=c.Replace(".","").TrimStart('0'); int end=digits.Length;
            while(end>0&&digits[end-1]=='0')end--;
            return end==1&&digits[0]=='1'&&exp==(long)fraction-(digits.Length-end);
        }
        private Snapshot Family(string path, bool expanded, bool recover, Dictionary<string,string> originals)
        {
            var all=new List<Snapshot>(); bool exists=false;
            foreach(string suffix in Suffixes)
            {
                Snapshot s=Read(path+suffix,true,expanded,originals); all.Add(s); exists |= s.State!=MigrationState.Missing;
            }
            foreach(Snapshot s in all) if(s.State==MigrationState.Blocked)return s;
            for(int i=0;i<(recover?3:1);i++)if(all[i].State==MigrationState.Ready)return all[i];
            return new Snapshot { State=exists?MigrationState.Invalid:MigrationState.Missing, Path=path };
        }
        public MigrationPlan Campaign(string expandedPath, string legacyPath, bool allowRecovery)
        {
            var originals=new Dictionary<string,string>(StringComparer.Ordinal);
            try
            {
                if(String.IsNullOrWhiteSpace(expandedPath)||(!String.IsNullOrEmpty(legacyPath)&&System.IO.Path.GetFullPath(expandedPath).Equals(System.IO.Path.GetFullPath(legacyPath),StringComparison.OrdinalIgnoreCase)))
                    return Failed("campaign",MigrationState.Blocked,"Distinct expanded and legacy paths required");
                Snapshot s=Family(expandedPath,true,allowRecovery,originals);
                if(s.State==MigrationState.Missing&&!String.IsNullOrEmpty(legacyPath))s=Family(legacyPath,false,allowRecovery,originals);
                return Plan("campaign",s,originals,s.Data==null?null:RawJson.Encode(s.Data));
            }
            catch(Exception ex){return Failed("campaign",MigrationState.Blocked,ex.Message);}
        }
        public MigrationPlan Lobby(string path, SourceProfile profile, IMapAvailability maps)
        {
            if(!Enum.IsDefined(typeof(SourceProfile),profile))return Failed("lobby",MigrationState.Blocked,"Unsupported source profile");
            var originals=new Dictionary<string,string>(StringComparer.Ordinal); Snapshot s=Read(path,false,false,originals);
            if(s.State!=MigrationState.Ready)return Plan("lobby",s,originals,null);
            try
            {
                var result=RawJson.Object(profile==SourceProfile.Quest?SourceSchemas.QuestDEFAULTS:SourceSchemas.SkinLibraryDEFAULTS);
                var limits=RawJson.Object(profile==SourceProfile.Quest?SourceSchemas.QuestLIMITS:SourceSchemas.SkinLibraryLIMITS); var choices=RawJson.Object(profile==SourceProfile.Quest?SourceSchemas.QuestCHOICES:SourceSchemas.SkinLibraryCHOICES);
                foreach(string key in new List<string>(result.Keys))
                {
                    string raw; if(!s.Data.TryGetValue(key,out raw))continue;
                    if(choices.ContainsKey(key))
                    {
                        if(!raw.StartsWith("\"",StringComparison.Ordinal))continue;
                        string v=RawJson.String(raw);
                        bool allowed=key=="map" ? (v=="valley"||v=="range"||v=="cyber_bird"||((v=="community_local"||v.StartsWith("archive:",StringComparison.Ordinal))&&maps!=null&&maps.IsPlayable(v))) :
                            key=="mode" ? v=="revive"||v=="respawn" : v=="standard"||v=="pistols"||v=="swords";
                        if(allowed)result[key]=raw;
                    }
                    else if(result[key]=="true"||result[key]=="false") { if(raw=="true"||raw=="false")result[key]=raw; }
                    else
                    {
                        double n; if(!Numeric(raw,out n))continue;
                        string[] range=limits[key].Trim('[',']').Split(','); double min=Double.Parse(range[0],CultureInfo.InvariantCulture),max=Double.Parse(range[1],CultureInfo.InvariantCulture);
                        if(result[key].IndexOf('.')<0)n=Math.Truncate(n);
                        result[key]=Math.Max(min,Math.Min(max,n)).ToString("R",CultureInfo.InvariantCulture);
                    }
                }
                MigrationPlan plan=Plan("lobby",s,originals,RawJson.Encode(result));plan.Profile=profile;return plan;
            }catch(Exception ex){return Failed("lobby",MigrationState.Blocked,ex.Message);}
        }
        public MigrationPlan Tuning(string path, bool weaponTiming)
        {
            string kind=weaponTiming?"weapon-timing":"explosive-tuning";
            var originals=new Dictionary<string,string>(StringComparer.Ordinal); Snapshot s=Read(path,false,false,originals);
            if(s.State!=MigrationState.Ready)return Plan(kind,s,originals,null);
            var result=RawJson.Object(weaponTiming?SourceSchemas.Rates:SourceSchemas.Explosives);
            foreach(string key in new List<string>(result.Keys))
            {
                double n; string raw;
                if(s.Data.TryGetValue(key,out raw)&&Numeric(raw,out n))result[key]=Math.Max(weaponTiming?.2:.05,Math.Min(weaponTiming?40:key=="rocket_speed"?240:60,n)).ToString("R",CultureInfo.InvariantCulture);
                // Arsenal applies this factor after overrides, including default rate.
                if(weaponTiming&&key=="matadors")result[key]=(Double.Parse(result[key],CultureInfo.InvariantCulture)*1.2).ToString("R",CultureInfo.InvariantCulture);
            }
            return Plan(kind,s,originals,RawJson.Encode(result));
        }
        private static MigrationPlan Plan(string kind,Snapshot s,Dictionary<string,string> originals,string data)
        {return new MigrationPlan{Kind=kind,State=s.State,SelectedSource=s.Path,Reason=s.State.ToString(),Data=data,Originals=RawJson.Encode(originals)};}
        private static MigrationPlan Failed(string kind,MigrationState state,string reason)
        {return new MigrationPlan{Kind=kind,State=state,Reason=reason};}
        // One dedicated destination per semantic kind. Accepted store supplies locking, conflict detection,
        // durable verification, atomic replacement and backup recovery. Never call unversioned importer.
        public static RuleResult Commit(MigrationPlan plan, AtomicSettingsStore destination)
        {
            if(plan==null||destination==null||plan.State!=MigrationState.Ready||String.IsNullOrEmpty(plan.Kind)||
                String.IsNullOrEmpty(plan.Data)||String.IsNullOrEmpty(plan.Originals)||String.IsNullOrEmpty(plan.SelectedSource))return RuleResult.Reject("Complete ready plan required");
            if(plan.Kind=="lobby"&&(!plan.Profile.HasValue||!Enum.IsDefined(typeof(SourceProfile),plan.Profile.Value)))return RuleResult.Reject("Lobby source profile required");
            foreach(string source in RawJson.Object(plan.Originals).Keys)
                foreach(string suffix in new[]{"",".pending",".backup",".backup.pending",".lock"})
                    if(destination.Path.Equals(source+suffix,StringComparison.OrdinalIgnoreCase))return RuleResult.Reject("Destination aliases source family");
            RuleResult load=destination.Load(false);
            if(!load.Accepted||destination.State!=SettingsState.Missing)return RuleResult.Reject("Dedicated missing Unity destination required");
            destination.Write("semantic.schema","1"); destination.Write("semantic.kind",plan.Kind);
            destination.Write("semantic.data",plan.Data); destination.Write("semantic.source",plan.SelectedSource);
            if(plan.Kind=="lobby")destination.Write("semantic.source-profile",plan.Profile==SourceProfile.Quest?"quest":"skin-library");
            destination.Write("semantic.originals.base64",plan.Originals);
            return destination.Flush();
        }
        // Read a semantic envelope only after accepted storage validation/recovery. The schema
        // and kind are distinct from opaque Persistence schema 1 and cannot be interchanged.
        public static MigrationPlan ReadCommitted(string destinationPath, string expectedKind, bool allowRecovery)
        { return ReadCommittedOwned(new AtomicSettingsStore(destinationPath,new SemanticFiles(destinationPath,new PhysicalSettingsFiles())),expectedKind,allowRecovery); }
        private static MigrationPlan ReadCommittedOwned(AtomicSettingsStore store, string expectedKind, bool allowRecovery)
        {
            try
            {
                if(store==null)return Failed(expectedKind,MigrationState.Blocked,"Store required");
                RuleResult loaded=store.Load(allowRecovery);
                if(!loaded.Accepted)return Failed(expectedKind,MigrationState.Blocked,loaded.Reason);
                if(store.State==SettingsState.Missing)return Failed(expectedKind,MigrationState.Missing,"Missing destination");
                string schema,kind,data,originals,source;
                if(!store.TryRead("semantic.schema",out schema)||schema!="1"||!store.TryRead("semantic.kind",out kind)||kind!=expectedKind)
                    return Failed(expectedKind,MigrationState.Blocked,"Unknown semantic schema/kind");
                if(!store.TryRead("semantic.data",out data)||!store.TryRead("semantic.originals.base64",out originals)||!store.TryRead("semantic.source",out source))
                    return Failed(kind,MigrationState.Invalid,"Incomplete semantic envelope");
                SourceProfile? profile=null;
                if(kind=="lobby") {string token;if(!store.TryRead("semantic.source-profile",out token)||(token!="quest"&&token!="skin-library"))return Failed(kind,MigrationState.Blocked,"Explicit supported lobby source profile required");profile=token=="quest"?SourceProfile.Quest:SourceProfile.SkinLibrary;}
                var values=RawJson.Object(data); var artifacts=RawJson.Object(originals);
                string selected;
                if(!artifacts.TryGetValue(source,out selected)||selected=="null")throw new FormatException("Selected source archive absent");
                foreach(var a in artifacts)if(a.Value!="null")Convert.FromBase64String(RawJson.String(a.Value));
                if(kind=="campaign")
                {
                    double chapter,kills,time;
                    if(Text(values,"campaign_id")!=CampaignId||!Number(values,"chapter",out chapter)||!Whole(chapter,Missions.Length-1)||
                       Text(values,"mission_id")!=Missions[(int)chapter]||!Number(values,"kills",out kills)||!Whole(kills,1000000)||
                       !Number(values,"play_time",out time)||time<0||time>31536000)throw new FormatException("Invalid semantic campaign");
                }
                else if(kind=="lobby")
                {
                    var defaults=RawJson.Object(profile==SourceProfile.Quest?SourceSchemas.QuestDEFAULTS:SourceSchemas.SkinLibraryDEFAULTS);
                    var limits=RawJson.Object(profile==SourceProfile.Quest?SourceSchemas.QuestLIMITS:SourceSchemas.SkinLibraryLIMITS);
                    foreach(var d in defaults)
                    {
                        string value;if(!values.TryGetValue(d.Key,out value))throw new FormatException("Missing lobby key");
                        if(d.Value.StartsWith("\"",StringComparison.Ordinal))
                        {
                            string v=RawJson.String(value);
                            bool valid=d.Key=="map" ? v=="range"||v=="valley"||v=="cyber_bird"||v=="community_local"||v.StartsWith("archive:",StringComparison.Ordinal) :
                                d.Key=="mode" ? v=="revive"||v=="respawn" : v=="standard"||v=="pistols"||v=="swords";
                            if(!valid)throw new FormatException("Invalid lobby choice");
                        }
                        else if(d.Value=="true"||d.Value=="false") { if(value!="true"&&value!="false")throw new FormatException("Invalid boolean"); }
                        else
                        {
                            double n;string[] bounds=limits[d.Key].Trim('[',']').Split(',');
                            if(!Numeric(value,out n)||n<Double.Parse(bounds[0],CultureInfo.InvariantCulture)||n>Double.Parse(bounds[1],CultureInfo.InvariantCulture)||
                               (d.Value.IndexOf('.')<0&&Math.Truncate(n)!=n))throw new FormatException("Invalid lobby number");
                        }
                    }
                }
                else if(kind=="weapon-timing"||kind=="explosive-tuning")
                {
                    var defaults=RawJson.Object(kind=="weapon-timing"?SourceSchemas.Rates:SourceSchemas.Explosives);
                    foreach(var d in defaults)
                    {
                        string value;double n;
                        if(!values.TryGetValue(d.Key,out value))throw new FormatException("Missing tuning key");
                        if(!Numeric(value,out n)&&(!d.Value.StartsWith("\"",StringComparison.Ordinal)||RawJson.String(value)!=RawJson.String(d.Value)))throw new FormatException("Invalid tuning value");
                        double number, baseline;
                        if(Numeric(value,out number))
                        {
                            double min=kind=="weapon-timing"?(d.Key=="matadors"?.24:.2):.05,max=kind=="weapon-timing"?(d.Key=="matadors"?48:40):d.Key=="rocket_speed"?240:60;
                            if((number<min||number>max)&&!(Numeric(d.Value,out baseline)&&number==baseline))throw new FormatException("Tuning out of range");
                        }
                    }
                }
                else return Failed(kind,MigrationState.Blocked,"Unknown semantic kind");
                return new MigrationPlan { State=MigrationState.Ready,Kind=kind,Data=data,Originals=originals,SelectedSource=source,Profile=profile,Reason="Validated semantic envelope" };
            }
            catch(Exception ex){return Failed(expectedKind,MigrationState.Invalid,ex.Message);}
        }
        internal static bool Numeric(string raw,out double n)
        {return Double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!Double.IsNaN(n)&&!Double.IsInfinity(n);}
        private static bool Number(Dictionary<string,string> d,string k,out double n){string raw;n=0;return d.TryGetValue(k,out raw)&&Numeric(raw,out n);}
        private static bool Whole(double n,int max){return n>=0&&n<=max&&Math.Floor(n)==n;}
        private static string Text(Dictionary<string,string> d,string k){string raw;return d.TryGetValue(k,out raw)&&raw.StartsWith("\"",StringComparison.Ordinal)?RawJson.String(raw):null;}
    }
}
