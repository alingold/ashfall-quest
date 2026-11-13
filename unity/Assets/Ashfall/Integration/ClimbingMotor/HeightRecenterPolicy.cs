using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
namespace Ashfall.Integration.ClimbingMotor
{
    public enum HeightChangeKind { Tracking,Calibration,RootRecenter }
    public struct HeightRecenterTransition
    {
        public Float3 PreviousRoot,PreviousHead,ProposedRoot,ProposedHead;
        public ConvexHeadPose ColliderPose;
        public Float3 Velocity;public float Seconds;
        public bool RequireSupport;
        public HeightChangeKind Kind;
    }
    // Stateless validation: previous must come from the committed host pose, never
    // a caller-selected last sample. Commit/reject remains with the root owner.
    public sealed class HeightRecenterPolicy
    {
        readonly ContinuousHeadClearance clearance;readonly IClimbSupport support;
        public HeightRecenterPolicy(ContinuousHeadClearance clearance,IClimbSupport support)
        {if(clearance==null||support==null)throw new ArgumentNullException("height policy");this.clearance=clearance;this.support=support;}
        public RuleResult Validate(HeightRecenterTransition t)
        {
            if(!Height(t.PreviousRoot,t.PreviousHead)||!Height(t.ProposedRoot,t.ProposedHead)||!t.Velocity.IsFinite||
                Single.IsNaN(t.Seconds)||t.Seconds<=0||t.Seconds>.1f)return RuleResult.Reject("height transition bounds");
            if(!Enum.IsDefined(typeof(HeightChangeKind),t.Kind))return RuleResult.Reject("height transition kind");
            if(t.Kind==HeightChangeKind.Calibration)
            {
                double target=(double)t.ProposedHead.Y-t.ProposedRoot.Y;
                if(target<(double).55f||target>(double)2.2f||!Same(t.PreviousRoot,t.ProposedRoot)||
                    t.PreviousHead.X!=t.ProposedHead.X||t.PreviousHead.Z!=t.ProposedHead.Z)
                    return RuleResult.Reject("calibration must be vertical origin lift at fixed root");
            }
            if(t.Kind==HeightChangeKind.RootRecenter&&(!Same(t.PreviousHead,t.ProposedHead)||t.PreviousRoot.Y!=t.ProposedRoot.Y||
                t.ProposedRoot.X!=t.PreviousHead.X||t.ProposedRoot.Z!=t.PreviousHead.Z))
                return RuleResult.Reject("source recenter preserves world eye and root elevation");
            var delta=Sub(t.ProposedHead,t.PreviousHead);
            try{if(!clearance.Clear(new SourceHeadTrajectory(t.PreviousHead,t.ColliderPose,delta,0),.0025))return RuleResult.Reject("height/recenter hull obstructed or unavailable");}
            catch(ArgumentException){return RuleResult.Reject("height trajectory envelope");}
            if(t.RequireSupport)
            {
                Float3 completion;
                if(!support.TryComplete(t.ProposedRoot,t.ProposedHead,new Float3(),t.Velocity,t.Seconds,out completion)||
                    !completion.IsFinite||Math.Abs(completion.X)>1e-6||Math.Abs(completion.Z)>1e-6||Math.Abs(completion.Y)>.0001)
                    return RuleResult.Reject("proposed support requires separately swept root correction");
            }
            return RuleResult.Accept();
        }
        static bool Height(Float3 root,Float3 head)
        {double h=(double)head.Y-root.Y;return root.IsFinite&&head.IsFinite&&Math.Abs(root.X)<=100000&&Math.Abs(root.Y)<=100000&&Math.Abs(root.Z)<=100000&&h>=.1&&h<=4;}
        static bool Same(Float3 a,Float3 b){return a.X==b.X&&a.Y==b.Y&&a.Z==b.Z;}
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
    }
}
