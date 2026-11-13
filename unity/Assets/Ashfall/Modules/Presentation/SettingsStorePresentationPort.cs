using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.Presentation
{
    public interface IPresentationSettingsHost
    {
        SettingsSnapshot CaptureSettings();
    }

    // Immutable copy: callers cannot mutate personal settings via an effective view.
    public sealed class SettingsSnapshot
    {
        private readonly Dictionary<string,string> personal, host, map;
        public SettingsSnapshot(IDictionary<string,string> personalValues,
            IDictionary<string,string> hostOverrides, IDictionary<string,string> mapOverrides)
        {
            personal=Copy(personalValues);host=Copy(hostOverrides);map=Copy(mapOverrides);
        }
        private static Dictionary<string,string> Copy(IDictionary<string,string> source)
        { return source==null?new Dictionary<string,string>(StringComparer.Ordinal):new Dictionary<string,string>(source,StringComparer.Ordinal); }
        public bool TryReadPersonal(string key,out string value) { return personal.TryGetValue(key,out value); }
        // Host has precedence over map, then personal. Override provenance is explicit.
        public bool TryReadOverride(string key,out string value)
        { return host.TryGetValue(key,out value) || map.TryGetValue(key,out value); }
        public bool IsOverridden(string key) { return host.ContainsKey(key)||map.ContainsKey(key); }
        public string Origin(string key) { return host.ContainsKey(key)?"host":map.ContainsKey(key)?"map":"personal"; }
        public bool TryReadEffective(string key,out string value)
        { return TryReadOverride(key,out value)||TryReadPersonal(key,out value); }
    }

    // Reads real Core.ISettingsStore; never flattens effective overrides into saves.
    // Writes/Flush and atomicity remain the Persistence/coordinator's responsibility.
    public sealed class SettingsStorePresentationPort
    {
        private readonly ISettingsStore personalStore;
        public SettingsStorePresentationPort(ISettingsStore personalStore)
        { if(personalStore==null)throw new ArgumentNullException("personalStore");this.personalStore=personalStore; }
        public SettingsSnapshot Capture(IDictionary<string,string> hostOverrides, IDictionary<string,string> mapOverrides)
        {
            var personal=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var spec in SettingsCatalog.Create()){
                string value;if(personalStore.TryRead(spec.Key,out value))personal.Add(spec.Key,value);
            }
            return new SettingsSnapshot(personal,hostOverrides,mapOverrides);
        }
    }
}
