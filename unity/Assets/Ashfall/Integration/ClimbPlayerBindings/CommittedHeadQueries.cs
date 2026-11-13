using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Player;
using Ashfall.Integration.ClimbingMotor;
namespace Ashfall.Integration.ClimbPlayerBindings
{
    // Trusted host observation, not an input-frame pose or client attestation.
    public interface IClimbPhysicalPose
    {
        object SourceIdentity {get;}
        bool TryRead(out Float3 root,out Float3 head,out ConvexHeadPose collider);
    }
    // One binding per physical source lifetime. No camera, tracking-space or collider writes.
    public sealed class CommittedHeadQueries
    {
        readonly IClimbPhysicalPose source;readonly object identity;
        readonly ISourceHullDistance distance;readonly IClimbSupport support;
        Float3 committedRoot,committedHead;ConvexHeadPose committedCollider;
        bool revoked;
        public CommittedHeadQueries(IClimbPhysicalPose source,ISourceHullDistance distance,IClimbSupport support)
        {
            if(source==null||distance==null||support==null)throw new ArgumentNullException("climb binding");
            this.source=source;this.distance=distance;this.support=support;identity=source.SourceIdentity;
            if(identity==null||!source.TryRead(out committedRoot,out committedHead,out committedCollider))throw new ArgumentException("physical source unavailable");
        }
        public bool Approved {get{return !revoked&&Object.ReferenceEquals(identity,source.SourceIdentity)&&distance.Approved;}}
        public long Revision {get{return distance.Revision;}}
        public void Invalidate(){revoked=true;}
        bool Read(out Float3 root,out Float3 head,out ConvexHeadPose pose)
        {root=head=new Float3();pose=new ConvexHeadPose();return Approved&&source.TryRead(out root,out head,out pose)&&root.IsFinite&&head.IsFinite;}
        public RuleResult Height(Float3 root,Float3 head,float seconds,HeightChangeKind kind,bool requireSupport)
        {
            Float3 actualRoot,actualHead;ConvexHeadPose pose;
            if(!Read(out actualRoot,out actualHead,out pose)||!Same(root,actualRoot)||!Same(head,actualHead)||!Same(pose,committedCollider))
                return RuleResult.Reject("live/committed physical pose binding");
            long revision=Revision;
            var transition=new HeightRecenterTransition{PreviousRoot=committedRoot,PreviousHead=committedHead,
                ProposedRoot=root,ProposedHead=head,ColliderPose=pose,Seconds=seconds,Kind=kind,RequireSupport=requireSupport};
            var r=new HeightRecenterPolicy(new ContinuousHeadClearance(distance),support).Validate(transition);
            return Stable(revision)?r:RuleResult.Reject("height source changed");
        }
        public RuleResult Sweep(Float3 head,Float3 delta)
        {
            Float3 r,h;ConvexHeadPose pose;
            if(!Read(out r,out h,out pose))return RuleResult.Reject("physical pose unavailable");
            return Clear(head,pose,delta,0,new Float3());
        }
        // Query-only owner preflight, BEFORE a calibration/recenter writer changes
        // any transforms. It grants no publication authority or tracked-pose write.
        public RuleResult ValidateProposedHeight(Float3 root,Float3 head,float seconds,HeightChangeKind kind,bool requireSupport)
        {
            Float3 r,h;ConvexHeadPose pose;
            if(!Read(out r,out h,out pose)||!Same(r,committedRoot)||!Same(h,committedHead)||!Same(pose,committedCollider))
                return RuleResult.Reject("committed owner baseline required");
            long revision=Revision;
            var transition=new HeightRecenterTransition{PreviousRoot=committedRoot,PreviousHead=committedHead,
                ProposedRoot=root,ProposedHead=head,ColliderPose=pose,Seconds=seconds,Kind=kind,RequireSupport=requireSupport};
            var result=new HeightRecenterPolicy(new ContinuousHeadClearance(distance),support).Validate(transition);
            return Stable(revision)?result:RuleResult.Reject("height source changed");
        }
        public RuleResult MoveThenYawArc(RootMotionProposal p)
        {
            Float3 r,h;ConvexHeadPose pose;
            if(!Read(out r,out h,out pose)||!Same(r,p.RootPosition)||!Same(h,p.HeadPosition))return RuleResult.Reject("live motor proposal required");
            long revision=Revision;
            var result=Clear(p.HeadPosition,pose,p.Translation,0,new Float3());
            if(!result.Accepted)return result;
            var moved=Add(p.HeadPosition,p.Translation);
            // Offset is already world-space. Its center orbits with the physical
            // root rotation, so include it in the pivot radius exactly once.
            var orbit=new Float3(p.HeadPosition.X-p.RootPosition.X+pose.WorldOffset.X,
                0,p.HeadPosition.Z-p.RootPosition.Z+pose.WorldOffset.Z);
            result=Clear(moved,pose,p.PivotCorrection,p.YawRadians,orbit);
            return Stable(revision)?result:RuleResult.Reject("angular source changed");
        }
        RuleResult Clear(Float3 head,ConvexHeadPose pose,Float3 delta,float yaw,Float3 orbit)
        {
            long revision=Revision;
            try {
                bool clear=new ContinuousHeadClearance(distance).Clear(new SourceHeadTrajectory(head,pose,delta,yaw,orbit),.0025);
                return clear&&Stable(revision)?RuleResult.Accept():RuleResult.Reject("continuous hull obstructed/unavailable");
            } catch(ArgumentException){return RuleResult.Reject("source hull envelope");}
        }
        // Called only after the real physical motor returns. Partial results cannot
        // silently become a new baseline. Rejected preflight leaves the old baseline.
        public bool CommitApplied(RootMotionProposal proposal,PlayerMotorResult result)
        {
            Float3 root,head;ConvexHeadPose pose;
            if(!result.Complete||!result.HasPhaseFeedback||!result.AppliedPhysicalTranslation.IsFinite||!result.AppliedPivotCorrection.IsFinite||
                !Read(out root,out head,out pose)||
                !Near(root,Add(Add(proposal.RootPosition,result.AppliedPhysicalTranslation),result.AppliedPivotCorrection))||
                !Near(head,Add(proposal.HeadPosition,result.AppliedPhysicalTranslation))) {Invalidate();return false;}
            committedRoot=root;committedHead=head;committedCollider=pose;return true;
        }
        public bool MatchesLive(RootMotionProposal p)
        {Float3 r,h;ConvexHeadPose pose;return Read(out r,out h,out pose)&&Same(r,committedRoot)&&Same(r,p.RootPosition)&&Same(h,p.HeadPosition);}
        bool Stable(long revision){return Approved&&Revision==revision;}
        static bool Same(Float3 a,Float3 b){return a.X==b.X&&a.Y==b.Y&&a.Z==b.Z;}
        static bool Near(Float3 a,Float3 b){double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;return x*x+y*y+z*z<=1e-8;}
        static bool Same(ConvexHeadPose a,ConvexHeadPose b){return a.Qx==b.Qx&&a.Qy==b.Qy&&a.Qz==b.Qz&&a.Qw==b.Qw&&Same(a.WorldOffset,b.WorldOffset);}
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
    }
    public sealed class CommittedClimbMotor:IExclusivePlayerMotor
    {
        readonly CommittedHeadQueries queries;readonly IExclusivePlayerMotor motor;readonly IRootCollisionPreflight preflight;
        public CommittedClimbMotor(CommittedHeadQueries queries,IExclusivePlayerMotor motor,IRootCollisionPreflight preflight)
        {if(queries==null||motor==null||preflight==null)throw new ArgumentNullException("committed motor");this.queries=queries;this.motor=motor;this.preflight=preflight;}
        public PlayerMotorResult Apply(RootMotionProposal p,float seconds)
        {
            if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<=0||seconds>.1f||
                !queries.MatchesLive(p)||!RootMotionSafety.Approve(p,true,preflight).Accepted)return new PlayerMotorResult();
            try{var result=motor.Apply(p,seconds);if(!queries.CommitApplied(p,result))result.Complete=false;return result;}
            catch{queries.Invalidate();throw;}
        }
    }
}
