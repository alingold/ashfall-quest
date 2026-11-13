using Ashfall.Core;
namespace Ashfall.Integration.SessionReplication
{
    // Implement ONLY from a trusted authenticated transport/session registry, never
    // from hello fields, a client authority bit, or an actor supplied by a datagram.
    // Generation is a centrally agreed connection lifetime, not a credential.
    public interface IAuthenticatedPeerPort
    {
        bool TryResolve(string connection, out AuthenticatedPeer peer);
    }
    public sealed class AuthenticatedPeer
    {
        public readonly string Actor, Session, Content;
        public readonly long Epoch, Generation;
        public readonly bool Authority;
        public AuthenticatedPeer(string actor,string session,string content,long epoch,long generation,bool authority)
        { Actor=actor; Session=session; Content=content; Epoch=epoch; Generation=generation; Authority=authority; }
    }
}
