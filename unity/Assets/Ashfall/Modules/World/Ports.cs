using Ashfall.Core;
namespace Ashfall.Modules.World
{
    // Module-local integration boundary; the coordinator supplies physics and approval.
    public interface ISpawnQuery
    {
        bool TryFloor(Float3 candidate, float probeUp, float probeDepth, out Float3 floor, out Float3 normal);
        bool HasStandingRoom(Float3 feet, float radius, float height);
        bool IsHazard(Float3 feet);
    }
    public interface IContentApproval { bool IsApproved(string mapId, string version, string sha256); }
    // Owned r3 ports. Implementations must be supplied by coordinator; no shared contract invented.
    public interface IWorldApproval { bool IsApproved(ContentIdentity identity); }
    public interface IWorldBuildPolicy
    {
        bool ApproveMaterial(ContentIdentity identity, LoadedSurface surface);
        bool AllowsSpawn(ContentIdentity identity, string mode, string team, Float3 finalFeet);
    }
}
