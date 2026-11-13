using System;
namespace Ashfall.Modules.Flight
{
    // Source menu controls_tuning_revision=1; not a runtime authority receipt.
    public sealed class FlightSettingsSnapshot
    {
        public const string Policy="ashfall-source-glide-steering-v1";
        public const float DefaultGain=.875f;
        public static readonly FlightSettingsSnapshot Default=new FlightSettingsSnapshot(DefaultGain);
        public float GlideSteeringGain { get; private set; }
        public FlightSettingsSnapshot(float gain)
        {
            if(Single.IsNaN(gain)||Single.IsInfinity(gain)||gain<.25f||gain>2.5f)throw new ArgumentOutOfRangeException("gain");
            GlideSteeringGain=gain;
        }
    }
}
