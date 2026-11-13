using System;
using Ashfall.Integration.DesktopPlaytest.Audio;
public static class Checks
{
    static int checks;
    static void Check(bool value,string name) { checks++; if(!value) throw new Exception(name); }
    public static void Main()
    {
        foreach(DesktopCue cue in new[]{DesktopCue.Hit,DesktopCue.Death})
        {
            var pcm=DesktopCuePcm.Create(cue); var copy=DesktopCuePcm.Create(cue);
            Check(pcm.Length==(cue==DesktopCue.Hit?2160:5280),"bounded sample count");
            Check(pcm[0]==0&&pcm[pcm.Length-1]==0,"silent edges");
            double energy=0,mean=0,maxDelta=0;
            for(int i=0;i<pcm.Length;i++)
            {
                Check(!Single.IsNaN(pcm[i])&&!Single.IsInfinity(pcm[i])&&Math.Abs(pcm[i])<=.160001,"finite headroom");
                Check(pcm[i]==copy[i],"deterministic");
                energy+=pcm[i]*pcm[i]; mean+=pcm[i];
                if(i>0) maxDelta=Math.Max(maxDelta,Math.Abs(pcm[i]-pcm[i-1]));
            }
            Check(energy/pcm.Length>.001&&energy/pcm.Length<.02,"audible energy bound, not listening");
            Check(Math.Abs(mean/pcm.Length)<.001,"near zero DC");
            Check(maxDelta<.02,"adjacent sample continuity");
            Console.WriteLine(cue+" samples="+pcm.Length+" rms="+Math.Sqrt(energy/pcm.Length)+" mean="+mean/pcm.Length+" maxDelta="+maxDelta);
        }
        Check((DesktopCuePcm.HitSamples+DesktopCuePcm.DeathSamples)*4==29760,"retained PCM bytes");
        bool invalid=false;try{DesktopCuePcm.Create(DesktopCue.None);}catch(ArgumentOutOfRangeException){invalid=true;}Check(invalid,"reject undefined cue");
        var gate=new DesktopCueGate();
        Check(gate.Observe(10,5,false,0)==DesktopCue.None,"initial snapshot silent");
        Check(gate.Observe(11,5,false,1)==DesktopCue.Hit,"real hit delta");
        Check(gate.Observe(500,5,false,1.01)==DesktopCue.None,"burst dropped");
        Check(gate.Observe(500,5,false,2)==DesktopCue.None,"no deferred burst");
        Check(gate.Observe(501,4,false,3)==DesktopCue.Death,"death wins simultaneous hit");
        Check(gate.Observe(502,3,false,3.1)==DesktopCue.None,"death throttle");
        Check(gate.Observe(503,3,true,4)==DesktopCue.None,"pause silent");
        Check(gate.Observe(503,3,false,5)==DesktopCue.None,"resume no replay");
        Check(gate.Observe(0,5,false,6)==DesktopCue.None,"reset silent");
        Check(gate.Observe(1,5,false,7)==DesktopCue.Hit,"post reset hit");
        Check(gate.Observe(2,5,false,Double.NaN)==DesktopCue.None,"NaN silence");
        Check(gate.Observe(3,5,false,Double.PositiveInfinity)==DesktopCue.None,"infinite silence");
        Check(gate.Observe(4,5,false,-1)==DesktopCue.None,"negative clock silence");
        Check(gate.Observe(5,5,false,1)==DesktopCue.None,"clock rewind silence");
        Check(gate.Observe(-1,5,false,2)==DesktopCue.None,"negative counters silence");
        gate.Reset(Int32.MaxValue,5);
        Check(gate.Observe(Int32.MinValue,4,false,3)==DesktopCue.None,"counter rollover silence");
        gate.Reset(0,5);
        int emitted=0;
        for(int i=0;i<10000;i++) if(gate.Observe(i,5,false,i*.001)!=DesktopCue.None) emitted++;
        Check(emitted<=112,"10k observations bounded rate");
        Console.WriteLine("PASS "+checks+" PCM/state assertions; burst cues="+emitted+"; native/listening NOT RUN");
    }
}
