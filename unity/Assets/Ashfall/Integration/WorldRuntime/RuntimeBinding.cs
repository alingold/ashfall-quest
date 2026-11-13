using System;
using Ashfall.Core;
using Ashfall.Modules.World;

namespace Ashfall.Integration.WorldRuntime
{
    // Coordinator-supplied immutable dependency receipt. This is a trust port, not an approval mint.
    // Must bind payload/version/source revision plus ALL mesh/material/texture dependencies and their hashes.
    public interface IRuntimeAttestation
    {
        bool Matches(ContentIdentity identity);
    }
    public sealed class RuntimeBinding : IInteractionQuery
    {
        readonly ApprovedWorld world;
        readonly IWorldApproval approval;
        readonly IRuntimeAttestation attestation;
        readonly ApprovedWorldQuery query;
        readonly WorldBuildPlan plan;
        readonly WorldHitRouting routing;
        public ContentIdentity Identity { get { return world.Identity; } }
        public RuntimeBinding(ApprovedWorld world, IWorldApproval approval, IWorldBuildPolicy policy,
            IRuntimeAttestation attestation, Func<int,string> layers, IWorldExternalHits nativeHits)
        {
            if(world==null || attestation==null)throw new ArgumentNullException("world/attestation");
            this.world=world;this.approval=approval;this.attestation=attestation;
            Validate();plan=world.PrepareBuild(approval,policy,layers);routing=new WorldHitRouting(plan);
            query=new ApprovedWorldQuery(world,approval,policy,layers,nativeHits);Validate();
        }
        public void Validate()
        {
            world.Revalidate(approval);
            if(!attestation.Matches(world.Identity))throw new InvalidOperationException("Immutable dependency attestation missing/revoked");
            world.Revalidate(approval);
        }
        public bool HasLineOfSight(string actorId,string owner,Float3 origin,Float3 target,float range)
        {Validate();bool result=query.HasLineOfSight(actorId,owner,origin,target,range);Validate();return result;}
        public bool RouteNative(string actor,string owner,float range,WorldQueryHit[] rows)
        {Validate();bool result=routing.CanInteract(actor,owner,range,rows);Validate();return result;}
        public bool Trigger(string bodyId,string owner,Float3 point)
        {Validate();bool result=query.TriggerContains(bodyId,owner,point);Validate();return result;}
        public LoadedSpawn[] SafeSpawns(string mode,string team,float height,ISpawnQuery native,IWorldBuildPolicy bounds)
        {Validate();var result=query.SafeSpawns(mode,team,height,native,bounds);Validate();return result;}
        public WorldBuildPlan PreparePlan(IWorldBuildPolicy policy,Func<int,string> layers)
        {Validate();var result=world.PrepareBuild(approval,policy,layers);Validate();return result;}
        public SemanticRecord ResolveBody(string bodyId) {Validate();return plan.Registry().Resolve(bodyId);}
    }
}
