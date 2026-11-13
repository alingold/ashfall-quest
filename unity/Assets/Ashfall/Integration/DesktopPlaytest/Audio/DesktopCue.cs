using System;
namespace Ashfall.Integration.DesktopPlaytest.Audio
{
    public enum DesktopCue { None, Hit, Death }
    // No queue, sum, threads, file IO or shared audio runtime. One serial voice.
    public sealed class DesktopCueGate
    {
        int hits, targets;
        double until, last;
        bool bound;
        public void Reset(int currentHits, int currentTargets)
        { hits=currentHits; targets=currentTargets; until=0; last=0; bound=currentHits>=0&&currentTargets>=0; }
        public DesktopCue Observe(int currentHits, int currentTargets, bool muted, double now)
        {
            if(currentHits<0||currentTargets<0) { bound=false; return DesktopCue.None; }
            // Drop feedback on invalid time without clearing a live cooldown or
            // queuing that sample's counters for later playback.
            if (Double.IsNaN(now)||Double.IsInfinity(now)||now<0||now+.09<=now||now+.14<=now)
            { if(bound) { hits=currentHits; targets=currentTargets; } return DesktopCue.None; }
            if (!bound) { Reset(currentHits,currentTargets); return DesktopCue.None; }
            bool death=currentTargets<targets, hit=currentHits>hits;
            bool reset=currentHits<hits || currentTargets>targets;
            hits=currentHits; targets=currentTargets;
            if (now<last) { until=now; reset=true; }
            last=now;
            if (muted||reset||currentHits<0||currentTargets<0)
            { until=now; return DesktopCue.None; }
            if (now<until) return DesktopCue.None;
            DesktopCue cue=death?DesktopCue.Death:hit?DesktopCue.Hit:DesktopCue.None;
            if (cue!=DesktopCue.None) until=now+(cue==DesktopCue.Death?.14:.09);
            return cue;
        }
    }
    public static class DesktopCuePcm
    {
        public const int SampleRate=48000;
        public const int HitSamples=2160, DeathSamples=5280;
        public static float[] Create(DesktopCue cue)
        {
            if(cue!=DesktopCue.Hit && cue!=DesktopCue.Death) throw new ArgumentOutOfRangeException("cue");
            int count=cue==DesktopCue.Hit?HitSamples:DeathSamples;
            var pcm=new float[count];
            double phase=0;
            for(int i=0;i<count;i++)
            {
                double t=(double)i/(count-1);
                double hz=cue==DesktopCue.Hit?880:620-300*t;
                // Sine envelope gives exactly silent endpoints; no randomized content.
                pcm[i]=(float)(.16*Math.Sin(Math.PI*t)*Math.Sin(phase));
                phase+=2*Math.PI*hz/SampleRate;
            }
            pcm[0]=pcm[count-1]=0;
            return pcm;
        }
    }
}
