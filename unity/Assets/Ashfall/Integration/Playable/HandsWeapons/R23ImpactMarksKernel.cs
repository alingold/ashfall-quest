using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public struct R23ImpactSurface
    {
        public object Source;
        public bool Static,AllowedLayer,DamageReceiver,HitReceiver,Excluded,Rendered,Painted;
        public bool Eligible {get{return Source!=null&&Static&&AllowedLayer&&!DamageReceiver&&!Excluded;}}
    }
    public struct R23ImpactSample
    {
        public bool Found;
        public Float3 Point,Normal;
        public R23ImpactSurface Surface;
    }
    public interface IR23ImpactProjection { R23ImpactSample Cast(Float3 from,Float3 to); }
    public sealed class R23ImpactPatch
    {
        // World-space positions, including the source3mm normal offset. Renderer
        // stores these relative to Origin; no second aim or tracked-pose authority.
        public Float3 Origin;
        public Float3[] Points=new Float3[9];
        public float[] U=new float[9],V=new float[9];
        public float Diameter,Angle;
        public int Rays;
    }
    // Direct port of original impact_marks.gd, including its reflected Godot basis.
    public static class R23ImpactMarksKernel
    {
        public const int Capacity=512,TextureSize=64;
        public const float Lifetime=90,FadeTime=10,RifleDiameter=.085f,PelletDiameter=.045f,SurfaceOffset=.003f;
        public static readonly int[] Indices={0,3,1,1,3,4,1,4,2,2,4,5,3,6,4,4,6,7,4,7,5,5,7,8};
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        public static Float3 Scale(Float3 a,float b){return new Float3(a.X*b,a.Y*b,a.Z*b);}
        static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        static Float3 Cross(Float3 a,Float3 b){return new Float3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
        static float Length(Float3 a){return (float)Math.Sqrt(Dot(a,a));}
        static Float3 Unit(Float3 a){return Scale(a,1/Length(a));}
        static Float3 Mix(Float3 a,Float3 b,float t){return Add(a,Scale(Sub(b,a),t));}
        public static float Diameter(long sequence,bool pellet){return (pellet?PelletDiameter:RifleDiameter)*(.9f+(sequence%5)*.05f);}
        public static float Alpha(float age){return age<0||age>=Lifetime?0:Math.Max(0,Math.Min(1,(Lifetime-age)/FadeTime));}
        public static Float3 Tangent(Float3 normal){var n=Unit(normal);var helper=Math.Abs(n.Y)>.9f?new Float3(1,0,0):new Float3(0,1,0);return Scale(Unit(Cross(helper,n)),-1);}
        static R23ImpactSample Cast(IR23ImpactProjection query,Float3 p,Float3 n,R23ImpactPatch patch){patch.Rays++;return query.Cast(Add(p,Scale(n,2)),Sub(p,Scale(n,2)));}
        static bool Same(object a,object b){return object.Equals(a,b);}
        public static bool TryBuild(Float3 point,Float3 normal,R23ImpactSurface source,bool pellet,long sequence,IR23ImpactProjection query,out R23ImpactPatch patch)
        {
            patch=null;if(!point.IsFinite||!normal.IsFinite||Dot(normal,normal)<.5f||!source.Eligible||query==null||sequence<0)return false;
            var result=new R23ImpactPatch();var n=Unit(normal);var t=Tangent(n);var b=Scale(Cross(n,t),-1);
            float angle=(float)(((sequence%360)*137)%360*Math.PI/180),c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            var xAxis=Add(Scale(t,c),Scale(b,s));var yAxis=Add(Scale(t,-s),Scale(b,c));
            result.Diameter=Diameter(sequence,pellet);result.Angle=angle;result.Origin=Add(point,Scale(n,SurfaceOffset));
            for(int y=0;y<3;y++)for(int x=0;x<3;x++){
                int index=y*3+x;float ox=x*.5f-.5f,oy=y*.5f-.5f;
                var sample=Add(point,Add(Scale(xAxis,ox*result.Diameter),Scale(yAxis,oy*result.Diameter)));
                var hit=Cast(query,sample,n,result);
                bool paintEdge=source.Painted&&hit.Found&&hit.Surface.Static&&!hit.Surface.DamageReceiver&&!hit.Surface.HitReceiver&&!hit.Surface.Excluded&&Math.Abs(n.Y)>.9f;
                float edgeFraction=1;
                if(source.Rendered&&!paintEdge&&(!hit.Found||!Same(hit.Surface.Source,source.Source))){
                    var insideHit=Cast(query,point,n,result);
                    if(insideHit.Found&&Same(insideHit.Surface.Source,source.Source)){
                        float inside=0,outside=1;
                        for(int step=0;step<8;step++){
                            float fraction=(inside+outside)*.5f;var candidate=Mix(point,sample,fraction);var candidateHit=Cast(query,candidate,n,result);
                            if(candidateHit.Found&&Same(candidateHit.Surface.Source,source.Source)&&Dot(candidateHit.Normal,n)>.7f){inside=fraction;insideHit=candidateHit;}else outside=fraction;
                        }
                        edgeFraction=inside;hit=insideHit;sample=Mix(point,sample,inside);
                    }
                }
                if(!hit.Found||(!Same(hit.Surface.Source,source.Source)&&!paintEdge)||!hit.Point.IsFinite||!hit.Normal.IsFinite||Dot(hit.Normal,n)<.7f||Length(Sub(hit.Point,sample))>result.Diameter)return false;
                result.Points[index]=Add(hit.Point,Scale(hit.Normal,SurfaceOffset));result.U[index]=.5f+ox*edgeFraction;result.V[index]=.5f-oy*edgeFraction;
            }
            patch=result;return true;
        }
        public static void TexturePixel(int x,int y,out float red,out float green,out float blue,out float alpha)
        {
            if(x<0||x>=64||y<0||y>=64)throw new ArgumentOutOfRangeException("pixel");
            float px=(x-31.5f)/32,py=(y-31.5f)/32,radius=(float)Math.Sqrt(px*px+py*py),angle=(float)Math.Atan2(py,px);
            float edge=.67f+(float)Math.Sin(angle*7)*.12f+(float)Math.Sin(angle*13+.8f)*.07f;
            alpha=Math.Max(0,Math.Min(1,(edge-radius)*18));float tone=radius<.32f?.025f:.62f+.12f*(float)Math.Sin(angle*9+radius*15);
            red=tone;green=tone*.94f;blue=tone*.86f;
        }
    }
    public sealed class R23ImpactMarksState
    {
        readonly float[] ages=new float[R23ImpactMarksKernel.Capacity];readonly object[] sources=new object[R23ImpactMarksKernel.Capacity];
        public int Cursor {get;private set;}public long Sequence {get;private set;}public long Accepted {get;private set;}public long Rejected {get;private set;}
        public bool Paused {get;set;}
        public R23ImpactMarksState(){Clear();}
        public float Age(int index){return ages[index];}public object Source(int index){return sources[index];}
        public int Count {get{int count=0;foreach(float age in ages)if(age>=0)count++;return count;}}
        public void Reject(){Rejected++;}
        public int Commit(object source){if(source==null)throw new ArgumentNullException("source");int slot=Cursor;ages[slot]=0;sources[slot]=source;Cursor=(Cursor+1)%ages.Length;Sequence++;Accepted++;return slot;}
        public void Advance(float seconds,Func<object,bool> sourceAlive)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds))throw new ArgumentOutOfRangeException("seconds");if(sourceAlive==null)throw new ArgumentNullException("sourceAlive");if(Paused)return;
            for(int i=0;i<ages.Length;i++)if(ages[i]>=0){if(!sourceAlive(sources[i])){ages[i]=-1;sources[i]=null;continue;}ages[i]+=Math.Max(0,seconds);if(ages[i]>=R23ImpactMarksKernel.Lifetime){ages[i]=-1;sources[i]=null;}}
        }
        // Source clear_all keeps deterministic fracture sequence and diagnostic counts.
        public void Clear(){for(int i=0;i<ages.Length;i++){ages[i]=-1;sources[i]=null;}Cursor=0;}
    }
}
