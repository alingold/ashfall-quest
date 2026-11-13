using System;
using System.IO;
using Ashfall.Integration.AudioRuntime.Quarantine;
public static class CompareClimbOracle
{
    public static int Main(string[] args)
    {
        if(args.Length!=1)throw new ArgumentException("Root-produced oracle directory required");
        for(int v=0;v<2;v++)
        {
            string prefix=Path.Combine(args[0],"climb_"+v.ToString("00"));
            var bytes=File.ReadAllBytes(prefix+".noise-f64le");var expected=File.ReadAllBytes(prefix+".pcm-s16le");
            if(bytes.Length!=76800||expected.Length!=19200)throw new InvalidDataException("Oracle shape");
            var noise=new double[9600];for(int i=0;i<noise.Length;i++)noise[i]=BitConverter.ToDouble(bytes,i*8);
            var actual=ClimbFromOracleNoise.Synthesize(v,noise);int different=0,max=0;
            for(int i=0;i<actual.Length;i++){int delta=Math.Abs(actual[i]-BitConverter.ToInt16(expected,i*2));if(delta!=0)different++;max=Math.Max(max,delta);}
            Console.WriteLine("variant="+v+" unequal="+different+" maxPCM16Delta="+max);
            if(different!=0)return 1;
        }
        Console.WriteLine("Exact PCM comparison PASS; still quarantined pending approval/listening");return 0;
    }
}
