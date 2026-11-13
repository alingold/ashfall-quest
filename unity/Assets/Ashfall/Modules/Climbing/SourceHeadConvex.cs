using System;
using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    // Convex hull of EXACT source sampling: 7 elevation rings x 24 segments.
    // This is not a sphere, capsule or bounding box; coplanar bottom is at the eye.
    public sealed class SourceHeadConvex
    {
        public const float Radius=.12f, TopToEye=.120f;
        public static readonly SourceHeadConvex Instance=new SourceHeadConvex();
        readonly Float3[] vertices=new Float3[168];
        private SourceHeadConvex()
        {
            for(int ring=0;ring<7;ring++)
            for(int segment=0;segment<24;segment++)
            {
                double e=ring*Math.PI/12, a=2*Math.PI*segment/24;
                // Godot -> Unity Z reflection exactly once.
                vertices[ring*24+segment]=new Float3((float)(Math.Cos(a)*Math.Cos(e)*Radius),
                    (float)(Math.Sin(e)*TopToEye),(float)(-Math.Sin(a)*Math.Cos(e)*Radius));
            }
        }
        public Float3[] CopyVertices() { return (Float3[])vertices.Clone(); }
        public static bool ValidPose(ConvexHeadPose p)
        {
            double n=(double)p.Qx*p.Qx+(double)p.Qy*p.Qy+(double)p.Qz*p.Qz+(double)p.Qw*p.Qw;
            return V.Within(p.WorldOffset,new Float3(),1)&&!double.IsNaN(n)&&!double.IsInfinity(n)&&Math.Abs(n-1)<.0001;
        }
        public static Float3 Rotate(Float3 p,ConvexHeadPose q)
        {
            // Normalize within accepted roundoff to keep support distances coherent.
            double n=Math.Sqrt((double)q.Qx*q.Qx+(double)q.Qy*q.Qy+(double)q.Qz*q.Qz+(double)q.Qw*q.Qw);
            double x=q.Qx/n,y=q.Qy/n,z=q.Qz/n,w=q.Qw/n;
            return new Float3((float)((1-2*y*y-2*z*z)*p.X+(2*x*y-2*z*w)*p.Y+(2*x*z+2*y*w)*p.Z),
                (float)((2*x*y+2*z*w)*p.X+(1-2*x*x-2*z*z)*p.Y+(2*y*z-2*x*w)*p.Z),
                (float)((2*x*z-2*y*w)*p.X+(2*y*z+2*x*w)*p.Y+(1-2*x*x-2*y*y)*p.Z));
        }
        public Float3[] WorldVertices(Float3 head,ConvexHeadPose pose)
        {
            if(!V.WorldPoint(head)||!ValidPose(pose)) throw new ArgumentException("Invalid convex pose");
            var result=new Float3[vertices.Length];var origin=V.Add(head,pose.WorldOffset);
            for(int i=0;i<result.Length;i++) result[i]=V.Add(origin,Rotate(vertices[i],pose));
            return result;
        }
        public Float3 Support(Float3 direction,ConvexHeadPose pose)
        {
            if(!direction.IsFinite||!ValidPose(pose)) throw new ArgumentException("Invalid convex support");
            double best=double.NegativeInfinity;var result=new Float3();
            foreach(var v in vertices)
            {
                var p=Rotate(v,pose);double d=(double)p.X*direction.X+(double)p.Y*direction.Y+(double)p.Z*direction.Z;
                if(d>best){best=d;result=p;}
            }
            return result;
        }
    }
}
