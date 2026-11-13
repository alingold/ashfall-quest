using System;
using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    // Geometry evidence, NOT a collision approval. Parameter 0..1 translates the
    // exact hull; 1..2 rotates it about the translated head (or explicit root
    // pivot with pivotToHead). No root/pose writes. Supported worlds +/-100 km,
    // translation components +/-1 km, pivot radius <=10m; derived bounds checked.
    public sealed class SourceHeadTrajectory
    {
        public const double WorldLimit=100000, MotionLimit=1000;
        readonly double hx,hy,hz;readonly Float3 translation,orbit;readonly Float3[] initial;readonly float yaw;
        // Error radius of a published Float3 vertex relative to the ideal double curve.
        // One full relative float ulp plus a double arithmetic/trig allowance per axis.
        public double PublicationError {get;private set;}
        public SourceHeadTrajectory(Float3 head,ConvexHeadPose pose,Float3 translation,float yaw)
            :this(head,pose,translation,yaw,new Float3()){}
        public SourceHeadTrajectory(Float3 head,ConvexHeadPose pose,Float3 translation,float yaw,Float3 pivotToHead)
        {
            if(!V.WorldPoint(head)||!translation.IsFinite||!SourceHeadConvex.ValidPose(pose)||
                Math.Abs((double)translation.X)>MotionLimit||Math.Abs((double)translation.Y)>MotionLimit||Math.Abs((double)translation.Z)>MotionLimit||
                !pivotToHead.IsFinite||!V.Within(pivotToHead,new Float3(),10)||
                Single.IsNaN(yaw)||Single.IsInfinity(yaw)||Math.Abs(yaw)>(double)(float)Math.PI)
                throw new ArgumentException("source trajectory");
            hx=(double)head.X+pose.WorldOffset.X;hy=(double)head.Y+pose.WorldOffset.Y;hz=(double)head.Z+pose.WorldOffset.Z;
            this.translation=translation;this.yaw=yaw;orbit=pivotToHead;
            initial=SourceHeadConvex.Instance.CopyVertices();
            for(int i=0;i<initial.Length;i++)initial[i]=SourceHeadConvex.Rotate(initial[i],pose);
            double radius=0;foreach(var v in initial)radius=Math.Max(radius,Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y+(double)v.Z*v.Z));
            radius+=2*Math.Sqrt((double)orbit.X*orbit.X+(double)orbit.Z*orbit.Z);
            double bx=Math.Max(Math.Abs(hx),Math.Abs(hx+translation.X))+radius;
            double by=Math.Max(Math.Abs(hy),Math.Abs(hy+translation.Y))+radius;
            double bz=Math.Max(Math.Abs(hz),Math.Abs(hz+translation.Z))+radius;
            if(Math.Max(bx,Math.Max(by,bz))>WorldLimit)throw new ArgumentException("trajectory world envelope");
            double ex=bx*Math.Pow(2,-23)+1e-9,ey=by*Math.Pow(2,-23)+1e-9,ez=bz*Math.Pow(2,-23)+1e-9;
            PublicationError=Math.Sqrt(ex*ex+ey*ey+ez*ez);
        }
        public int VertexCount {get{return initial.Length;}}
        public Float3 Vertex(int index,double phase)
        {
            if(index<0||index>=initial.Length||Double.IsNaN(phase)||phase<0||phase>2)throw new ArgumentException("trajectory sample");
            var v=initial[index];double t=Math.Min(1,phase),angle=Math.Max(0,phase-1)*yaw;
            double c=Math.Cos(angle),s=Math.Sin(angle);
            return new Float3((float)(hx+t*translation.X+c*((double)v.X+orbit.X)+s*((double)v.Z+orbit.Z)-orbit.X),
                (float)(hy+t*translation.Y+v.Y),(float)(hz+t*translation.Z-s*((double)v.X+orbit.X)+c*((double)v.Z+orbit.Z)-orbit.Z));
        }
        // Conservative Hausdorff bound for ALL points in the convex hull, since
        // every convex combination moves no further than its furthest vertex.
        // A trusted native whole-hull distance lower bound greater than this plus
        // margin would certify an interval. Endpoint overlap booleans cannot.
        public double TravelBound(double from,double to)
        {
            if(Double.IsNaN(from)||Double.IsNaN(to)||from<0||to>2||to<from)throw new ArgumentException("trajectory interval");
            if(from==to)return 0;
            double linear=Math.Max(0,Math.Min(1,to)-Math.Min(1,from));
            double angular=Math.Max(0,to-Math.Max(1,from))*Math.Abs(yaw),radius=0;
            foreach(var v in initial)radius=Math.Max(radius,Math.Sqrt(((double)v.X+orbit.X)*((double)v.X+orbit.X)+((double)v.Z+orbit.Z)*((double)v.Z+orbit.Z)));
            return linear*Math.Sqrt((double)translation.X*translation.X+(double)translation.Y*translation.Y+(double)translation.Z*translation.Z)+angular*radius+2*PublicationError;
        }
    }
}
