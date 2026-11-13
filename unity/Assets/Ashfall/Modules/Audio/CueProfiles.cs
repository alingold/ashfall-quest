using System;
using Ashfall.Core;

namespace Ashfall.Modules.Audio
{
    public interface IProfiledAudioPlayback : IAudioPlayback
    {
        IAudioVoice Start(AudioAsset asset, CueProfile profile, bool spatial, float gain, float pitch, Float3 position);
    }
    public interface IProfiledAudioVoice : IAudioVoice
    {
        void SetProfile(CueProfile profile);
    }
    public static class AudioMix
    {
        public static float Gain(float voice, CueProfile profile, float bus, float master, bool masterBus)
        { return voice * profile.SourceGain * bus * (masterBus ? 1 : master); }
    }
    public sealed class CueProfile
    {
        public readonly string Id;
        public readonly float UnitMeters, MaxMeters, SourceGain, MaxGain;
        internal CueProfile(string id,float unit,float max,float db,float maxDb)
        {Id=id;UnitMeters=unit;MaxMeters=max;SourceGain=(float)Math.Pow(10,db/20);MaxGain=(float)Math.Pow(10,maxDb/20);}
        public float Attenuation(float distance)
        {
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<0)throw new ArgumentException("Distance");
            // Godot inverse distance uses CMP_EPSILON=0.00001, applies the
            // absolute max_db cap after authored gain, then linear max-distance fade.
            // Return a relative multiplier: AudioMix applies SourceGain exactly once.
            return distance>=MaxMeters?0:(float)(Math.Min(MaxGain/SourceGain,1.0/(distance/UnitMeters+.00001))*(1.0-distance/MaxMeters));
        }
    }
    public static class CueProfiles
    {
        public const string Version="ASHFALL-AUDIO-POLICY/4";
        public static readonly CueProfile Default=new CueProfile("default-unverified",2,55,0,0);
        public static readonly CueProfile LocalShot=new CueProfile("local-shot",2,85,-5,3);
        public static readonly CueProfile LocalReload=new CueProfile("local-reload",10,14,-10,3);
        public static readonly CueProfile Shot=new CueProfile("remote-shot",12,140,0,3);
        public static readonly CueProfile Reload=new CueProfile("remote-reload",4,30,0,3);
        public static readonly CueProfile Footstep=new CueProfile("walk",4,8,-22,-22);
        public static readonly CueProfile Sprint=new CueProfile("sprint",4,8,-15,-15);
        public static readonly CueProfile Climb=new CueProfile("climb",2,5,-25,-25);
        public static readonly CueProfile Creak=new CueProfile("creak",10,35,-21,-21);
        public static readonly CueProfile Turbine=new CueProfile("turbine",7,48,-24,-24);
        public static readonly CueProfile Impact=new CueProfile("impact",2,55,-12,-12);
        public static CueProfile For(string kind,bool weapon)
        { return For(kind,weapon,AudioEmitterRole.Remote); }
        public static CueProfile For(string kind,bool weapon,AudioEmitterRole role)
        {
            if(kind==null || (role!=AudioEmitterRole.Remote && role!=AudioEmitterRole.Local))throw new ArgumentException("Cue/role");
            if(weapon){if(kind=="shot")return role==AudioEmitterRole.Local?LocalShot:Shot;
                if(kind=="dry" && role==AudioEmitterRole.Local)return LocalReload;if(Array.IndexOf(new[]{"mag_eject","pouch","mag_insert","bolt","open","close","latch","pump","shell_insert"},kind)>=0)return role==AudioEmitterRole.Local?LocalReload:Reload;}
            if(kind.EndsWith("_run_step",StringComparison.Ordinal))return Sprint;
            if(kind.EndsWith("_step",StringComparison.Ordinal))return Footstep;
            if(kind=="climb")return Climb;if(kind=="creak")return Creak;if(kind=="turbine")return Turbine;
            if(kind.StartsWith("impact_",StringComparison.Ordinal))return Impact;
            return Default;
        }
    }
    // Linked-channel instantaneous attack, exponential release. No allocation in Process.
    // Per-source bounds do not bound the sum; install the same DSP on the final listener.
    public sealed class PeakLimiter
    {
        public const float CeilingDb=-1f, ReleaseSeconds=.12f;
        public static readonly float Ceiling=(float)Math.Pow(10,CeilingDb/20);
        private float gain=1;
        public void Process(float[] samples,int channels,int sampleRate)
        {
            if(samples==null||channels<1||sampleRate<1||samples.Length%channels!=0)throw new ArgumentException("PCM buffer");
            float release=(float)Math.Exp(-1.0/(sampleRate*ReleaseSeconds));
            for(int i=0;i<samples.Length;i+=channels)
            {
                float peak=0;
                for(int c=0;c<channels;c++){float s=samples[i+c];if(float.IsNaN(s)||float.IsInfinity(s))samples[i+c]=0;else peak=Math.Max(peak,Math.Abs(s));}
                float required=peak>Ceiling?Ceiling/peak:1;
                gain=Math.Min(required,1-(1-gain)*release);
                for(int c=0;c<channels;c++)samples[i+c]*=gain;
            }
        }
    }
}
