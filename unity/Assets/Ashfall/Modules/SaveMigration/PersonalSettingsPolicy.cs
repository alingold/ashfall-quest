using System;
using System.Collections.Generic;
namespace Ashfall.Modules.SaveMigration
{
    // Snapshot source map override entries. Only player/field-menu saved keys belong here;
    // transient charges, health and reserves must not enter personal settings.
    public sealed class PersonalOverride
    {
        public readonly string Key, PreviousJson, AppliedJson;
        public PersonalOverride(string key,string previousJson,string appliedJson)
        { Key=key; PreviousJson=previousJson; AppliedJson=appliedJson; RawJson.Object("{\"v\":"+previousJson+"}"); RawJson.Object("{\"v\":"+appliedJson+"}"); }
    }
    public static class PersonalSettingsPolicy
    {
        private static readonly string[] SavedRuleKeys = { "walk_speed_multiplier", "fling_power", "sprint_slow_gain", "sprint_fast_gain", "sprint_stamina_seconds", "player_damage_multiplier", "enemy_damage_multiplier" };
        public static string ForSourceSave(string currentJson, IEnumerable<PersonalOverride> mapEntries, string cachedLocalJson, bool liveNetwork, SourceProfile profile)
        {
            if (!liveNetwork) return profile == SourceProfile.Quest ? ForSave(currentJson,mapEntries) : RawJson.Encode(RawJson.Object(currentJson));
            var result=RawJson.Object(currentJson);
            var cached=RawJson.Object(cachedLocalJson);
            foreach(string key in SavedRuleKeys) { string value; if(cached.TryGetValue(key,out value))result[key]=value; }
            return RawJson.Encode(result);
        }
        public static string ForSave(string currentJson,IEnumerable<PersonalOverride> entries)
        {
            var values=RawJson.Object(currentJson);
            foreach(var e in entries)
            {
                string current; if(Array.IndexOf(SavedRuleKeys,e.Key)<0 || !values.TryGetValue(e.Key,out current))continue;
                double a,b;
                bool same=SemanticMigration.Numeric(current,out a)&&SemanticMigration.Numeric(e.AppliedJson,out b)?
                    (a==b || Math.Abs(a-b)<Math.Max(0.00001*Math.Abs(a),0.00001)) : current==e.AppliedJson;
                if(same)values[e.Key]=e.PreviousJson;
            }
            return RawJson.Encode(values);
        }
    }
}
