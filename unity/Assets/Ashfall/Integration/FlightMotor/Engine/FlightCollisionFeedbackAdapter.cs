#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.FlightMotor
{
    // Staged central-root bridge only. No Update, Physics queries, transforms or collider writes.
    // The existing sole root solver supplies the resulting SI velocity after its one move.
    public sealed class FlightCollisionFeedbackAdapter
    {
        readonly IFlightMotor motor;
        readonly IFlightCollisionAuthorization authorization;
        public FlightCollisionFeedbackAdapter(IFlightMotor motor,IFlightCollisionAuthorization authorization)
        {
            if(motor==null||authorization==null)throw new System.ArgumentNullException();
            this.motor=motor;this.authorization=authorization;
        }
        public RuleResult SubmitResolved(CommandContext context,FlightMotionReceipt producedStep,FlightCollisionResult resolved)
        {return motor.ApplyCollisionResult(context,producedStep,resolved,authorization);}
    }
}
#endif
