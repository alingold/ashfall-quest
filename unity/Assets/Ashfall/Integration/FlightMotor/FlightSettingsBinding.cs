using Ashfall.Core;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.FlightMotor
{
    public sealed partial class PlayerFlightMotor
    {
        // Host publishes BEFORE PrepareStep/PreparePadStep on its serialized lane.
        // Replacement invalidates outstanding work, which must Abort then retry.
        public RuleResult PublishFlightSettings(CommandContext context,long generation,long revision,FlightSettingsSnapshot snapshot)
        {return flight.PublishSettings(context,generation,revision,snapshot);}
        public FlightSettingsSnapshot FlightSettings {get{return flight.Settings;}}
        public long FlightSettingsRevision {get{return flight.SettingsRevision;}}
    }
}
