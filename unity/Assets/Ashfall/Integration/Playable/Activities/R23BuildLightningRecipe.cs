using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.Activities
{
    // Original build_lightning_085.gd (its current recipe is BuildLightning088).
    public sealed class R23BuildLightningRecipe
    {
        public const int StrandCount=10;
        public const float HalfWidth=.004f,MaximumPeriod=.4572f;
        public readonly List<Float3> Vertices=new List<Float3>(4096);
        public readonly List<uint> Colors=new List<uint>(4096);
        public readonly List<float> Spacings=new List<float>(320);
        public readonly List<Float3> FirstStrand=new List<Float3>(40);
        public void Clear(){Vertices.Clear();Colors.Clear();Spacings.Clear();FirstStrand.Clear();}
        public bool Build(Float3 unityStart,Float3 unityEnd,bool enabled,double milliseconds)
        {
            Clear();if(!enabled||!unityStart.IsFinite||!unityEnd.IsFinite||Double.IsNaN(milliseconds)||Double.IsInfinity(milliseconds))return false;
            // Compute the original recipe in Godot coordinates, then reflect vertices once.
            Float3 start=Reflect(unityStart),end=Reflect(unityEnd),ray=Sub(end,start);float length=Length(ray);
            if(length<.0001f||length>6.001f)return false;
            Float3 axis=Mul(ray,1/length),side=Cross(axis,new Float3(0,1,0));
            if(Dot(side,side)<.001f)side=Cross(axis,new Float3(1,0,0));side=Unit(side);Float3 up=Unit(Cross(side,axis));
            double clock=milliseconds*.006;long tick=(long)Math.Floor(clock);float fraction=(float)(clock-Math.Floor(clock));float blend=fraction*fraction*(3-2*fraction);
            float minimumPeriod=(float)Math.Max(.025,Math.Min(MaximumPeriod*.5,length*(Math.PI*2)/(12*2.4)));
            for(int strand=0;strand<StrandCount;strand++)
            {
                uint color=strand<6?0x58ff44u:strand<8?0x050706u:0xffffffu;
                var spacing=new SourceRandom((ulong)(88271+strand*1471));Float3 previous=start;float distance=0;int knot=0;
                if(strand==0)FirstStrand.Add(Reflect(start));
                while(distance<length)
                {
                    float step=Math.Min(spacing.Between(minimumPeriod,MaximumPeriod),length-distance);distance+=step;Spacings.Add(step);
                    float t=distance/length,envelope=Math.Min(1,Math.Min(t,1-t)*6);Float3 before=Noise(strand,knot,tick),after=Noise(strand,knot,tick+1);
                    Float3 irregular=Add(before,Mul(Sub(after,before),blend));
                    Float3 offset=Mul(Add(Mul(side,irregular.X),Mul(up,irregular.Y)),Math.Min(.12f,length*.075f)*envelope);
                    Float3 next=distance>=length-.00001f?end:Add(Add(start,Mul(axis,distance)),offset);knot++;
                    if(strand==0)FirstStrand.Add(Reflect(next));
                    Ribbon(previous,next,Mul(side,HalfWidth),color);Ribbon(previous,next,Mul(up,HalfWidth),color);previous=next;
                    if(knot>256){Clear();return false;}
                }
            }
            return true;
        }
        void Ribbon(Float3 a,Float3 b,Float3 width,uint color)
        {Vertex(Sub(a,width),color);Vertex(Add(a,width),color);Vertex(Add(b,width),color);Vertex(Sub(a,width),color);Vertex(Add(b,width),color);Vertex(Sub(b,width),color);}
        void Vertex(Float3 point,uint color){Vertices.Add(Reflect(point));Colors.Add(color);}
        static Float3 Noise(int strand,int knot,long tick)
        {var rng=new SourceRandom(unchecked((ulong)(88217+strand*15737+knot*379+tick*577)));return new Float3(rng.Between(-1,1),rng.Between(-1,1),0);}
        // Godot RandomPCG: default increment, PCG32 and CLZ-based single-real randf.
        public struct SourceRandom
        {
            ulong state;const ulong Increment=2885390081777926815UL;
            public SourceRandom(ulong seed){state=0;Next();state=unchecked(state+seed);Next();}
            public uint Next(){ulong old=state;state=unchecked(old*6364136223846793005UL+Increment);uint x=(uint)(((old>>18)^old)>>27);int r=(int)(old>>59);return(x>>r)|(x<<((-r)&31));}
            public float Value(){uint exponent=Next();if(exponent==0)return 0;int leading=0;while((exponent&0x80000000u)==0){leading++;exponent<<=1;}return(float)((float)(Next()|0x80000001u)*Math.Pow(2,-32-leading));}
            public float Between(float a,float b){return Value()*(b-a)+a;}
        }
        static Float3 Reflect(Float3 a){return new Float3(a.X,a.Y,-a.Z);}
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        static Float3 Mul(Float3 a,float s){return new Float3(a.X*s,a.Y*s,a.Z*s);}
        static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        static Float3 Cross(Float3 a,Float3 b){return new Float3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
        static float Length(Float3 a){return(float)Math.Sqrt(Dot(a,a));}
        static Float3 Unit(Float3 a){return Mul(a,1/Length(a));}
    }
}
