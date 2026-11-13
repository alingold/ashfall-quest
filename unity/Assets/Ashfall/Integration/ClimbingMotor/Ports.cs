using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Player;
namespace Ashfall.Integration.ClimbingMotor
{
    // All providers are query-only and stable for the host's serialized step lane.
    public struct GestureReservations
    {
        public bool LeftWeapon,RightWeapon,LeftInventory,RightInventory,LeftUi,RightUi,LeftOther,RightOther;
        public bool Left { get { return LeftWeapon||LeftInventory||LeftUi||LeftOther; } }
        public bool Right { get { return RightWeapon||RightInventory||RightUi||RightOther; } }
    }
    public interface IClimbSupport
    {
        // Returns a collision-tested completion displacement; unavailable refuses motion.
        bool TryComplete(Float3 root,Float3 head,Float3 proposed,Float3 velocity,float seconds,out Float3 completion);
    }
    public interface IClimbFrameAuthorization
    {
        // Trusted host validates lifecycle/readiness, authenticated context and exact
        // approved source-hull/query revision. This is not a client-controlled bool.
        RuleResult Validate(CommandContext context,long sequence,long lifecycleGeneration);
    }
    public struct MotorFrame
    {
        public CommandContext Context;
        public InputFrame Input;
        public MotionEnvironment Environment;
        public Ashfall.Modules.Flight.FlightFrame Flight;
        public ClimbOptions Climb;
        public GestureReservations Reservations;
        public Float3 Root,DesktopForward;
        public bool DesktopE,Ready;
        public long LifecycleGeneration;
    }
}
