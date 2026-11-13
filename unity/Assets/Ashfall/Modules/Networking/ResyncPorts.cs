using Ashfall.Core;
namespace Ashfall.Modules.Networking
{
    // OWNED port: Sessions authenticates the authority and binds a monotonically
    // increasing connection generation. A decoded hello alone is NOT authentication.
    // All receiver and sink calls are single-threaded, synchronous, non-reentrant.
    public interface IReplicationAdmission
    {
        RuleResult Authorize(string authorityPeer, long generation, string sessionId, string contentHash, long epoch);
    }
    public sealed class ResyncRequest
    {
        public readonly long Generation, RequestId, MinimumSequence;
        public ResyncRequest(long generation, long requestId, long minimumSequence)
        { Generation=generation; RequestId=requestId; MinimumSequence=minimumSequence; }
    }
}
