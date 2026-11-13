using Ashfall.Core;
namespace Ashfall.Modules.Flight
{
    public enum FlightLifecycle { Respawn, Death, Disconnect }
    // Host authenticates this exact actor, session, epoch, generation and reason
    // against its Sessions/health record. Pure, non-reentrant query; not client intent.
    public interface IFlightLifecycleAuthorization
    {
        RuleResult ValidateTransition(CommandContext context, long generation, FlightLifecycle reason);
    }
}
