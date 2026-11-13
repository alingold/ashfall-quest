#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using Ashfall.Modules.World;
using UnityEngine;

namespace Ashfall.Integration.WorldRuntime
{
    public enum WorldInvalidation { RootRetired, ApprovalRevoked, ReceiptRevoked, SourceDisabled, SourceDestroyed, Replaced }

    // A registration binds real provider objects to a concrete scene/component incarnation.
    // It does not mint content approval: ApprovedWorld and the exact receipt still revalidate.
    public sealed class WorldSourceRegistration
    {
        internal readonly WorldSourceLifetime Source;
        internal readonly ApprovedWorld World;
        internal readonly IWorldApproval Approval;
        internal readonly IWorldBuildPolicy Policy;
        internal readonly IRuntimeAttestation Receipt;
        internal readonly IWorldMaterialResolver Materials;
        internal readonly INativeActorIdentity Actors;
        internal readonly INativeHazards Hazards;
        internal event Action<WorldSourceRegistration,WorldInvalidation> Invalidated;
        bool retired;
        internal WorldSourceRegistration(WorldSourceLifetime source,ApprovedWorld world,IWorldApproval approval,
            IWorldBuildPolicy policy,IRuntimeAttestation receipt,IWorldMaterialResolver materials,INativeActorIdentity actors,INativeHazards hazards)
        {Source=source;World=world;Approval=approval;Policy=policy;Receipt=receipt;Materials=materials;Actors=actors;Hazards=hazards;}
        internal void ValidateLifetimeOnly()
        {
            Source.RequireLane();
            if(retired||!Source||!Source.isActiveAndEnabled||!Source.IsCurrent(this))throw new InvalidOperationException("Retired concrete world source");
        }
        internal void Validate()
        {
            Source.RequireLane();
            if(retired||!Source||!Source.isActiveAndEnabled||!Source.IsCurrent(this))throw new InvalidOperationException("Retired concrete world source");
            World.Revalidate(Approval);
            if(!Receipt.Matches(World.Identity))throw new InvalidOperationException("Registered receipt revoked");
            World.Revalidate(Approval);
            if(retired||!Source.isActiveAndEnabled||!Source.IsCurrent(this))throw new InvalidOperationException("Source changed during validation");
        }
        internal void Retire(WorldInvalidation reason)
        {
            if(retired)return;retired=true;
            var handlers=Invalidated;Invalidated=null;
            if(handlers!=null)foreach(Action<WorldSourceRegistration,WorldInvalidation> handler in handlers.GetInvocationList())
                try{handler(this,reason);}catch(Exception e){Debug.LogException(e);}
        }
        public void RetireRoot(){Source.Revoke(this,WorldInvalidation.RootRetired);}
        public void RevokeApproval(IWorldApproval exact)
        {if(!ReferenceEquals(exact,Approval))throw new ArgumentException("Exact registered approval required");Source.Revoke(this,WorldInvalidation.ApprovalRevoked);}
        public void RevokeReceipt(IRuntimeAttestation exact)
        {if(!ReferenceEquals(exact,Receipt))throw new ArgumentException("Exact registered receipt required");Source.Revoke(this,WorldInvalidation.ReceiptRevoked);}
    }

    // Attach to the external host, never to NativeWorld.Root (whose component inventory is attested).
    public sealed class WorldSourceLifetime : MonoBehaviour
    {
        int lane;
        WorldSourceRegistration current;
        void Awake(){lane=Thread.CurrentThread.ManagedThreadId;}
        internal void RequireLane()
        {if(lane==0||Thread.CurrentThread.ManagedThreadId!=lane)throw new InvalidOperationException("World source requires its Unity host lane");}
        internal bool IsCurrent(WorldSourceRegistration registration){return ReferenceEquals(current,registration);}
        public WorldSourceRegistration Register(ApprovedWorld world,IWorldApproval approval,IWorldBuildPolicy policy,
            IRuntimeAttestation receipt,IWorldMaterialResolver materials,INativeActorIdentity actors,INativeHazards hazards)
        {
            RequireLane();
            if(!isActiveAndEnabled||world==null||approval==null||policy==null||receipt==null||materials==null||actors==null||hazards==null)
                throw new InvalidOperationException("Complete active concrete source required");
            world.Revalidate(approval);
            if(!receipt.Matches(world.Identity))throw new InvalidOperationException("Registered dependency receipt missing");
            world.Revalidate(approval);
            var next=new WorldSourceRegistration(this,world,approval,policy,receipt,materials,actors,hazards);
            var old=current;current=next;if(old!=null)old.Retire(WorldInvalidation.Replaced);
            return next;
        }
        internal void Revoke(WorldSourceRegistration registration,WorldInvalidation reason)
        {RequireLane();if(ReferenceEquals(current,registration))current=null;registration.Retire(reason);}
        void Retire(WorldInvalidation reason){var old=current;current=null;if(old!=null)old.Retire(reason);}
        void OnDisable(){Retire(WorldInvalidation.SourceDisabled);}
        void OnDestroy(){Retire(WorldInvalidation.SourceDestroyed);}
    }
}
#endif
