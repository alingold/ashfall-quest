using Ashfall.Core;
using Ashfall.Integration.Sessions;
namespace Ashfall.Integration.NetworkRuntime
{
    // This port is a trusted host capability, never a wire/credential service.
    public interface IBindingRetirement
    {
        object Capture(string connection,long leaseGeneration,long authenticatedGeneration);
        RuleResult Retire(object receipt,long currentEpoch);
    }
    internal sealed class CoordinatorBindingRetirement : IBindingRetirement
    {
        private readonly SessionNetworkCoordinator sessions;
        internal CoordinatorBindingRetirement(SessionNetworkCoordinator sessions) { this.sessions=sessions; }
        public object Capture(string connection,long leaseGeneration,long authenticatedGeneration)
        {
#if ASHFALL_BINDING_RETIREMENT_API
            return sessions.CaptureBindingRetirement(connection,leaseGeneration,authenticatedGeneration);
#else
            // No fake cleanup success against an unpatched coordinator.
            return null;
#endif
        }
        public RuleResult Retire(object receipt,long currentEpoch)
        {
#if ASHFALL_BINDING_RETIREMENT_API
            return sessions.RetireObsoleteBinding(receipt as SessionNetworkCoordinator.ObsoleteBindingReceipt,currentEpoch);
#else
            return RuleResult.Reject("coordinator-retirement-api-required:"+sessions.Epoch);
#endif
        }
    }
}
