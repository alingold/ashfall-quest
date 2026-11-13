using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.FlightMotor
{
    // Central root loop calls once per tick and applies the returned MotionStep
    // through its existing collision preflight and sole mover. Never run a second motor.
    public interface IFlightMotor
    {
        FlightMotionReceipt PendingMotion { get; }
        RuleResult ApplyCollisionResult(CommandContext context,FlightMotionReceipt receipt,FlightCollisionResult result,IFlightCollisionAuthorization authorization);
        RuleResult Step(CommandContext context, InputFrame input, MotionEnvironment environment,
            Ashfall.Modules.Flight.FlightFrame observation, out MotionStep motion);
    }
}
