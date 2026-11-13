#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ashfall.Modules.Climbing.Engine
{
    // Explicit invocation only. No initializer, scene creation, simulation or global Physics writes.
    public sealed class UnityConvexClimbScene:IConvexClimbScene
    {
        readonly IUnityConvexContext context;
        readonly Collider[] overlaps=new Collider[256];
        readonly RaycastHit[] rays=new RaycastHit[256];
        public UnityConvexClimbScene(IUnityConvexContext context)
        { if(context==null)throw new ArgumentNullException("context");this.context=context; }
        public long Revision {get {return context.Validate()?context.Revision:-1;} }
        public bool Approved {get {return context.Approved&&context.Validate();} }
        static Vector3 U(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        bool Ready(int mask)
        {return (mask&(1<<8))!=0&&(mask&~((1<<8)|(1<<9)))==0&&context.Validate();}
        bool Pose(ConvexHeadQuery q)
        {
            if(q.Shape!=SourceHeadConvex.Instance||!SourceHeadConvex.ValidPose(q.Pose)||Math.Abs(q.Pose.Qx)>.000001f||Math.Abs(q.Pose.Qz)>.000001f||!q.Origin.IsFinite||!q.Motion.IsFinite||!Ready(q.Mask))return false;
            context.HeadProxy.position=U(q.Origin);
            context.HeadProxy.rotation=new Quaternion(q.Pose.Qx,q.Pose.Qy,q.Pose.Qz,q.Pose.Qw);
            return true;
        }
        bool Identity(Collider c,int mask,out ConvexBodyIdentity id)
        {
            id=new ConvexBodyIdentity();
            return c!=null&&c!=context.HeadCollider&&!c.isTrigger&&(mask&(1<<c.gameObject.layer))!=0&&
                context.Resolve(c,out id)&&!String.IsNullOrEmpty(id.BodyId)&&!String.IsNullOrEmpty(id.OwnerId)&&!String.IsNullOrEmpty(id.Approval);
        }
        public bool Penetrating(ConvexHeadQuery q,out bool penetrating)
        {
            penetrating=true;if(!Pose(q))return false;
            // Sphere is broadphase ONLY; narrowphase uses the exact convex MeshCollider.
            int n=context.QueryScene.GetPhysicsScene().OverlapSphere(U(q.Origin),.121f,overlaps,q.Mask,QueryTriggerInteraction.Ignore);
            if(n>=overlaps.Length)return false;
            for(int i=0;i<n;i++)
            {
                var c=overlaps[i];if(c==context.HeadCollider)continue;ConvexBodyIdentity id;
                if(!Identity(c,q.Mask,out id))return false;
                Vector3 direction;float distance;
                if(Physics.ComputePenetration(context.HeadCollider,U(q.Origin),context.HeadProxy.rotation,c,c.transform.position,c.transform.rotation,out direction,out distance)&&distance>0)return true;
            }
            penetrating=false;return context.Validate();
        }
        public bool Cast(ConvexHeadQuery q,out bool hit,out ClimbQueryHit nearest)
        {
            hit=false;nearest=new ClimbQueryHit();if(!Pose(q))return false;
            float length=U(q.Motion).magnitude;if(length==0)return true;
            var hits=context.HeadProxy.SweepTestAll(U(q.Motion)/length,length,QueryTriggerInteraction.Ignore);
            if(hits.Length>=256)return false;
            foreach(var h in hits)
            {
                if(h.collider==context.HeadCollider)continue;ConvexBodyIdentity id;
                // SweepTestAll has no layer mask: ignore known bodies on unrequested layers,
                // but never ignore an unregistered collider in this private scene.
                if(!context.Resolve(h.collider,out id))return false;
                if((q.Mask&(1<<h.collider.gameObject.layer))==0)continue;
                if(!Identity(h.collider,q.Mask,out id)||h.distance<0||!F(h.normal).IsFinite||h.normal.sqrMagnitude<.99f)return false;
                // Only separating numerical floor contacts are recoverable; still inspect ALL hits.
                if(q.Recovery&&h.distance<=.000001f&&q.Motion.Y>0&&h.normal.y>=.70710678f&&Vector3.Dot(U(q.Motion),h.normal)>0)continue;
                if(!hit||h.distance<nearest.Distance){nearest=Hit(h,id);hit=true;}
            }
            return context.Validate();
        }
        static ClimbQueryHit Hit(RaycastHit h,ConvexBodyIdentity id)
        {return new ClimbQueryHit{Distance=h.distance,Surface=new SurfaceHit{Point=F(h.point),Normal=F(h.normal),ArchiveGeometry=id.ArchiveGeometry,AuthoredClimbable=id.AuthoredClimbable}};}
        public bool Ray(ClimbQuery q,out bool hit,out ClimbQueryHit nearest)
        {
            hit=false;nearest=new ClimbQueryHit();if(!Ready(q.Mask)||!q.Origin.IsFinite||!q.Motion.IsFinite)return false;
            float length=U(q.Motion).magnitude;if(length<=0)return false;
            int n=context.QueryScene.GetPhysicsScene().Raycast(U(q.Origin),U(q.Motion)/length,rays,length,q.Mask,QueryTriggerInteraction.Ignore);
            if(n>=rays.Length)return false;
            for(int i=0;i<n;i++){ConvexBodyIdentity id;if(!Identity(rays[i].collider,q.Mask,out id))return false;if(!hit||rays[i].distance<nearest.Distance){hit=true;nearest=Hit(rays[i],id);}}
            return context.Validate();
        }
        public bool Grab(ClimbQuery q,out bool hit,out ClimbQueryHit nearest)
        {
            hit=false;nearest=new ClimbQueryHit();if(!Ready(q.Mask)||!q.Origin.IsFinite||q.Radius<=0||q.Radius>.18f)return false;
            int n=context.QueryScene.GetPhysicsScene().OverlapSphere(U(q.Origin),q.Radius,overlaps,q.Mask,QueryTriggerInteraction.Ignore);
            if(n>=overlaps.Length)return false;
            for(int i=0;i<n;i++)
            {
                if(overlaps[i]==context.HeadCollider)continue;ConvexBodyIdentity id;if(!Identity(overlaps[i],q.Mask,out id))return false;
                if(id.ArchiveGeometry&&!id.AuthoredClimbable)continue;
                Vector3 p=overlaps[i].ClosestPoint(U(q.Origin)),d=U(q.Origin)-p;float length=d.magnitude;
                if(!hit||length<nearest.Distance){hit=true;nearest=new ClimbQueryHit{Distance=length,Surface=new SurfaceHit{Point=F(p),Normal=F(length>0?d/length:Vector3.up),ArchiveGeometry=id.ArchiveGeometry,AuthoredClimbable=id.AuthoredClimbable}};}
            }
            return context.Validate();
        }
    }
    public sealed class UnityRootYawHeadSource:IConvexHeadSource
    {
        readonly Transform root;readonly Float3 offset;
        public UnityRootYawHeadSource(Transform root,Float3 alreadyWorldOffset){this.root=root;offset=alreadyWorldOffset;}
        public bool TryRead(out ConvexHeadPose pose)
        {
            pose=new ConvexHeadPose();if(root==null||!RigidYaw(root.localToWorldMatrix))return false;
            Quaternion q=root.rotation;pose=new ConvexHeadPose{WorldOffset=offset,Qx=q.x,Qy=q.y,Qz=q.z,Qw=q.w};return SourceHeadConvex.ValidPose(pose);
        }
        public static bool RigidYaw(Matrix4x4 m)
        {
            Vector3 x=m.GetColumn(0),y=m.GetColumn(1),z=m.GetColumn(2);
            return F(x).IsFinite&&F(y).IsFinite&&F(z).IsFinite&&Mathf.Abs(x.sqrMagnitude-1)<.00001f&&
                Mathf.Abs(y.sqrMagnitude-1)<.00001f&&Mathf.Abs(z.sqrMagnitude-1)<.00001f&&
                Mathf.Abs(Vector3.Dot(x,y))<.00001f&&Mathf.Abs(Vector3.Dot(x,z))<.00001f&&Mathf.Abs(Vector3.Dot(y,z))<.00001f&&
                Vector3.Dot(Vector3.Cross(x,y),z)>.99999f&&(y-Vector3.up).sqrMagnitude<.00000001f;
        }
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
    }
}
#endif
