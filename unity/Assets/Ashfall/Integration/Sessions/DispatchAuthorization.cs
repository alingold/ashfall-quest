using Ashfall.Core;
using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Sessions
{
    // Host-provisioned registry port. Pure/stable/nonthrowing on the serialized lane.
    // lease is the ACTUAL accepted queue item's lease, not the dispatcher identity.
    public interface IDispatchAuthorization
    {
        RuleResult Validate(PeerLease lease,string contentHash,long authenticatedGeneration);
    }
}
