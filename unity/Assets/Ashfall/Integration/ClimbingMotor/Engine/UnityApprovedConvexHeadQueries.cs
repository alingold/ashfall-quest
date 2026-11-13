#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Climbing.Engine;
using Ashfall.Modules.Player;
namespace Ashfall.Integration.ClimbingMotor.Engine
{
    // Read-only adapter for the real Player R5 API. Approval belongs to the supplied
    // immutable context, never to this candidate or merely a successfully created mesh.
    public sealed class UnityApprovedConvexHeadQueries:IUnityApprovedHeadQueries
    {
        readonly UnityConvexClimbScene scene;readonly ConvexClimbCollision collision;
        readonly ISourceHullDistance distance;readonly IConvexHeadSource poseSource;
        public UnityApprovedConvexHeadQueries(UnityConvexClimbScene scene,ConvexClimbCollision collision)
        {if(scene==null||collision==null)throw new System.ArgumentNullException("convex queries");this.scene=scene;this.collision=collision;}
        public UnityApprovedConvexHeadQueries(UnityConvexClimbScene scene,ConvexClimbCollision collision,ISourceHullDistance distance,IConvexHeadSource poseSource):this(scene,collision)
        {this.distance=distance;this.poseSource=poseSource;}
        public bool Approved {get{return scene.Approved;}} public long Revision {get{return scene.Revision;}}
        public RuleResult Sweep(Float3 head,Float3 delta)
        {return Approved&&collision.HeadMotionClear(head,delta)?RuleResult.Accept():RuleResult.Reject("convex head sweep unavailable/blocked");}
        public RuleResult HeightSupportObstruction(Float3 root,Float3 head)
        {
            return RuleResult.Reject("committed previous-to-proposed height/recenter transition required");
        }
        // New explicit transition port. The legacy Player signature above cannot
        // convey a committed previous sample and does not certify recalibration.
        public RuleResult HeightSupportObstruction(HeightRecenterTransition transition,IClimbSupport support)
        {
            if(!Approved||distance==null||distance.Revision!=Revision||support==null)return RuleResult.Reject("height transition binding");
            ConvexHeadPose actual;
            if(poseSource==null||!poseSource.TryRead(out actual)||!Same(actual,transition.ColliderPose))return RuleResult.Reject("height collider pose binding");
            long revision=Revision;
            var result=new HeightRecenterPolicy(new ContinuousHeadClearance(distance),support).Validate(transition);
            return Approved&&Revision==revision?result:RuleResult.Reject("height geometry changed");
        }
        public RuleResult MoveThenYawArc(RootMotionProposal p)
        {
            ConvexHeadPose pose;
            if(!Approved||distance==null||poseSource==null||distance.Revision!=Revision||!poseSource.TryRead(out pose)||
                pose.WorldOffset.X!=0||pose.WorldOffset.Y!=0||pose.WorldOffset.Z!=0)
                return RuleResult.Reject("continuous source-hull binding required");
            long revision=Revision;
            try
            {
                var clearances=new ContinuousHeadClearance(distance);
                var moved=Add(p.HeadPosition,p.Translation);
                var pivoted=Add(moved,p.PivotCorrection);
                var root=Add(Add(p.RootPosition,p.Translation),p.PivotCorrection);
                var orbit=new Float3(pivoted.X-root.X,0,pivoted.Z-root.Z);
                // Match Player's physical Move, correction Move, then root.Rotate.
                // The correction's intermediate head positions also need clearance.
                bool clear=clearances.Clear(new SourceHeadTrajectory(p.HeadPosition,pose,p.Translation,0),.0025)&&
                    clearances.Clear(new SourceHeadTrajectory(moved,pose,p.PivotCorrection,p.YawRadians,orbit),.0025);
                return clear&&Approved&&Revision==revision?RuleResult.Accept():RuleResult.Reject("continuous hull obstructed/unavailable");
            }
            catch(System.ArgumentException){return RuleResult.Reject("continuous hull envelope");}
        }
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        static bool Same(ConvexHeadPose a,ConvexHeadPose b)
        {return a.Qx==b.Qx&&a.Qy==b.Qy&&a.Qz==b.Qz&&a.Qw==b.Qw&&a.WorldOffset.X==b.WorldOffset.X&&a.WorldOffset.Y==b.WorldOffset.Y&&a.WorldOffset.Z==b.WorldOffset.Z;}
    }
}
#endif
