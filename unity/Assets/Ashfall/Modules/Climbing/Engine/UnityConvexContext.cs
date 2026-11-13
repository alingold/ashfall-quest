#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ashfall.Modules.Climbing.Engine
{
    public struct ConvexBodyIdentity
    {
        public string BodyId,OwnerId,Approval;
        public bool ArchiveGeometry,AuthoredClimbable;
    }
    public interface IUnityConvexApproval
    {
        // Coordinator validates the exact hull, binding revision and filtering receipt.
        RuleResult ValidateSourceHull(long queryRevision);
    }
    // Coordinator explicitly supplies a PRIVATE local-physics query scene, a single owned
    // source-hull proxy and source->proxy identity/geometry binding. Never the live actor scene.
    // Validate must census every enabled collider, reject unbound proxies and check source
    // geometry/transforms, actor hierarchy exclusion, masks and approval against Revision.
    public interface IUnityConvexContext
    {
        Scene QueryScene {get;}
        Rigidbody HeadProxy {get;}
        MeshCollider HeadCollider {get;}
        Transform ActorRoot {get;}
        long Revision {get;}
        bool Approved {get;}
        bool Validate();
        bool Resolve(Collider proxy,out ConvexBodyIdentity identity);
    }
}
#endif
