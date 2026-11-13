using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Integration.Sessions;
namespace Ashfall.Integration.SessionReplication
{
    internal sealed class RegistryDispatchAuthorization : IDispatchAuthorization
    {
        private readonly IAuthenticatedPeerPort registry;
        internal RegistryDispatchAuthorization(IAuthenticatedPeerPort registry) { this.registry=registry; }
        public RuleResult Validate(PeerLease lease,string contentHash,long authenticatedGeneration)
        {
            AuthenticatedPeer p;
            return lease!=null && registry.TryResolve(lease.PeerId,out p) && p!=null && !p.Authority &&
                p.Actor==lease.ActorId && p.Session==lease.SessionId && p.Content==contentHash &&
                p.Epoch==lease.Epoch && p.Generation==authenticatedGeneration
                ? RuleResult.Accept() : RuleResult.Reject("dispatch-principal");
        }
    }
}
