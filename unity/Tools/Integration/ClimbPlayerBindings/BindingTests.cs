using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Climbing;
using Ashfall.Integration.ClimbingMotor;
using Ashfall.Integration.ClimbPlayerBindings;
class BindingTests
{
    static int checks;static void Check(bool b,string n){checks++;if(!b)throw new Exception(n);}
    sealed class Physical:IClimbPhysicalPose
    {
        public object Identity=new object();public Float3 Root,Head=new Float3(0,1,0);public ConvexHeadPose Pose=new ConvexHeadPose{Qw=1};
        public object SourceIdentity{get{return Identity;}}
        public bool TryRead(out Float3 r,out Float3 h,out ConvexHeadPose p){r=Root;h=Head;p=Pose;return true;}
    }
    // Distance from the entire published convex hull to an axis halfspace.
    sealed class Distance:ISourceHullDistance
    {
        public bool Live=true,Mutate;public long Rev=1;public int Axis;public double Wall=10;
        public bool Approved{get{return Live;}}public long Revision{get{return Rev;}}
        public bool TryLowerBound(SourceHeadTrajectory t,double phase,out double d)
        {double max=-double.MaxValue;for(int i=0;i<t.VertexCount;i++){var p=t.Vertex(i,phase);max=Math.Max(max,Axis==0?p.X:Axis==1?p.Y:p.Z);}d=Math.Max(0,Wall-max);if(Mutate)Rev++;return Live;}
    }
    sealed class Support:IClimbSupport
    {public bool Live=true;public Float3 Correction;public bool TryComplete(Float3 r,Float3 h,Float3 p,Float3 v,float s,out Float3 c){c=Correction;return Live;}}
    sealed class Geometry:IApprovedPlayerGeometry
    {
        public readonly CommittedHeadQueries Q;public Geometry(CommittedHeadQueries q){Q=q;}
        public long Revision{get{return Q.Revision;}}public bool Approved{get{return Q.Approved;}}
        public RuleResult HeadSweep(Float3 h,Float3 d){return Q.Sweep(h,d);}
        public RuleResult RootSweep(Float3 r,Float3 d){return RuleResult.Accept();}
        public RuleResult HeightSupportObstruction(Float3 r,Float3 h){return Q.Height(r,h,.02f,HeightChangeKind.Tracking,false);}
        public RuleResult PivotClearance(RootMotionProposal p){return Q.MoveThenYawArc(p);}
    }
    sealed class Motor:IExclusivePlayerMotor
    {
        public Physical P;public int Calls;
        public PlayerMotorResult Apply(RootMotionProposal p,float seconds)
        {Calls++;P.Root=new Float3(P.Root.X+p.Translation.X+p.PivotCorrection.X,P.Root.Y+p.Translation.Y,P.Root.Z+p.Translation.Z+p.PivotCorrection.Z);
         P.Head=new Float3(P.Head.X+p.Translation.X,P.Head.Y+p.Translation.Y,P.Head.Z+p.Translation.Z);
         return PlayerMotorFeedback.ResolvePhases(p,p.Translation,p.PivotCorrection,seconds,false,false);}
    }
    static int Main()
    {
        var p=new Physical();var d=new Distance();var s=new Support();s.Correction=new Float3();
        var q=new CommittedHeadQueries(p,d,s);var g=new PlayerGeometryPreflight(new Geometry(q));
        var native=new Motor{P=p};var motor=new CommittedClimbMotor(q,native,g);
        p.Head=new Float3(.35f,1.2f,.25f);p.Pose.WorldOffset=new Float3(.1f,0,.05f);
        Check(!q.Height(p.Root,p.Head,.02f,HeightChangeKind.Tracking,false).Accepted,"changed collider pose requires rebind");
        q=new CommittedHeadQueries(p,d,s);g=new PlayerGeometryPreflight(new Geometry(q));motor=new CommittedClimbMotor(q,native,g);
        var plan=RootMotionSafety.Plan(p.Root,p.Head,new Float3(.1f,0,0),.7f,true);
        Check(g.Validate(plan).Accepted,"actual Player preflight height plus angular offset path");
        var result=motor.Apply(plan,.02f);
        Check(result.Complete&&result.HasPhaseFeedback&&native.Calls==1,"one motor and exact physical phase");
        Check(Math.Abs(result.Velocity.X-5)<.0001,"pivot excluded from velocity");
        p.Head=new Float3(p.Head.X,1.5f,p.Head.Z);
        d.Axis=1;d.Wall=1.45;
        var zero=RootMotionSafety.Plan(p.Root,p.Head,new Float3(),0,true);
        Check(!g.Validate(zero).Accepted,"observed height swept into ceiling");
        Check(!motor.Apply(zero,.02f).Complete&&native.Calls==1,"denial preserves root calls and baseline");
        d.Wall=10;
        Check(motor.Apply(zero,.02f).Complete&&native.Calls==2,"same proposal retry after geometry available");
        Check(q.Height(p.Root,p.Head,.02f,HeightChangeKind.Calibration,true).Accepted,"actual observed calibration supports unchanged root");
        s.Correction=new Float3(0,.01f,0);
        Check(!q.Height(p.Root,p.Head,.02f,HeightChangeKind.Calibration,true).Accepted,"moving support requires swept correction");
        s.Correction=new Float3();s.Live=false;
        Check(!q.Height(p.Root,p.Head,.02f,HeightChangeKind.Calibration,true).Accepted,"lost support refuses");s.Live=true;
        d.Mutate=true;Check(!g.Validate(zero).Accepted,"distance revision drift");d.Mutate=false;
        Check(g.Validate(zero).Accepted,"new geometry revision retry");
        p.Identity=new object();Check(!q.Approved&&!g.Validate(zero).Accepted,"physical lifetime rotation terminal");
        q.Invalidate();Check(!q.Approved,"explicit invalidation terminal");
        // Committed previous is retained internally, so a fabricated proposed head
        // cannot replace it and a recenter cannot change the world eye.
        q=new CommittedHeadQueries(p,d,s);
        var beforeRoot=p.Root;var beforeHead=p.Head;
        Check(q.ValidateProposedHeight(new Float3(p.Head.X,0,p.Head.Z),p.Head,.02f,HeightChangeKind.RootRecenter,true).Accepted,"before-mutation recenter query from committed source");
        Check(q.ValidateProposedHeight(p.Root,new Float3(p.Head.X,1.7f,p.Head.Z),.02f,HeightChangeKind.Calibration,true).Accepted,"before-mutation calibration query");
        Check(p.Root.X==beforeRoot.X&&p.Root.Y==beforeRoot.Y&&p.Root.Z==beforeRoot.Z&&p.Head.X==beforeHead.X&&p.Head.Y==beforeHead.Y&&p.Head.Z==beforeHead.Z,"preflight does not write physical pose");
        Check(!q.Height(new Float3(9,0,0),p.Head,.02f,HeightChangeKind.RootRecenter,false).Accepted,"fabricated root rejected");
        p.Root=new Float3(p.Head.X,0,p.Head.Z);
        Check(q.Height(p.Root,p.Head,.02f,HeightChangeKind.RootRecenter,true).Accepted,"observed source recenter preserves eye");
        p.Head=new Float3(p.Head.X+.1f,p.Head.Y,p.Head.Z);
        Check(!q.Height(p.Root,p.Head,.02f,HeightChangeKind.RootRecenter,true).Accepted,"recenter moved eye refused");
        // A foreign root writer cannot turn an observed recenter query into motor
        // authority. Recreate under the host's final handoff contract instead.
        var foreign=RootMotionSafety.Plan(p.Root,p.Head,new Float3(),0,true);
        Check(!q.MatchesLive(foreign),"external root movement needs new host binding");
        d.Live=false;Check(!q.Sweep(p.Head,new Float3()).Accepted,"revoked geometry");
        p=new Physical{Head=new Float3(0,1,1),Pose=new ConvexHeadPose{Qw=1,WorldOffset=new Float3(.08f,0,.03f)}};
        d=new Distance{Axis=0,Wall=.75};q=new CommittedHeadQueries(p,d,s);
        var angular=RootMotionSafety.Plan(p.Root,p.Head,new Float3(),(float)Math.PI,true);
        Check(!q.MoveThenYawArc(angular).Accepted,"offset angular interior blocked despite clear endpoints");
        d.Wall=10;Check(q.MoveThenYawArc(angular).Accepted,"angular query same proposal retry");
        g=new PlayerGeometryPreflight(new Geometry(q));native=new Motor{P=p};motor=new CommittedClimbMotor(q,native,g);
        Check(!motor.Apply(angular,0).Complete&&native.Calls==0&&q.Approved,"bad time rejects without spending binding");
        Console.WriteLine("PASS "+checks+" R8 binding checks; synthetic host ports, no native execution");return 0;
    }
}
