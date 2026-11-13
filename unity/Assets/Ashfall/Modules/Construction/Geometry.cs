using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Core;

namespace Ashfall.Modules.Construction
{
    public sealed class Panel
    {
        public readonly int Tier, Variant;
        internal readonly Float3? BoxHalf;
        public string PieceName {get{return Kind=="floor"?"FLOOR":Variant==1?"HALF WALL":Variant==2?"WINDOW":Variant==3?"DOORWAY":"WALL";}}
        public int Cost {get{return Tier+1;}}
        public float MaximumHealth {get{return (Kind=="floor"?140:Variant==1?110:Variant==2?160:Variant==3?150:180)*(Tier==0?1:Tier==1?1.75f:2.75f);}}
        public readonly string Kind;
        public readonly Float3 Center;
        public readonly int Axis;
        public readonly bool LowerSupport;
        public readonly float Distance;
        public Panel(string kind, Float3 center, int axis, bool support, float distance)
         : this(kind,center,axis,support,distance,0) {}
        public Panel(string kind, Float3 center, int axis, bool support, float distance,int tier)
        : this(kind,center,axis,support,distance,tier,0) {}
        public Panel(string kind, Float3 center, int axis, bool support, float distance,int tier,int variant)
        { if(variant<0||variant>3||kind=="floor"&&variant!=0)throw new ArgumentOutOfRangeException("variant");Variant=variant; if(tier<0||tier>2)throw new ArgumentOutOfRangeException("tier");Tier=tier;Kind = kind; Center = center; Axis = axis; LowerSupport = support; Distance = distance; }
        internal Panel(Panel source,Float3 center,Float3 half) : this(source.Kind,center,source.Axis,source.LowerSupport,source.Distance,source.Tier,0) {BoxHalf=half;}
        public string Slot
        {
            get
            {
                // Source slot identity in Godot coordinates; reflect Z exactly once.
                return String.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}:{4}", Kind,
                    Geometry.Round(Center.X * 2 / Geometry.Side), Geometry.Round(Center.Y * 2 / Geometry.Side),
                    Geometry.Round(-Center.Z * 2 / Geometry.Side), Axis == 0 ? 1 : 0);
            }
        }
    }
    public static class Geometry
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
        public static Float3 HalfSize(Panel p)
        { if(p.BoxHalf.HasValue)return p.BoxHalf.Value;return p.Axis==0?new Float3(.04f,Side/2,Side/2):p.Axis==1?new Float3(Side/2,.04f,Side/2):new Float3(Side/2,Side/2,.04f); }
        // Sub-boxes retain the port type for legacy world adapters. They are not face slots.
        public static Panel[] Boxes(Panel p)
        {
            if(p.BoxHalf.HasValue||p.Kind=="floor"||p.Variant==0)return new[]{p};
            float side=Side,bottom=p.Center.Y-side/2;
            var boxes=new List<Panel>();
            if(p.Variant==1)AddBox(boxes,p,0,bottom+1.41f/2,side,1.41f);
            else {
                float width=p.Variant==2?1.30f:1.10f, sill=p.Variant==2?1.05f:0, height=p.Variant==2?.95f:2.10f;
                float rail=(side-width)/2;
                AddBox(boxes,p,-(width+rail)/2,p.Center.Y,rail,side);
                AddBox(boxes,p,(width+rail)/2,p.Center.Y,rail,side);
                if(sill>0)AddBox(boxes,p,0,bottom+sill/2,width,sill);
                float top=side-sill-height;
                AddBox(boxes,p,0,bottom+sill+height+top/2,width,top);
            }
            return boxes.ToArray();
        }
        static void AddBox(List<Panel> boxes,Panel p,float across,float y,float width,float height)
        {
            boxes.Add(new Panel(p,new Float3(p.Center.X+(p.Axis==2?across:0),y,p.Center.Z+(p.Axis==0?across:0)),
                p.Axis==0?new Float3(.04f,height/2,width/2):new Float3(width/2,height/2,.04f)));
        }
        public static bool Contains(Panel p, Float3 point, float margin)
        {
            foreach(var box in Boxes(p)){
                Float3 h=HalfSize(box), d=Sub(point,box.Center);
                if(Math.Abs(d.X)<=h.X+margin && Math.Abs(d.Y)<=h.Y+margin && Math.Abs(d.Z)<=h.Z+margin)return true;
            }return false;
        }
        public static bool Connected(Panel a, Panel b)
        {
            foreach(var x in Boxes(a))foreach(var y in Boxes(b))if(BoxesConnected(x,y))return true;
            return false;
        }
        static bool BoxesConnected(Panel a,Panel b)
        {
            Float3 ah=HalfSize(a),bh=HalfSize(b); float largest=0;
            for(int i=0;i<3;i++){
                float overlap=Math.Min(C(a.Center,i)+C(ah,i),C(b.Center,i)+C(bh,i))-Math.Max(C(a.Center,i)-C(ah,i),C(b.Center,i)-C(bh,i));
                if(overlap < -ContactTolerance)return false;
                largest=Math.Max(largest,overlap);
            }return largest>=.25f;
        }
        public static float Ray(Float3 origin,Float3 direction,Panel panel,float range,float margin)
        {
            float nearest=Single.PositiveInfinity;
            foreach(var box in Boxes(panel)){
                var size=HalfSize(box);float near=0,far=range;
                float[] o={origin.X-box.Center.X,origin.Y-box.Center.Y,origin.Z-box.Center.Z},d={direction.X,direction.Y,direction.Z},h={size.X+margin,size.Y+margin,size.Z+margin};
                bool hit=true;
                for(int i=0;i<3;i++){
                    if(Math.Abs(d[i])<.000001f){if(Math.Abs(o[i])>h[i]){hit=false;break;}continue;}
                    float a=(-h[i]-o[i])/d[i],b=(h[i]-o[i])/d[i];if(a>b){float t=a;a=b;b=t;}
                    near=Math.Max(near,a);far=Math.Min(far,b);if(near>far){hit=false;break;}
                }if(hit)nearest=Math.Min(nearest,near);
            }return nearest;
        }
        internal static Panel Lower(Panel p)
        {
            return p.Kind=="wall" ? new Panel("wall",Add(p.Center,new Float3(0,-Side,0)),p.Axis,true,p.Distance) :
                new Panel("wall",Add(p.Center,new Float3(0,-Side/2,Side/2)),2,true,p.Distance);
        }
    }
}
