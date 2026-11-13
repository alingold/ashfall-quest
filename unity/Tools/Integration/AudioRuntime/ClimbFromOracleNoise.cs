using System;
namespace Ashfall.Integration.AudioRuntime.Quarantine
{
    // Test-only candidate, never routed as authored/approved content. Noise must be
    // captured from the pinned Godot RNG oracle; RNG equivalence is still OPEN.
    public static class ClimbFromOracleNoise
    {
        public static short[] Synthesize(int variant,float[] noise)
        {if(noise==null)throw new ArgumentNullException("noise");var values=new double[noise.Length];for(int i=0;i<values.Length;i++)values[i]=noise[i];return Synthesize(variant,values);}
        public static short[] Synthesize(int variant,double[] noise)
        {
            if(variant<0||variant>1||noise==null||noise.Length!=9600)throw new ArgumentException("Pinned climb oracle input");
            const double duration=.30,rate=32000;var samples=new float[9600];double low=0,mid=0,peak=0,detune=1+(variant-1)*.027;
            for(int i=0;i<samples.Length;i++)
            {
                double n=noise[i];if(double.IsNaN(n)||double.IsInfinity(n)||n < -1||n>1)throw new ArgumentException("Oracle noise range");
                double t=i/rate;low=low+(n-low)*.025;mid=mid+(n-mid)*.24;double high=n-mid;
                double s=(mid*.35+high*.08)*Math.Pow(Math.Sin(Math.PI*t/duration),2);
                s+=(low*.55+Math.Sin(2*Math.PI*121*detune*t)*.035)*Math.Exp(-Math.Pow((t-.14)/.035,2));
                s*=Math.Min(1,t/.002)*Math.Min(1,(duration-t)/.002);samples[i]=(float)s;peak=Math.Max(peak,Math.Abs(s));
            }
            double gain=.62/Math.Max(peak,.001);var result=new short[9600];for(int i=0;i<result.Length;i++)result[i]=(short)(samples[i]*gain*32767);return result;
        }
    }
}
