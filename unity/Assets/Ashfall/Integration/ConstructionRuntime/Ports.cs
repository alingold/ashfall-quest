using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Integration.ConstructionGeometry;
namespace Ashfall.Integration.ConstructionRuntime
{
    // Trusted host-lane snapshot, never deserialized client metadata. Tick is current
    // monotonic host time; all generations are canonical and retained across remaps.
    public sealed class HostBuilderStamp
    {
        public readonly string Session,Actor,AuthorityLease;
        public readonly long Epoch,Life,Connection,RootRevision,PoseRevision,InputGeneration;
        public readonly bool Active;
        public HostBuilderStamp(string session,long epoch,string actor,string authorityLease,long life,long connection,long rootRevision,long poseRevision,bool active,long inputGeneration=0)
        {Session=session;Epoch=epoch;Actor=actor;AuthorityLease=authorityLease;Life=life;Connection=connection;RootRevision=rootRevision;PoseRevision=poseRevision;InputGeneration=inputGeneration;Active=active;}
    }
    public interface IConstructionHostState
    {
        long Tick {get;}
        bool TryRead(string canonicalActor,out HostBuilderStamp stamp);
    }
    // Opaque instance-bound intent. Queue this token with the command; do not replace
    // it with a newly acquired token when dispatching an old callback.
    public sealed class ConstructionLease
    {
        internal readonly RuntimeHost Host;internal readonly HostBuilderStamp Stamp;internal readonly long Expires;
        internal ConstructionLease(RuntimeHost host,HostBuilderStamp stamp,long expires){Host=host;Stamp=stamp;Expires=expires;}
    }
    // Trusted host policy. Approval binds the exact payload, source surface, stable body,
    // semantic owner and explicit role. Never infer terrain from appearance or normals.
    public interface IGeometryAttestation
    {
        bool Resolve(ContentIdentity map,string sourceId,string ownerId,out string bodyId,out SurfaceRole role);
        bool Current(ContentIdentity map);
    }
    // Complete immutable-per-host-step snapshot, including requester and every objective.
    public interface IRuntimeSnapshot : IConstructionOccupants
    {
        long Generation { get; }
        bool IsCurrent(long generation);
    }
    public interface IConstructionDamageAuthorization
    {
        // Must validate registered Combat capability, amount, target, lifetime and context.
        // No implementation here: missing Combat integration rejects, never stub success.
        bool Validate(CommandContext context,string hostInstance,string pieceId,float amount,string capability);
    }
}
