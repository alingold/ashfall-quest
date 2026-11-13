using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;
namespace Ashfall.Modules.ConstructionCandidate {
    public static class CandidateGeometry
    {
        public const float Side = (float)(4 * .6096 * (48.0 / 41.5));
        public const float Range = 6, WallMinimum = .4572f, ContactTolerance = .045f;
        internal static int Round(float x) { return (int)Math.Round(x, MidpointRounding.AwayFromZero); }
        internal static float C(Float3 v, int i) { return i == 0 ? v.X : i == 1 ? v.Y : v.Z; }
        internal static Float3 Add(Float3 a, Float3 b) { return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        internal static Float3 Mul(Float3 a, float s) { return new Float3(a.X*s,a.Y*s,a.Z*s); }
        internal static Float3 Sub(Float3 a, Float3 b) { return Add(a,Mul(b,-1)); }
        internal static float Length(Float3 v) { return (float)Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y+(double)v.Z*v.Z); }
        public static List<Panel> Intersections(Float3 origin, Float3 direction)
        {
            var result = new List<Panel>();
            float length = Length(direction);
            float squared = direction.X*direction.X + direction.Y*direction.Y + direction.Z*direction.Z;
            if (!origin.IsFinite || !direction.IsFinite || Length(origin)>100000 || float.IsInfinity(squared) || squared < .001f) return result;
            var ray = Mul(direction,1/length);
            // Select cells in source space: floor quantization does not commute
            // with reflection at exact boundaries, including a zero Z ray.
            origin = new Float3(origin.X,origin.Y,-origin.Z);
            ray = new Float3(ray.X,ray.Y,-ray.Z);
            // Bounded by placement range, independent of world coordinate magnitude.
            for (int axis=0;axis<3;axis++)
            {
                float r=C(ray,axis), o=C(origin,axis);
                if (Math.Abs(r)<.000001f) continue;
                double start=Math.Floor(Math.Min(o,o+r*Range)/Side)-1;
                double end=Math.Ceiling(Math.Max(o,o+r*Range)/Side)+1;
                for (double plane=start;plane<=end;plane++)
                {
                    float depth=(float)(plane*Side), distance=(depth-o)/r;
                    if(distance<-.00001f || distance>Range+.00001f || (axis!=1 && distance<WallMinimum-.00001f))continue;
                    Float3 point=Add(origin,Mul(ray,distance));
                    float[] center=new float[3];
                    for(int i=0;i<3;i++)center[i]=(float)((Math.Floor(C(point,i)/Side+C(ray,i)*.000001)+.5)*Side);
                    center[axis]=depth;
                    result.Add(new Panel(axis==1?"floor":"wall",new Float3(center[0],center[1],-center[2]),axis,false,distance));
                }
            }
            result.Sort(delegate(Panel a, Panel b) { return Math.Abs(a.Distance-b.Distance)<.00001f?a.Axis.CompareTo(b.Axis):a.Distance.CompareTo(b.Distance); });
            return result;
        }
        public static Float3 HalfSize(Panel p){return Geometry.HalfSize(p);}
        public static Panel[] Boxes(Panel p){return Geometry.Boxes(p);}
        public static bool Contains(Panel p,Float3 point,float margin){return Geometry.Contains(p,point,margin);}
        public static bool Connected(Panel a,Panel b){return Geometry.Connected(a,b);}
        internal static Panel Lower(Panel p)
        {
            return p.Kind=="wall" ? new Panel("wall",Add(p.Center,new Float3(0,-Side,0)),p.Axis,true,p.Distance) :
                new Panel("wall",Add(p.Center,new Float3(0,-Side/2,Side/2)),2,true,p.Distance);
        }
    }
}
