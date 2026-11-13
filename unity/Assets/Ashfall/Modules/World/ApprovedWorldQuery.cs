using System;
using Ashfall.Core;
namespace Ashfall.Modules.World
{
    // Trusted host port: complete actor/dynamic/foreign occluders for this segment in SI metres.
    // Static approved structure and authored interaction boxes are always queried internally.
    public interface IWorldExternalHits
    {
        WorldQueryHit[] Query(string actorId,Float3 origin,Float3 target);
    }
    public sealed class ApprovedWorldQuery : IInteractionQuery
    {
        readonly ApprovedWorld world; readonly IWorldApproval approval;
        readonly WorldBuildPlan plan; readonly WorldGeometry geometry; readonly IWorldExternalHits external;
        public ContentIdentity Identity { get { return world.Identity; } }
        public ApprovedWorldQuery(ApprovedWorld approved,IWorldApproval approvalPolicy,IWorldBuildPolicy buildPolicy,Func<int,string> layers,IWorldExternalHits externalHits)
        {
            if(approved==null||externalHits==null)throw new ArgumentNullException("Approved world and external hit provider required");
            world=approved;approval=approvalPolicy;external=externalHits;plan=world.PrepareBuild(approval,buildPolicy,layers);geometry=new WorldGeometry(plan);
        }
        public bool HasLineOfSight(string actorId,string semanticOwnerId,Float3 origin,Float3 target,float range)
        {
            if(String.IsNullOrWhiteSpace(actorId)||String.IsNullOrWhiteSpace(semanticOwnerId))return false;
            world.Revalidate(approval);var hits=external.Query(actorId,origin,target);
            if(hits==null)return false;
            // Clone caller array; rows themselves are immutable. No shared map record escapes.
            bool result=geometry.Query(semanticOwnerId,origin,target,range,actorId,(WorldQueryHit[])hits.Clone());world.Revalidate(approval);return result;
        }
        public bool TriggerContains(string bodyId,string semanticOwnerId,Float3 point)
        {world.Revalidate(approval);bool result=geometry.TriggerContains(bodyId,semanticOwnerId,point);world.Revalidate(approval);return result;}
        public LoadedSpawn[] SafeSpawns(string mode,string team,float height,ISpawnQuery query,IWorldBuildPolicy bounds)
        {world.Revalidate(approval);var result=plan.SafeSpawns(mode,team,height,query,bounds);world.Revalidate(approval);return result;}
        public SemanticRecord ResolveBody(string bodyId)
        {world.Revalidate(approval);return plan.Registry().Resolve(bodyId);}
    }
}
