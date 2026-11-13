using Ashfall.Core;
namespace Ashfall.Modules.Flight
{
    // All observations come from the authoritative root/collision owner, never camera transforms.
    public interface IFlightWorld
    {
        bool ClearPadContact(string padId, Float3 face, Float3 contact);
        bool ClearCapsuleExit(string actorId, Float3 rootDestination);
    }
    public interface IFlightRoot
    {
        void SetFlightVelocity(Float3 velocity);
        void RelocateFlightRoot(Float3 destination);
    }
}
