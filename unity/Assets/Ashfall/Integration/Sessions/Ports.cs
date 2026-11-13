using Ashfall.Core;
namespace Ashfall.Integration.Sessions
{
    // Trusted, pure/stable, synchronous, nonthrowing coordinator ports on one host thread.
    // No callback may reenter the coordinator or mutate admission/domain state.
    // Resolve authenticates the transport principal; packet ActorId is never identity.
    public interface ISessionPrincipal { bool Resolve(string peer, out string actor); }
    // Receives accepted root revisions; health/equipment reconstruction is a separate gate.
    public interface ISessionPlacement { void Publish(string actor, long epoch, Ashfall.Modules.Sessions.MemberSnapshot member); }
}
