using System;using Ashfall.Core;
namespace Ashfall.Integration.Playable.UI
{
    public sealed class R26DamageDirectionState
    {
        public const int Capacity=4;public const double Seconds=1;
        readonly Float3[] sources=new Float3[Capacity];readonly double[] until=new double[Capacity];
        public Float3 Source(int i){return sources[i];}
        public float Remaining(int i,double now){return (float)Math.Max(0,Math.Min(1,until[i]-now));}
        public void Clear(){Array.Clear(until,0,until.Length);}
        public void Notify(Float3 source,Float3 eye,double now)
        {
            if(!source.IsFinite||!eye.IsFinite||Double.IsNaN(now)||Double.IsInfinity(now))return;
            int slot=-1;double oldest=Double.MaxValue;float bearing=Bearing(eye,new Float3(0,0,1),source);
            for(int i=0;i<Capacity;i++){
                if(Remaining(i,now)>0){float other=Bearing(eye,new Float3(0,0,1),sources[i]);if(Math.Cos(bearing-other)>.9781476){slot=i;break;}}
                if(until[i]<oldest){oldest=until[i];slot=i;}
            }
            sources[slot]=source;until[slot]=now+Seconds;
        }
        public static float Bearing(Float3 eye,Float3 forward,Float3 source)
        {
            double x=source.X-eye.X,z=source.Z-eye.Z,fx=forward.X,fz=forward.Z;if(x*x+z*z<.000001)return 0;
            double length=Math.Sqrt(fx*fx+fz*fz);if(length<.000001){fx=0;fz=1;length=1;}
            return (float)Math.Atan2((x*fz-z*fx)/length,(x*fx+z*fz)/length);
        }
    }
}
