using System;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.PersonalSettings
{
    public static class FlightSettingsPort
    {
        // EffectiveSettings already captured source migrations and host/map precedence.
        // Copy exactly once; no live callback or mutable tuning reaches Flight.
        public static FlightSettingsSnapshot Capture(EffectiveSettings source)
        {
            if(source==null)throw new ArgumentNullException("source");
            return new FlightSettingsSnapshot(source.Number("glide_steering_gain"));
        }
    }
}
