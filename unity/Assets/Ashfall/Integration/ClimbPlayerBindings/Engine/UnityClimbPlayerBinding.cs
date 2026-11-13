#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Player;
using Ashfall.Integration.ClimbingMotor;
using UnityEngine;
namespace Ashfall.Integration.ClimbPlayerBindings.Engine
{
    // Explicit factory, no Update or auto-activation. Root chooses EITHER the
    // Driver loop OR ClimbingRootMotor with Motor; never both schedulers.
    public sealed class UnityClimbPlayerBinding:IUnityApprovedHeadQueries,IClimbPhysicalPose,IDisposable
    {
        readonly UnityPlayerDriver driver;readonly IConvexHeadSource collider;
        readonly CommittedHeadQueries queries;readonly object identity;
        readonly ClimbController climb;
        bool disposed;
        public readonly IExclusivePlayerMotor Motor;
        public readonly IRootCollisionPreflight Preflight;
        public UnityClimbPlayerBinding(UnityPlayerDriver driver,IConvexHeadSource collider,
            ISourceHullDistance distance,IClimbSupport support,int mask,ClimbController climb=null)
        {
            if(driver==null||driver.Head==null||collider==null)throw new ArgumentException("bound physical driver");
            this.driver=driver;this.collider=collider;this.climb=climb;identity=driver.PoseSourceIdentity;
            queries=new CommittedHeadQueries(this,distance,support);
            Preflight=new PlayerGeometryPreflight(new UnityPlayerGeometry(driver.GetComponent<CharacterController>(),this,mask));
            Motor=new CommittedClimbMotor(queries,new UnityExclusivePlayerMotor(driver.GetComponent<CharacterController>(),Preflight),Preflight);
            driver.PoseSourceInvalidated+=Invalidated;
        }
        public object SourceIdentity {get{return disposed||driver==null?null:driver.PoseSourceIdentity;}}
        public bool TryRead(out Float3 root,out Float3 head,out ConvexHeadPose pose)
        {
            root=head=new Float3();pose=new ConvexHeadPose();
            if(disposed||driver==null||driver.Head==null||!driver.isActiveAndEnabled||
                !System.Object.ReferenceEquals(identity,driver.PoseSourceIdentity)||
                (driver.UseXr&&(driver.XrLifecycle==null||!driver.XrLifecycle.Present||!driver.XrLifecycle.SessionFocused)))return false;
            root=F(driver.transform.position);head=F(driver.Head.position);return collider.TryRead(out pose);
        }
        public bool Approved {get{return !disposed&&queries.Approved;}}
        public long Revision {get{return queries.Revision;}}
        public RuleResult Sweep(Float3 head,Float3 delta){return queries.Sweep(head,delta);}
        // Uses committed previous and actual physical observations. Action pause is
        // deliberately absent; it is not evidence that physical tracking was lost.
        public RuleResult HeightSupportObstruction(Float3 root,Float3 head)
        {return queries.Height(root,head,.02f,HeightChangeKind.Tracking,false);}
        public RuleResult MoveThenYawArc(RootMotionProposal p){return queries.MoveThenYawArc(p);}
        public RuleResult ValidateObservedHeight(HeightChangeKind kind,float seconds,bool requireSupport)
        {Float3 r,h;ConvexHeadPose p;return TryRead(out r,out h,out p)?queries.Height(r,h,seconds,kind,requireSupport):RuleResult.Reject("physical height source unavailable");}
        public RuleResult ValidateProposedHeight(Float3 root,Float3 head,HeightChangeKind kind,float seconds,bool requireSupport)
        {return queries.ValidateProposedHeight(root,head,seconds,kind,requireSupport);}
        public void InstallOnDriver()
        {if(!Approved)throw new InvalidOperationException("binding invalidated");driver.CollisionPreflight=Preflight;driver.Motor=Motor;}
        void Invalidated()
        {queries.Invalidate();if(climb!=null&&driver!=null&&System.Object.ReferenceEquals(driver.Motor,Motor))climb.Reset();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;Invalidated();
            if(driver!=null){driver.PoseSourceInvalidated-=Invalidated;
                if(System.Object.ReferenceEquals(driver.Motor,Motor))driver.Motor=null;
                if(System.Object.ReferenceEquals(driver.CollisionPreflight,Preflight))driver.CollisionPreflight=null;}
        }
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}
    }
}
#endif
