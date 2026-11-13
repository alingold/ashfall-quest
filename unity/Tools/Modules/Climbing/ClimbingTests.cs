using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;

// Analytic geometry fixture: horizontal ledge with finite X/Z footprint and a ceiling.
// It computes ray/plane intersections and clamps sweeps at a ceiling independently of production rules.
sealed class Geometry : IClimbCollision
{
    public bool Contact=true, Archive, Authored, Ledge;
    public float Top=1.7f, MinX=-1, MaxX=1, MinZ=-1, MaxZ=1, Ceiling=100;
    public bool TryGrab(Float3 point,float radius,out SurfaceHit hit)
    { hit=new SurfaceHit {Point=point,Normal=new Float3(0,0,-1),ArchiveGeometry=Archive,AuthoredClimbable=Authored}; return Contact; }
    public bool Raycast(Float3 a,Float3 b,QueryKind kind,out SurfaceHit hit)
    {
        hit=new SurfaceHit(); if(!Ledge || a.Y==b.Y) return false;
        float t=(Top-a.Y)/(b.Y-a.Y);
        float x=a.X+(b.X-a.X)*t,z=a.Z+(b.Z-a.Z)*t;
        if(t<0||t>1||x<MinX||x>MaxX||z<MinZ||z>MaxZ) return false;
        hit=new SurfaceHit {Point=new Float3(x,Top,z),Normal=new Float3(0,1,0)}; return true;
    }
    public bool HeadMotionClear(Float3 head,Float3 motion) { return head.Y+motion.Y<=Ceiling; }
    public MotionResult SweepHead(Float3 head,Float3 motion,float seconds,bool slide)
    {
        float y=motion.Y>0 ? Math.Max(0,Math.Min(motion.Y,Ceiling-head.Y)) : motion.Y;
        Float3 d=new Float3(motion.X,y,motion.Z);
        return new MotionResult(d,new Float3(d.X/seconds,d.Y/seconds,d.Z/seconds));
    }
}
static class ClimbingTests
{
    static int checks; static long seq;
    static void Check(bool b,string name) { if(!b) throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
    static bool Near(float a,float b) { return Math.Abs(a-b)<.0001; }
    static InputFrame Frame(bool left,bool right,float handY)
    { return new InputFrame { Tick=++seq, Sequence=seq, Tracked=true, HeadPosition=new Float3(0,1.75f,0),LeftHandPosition=new Float3(-.2f,handY,0),RightHandPosition=new Float3(.2f,handY,0),GripLeft=left,GripRight=right }; }
    static ClimbOptions Options() { var o=ClimbOptions.Default; o.LeftContact=new Float3(-.2f,1.4f,0); o.RightContact=new Float3(.2f,1.4f,0); return o; }
    static int Main()
    {
        try
        {
            var w=new Geometry(); var c=new ClimbController(w); var o=Options(); var root=new Float3();
            Check(c.Step(Frame(true,false,1.4f),root,.1f,o).Climbing,"hand acquires physical wall");
            o.LeftReserved=true;
            var s=c.Step(Frame(true,false,1.2f),root,.1f,o);
            Check(c.LeftAnchored&&Near(s.RootDisplacement.Y,.2f),"reservation retains physical anchor and hand pull moves root");
            Check(Near(c.LeftAnchor.Y,1.4f),"anchor stays world fixed");
            s=c.Step(Frame(false,false,1.2f),root,.1f,o);
            Check(s.Launch&&Near(s.Velocity.Y,.875f),"intentional release uses smoothed collision velocity and fling gain");
            c.Reset(); o=Options(); c.Step(Frame(true,true,1.4f),root,.1f,o);
            var f=Frame(true,true,1.4f); f.LeftHandPosition=new Float3(-.2f,1,0); f.RightHandPosition=new Float3(.2f,1.2f,0);
            s=c.Step(f,root,.1f,o); Check(Near(s.RootDisplacement.Y,.3f),"two-hand pull averages then caps at 30cm");
            s=c.Step(Frame(false,true,1.2f),root,.1f,o); Check(c.RightAnchored&&!s.Launch&&!s.Mantling,"first release cannot launch or mantle");
            o.RightTracked=false; s=c.Step(Frame(false,true,1.2f),root,.1f,o);
            Check(!c.RightAnchored&&!s.Launch&&Near(s.Velocity.Y,0),"per-hand tracking loss cannot fling");
            c.Reset(); o=Options(); o.LeftReserved=true;
            Check(!c.Step(Frame(true,false,1.4f),root,.1f,o).Climbing,"reserved hand cannot acquire");
            o.LeftReserved=false; w.Archive=true;
            Check(!c.Step(Frame(true,false,1.4f),root,.1f,o).Climbing,"archive barrier remains ungrabbable");
            w.Authored=true;
            Check(c.Step(Frame(true,false,1.4f),root,.1f,o).Climbing,"authored archive tile supports anchor");
            f=Frame(true,false,1.4f); f.Paused=true; s=c.Step(f,root,.1f,o);
            Check(!c.LeftAnchored&&!s.Launch&&s.OwnsMotion,"pause safely releases all actions");
            c.Reset(); w=new Geometry(); c=new ClimbController(w); o=Options();
            c.Step(Frame(true,false,1.4f),root,.01f,o); c.Step(Frame(true,false,-1),root,.001f,o); o.FlingPower=5;
            s=c.Step(Frame(false,false,-1),root,.01f,o);
            Check(Near(s.Velocity.Y,43.75f),"fling limits sample to 7m/s before configured power");
            c.Reset(); c.Step(Frame(true,false,1.4f),root,.1f,o); f=Frame(true,false,1.2f); c.Step(f,root,.1f,o);
            Check(!c.Step(f,root,.1f,o).Accepted,"duplicate frame cannot move twice");
            f=Frame(true,false,1.2f); f.Tracked=false; s=c.Step(f,root,.1f,o);
            Check(!c.LeftAnchored&&!s.Launch,"head tracking loss disarms climb");
            c.Reset(); f=Frame(true,false,1.4f); c.Step(f,root,.1f,o);
            s=c.Step(Frame(true,false,1.2f),root,float.NaN,o);
            Check(!s.Accepted&&!c.LeftAnchored,"invalid delta safely cancels state");
            o=Options(); o.LeftContact=new Float3(0,4,0); c.Reset();
            Check(!c.Step(Frame(true,false,1.4f),root,.1f,o).Climbing,"remote contact cannot acquire");
            o=Options(); o.GrabCooldown=.1f;
            Check(!c.Step(Frame(true,false,1.4f),root,.1f,o).Climbing,"cooldown prevents acquisition");

            w=new Geometry {Ledge=true}; c=new ClimbController(w); o=Options();
            c.Step(Frame(true,false,1.4f),root,.1f,o);
            s=c.Step(Frame(false,false,1.4f),root,.1f,o);
            Check(s.Mantling&&!s.Launch&&Near(s.RootDisplacement.Y,0),"release mantle preflight is atomic");
            s=c.Step(Frame(false,false,1.4f),root,.1f,o);
            Check(s.Mantling&&Near(s.RootDisplacement.Y,.85f),"200ms mantle eased midpoint");
            f=Frame(false,false,1.4f); f.HeadPosition=new Float3(0,2.6f,0);
            s=c.Step(f,new Float3(0,.85f,0),.1f,o);
            Check(!s.Mantling&&Near(s.RootDisplacement.Y,.85f),"mantle completes remaining route");
            c.Reset(); w.Ceiling=2; c.Step(Frame(true,false,1.4f),root,.1f,o);
            s=c.Step(Frame(false,false,1.4f),root,.1f,o);
            Check(!s.Mantling&&Near(s.RootDisplacement.Y,0),"blocked standing route leaves root unchanged");
            w.Ceiling=100; c.Reset(); c.Step(Frame(true,false,1.4f),root,.1f,o); c.Step(Frame(false,false,1.4f),root,.1f,o);
            w.Ceiling=2; s=c.Step(Frame(false,false,1.4f),root,.1f,o);
            Check(s.MantleBlocked&&!s.Mantling&&Near(s.RootDisplacement.Y,.25f),"live ceiling abort keeps only collision-safe partial position");
            Float3[] route; w.Ceiling=100; w.MinZ=.02f; w.Top=1.72f;
            Check(MantlePlanner.TryPlan(w,root,new Float3(0,1.75f,0),new Float3(0,1.7f,.05f),1.75f,.707f,out route)&&route.Length==3,"grazing lip plans lift inward and standing rise");
            Check(route[1].Z>=.02f&&route[1].Z<=.115f,"grazing route reaches actual finite ledge");
            Check(!MantlePlanner.TryPlan(w,root,new Float3(0,1.75f,0),new Float3(0,1.7f,4),1.75f,.707f,out route),"stale remote release anchor cannot invoke grazing recovery");
            w.Top=1.8f;
            Check(!MantlePlanner.TryPlan(w,root,new Float3(0,1.75f,0),new Float3(0,1.7f,.05f),1.75f,.707f,out route),"taller wall cannot masquerade as grazing lip");
            w.Top=1.68f;
            Check(!MantlePlanner.TryPlan(w,root,new Float3(0,1.75f,0),new Float3(0,1.7f,.05f),1.75f,.707f,out route),"top beyond 65mm assistance band rejected");
            w.Top=1.72f; w.Ceiling=1.754f;
            Check(!MantlePlanner.TryPlan(w,root,new Float3(0,1.75f,0),new Float3(0,1.7f,.05f),1.75f,.707f,out route),"ceiling blocks grazing lift without collision bypass");
            for(int bearing=0;bearing<4;bearing++)
            {
                w=new Geometry {Ledge=true,Top=1.72f};
                Float3 anchor;
                if(bearing==0) { w.MinZ=.025f; anchor=new Float3(0,1.7f,.05f); }
                else if(bearing==1) { w.MaxZ=-.025f; anchor=new Float3(0,1.7f,-.05f); }
                else if(bearing==2) { w.MinX=.025f; anchor=new Float3(.05f,1.7f,0); }
                else { w.MaxX=-.025f; anchor=new Float3(-.05f,1.7f,0); }
                Check(MantlePlanner.TryPlan(w,root,new Float3(0,1.75f,0),anchor,1.75f,.707f,out route),"grazing recovery bearing "+bearing);
            }
            w=new Geometry {Ledge=true}; c=new ClimbController(w); o=Options();
            c.Step(Frame(true,false,1.4f),root,.1f,o); c.Step(Frame(false,false,1.4f),root,.1f,o);
            f=Frame(false,false,1.4f); f.Tracked=false; s=c.Step(f,root,.1f,o);
            Check(!c.Mantling&&Near(s.RootDisplacement.Y,0)&&!s.Launch,"tracking loss cancels in-progress mantle before movement");
            w=new Geometry(); c=new ClimbController(w); o=Options(); o.FlingPower=-1;
            c.Step(Frame(true,false,1.4f),root,.1f,o); c.Step(Frame(true,false,1.2f),root,.1f,o);
            Check(!c.Step(Frame(false,false,1.2f),root,.1f,o).Launch,"negative fling setting clamps to zero");
            o=Options(); c.Reset(); c.Step(Frame(true,false,1.4f),root,.1f,o);
            f=Frame(true,false,1.2f); f.LeftHandPosition=new Float3(float.NaN,0,0);
            s=c.Step(f,root,.1f,o);
            Check(!c.LeftAnchored&&!s.Launch,"nonfinite hand pose behaves as tracking loss");
            Console.WriteLine("RESULT checks="+checks+" failures=0 Unity/XR/Quest/network=UNKNOWN"); return 0;
        }
        catch(Exception e) { Console.WriteLine("FAIL "+e); return 1; }
    }
}
