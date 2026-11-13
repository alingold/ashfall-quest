#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ashfall.Modules.Player
{
    // Head provider must bind approved source-convex geometry; absent API remains blocked.
    public interface IUnityApprovedHeadQueries
    {
        bool Approved {get;} long Revision {get;}
        RuleResult Sweep(Float3 head,Float3 delta);
        RuleResult HeightSupportObstruction(Float3 root,Float3 head);
        RuleResult MoveThenYawArc(RootMotionProposal proposal);
    }
    public sealed class UnityPlayerGeometry:IApprovedPlayerGeometry
    {
        readonly CharacterController body;readonly IUnityApprovedHeadQueries head;readonly int mask;
        readonly RaycastHit[] hits=new RaycastHit[128];readonly Collider[] overlaps=new Collider[128];
        public UnityPlayerGeometry(CharacterController body,IUnityApprovedHeadQueries head,int approvedMask)
        {if(body==null||head==null||(approvedMask&(1<<8))==0||(approvedMask&~((1<<8)|(1<<9)))!=0||LayerMask.LayerToName(8)!="WorldStructure"||LayerMask.LayerToName(10)!="PlayerActor")throw new ArgumentException("Central layer/shape binding required");this.body=body;this.head=head;mask=approvedMask;}
        public long Revision {get {return head.Revision;} }
        public bool Approved {get {
            if(!head.Approved||body==null||!body.enabled||body.gameObject.scene.GetPhysicsScene()!=Physics.defaultPhysicsScene)return false;
            foreach(var collider in body.GetComponentsInChildren<Collider>(true))if(collider!=body)return false;
            return true;
        } }
        bool Excluded(Collider c) {return c==body;}
        public RuleResult RootSweep(Float3 root,Float3 delta)
        {
            if(!Approved||!root.IsFinite||!delta.IsFinite)return RuleResult.Reject("Root query unavailable");
            Vector3 center=U(root)+body.transform.TransformVector(body.center);
            float radius=body.radius,extent=body.height*.5f-radius;
            Vector3 a=center+Vector3.up*extent,b=center-Vector3.up*extent;
            int count=Physics.OverlapCapsuleNonAlloc(a,b,radius,overlaps,mask,QueryTriggerInteraction.Ignore);
            if(count>=overlaps.Length)return RuleResult.Reject("Root overlap overflow");
            for(int i=0;i<count;i++)if(!Excluded(overlaps[i]))
            {
                var c=overlaps[i];Vector3 normal;float depth;
                // Unity guarantees ComputePenetration only when one member is a
                // primitive or convex mesh. CharacterController is not that guarantee.
                // Unsupported mesh/controller pairs remain blocked, never 'clear'.
                var mesh=c as MeshCollider;
                if(!(c is BoxCollider)&&!(c is SphereCollider)&&!(c is CapsuleCollider)&&(mesh==null||!mesh.convex))
                    return RuleResult.Reject("Unsupported root penetration pair requires approved narrowphase");
                if(c==null||Physics.ComputePenetration(body,U(root),body.transform.rotation,c,c.transform.position,c.transform.rotation,out normal,out depth)&&depth>0)
                    return RuleResult.Reject("Root penetration");
            }
            Vector3 motion=U(delta);if(motion.sqrMagnitude==0)return RuleResult.Accept();
            count=Physics.CapsuleCastNonAlloc(a,b,radius,motion.normalized,hits,motion.magnitude,mask,QueryTriggerInteraction.Ignore);
            if(count>=hits.Length)return RuleResult.Reject("Root cast overflow");
            for(int i=0;i<count;i++)if(!Excluded(hits[i].collider))
            {
                var hit=hits[i];
                // Controller.Move resolves an immediate floor contact, including its
                // downward grounding velocity. Never ignore later floors or side hits.
                if(hit.collider!=null&&hit.distance<=body.skinWidth+.000001f&&hit.normal.y>=Mathf.Cos(body.slopeLimit*Mathf.Deg2Rad)&&
                    hit.normal.y>=.70710678f&&motion.y<=0&&
                    Vector3.Dot(new Vector3(motion.x,0,motion.z),hit.normal)>=0)continue;
                return RuleResult.Reject("Root path blocked");
            }
            return RuleResult.Accept();
        }
        public RuleResult HeadSweep(Float3 origin,Float3 delta) {return head.Sweep(origin,delta);}
        public RuleResult HeightSupportObstruction(Float3 root,Float3 eye) {return head.HeightSupportObstruction(root,eye);}
        public RuleResult PivotClearance(RootMotionProposal p) {return head.MoveThenYawArc(p);}
        static Vector3 U(Float3 p) {return new Vector3(p.X,p.Y,p.Z);}
    }
}
#endif
