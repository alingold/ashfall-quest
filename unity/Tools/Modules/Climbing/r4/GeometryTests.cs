using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Climbing;

// Analytic convex-vs-infinite-solid-halfspace fixture, NOT Unity/PhysX emulation.
public static class GeometryTests
{
    static int checks,failed;
    static void Check(bool yes,string name){checks++;if(!yes)failed++;Console.WriteLine((yes?"PASS ":"FAIL ")+name);}
    static bool Near(float a,float b,float epsilon=.000002f){return Math.Abs(a-b)<=epsilon;}
    static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
    static double Dot(Float3 a,Float3 b){return (double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;}
    sealed class PoseSource:IConvexHeadSource
    {
        public ConvexHeadPose Pose=new ConvexHeadPose{Qw=1};public bool Available=true;
        public bool TryRead(out ConvexHeadPose p){p=Pose;return Available;}
    }
    sealed class Plane
    {public Float3 Normal;public double D;public Plane(Float3 n,double d){Normal=n;D=d;}}
    sealed class Halfspaces:IConvexClimbScene
    {
        public readonly List<Plane> Planes=new List<Plane>();public long Version;
        public bool Available=true,ChangeDuringCast;public int Casts,Recoveries;public PoseSource MovePose;
        public long Revision{get{return Version;}}
        double Gap(ConvexHeadQuery q,Plane p)
        {
            // Enumerate transformed vertices independently of production Support().
            double min=double.PositiveInfinity;
            foreach(var v in q.Shape.WorldVertices(q.Origin,q.PoseWithZeroOffset()))min=Math.Min(min,Dot(v,p.Normal));
            return min-p.D;
        }
        public bool Penetrating(ConvexHeadQuery q,out bool penetrating)
        {penetrating=false;foreach(var p in Planes)if(Gap(q,p)<0)penetrating=true;return Available;}
        public bool Cast(ConvexHeadQuery q,out bool hit,out ClimbQueryHit nearest)
        {
            Casts++;if(q.Recovery)Recoveries++;if(ChangeDuringCast)Version++;
            if(MovePose!=null)MovePose.Pose.WorldOffset=new Float3(.1f,0,0);
            hit=false;nearest=new ClimbQueryHit();double best=double.PositiveInfinity;
            double length=Math.Sqrt(Dot(q.Motion,q.Motion));
            foreach(var p in Planes)
            {
                double gap=Gap(q,p),speed=Dot(q.Motion,p.Normal),t=double.PositiveInfinity;
                // First cast exposes numerical touching floor contacts; recovery only removes
                // contacts separating from this exact plane. Other solids remain queried.
                if(gap<=.00001&&(!q.Recovery||speed<=0))t=0;
                if(speed<0)t=Math.Min(t,gap/-speed);
                if(t>=0&&t<=1&&t*length<best)
                {best=t*length;hit=true;nearest=new ClimbQueryHit{Distance=(float)best,Surface=new SurfaceHit{Point=q.Origin,Normal=p.Normal}};}
            }
            return Available;
        }
        public bool Ray(ClimbQuery q,out bool hit,out ClimbQueryHit h){hit=false;h=new ClimbQueryHit();return Available;}
        public bool Grab(ClimbQuery q,out bool hit,out ClimbQueryHit h){hit=false;h=new ClimbQueryHit();return Available;}
    }
    // Deliberate backend contract fixture for retry budget/tolerance, separate from analytic tests.
    sealed class Contacts:IConvexClimbScene
    {
        public int Count;public int ClearAt=100;public float Travel=0;public long Revision{get{return 0;}}
        public bool Penetrating(ConvexHeadQuery q,out bool p){p=false;return true;}
        public bool Cast(ConvexHeadQuery q,out bool hit,out ClimbQueryHit h)
        {Count++;hit=Count<ClearAt;h=new ClimbQueryHit{Distance=Travel,Surface=new SurfaceHit{Normal=new Float3(0,1,0)}};return true;}
        public bool Ray(ClimbQuery q,out bool hit,out ClimbQueryHit h){hit=false;h=new ClimbQueryHit();return true;}
        public bool Grab(ClimbQuery q,out bool hit,out ClimbQueryHit h){hit=false;h=new ClimbQueryHit();return true;}
    }
    static CollisionLayers Layers(){return new CollisionLayers{StructureMask=256,SupportMask=768,HeadMask=768};}
    static ConvexClimbCollision Adapter(IConvexClimbScene s,PoseSource p){return new ConvexClimbCollision(s,p,Layers(),.70710678f);}
    static double Min(Float3 head,ConvexHeadPose pose,Float3 normal)
    {double min=double.PositiveInfinity;foreach(var v in SourceHeadConvex.Instance.WorldVertices(head,pose))min=Math.Min(min,Dot(v,normal));return min;}
    public static int Main()
    {
        var shape=SourceHeadConvex.Instance;var vertices=shape.CopyVertices();
        Check(vertices.Length==168,"source 7x24 vertices retained including duplicate pole samples");
        bool equivalent=true;
        for(int r=0;r<7;r++)for(int s=0;s<24;s++)
        {
            double e=r*Math.PI/12,a=s*Math.PI/12;var p=vertices[r*24+s];
            equivalent &= Near(p.X,(float)(Math.Cos(a)*Math.Cos(e)*.12),.00000002f)&&
                Near(p.Y,(float)(Math.Sin(e)*.120),.00000002f)&&Near(p.Z,(float)(-Math.Sin(a)*Math.Cos(e)*.12),.00000002f);
        }
        Check(equivalent,"all source samples equivalent after once-only Z reflection");
        vertices[0]=new Float3(900,900,900);Check(Near(shape.CopyVertices()[0].X,.12f),"vertices copy cannot mutate hull");
        Check(Near(shape.Support(new Float3(0,-1,0),new ConvexHeadPose{Qw=1}).Y,0),"no invisible chin below eye");
        Check(Near(shape.Support(new Float3(0,1,0),new ConvexHeadPose{Qw=1}).Y,.12f),"authored eye-to-top preserved");
        Check(Dot(shape.Support(new Float3(1,1,0),new ConvexHeadPose{Qw=1}),new Float3(1,1,0))<.18,"source diagonal differs from .12 box corner (.24)");
        var poses=new[]{new ConvexHeadPose{Qw=1},new ConvexHeadPose{Qy=(float)Math.Sin(.41),Qw=(float)Math.Cos(.41),WorldOffset=new Float3(.2f,.03f,-.1f)},
            new ConvexHeadPose{Qz=(float)Math.Sin(.25),Qw=(float)Math.Cos(.25),WorldOffset=new Float3(-.12f,.02f,.08f)}};
        foreach(var pose in poses)
        {
            var head=new Float3(4,2,-3);var world=shape.WorldVertices(head,pose);bool rigid=true;
            var original=shape.CopyVertices();
            for(int i=0;i<world.Length;i++)
            {
                var v=original[i];double x=pose.Qx,y=pose.Qy,z=pose.Qz,w=pose.Qw;
                // Independent quaternion v + 2w(q cross v) + 2q cross(q cross v).
                double tx=2*(y*v.Z-z*v.Y),ty=2*(z*v.X-x*v.Z),tz=2*(x*v.Y-y*v.X);
                rigid &= Near(world[i].X,(float)(head.X+pose.WorldOffset.X+v.X+w*tx+y*tz-z*ty))&&
                    Near(world[i].Y,(float)(head.Y+pose.WorldOffset.Y+v.Y+w*ty+z*tx-x*tz))&&
                    Near(world[i].Z,(float)(head.Z+pose.WorldOffset.Z+v.Z+w*tz+x*ty-y*tx));
            }
            Check(rigid,"all vertices rotation and already-world offset applied once");
            foreach(var n in new[]{new Float3(1,0,0),new Float3(0,-1,0),new Float3(.6f,.8f,0)})
            {double max=double.NegativeInfinity;foreach(var v in world)max=Math.Max(max,Dot(v,n));var support=Add(Add(head,pose.WorldOffset),shape.Support(n,pose));Check(Math.Abs(max-Dot(support,n))<.000002,"support equals transformed convex hull extremum");}
        }
        foreach(float height in new[]{-50f,0f,250f})foreach(var n in new[]{new Float3(0,1,0),new Float3(.6f,.8f,0),new Float3(-.3f,.9539392f,0)})
        {
            var p=new PoseSource();var s=new Halfspaces();var head=new Float3(.3f,height,-.2f);
            s.Planes.Add(new Plane(n,Min(head,p.Pose,n)));var a=Adapter(s,p);
            Check(a.HeadMotionClear(head,new Float3(0,.2f,0)),"analytic touching floor/slope preflight translated "+height);
            Check(s.Casts==2&&s.Recoveries==1,"touching separating contact retried geometrically");
            var r=a.Solve(head,new Float3(0,.2f,0),false,.001f);
            Check(r.Complete&&r.Available&&Near(r.Displacement.Y,.2f),"analytic touching floor/slope sweep complete");
            // A wall encountered AFTER resolving the floor still blocks this same continuous query.
            s.Planes.Add(new Plane(new Float3(0,-1,0),Min(head,p.Pose,new Float3(0,-1,0))-.05));
            r=a.Solve(head,new Float3(0,.2f,0),false,.001f);
            Check(!r.Complete&&r.Displacement.Y<.051f,"ceiling after floor recovery blocks");
        }
        foreach(float depth in new[]{.0000001f,.00001f,.01f})
        {
            var s=new Halfspaces();s.Planes.Add(new Plane(new Float3(0,1,0),depth));var a=Adapter(s,new PoseSource());
            Check(!a.HeadMotionClear(new Float3(),new Float3(0,.2f,0))&&s.Casts==0,"initial true penetration refused depth "+depth);
        }
        foreach(var pose in poses)
        {
            var p=new PoseSource{Pose=pose};var s=new Halfspaces();var head=new Float3(3,2,-4);
            s.Planes.Add(new Plane(new Float3(0,1,0),Min(head,pose,new Float3(0,1,0))));
            var a=Adapter(s,p);var r=a.Solve(head,new Float3(0,.15f,0),false,.001f);
            Check(r.Complete&&Near(r.Displacement.Y,.15f),"rotated offset source hull touching-floor recovery");
            s.Planes.Clear();s.Planes.Add(new Plane(new Float3(0,-1,0),Min(head,pose,new Float3(0,-1,0))-.03));
            Check(!a.HeadMotionClear(head,new Float3(0,.15f,0)),"rotated offset convex ceiling tested at actual upper extent");
        }
        foreach(float distance in new[]{.00005f,.00011f,.0009f,.0011f,.0149f,.0151f})
        {
            var c=new Contacts();var a=Adapter(c,new PoseSource());var m=new Float3(0,distance,0);
            var mantle=a.Solve(new Float3(),m,false,.001f);Check(mantle.Complete==(distance<=.001f),"mantle endpoint tolerance "+distance);
            c.Count=0;var support=a.Solve(new Float3(),m,false,.015f);Check(support.Complete==(distance<=.015f),"support endpoint tolerance "+distance);
            Check(c.Count==(distance<.0001f?1:5),"source .0001 rest and four retries bounded "+distance);
        }
        foreach(int clearAt in new[]{2,5,6})
        {
            var c=new Contacts{ClearAt=clearAt};var r=Adapter(c,new PoseSource()).Solve(new Float3(),new Float3(0,.2f,0),false,.001f);
            Check(r.Complete==(clearAt<=5)&&c.Count==Math.Min(clearAt,5),"initial cast plus exactly four recovery retries clearAt "+clearAt);
        }
        {
            var s=new Halfspaces();var p=new PoseSource();var a=Adapter(s,p);var head=new Float3();
            s.Planes.Add(new Plane(new Float3(-1,0,0),-.17));
            Check(!a.HeadMotionClear(head,new Float3(.2f,.1f,0)),"side wall rejects diagonal upward motion");
            s.Planes.Clear();s.Planes.Add(new Plane(new Float3(0,-1,0),-.17));
            Check(!a.HeadMotionClear(head,new Float3(0,.2f,0)),"ceiling rejects upward motion");
            s.Planes.Clear();s.Planes.Add(new Plane(new Float3(.8f,.6f,0),Min(head,p.Pose,new Float3(.8f,.6f,0))));
            Check(!a.HeadMotionClear(head,new Float3(0,.2f,0))&&s.Recoveries==0,"steep slope below floor threshold not recovered");
        }
        {
            var s=new Halfspaces();var p=new PoseSource();var a=Adapter(s,p);var m=new Float3(0,.2f,0);
            Check(a.HeadMotionClear(new Float3(),m),"clear preflight before geometry moves");
            s.Planes.Add(new Plane(new Float3(0,-1,0),-.17));s.Version++;
            Check(a.SweepHead(new Float3(),m,.1f,false).Displacement.Y<.051f,"moved ceiling rechecked during actual sweep");
            s.Planes.Clear();s.ChangeDuringCast=true;var r=a.Solve(new Float3(),m,false,.001f);
            Check(!r.Available&&!r.Complete&&r.Displacement.Y==0,"revision changing during cast fails closed");
            s.ChangeDuringCast=false;s.Available=false;Check(!a.HeadMotionClear(new Float3(),new Float3(0,.00001f,0)),"unavailable tiny query cannot tolerance-pass");
            s.Available=true;s.MovePose=p;Check(!a.HeadMotionClear(new Float3(),m),"live pose changing during cast invalidates preflight");s.MovePose=null;p.Pose.WorldOffset=new Float3();
            Check(a.HeadMotionClear(new Float3(),new Float3(.2f,.1f,0)),"clear preflight before wall moves");
            s.Planes.Add(new Plane(new Float3(-1,0,0),-.17));s.Version++;
            var partial=a.SweepHead(new Float3(),new Float3(.2f,.1f,0),.1f,false);
            Check(partial.Displacement.X<.051f&&partial.Displacement.Y<.026f&&Near(partial.Velocity.X,partial.Displacement.X/.1f),"moved wall retains safe partial displacement and measured velocity");
            s.Planes.Clear();
            s.Available=true;p.Pose.Qw=2;Check(!a.HeadMotionClear(new Float3(),m),"invalid pose refuses");
            p.Pose.Qw=1;p.Available=false;Check(!a.HeadMotionClear(new Float3(),m),"missing live collider pose refuses");
        }
        Console.WriteLine("Geometry "+checks+" checks, "+failed+" failed");return failed==0?0:1;
    }
}
static class GeometryQueryExtensions
{
    public static ConvexHeadPose PoseWithZeroOffset(this ConvexHeadQuery q)
    {var p=q.Pose;p.WorldOffset=new Float3();return p;}
}
