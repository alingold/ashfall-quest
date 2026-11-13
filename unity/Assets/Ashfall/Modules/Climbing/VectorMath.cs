using System;
using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    internal static class V
    {
        internal static Float3 Add(Float3 a, Float3 b) { return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        internal static Float3 Sub(Float3 a, Float3 b) { return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        internal static Float3 Mul(Float3 a, float b) { return new Float3(a.X*b,a.Y*b,a.Z*b); }
        internal static Float3 Up(float y) { return new Float3(0,y,0); }
        internal static float Length(Float3 a) { return (float)Math.Sqrt((double)a.X*a.X+(double)a.Y*a.Y+(double)a.Z*a.Z); }
        internal static Float3 Limit(Float3 a, float n) { double l=Math.Sqrt((double)a.X*a.X+(double)a.Y*a.Y+(double)a.Z*a.Z); return l>n ? new Float3((float)(a.X*n/l),(float)(a.Y*n/l),(float)(a.Z*n/l)) : a; }
        // Explicit local tracking and world-coordinate bounds prevent derived overflow before queries.
        internal static bool WorldPoint(Float3 p) { return p.IsFinite && Math.Abs((double)p.X)<=100000 && Math.Abs((double)p.Y)<=100000 && Math.Abs((double)p.Z)<=100000; }
        internal static bool Within(Float3 a,Float3 b,double radius)
        {
            double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;
            return a.IsFinite && b.IsFinite && x*x+y*y+z*z<=radius*radius;
        }
        internal static Float3 MeanPull(Float3[] anchors,Float3[] hands,bool[] held,double limit)
        {
            double x=0,y=0,z=0; int count=0;
            for(int i=0;i<2;i++) if(held[i]) { x+=(double)anchors[i].X-hands[i].X; y+=(double)anchors[i].Y-hands[i].Y; z+=(double)anchors[i].Z-hands[i].Z; count++; }
            x/=count; y/=count; z/=count;
            double length=Math.Sqrt(x*x+y*y+z*z), scale=length>limit ? limit/length : 1;
            return new Float3((float)(x*scale),(float)(y*scale),(float)(z*scale));
        }
        internal static bool Finite(float a) { return !float.IsNaN(a)&&!float.IsInfinity(a); }
    }
}
