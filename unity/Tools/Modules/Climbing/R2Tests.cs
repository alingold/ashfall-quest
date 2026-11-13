using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
sealed class BoundaryWorld : IClimbCollision
{
    public int Queries; public bool Invalid, Wall=true, Ledge; public float NormalY, Top=1.4f;
    public Float3 RayEnd;
    void Seen(Float3 a,Float3 b) { Queries++; Invalid|=!a.IsFinite||!b.IsFinite; }
    public bool TryGrab(Float3 p,float radius,out SurfaceHit hit) { Seen(p,new Float3()); hit=new SurfaceHit {Point=p}; return Wall; }
    public bool Raycast(Float3 a,Float3 b,QueryKind kind,out SurfaceHit hit)
    {
        Seen(a,b); hit=new SurfaceHit();
        if(Math.Abs(a.Y-b.Y)<.00001f) { RayEnd=b; hit=new SurfaceHit {Point=b,Normal=new Float3(0,NormalY,-1)}; return Wall; }
        if(!Ledge||Top>a.Y||Top<b.Y) return false;
        hit=new SurfaceHit {Point=new Float3(a.X,Top,a.Z),Normal=new Float3(0,1,0)}; return true;
    }
    public bool HeadMotionClear(Float3 h,Float3 d) { Seen(h,d); return true; }
    public MotionResult SweepHead(Float3 h,Float3 d,float seconds,bool slide) { Seen(h,d); return new MotionResult(d,new Float3(d.X/seconds,d.Y/seconds,d.Z/seconds)); }
}
static class R2Tests
{
    static int checks; static long seq;
    static void Check(bool value,string label) { checks++; if(!value) throw new Exception(label); Console.WriteLine("PASS "+label); }
    static InputFrame F(bool l,bool r,float y=1.4f) { return new InputFrame {Sequence=++seq,Tick=seq,Tracked=true,HeadPosition=new Float3(0,1.75f,0),LeftHandPosition=new Float3(-.2f,y,0),RightHandPosition=new Float3(.2f,y,0),GripLeft=l,GripRight=r}; }
    static ClimbOptions O() { var o=ClimbOptions.Default; o.LeftContact=new Float3(-.2f,1.4f,0); o.RightContact=new Float3(.2f,1.4f,0); return o; }
    static bool Near(float a,float b) { return Math.Abs(a-b)<.0001f; }
    static int Main()
    {
        try {
        var root=new Float3();
        // Symmetric central decision, independently varied by support, tracking and reservation.
        for(int direction=0;direction<2;direction++) for(int support=0;support<2;support++)
        for(int reserved=0;reserved<2;reserved++) for(int lost=0;lost<2;lost++)
        {
            bool left=direction==0; var w=new Geometry {Ledge=support==1,Top=1.4f}; var c=new ClimbController(w); var o=O();
            c.Step(F(left,!left),root,.1f,o); c.Step(F(left,!left,1.2f),root,.1f,o);
            if(left) { o.RightReserved=reserved==1; o.LeftTracked=lost==0; }
            else { o.LeftReserved=reserved==1; o.RightTracked=lost==0; }
            var s=c.Step(F(!left,left,1.2f),root,.1f,o);
            if(reserved==0) Check(s.Climbing&&!s.Mantling&&!s.Launch&&(left?c.RightAnchored:c.LeftAnchored),"atomic handoff dir/support/reserved/lost="+direction+support+reserved+lost);
            else if(lost==1) Check(!s.Climbing&&!s.Mantling&&!s.Launch,"reserved target and lost source safely cancel "+direction+support);
            else if(support==1) Check(s.Mantling&&!s.Launch,"reserved target permits intentional final-release mantle "+direction);
            else Check(s.Launch&&!s.Climbing,"reserved target retains intentional final-release fling "+direction);
        }
        for(int support=0;support<2;support++) for(int lost=0;lost<2;lost++)
        {
            var w=new Geometry {Ledge=support==1,Top=1.4f}; var c=new ClimbController(w); var o=O();
            c.Step(F(true,true),root,.1f,o); c.Step(F(true,true,1.2f),root,.1f,o); o.RightTracked=lost==0;
            var s=c.Step(F(false,false,1.2f),root,.1f,o);
            Check(!c.LeftAnchored&&!c.RightAnchored&&s.Released&&(lost==1 ? !s.Mantling&&!s.Launch : support==1 ? s.Mantling&&!s.Launch : s.Launch&&!s.Mantling),"simultaneous final release support/lost="+support+lost);
        }
        for(int direction=0;direction<2;direction++) for(int support=0;support<2;support++)
        {
            bool left=direction==0; var w=new Geometry {Ledge=support==1,Top=1.4f}; var c=new ClimbController(w); var o=O();
            c.Step(F(left,!left),root,.1f,o); c.Step(F(left,!left,1.2f),root,.1f,o);
            if(left) o.RightTracked=false; else o.LeftTracked=false;
            var s=c.Step(F(!left,left),root,.1f,o);
            Check(!s.Climbing&&(support==1 ? s.Mantling&&!s.Launch : s.Launch&&!s.Mantling),"untracked target cannot claim dir/support="+direction+support);
        }
        // Source holdouts: original support, short rise, floor normal and ceiling clearance.
        Float3[] route;
        foreach(float top in new float[]{.02f,.5f,1.4f,1.7f}) {
            var w=new Geometry {Ledge=true,Top=top};
            Check(MantlePlanner.TryPlan(w,root,new Float3(0,1.75f,0),new Float3(0,1.4f,0),1.75f,.707f,out route)&&Near(route[route.Length-1].Y,top),"supported height holdout "+top);
            w.Ceiling=1.75f+top-.01f;
            Check(!MantlePlanner.TryPlan(w,root,new Float3(0,1.75f,0),new Float3(0,1.4f,0),1.75f,.707f,out route),"blocked support holdout "+top);
        }
        {
            var w=new Geometry {Ledge=true,Top=1.4f}; var c=new ClimbController(w);
            Check(!c.Step(F(false,false),root,.1f,O()).Mantling,"support without final release cannot mantle");
            c.Step(F(true,true),root,.1f,O()); var s=c.Step(F(true,false),root,.1f,O());
            Check(s.Climbing&&!s.Mantling&&!s.Launch,"supported first of two releases stays hanging");
        }
        {
            var w=new BoundaryWorld(); var c=new ClimbController(w); var o=O(); var f=F(true,false); f.LeftHandPosition=new Float3(float.MaxValue,0,0);
            c.Step(f,root,.1f,o); f=F(true,false); f.LeftHandPosition=new Float3(-float.MaxValue,0,0); c.Step(f,root,.1f,o);
            Check(!c.LeftAnchored&&!w.Invalid&&w.Queries==0,"extreme finite tracked coordinates query-free");
            c.Step(F(true,false),root,.1f,o); int before=w.Queries; f=F(true,false); f.LeftHandPosition=new Float3(0,20,0); var s=c.Step(f,root,.1f,o);
            Check(!c.LeftAnchored&&!s.Launch&&!w.Invalid&&w.Queries==before,"implausible held hand becomes nonlaunch tracking loss");
            f=F(true,false); f.HeadPosition=new Float3(float.MaxValue,0,0); before=w.Queries; c.Step(f,root,.1f,o);
            Check(w.Queries==before&&!w.Invalid,"extreme finite head rejected before queries");
        }
        {
            var w=new BoundaryWorld(); var c=new ClimbController(w); var o=O(); var forward=new Float3(0,0,1);
            var f=F(false,false); var s=c.StepDesktop(f,root,.1f,o,true,forward);
            Check(s.Climbing&&Near(w.RayEnd.Z,1.35f)&&Near(s.RootDisplacement.Y,0),"desktop E ray 1.35m and acquisition without auto-rise");
            int before=w.Queries; Check(!c.StepDesktop(f,root,.1f,o,true,forward).Accepted&&w.Queries==before,"desktop replay cannot query or rise twice");
            s=c.StepDesktop(F(false,false),root,.1f,o,true,forward);
            Check(Near(s.RootDisplacement.Y,.22f),"desktop held E advances camera anchor at 2.2m/s");
            s=c.StepDesktop(F(false,false),root,.1f,o,false,forward);
            Check(s.Released&&s.Launch&&!s.Mantling,"desktop E final release uses shared fling");
            c.Reset(); w.NormalY=.75f; Check(!c.StepDesktop(F(false,false),root,.1f,o,true,forward).Climbing,"desktop horizontal surface threshold excludes .75");
            w.NormalY=0; o.GrabCooldown=.1f; before=w.Queries;
            Check(!c.StepDesktop(F(false,false),root,.1f,o,true,forward).Climbing&&w.Queries==before,"desktop cooldown prevents acquisition query");
            o=O(); c.Reset(); before=w.Queries;
            Check(!c.StepDesktop(F(false,false),root,.1f,o,true,new Float3(float.MaxValue,0,0)).Climbing&&w.Queries==before&&!w.Invalid,"desktop extreme forward query-free");
            c.Reset(); before=w.Queries;
            Check(!c.StepDesktop(F(false,false),root,float.MaxValue,o,true,forward).Accepted&&w.Queries==before,"unbounded timestep refused before desktop queries");
            w.Ledge=true; c.Reset(); c.StepDesktop(F(false,false),root,.1f,o,true,forward);
            s=c.StepDesktop(F(false,false),root,.1f,o,false,forward);
            Check(s.Mantling&&!s.Launch,"desktop final release supports source lower ledge");
        }
        Console.WriteLine("RESULT checks="+checks+" failures=0 runtime=UNKNOWN"); return 0;
        } catch(Exception e) {Console.WriteLine("FAIL "+e); return 1;}
    }
}

