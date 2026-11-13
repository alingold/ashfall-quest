using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Player;
using Ashfall.Integration.ClimbingMotor;
public static class R7Tests
{
    static int count;
    static void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
    // Analytic halfspace, evaluating ALL published source-hull vertices. A real
    // distance lower bound, independent from trajectory travel implementation.
    sealed class Plane:ISourceHullDistance
    {
        public bool Approved {get{return true;}} public long Revision {get{return 1;}}
        public double X=2;
        public bool TryLowerBound(SourceHeadTrajectory t,double phase,out double metres)
        {double maximum=-1e100;for(int i=0;i<t.VertexCount;i++)maximum=Math.Max(maximum,t.Vertex(i,phase).X);metres=Math.Max(0,X-maximum);return true;}
    }
    sealed class Support:IClimbSupport
    {public bool TryComplete(Float3 r,Float3 h,Float3 p,Float3 v,float dt,out Float3 c){c=new Float3();return true;}}
    sealed class InvalidDistance:ISourceHullDistance
    {
        public int Mode;long revision=1;
        public bool Approved {get{return Mode!=0;}}public long Revision {get{return revision;}}
        public bool TryLowerBound(SourceHeadTrajectory t,double phase,out double metres)
        {metres=Mode==1?Double.NaN:Mode==2?Double.PositiveInfinity:Mode==3?-1:Mode==4?0:100;if(Mode==5)revision++;return Mode!=6;}
    }
    public static int Main()
    {
        try
        {
            var pose=new ConvexHeadPose{Qw=1};
            var exact=new SourceHeadTrajectory(new Float3(1000,2,3),pose,new Float3(.0001f,0,0),0);
            double actual=Math.Abs((double)exact.Vertex(0,1).X-exact.Vertex(0,0).X);
            Console.WriteLine("R6-N1 actual="+actual.ToString("R")+" bound="+exact.TravelBound(0,1).ToString("R"));
            Check(actual<=exact.TravelBound(0,1),"exact review reproduction");
            foreach(float origin in new[]{0f,1000f,99990f,-1000f,-99990f})
            foreach(float yaw in new[]{0f,.7f,-3f})
            {
                var path=new SourceHeadTrajectory(new Float3(origin,2,3),pose,new Float3(.0001f,.00003f,-.00004f),yaw);
                Check(path.VertexCount==168,"source shape");
                foreach(double a in new[]{0,.00000001,.49999999,.99999999,1,1.00000001,1.5,1.99999999})
                {
                    double b=Math.Min(2,a+.00000002);
                    Check(path.TravelBound(a,a)==0,"zero interval");
                    for(int i=0;i<168;i++)
                    {
                        var x=path.Vertex(i,a);var y=path.Vertex(i,b);
                        double dx=(double)x.X-y.X,dy=(double)x.Y-y.Y,dz=(double)x.Z-y.Z;
                        Check(x.IsFinite&&y.IsFinite&&Math.Sqrt(dx*dx+dy*dy+dz*dz)<=path.TravelBound(a,b),"quantized tiny interval");
                    }
                }
                for(int i=0;i<168;i++)
                {var a=path.Vertex(i,0);var b=path.Vertex(i,1);Check(Math.Abs((double)a.X-b.X)<=path.TravelBound(0,1),"R6-N1");}
            }
            bool rejected=false;try{new SourceHeadTrajectory(new Float3(float.MaxValue,0,0),pose,new Float3(float.MaxValue,0,0),0);}catch(ArgumentException){rejected=true;}
            Check(rejected,"R6-N2");
            for(int mode=0;mode<=6;mode++)Check(!new ContinuousHeadClearance(new InvalidDistance{Mode=mode}).Clear(exact,.0025),"invalid/stale/unavailable certificate "+mode);
            var plane=new Plane();var clearance=new ContinuousHeadClearance(plane);
            Check(clearance.Clear(new SourceHeadTrajectory(new Float3(),pose,new Float3(.5f,0,0),1),.0025),"continuous clear");
            Check(!clearance.Clear(new SourceHeadTrajectory(new Float3(),pose,new Float3(3,0,0),0),.0025),"crossing blocked");
            // Ends clear of x=.5, orbit crosses it at the middle of a pi turn.
            plane.X=.5;
            Check(!clearance.Clear(new SourceHeadTrajectory(new Float3(),pose,new Float3(),(float)Math.PI,new Float3(0,0,1)),.0025),"interior angular collision");
            plane.X=2;
            var policy=new HeightRecenterPolicy(clearance,new Support());
            var transition=new HeightRecenterTransition{PreviousHead=new Float3(0,1,0),ProposedHead=new Float3(.2f,2,0),ColliderPose=pose,Seconds=.02f,RequireSupport=true};
            Check(policy.Validate(transition).Accepted,"previous proposed clearance");
            transition.ProposedHead=new Float3(3,2,0);Check(!policy.Validate(transition).Accepted,"recenter wall");
            transition.Kind=HeightChangeKind.Calibration;transition.ProposedHead=new Float3(0,3,0);Check(!policy.Validate(transition).Accepted,"unsupported calibration height");
            transition.ProposedHead=new Float3(0,1.6f,0);Check(policy.Validate(transition).Accepted,"vertical calibration lift");
            transition.Kind=HeightChangeKind.Tracking;transition.ProposedHead=new Float3(0,.3f,0);Check(policy.Validate(transition).Accepted,"deep crouch actual hull stays at eye");
            transition.Kind=HeightChangeKind.RootRecenter;transition.PreviousHead=new Float3(.4f,1,0);transition.ProposedHead=transition.PreviousHead;transition.ProposedRoot=new Float3(.4f,0,0);
            Check(policy.Validate(transition).Accepted,"root recenter preserves world head");
            transition.ProposedHead=new Float3(.5f,1,0);Check(!policy.Validate(transition).Accepted,"recenter cannot move tracked head");
            var result=new PlayerMotorResult{HasPhaseFeedback=true,AppliedPhysicalTranslation=new Float3(.02f,0,0),AppliedPivotCorrection=new Float3(10,0,0),Velocity=new Float3(500,0,0)};
            Check(Math.Abs(ReleaseFeedback.Resolve(false,new ClimbStep(),result,.02f).Velocity.X-1)<1e-5,"physical phase excludes pivot");
            Console.WriteLine(count+" assertions PASS");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
