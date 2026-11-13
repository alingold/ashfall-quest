using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Explosives
{
    // Published R5 explosive_effect.gd. Every returned vector is reflected once
    // from Godot local coordinates; the actor/health owner is never touched.
    public static class R24ExplosionMotion
    {
        public const float Lifetime=2.5f;
        static Float3 P(float x,float y,float z){return new Float3(x,y,-z);}
        static Float3 Direction(int i,float y,float offset=0){float phi=i*2.399963f+offset,n=(float)Math.Sqrt(1+y*y);return P((float)Math.Cos(phi)/n,y/n,(float)Math.Sin(phi)/n);}
        static Float3 Mul(Float3 p,float n){return new Float3(p.X*n,p.Y*n,p.Z*n);}
        static Float3 Up(Float3 p,float y){return new Float3(p.X,p.Y+y,p.Z);}
        static float Expansion(float age){return 1-(float)Math.Exp(-age*9);}
        static float Size(bool rocket){return rocket?1.12f:1;}
        public static float CoreScale(float age,bool rocket=false){return (.32f+Expansion(age)*1.75f)*Size(rocket);}
        public static Float3 FirePosition(float age,int i,bool rocket=false){return Mul(Up(Mul(Direction(i,.08f+(i%3)*.24f),.08f+Expansion(age)*.87f),age*.5f),Size(rocket));}
        public static Float3 FireScale(float age,int i,bool rocket=false){float r=(.25f+Expansion(age)*(1.1f+(i%3)*.14f))*Size(rocket);return new Float3(r,r*(1.08f+(i%2)*.18f),r);}
        public static Float3 SmokePosition(float age,int i,bool rocket=false){return Mul(Up(Mul(Direction(i,.18f+(i%4)*.23f),.20f+age*1.15f),.10f+age*.64f),Size(rocket));}
        public static Float3 SmokeScale(float age,int i,bool rocket=false){float r=(.12f+(float)Math.Sqrt(Math.Max(0,age))*(1+(i%3)*.16f))*Size(rocket);return new Float3(r,r*1.12f,r);}
        public static Float3 EmberDirection(int i){return Direction(i,.05f+(i%7)*.13f);}
        public static Float3 EmberPosition(float age,int i){return Up(Mul(EmberDirection(i),age*(4+(i%5)*1.1f)),-age*age*3);}
        public static Float3 DebrisPosition(float age,int i){return Up(Mul(Direction(i,.3f+(i%3)*.3f,.7f),age*(3.4f+(i%4)*.72f)),-age*age*3.6f);}
        public static Float3 DebrisEuler(float age,int i){return new Float3((2.1f+i*.17f)*age,(-3.3f+i*.21f)*age,(1.6f+i*.13f)*age);}
        public static Float3 ZoneDirection(int i){float y=1-2*(i+.5f)/28,phi=i*2.399963f,r=(float)Math.Sqrt(1-y*y);return P((float)Math.Cos(phi)*r,y,(float)Math.Sin(phi)*r);}
        public static Float3 ZoneSparkPosition(float age,int i){return Up(Mul(ZoneDirection(i),age*8),-age*age*3);}
        public static float ZoneRingScale(float age,int i){return .2f+age*(4.5f+i);}
        public static float ZoneFade(float age){return Math.Max(0,1-age/1.8f);}
        public static float EmberFade(float age){return Math.Max(0,1-age/1.65f);}
        public static float DebrisFade(float age){return Math.Max(0,1-Math.Max(0,age-.55f)/1.7f);}
        public static float LightEnergy(float age){return Math.Max(0,3-age*9);}
    }
}
