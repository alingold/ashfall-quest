using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.Playable.JumpPads
{
    public sealed class R23SourcePad
    {
        public readonly LaunchPad Pad;public readonly double Radius;public readonly bool Tilted;
        public R23SourcePad(string id,Float3 center,Float3 normal,double radius,float speed,bool tilted)
        {Pad=new LaunchPad(id,center,normal,(float)radius,speed);Radius=radius;Tilted=tilted;}
    }
    // Current map_semantics_r14.gd rounded witness search. No enlarged volume or projected feet.
    public static class R23PadContactMath
    {
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        static Float3 Mul(Float3 a,float k){return new Float3(a.X*k,a.Y*k,a.Z*k);}
        static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        static double Clamp(double x,double lo,double hi){return Math.Max(lo,Math.Min(hi,x));}
        static bool Finite(double x){return !Double.IsNaN(x)&&!Double.IsInfinity(x);}
        static uint Bits(float x){return BitConverter.ToUInt32(BitConverter.GetBytes(x),0);}
        static float Value(uint bits){return BitConverter.ToSingle(BitConverter.GetBytes(bits),0);}
        public static bool Contains(Float3 center,Float3 normal,double radius,Float3 point)
        {
            if(!point.IsFinite||!center.IsFinite||!normal.IsFinite||!Finite(radius)||radius<=0)return false;
            Float3 delta=Sub(point,center);float signed=Dot(delta,normal);Float3 lateral=Sub(delta,Mul(normal,signed));
            return (double)signed>=-.28&&(double)signed<=.75&&(double)Dot(lateral,lateral)<=radius*radius;
        }
        static bool Bounds(Float3 p,Float3 a,Float3 b)
        {return p.IsFinite&&p.X>=Math.Min(a.X,b.X)&&p.X<=Math.Max(a.X,b.X)&&p.Y>=Math.Min(a.Y,b.Y)&&p.Y<=Math.Max(a.Y,b.Y)&&p.Z>=Math.Min(a.Z,b.Z)&&p.Z<=Math.Max(a.Z,b.Z);}
        public static bool TrySwept(Float3 center,Float3 normal,double radius,Float3 start,Float3 end,out Float3 contact)
        {
            contact=new Float3();if(!center.IsFinite||!normal.IsFinite||!start.IsFinite||!end.IsFinite||!Finite(radius)||radius<=0)return false;
            Float3 motion=Sub(end,start);float length2=Dot(motion,motion);
            if(length2>=.000001f)
            {
                Float3 legacy=Add(start,Mul(motion,(float)Clamp(Dot(Sub(center,start),motion)/length2,0,1)));
                if(Contains(center,normal,radius,legacy)){contact=legacy;return true;}
            }
            float reach=(float)(radius+.76);
            if(center.X<Math.Min(start.X,end.X)-reach||center.X>Math.Max(start.X,end.X)+reach||center.Y<Math.Min(start.Y,end.Y)-reach||center.Y>Math.Max(start.Y,end.Y)+reach||center.Z<Math.Min(start.Z,end.Z)-reach||center.Z>Math.Max(start.Z,end.Z)+reach)return false;
            double dx=(double)start.X-center.X,dy=(double)start.Y-center.Y,dz=(double)start.Z-center.Z,mx=(double)end.X-start.X,my=(double)end.Y-start.Y,mz=(double)end.Z-start.Z;
            double h=dx*normal.X+dy*normal.Y+dz*normal.Z,dh=mx*normal.X+my*normal.Y+mz*normal.Z,low=0,high=1;
            if(dh!=0){double a=(-.28-h)/dh,b=(.75-h)/dh;low=Math.Max(low,Math.Min(a,b));high=Math.Min(high,Math.Max(a,b));}
            else if(h<-.28||h>.75){low=1;high=0;}
            double rx=dx-normal.X*h,ry=dy-normal.Y*h,rz=dz-normal.Z*h,vx=mx-normal.X*dh,vy=my-normal.Y*dh,vz=mz-normal.Z*dh;
            double aa=vx*vx+vy*vy+vz*vz,closest=aa>0?-(rx*vx+ry*vy+rz*vz)/aa:.5;
            var seeds=new List<double>{.5,0,1,Clamp(closest,0,1)};
            if(low<=high)
            {
                seeds.Add(Clamp(closest,low,high));seeds.Add((low+high)*.5);seeds.Add(low);seeds.Add(high);
                if(aa>0)
                {
                    double x=rx+vx*closest,y=ry+vy*closest,z=rz+vz*closest,room=radius*radius-(x*x+y*y+z*z);
                    if(room>=0){double width=Math.Sqrt(room/aa),enter=Math.Max(low,closest-width),leave=Math.Min(high,closest+width);if(enter<=leave){seeds.Add((enter+leave)*.5);seeds.Add(enter);seeds.Add(leave);}}
                }
            }
            var times=new List<double>();var seen=new HashSet<double>();int[] steps={0,-1,1,-2,2,-3,3,-4,4,-8,8,-16,16,-32,32,-64,64};
            foreach(double seed in seeds)
            {
                if(!Finite(seed)||seed<0||seed>1)continue;if(seen.Add(seed))times.Add(seed);long bits=Bits((float)seed);
                foreach(int step in steps){long adjacent=bits+step;if(adjacent<0||adjacent>1065353216)continue;double t=Value((uint)adjacent);if(seen.Add(t))times.Add(t);}
            }
            double[] coordinateSteps={-4,-2,-1,-.5,-.25,.25,.5,1,2,4};
            foreach(double seed in seeds)
            {
                if(!Finite(seed)||seed<0||seed>1)continue;
                var quantized=new Float3((float)(start.X+mx*seed),(float)(start.Y+my*seed),(float)(start.Z+mz*seed));
                double[] componentMotion={mx,my,mz};float[] coordinates={quantized.X,quantized.Y,quantized.Z};
                for(int axis=0;axis<3;axis++)
                {
                    if(componentMotion[axis]==0)continue;float component=coordinates[axis];uint raw=Bits(component);float neighbor=Value(raw==0||raw==2147483648?1:raw+1);
                    double dt=Math.Abs((double)neighbor-component)/Math.Abs(componentMotion[axis]);if(!Finite(dt))continue;
                    foreach(double step in coordinateSteps){double t=seed+step*dt;if(Finite(t)&&t>=0&&t<=1&&seen.Add(t))times.Add(t);}
                }
            }
            Float3 reverse=Sub(start,end);
            foreach(double t in times)
            {
                Float3[] points={Add(start,Mul(motion,(float)t)),Add(end,Mul(reverse,(float)(1-t))),Add(Mul(start,(float)(1-t)),Mul(end,(float)t)),new Float3((float)(start.X+mx*t),(float)(start.Y+my*t),(float)(start.Z+mz*t))};
                foreach(var point in points)if(Bounds(point,start,end)&&Contains(center,normal,radius,point)){contact=point;return true;}
            }
            return false;
        }
    }
    public sealed class R23PadContacts
    {
        readonly R23SourcePad[] pads;readonly double[] cooldown;Float3 previous;bool hasPrevious;double lastNow=-1;
        public string LastPadId {get;private set;}public Float3 LastContact {get;private set;}public Float3 LastPadCenter {get;private set;}
        public R23PadContacts(R23SourcePad[] pads){if(pads==null)throw new ArgumentNullException("pads");this.pads=(R23SourcePad[])pads.Clone();cooldown=new double[pads.Length];}
        public void ResetContacts(){hasPrevious=false;}
        public bool TryLaunch(Float3 feet,double now,bool ready,bool climbing,bool mantling,bool ghost,Func<Float3,Float3,bool> clearContact,Func<Float3,float,bool> launch)
        {
            if(!feet.IsFinite||Double.IsNaN(now)||Double.IsInfinity(now)||now<0||now<lastNow||clearContact==null||launch==null)return false;
            lastNow=now;if(!ready||ghost)return false;
            Float3 from=previous;bool swept=hasPrevious;previous=feet;hasPrevious=true;
            if(climbing||mantling)return false;
            for(int i=0;i<pads.Length;i++)
            {
                if(now<cooldown[i])continue;var row=pads[i];Float3 contact=feet;
                if(!R23PadContactMath.Contains(row.Pad.Center,row.Pad.Normal,row.Radius,contact))
                {
                    float dx=feet.X-from.X,dy=feet.Y-from.Y,dz=feet.Z-from.Z;
                    if(!swept||dx*dx+dy*dy+dz*dz>225||!R23PadContactMath.TrySwept(row.Pad.Center,row.Pad.Normal,row.Radius,from,feet,out contact))continue;
                }
                Float3 n=row.Pad.Normal,c=row.Pad.Center;
                var face=new Float3(c.X+n.X*.2f,c.Y+n.Y*.2f,c.Z+n.Z*.2f);var end=new Float3(contact.X+n.X*.12f,contact.Y+n.Y*.12f,contact.Z+n.Z*.12f);
                if(!clearContact(face,end)||!launch(n,row.Pad.Speed))continue;
                cooldown[i]=now+1.2;LastPadId=row.Pad.Id;LastContact=contact;LastPadCenter=row.Pad.Center;return true;
            }
            return false;
        }
    }
}
