using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Presentation;
namespace Ashfall.Integration.PersonalSettings
{
    // Use this host for the menu on the single coordinator lane. Durable commit
    // and runtime publication are separate facts; retry never rewrites settings.
    public sealed class PersonalSettingsRuntimeHost : IPresentationHost,IPresentationSettingsHost,IPresentationCatalogHost
    {
        private readonly PersonalSettingsAdapter settings;
        private readonly PlayerSettingsBinding player;
        private bool requestedPause;
        public bool RuntimePending { get; private set; }
        public PersonalSettingsRuntimeHost(PersonalSettingsAdapter settings,PlayerSettingsBinding player)
        { if(settings==null||player==null)throw new ArgumentNullException();this.settings=settings;this.player=player;RuntimePending=true; }
        public bool SkinRuntimeSettings { get { return settings.SkinRuntimeSettings; } }
        public bool RefreshRuntime()
        {
            try { player.Apply(settings.Effective);RuntimePending=false;player.SetPresentationPaused(requestedPause);return true; }
            catch { RuntimePending=true;player.SetPresentationPaused(true);return false; }
        }
        public SettingsSnapshot CaptureSettings()
        { var snapshot=settings.CaptureSettings();RefreshRuntime();return snapshot; }
        public RuleResult CommitSettings(IDictionary<string,string> batch)
        {
            var result=settings.CommitSettings(batch);
            if(result.Accepted)RefreshRuntime();
            return result;
        }
        public bool TryReadSetting(string key,out string value){return settings.TryReadSetting(key,out value);}
        public bool CanEdit(string key){return settings.CanEdit(key);}
        public void SetGameplayPaused(bool value){requestedPause=value;player.SetPresentationPaused(value||RuntimePending);settings.SetGameplayPaused(value);}
        public bool CanRequest(string action){return settings.CanRequest(action);}
        public void Request(string action,long operation){settings.Request(action,operation);}
    }
}
