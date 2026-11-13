using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.Presentation
{
    // Coordinator implements this boundary. Keys preserve source spelling; nested
    // Godot dictionaries use a dotted path, not a new shared save schema.
    public interface IPresentationCatalogHost { bool SkinRuntimeSettings { get; } }
    public interface IPresentationHost
    {
        bool TryReadSetting(string path, out string value);
        bool CanEdit(string path);
        // Must validate and durably commit the entire batch atomically before
        // returning Accepted. A rejection leaves both runtime and disk unchanged.
        RuleResult CommitSettings(IDictionary<string, string> changes);
        void SetGameplayPaused(bool paused);
        bool CanRequest(string action);
        void Request(string action, long operation);
    }

    public sealed class HudInput
    {
        public float Health, Shield, Stamina, StaminaMaximum;
        public bool Dead, Complete, NetworkGhost;
        public double MatchSeconds;
        public bool ZoneActive, OutsideZone;
        public bool NoTimeCap, FinalZone, ZoneMoving;
        public double ZoneHoldSeconds;
        public float ZoneDamagePerSecond;
    }
    public interface IPresentationStateSource { HudInput Capture(); }
    // View consumes committed model state after the adapter's HUD update/reset.
    public interface IPresentationTransientView { void Refresh(float deltaSeconds); void Clear(); }
}
