using Ashfall.Core;

namespace Ashfall.Modules.Sessions
{
    public enum LifecycleAction { AdvanceTime, BeginLoading, WorldReady, Activate, EnterResults }
    // Trusted coordinator adapters; clients must never supply implementations.
    // Prepared admission requires one serialized host lane and pure/stable, nonthrowing ports.
    // Clock is frozen across prepare/validate/publication. Issue may reserve a unique opaque
    // token externally, but must not mutate Session state or reenter; abort need not reuse it.
    public interface ISessionClock { double Now { get; } }
    public interface IReconnectTokens { string Issue(); }
    public interface ISessionManagement { bool CanManage(CommandContext context); }
    public interface ISpawnWorld
    {
        // Return immutable, versioned map sites in Unity SI coordinates, including
        // floor-resolved zone candidates. Do not raycast downward from above roofs.
        SpawnSite[] Sites(string mapId, bool warmup);
        // Must validate floor, bounds, current zone, standing capsule clearance,
        // and occupied roots. Radius is .27m; height comes from Player authority.
        bool IsSafe(string mapId, string actorId, Float3 feet);
    }
}
