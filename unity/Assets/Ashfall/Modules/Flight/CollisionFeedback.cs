using Ashfall.Core;
namespace Ashfall.Modules.Flight
{
    // Not serializable/client constructible. Reference identity binds the producing machine.
    public sealed class FlightMotionReceipt
    {
        public readonly string SessionId, ActorId;
        public readonly long AuthorityEpoch, Tick, Generation, Revision;
        public Float3 ProposedVelocity { get; internal set; }
        internal FlightMotionReceipt(CommandContext c,long generation,long revision,Float3 velocity)
        {SessionId=c.SessionId;ActorId=c.ActorId;AuthorityEpoch=c.AuthorityEpoch;Tick=c.Tick;Generation=generation;Revision=revision;ProposedVelocity=velocity;}
    }
    public struct FlightCollisionResult
    {
        public Float3 Velocity;
        public bool Grounded, CeilingBlocked, ExternalMotionOwner;
    }
    // Trusted sole root owner must authorize the exact solver output, including handoff.
    // Pure, nonthrowing, nonreentrant host port under the serialized motion lane.
    public interface IFlightCollisionAuthorization
    {
        RuleResult ValidateCollision(CommandContext context,FlightMotionReceipt receipt,FlightCollisionResult result);
    }
}
