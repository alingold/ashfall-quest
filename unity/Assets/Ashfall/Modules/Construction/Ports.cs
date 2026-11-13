using Ashfall.Core;

namespace Ashfall.Modules.Construction
{
    // Sequence orders host events, independently of simulation time. Retries must
    // preserve context, sequence and identity. Never mint a new tick for a retry.
    public struct ConstructionCommand
    {
        public readonly CommandContext Context;
        public readonly long Sequence;
        public ConstructionCommand(CommandContext context,long sequence) { Context=context;Sequence=sequence; }
    }
    // All inputs supplied by host simulation, never copied from client intent.
    public sealed class BuilderState
    {
        public readonly Float3 Origin, Direction, Head;
        public readonly int Tier,Variant;
        public readonly bool Expanded;
        public readonly bool Eligible, Tracked;
        public BuilderState(Float3 origin, Float3 direction, Float3 head, bool eligible, bool tracked)
        : this(origin,direction,head,eligible,tracked,0,true) {}
        public BuilderState(Float3 origin, Float3 direction, Float3 head, bool eligible, bool tracked,int tier,bool expanded)
        : this(origin,direction,head,eligible,tracked,tier,expanded,0) {}
        public BuilderState(Float3 origin, Float3 direction, Float3 head, bool eligible, bool tracked,int tier,bool expanded,int variant)
        { Variant=variant;Tier=tier;Expanded=expanded;Origin = origin; Direction = direction; Head = head; Eligible = eligible; Tracked = tracked; }
    }
    public interface IConstructionActors
    {
        bool TryRead(string actorId, out BuilderState state);
    }
    public interface IConstructionWorld
    {
        // Static map contacts only; exclude loot, actors and construction itself.
        bool HasStaticContact(Panel panel, float tolerance);
        // Terrain-only for buried walls; archive roofs/catwalks are not terrain.
        bool IsBuried(Panel panel);
        // Return player_clearance/objective_clearance/etc, or empty. Test all actors.
        // Ordinary static geometry overlap is intentionally permitted.
        string Clearance(Panel panel, string actorId);
        bool HasLineOfSight(string actorId, Float3 origin, Float3 target);
    }
}
