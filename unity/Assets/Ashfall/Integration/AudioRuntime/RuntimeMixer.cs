using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Audio;
namespace Ashfall.Integration.AudioRuntime
{
    // One mixed output, never a per-clip ceiling masquerading as a master limiter.
    // Control and DSP serialized here; coordinator must profile lock contention on device.
    public sealed class RuntimeMixer : IProfiledAudioPlayback, IDisposable
    {
        private sealed class Voice : IProfiledAudioVoice
        {
            internal RuntimeMixer Mixer; internal PcmWave Pcm; internal AudioAsset Asset;
            internal CueProfile Profile; internal Float3 At; internal float Gain,Pitch;
            internal double Cursor; internal bool Paused,Stopped,Spatial; internal string Owner;
            public void Set(float gain,float pitch,Float3 at){Validate(gain,pitch,at);lock(Mixer.gate){Mixer.Check();Gain=gain;Pitch=pitch;At=at;}}
            public void SetProfile(CueProfile profile){if(profile==null)throw new ArgumentNullException("profile");lock(Mixer.gate){Mixer.Check();Profile=profile;}}
            public void Pause(bool value){lock(Mixer.gate){Mixer.Check();Paused=value;}}
            public void Stop(){lock(Mixer.gate){Stopped=true;}}
        }
        private readonly object gate=new object();
        internal readonly ProximityVoicePcm Proximity=new ProximityVoicePcm();
        private readonly IApprovedAudioBytes bytes;
        private readonly Dictionary<string,PcmWave> cache=new Dictionary<string,PcmWave>(StringComparer.OrdinalIgnoreCase);
        private readonly long retainedLimit;
        private long retainedPcmBytes;
        public long RetainedSampleBytes {get{lock(gate){return retainedPcmBytes+Proximity.RetainedSampleBytes;}}}
        internal void ReceiveVoice(string owner,byte[] packet,Float3 at,float gain)
        {lock(gate){Check();Prune();if(!Proximity.Contains(owner)){
            if(voices.Count+Proximity.PeerCount>=128)throw new InvalidOperationException("Voice budget exhausted");
            if(ProximityVoicePcm.BytesPerPeer>retainedLimit-RetainedSampleBytes)throw new InvalidOperationException("Session voice PCM budget");}
            Proximity.Push(owner,packet,at,gain);}}
        public int CacheCount {get{lock(gate){return cache.Count;}}}
        public bool UsesBoundedApprovedFiles {get{return bytes is BoundedApprovedAudioFiles;}}
        private readonly Dictionary<string,float> buses=new Dictionary<string,float>(StringComparer.Ordinal);
        private readonly List<Voice> voices=new List<Voice>();
        private PeakLimiter limiter=new PeakLimiter();
        private Float3 listener;
        private bool disposed;
        internal string EmissionOwner;
        private readonly HashSet<string> pausedOwners=new HashSet<string>(StringComparer.Ordinal);
        public float SumPeak {get;private set;}
        public float OutputPeak {get;private set;}
        public long RenderedFrames {get;private set;}
        public int VoiceCount {get{lock(gate){Prune();return voices.Count;}}}
        public RuntimeMixer(IApprovedAudioBytes source):this(source,VerifiedPcm.MaxRetainedBytes){}
        public RuntimeMixer(IApprovedAudioBytes source,long retainedBudget){if(retainedBudget<1||retainedBudget>VerifiedPcm.MaxRetainedBytes)throw new ArgumentOutOfRangeException("retainedBudget");retainedLimit=retainedBudget;if(source==null)throw new ArgumentNullException("source");bytes=source;foreach(string b in new[]{"Master","SFX","Voice","Music","Ambience"})buses.Add(b,b=="Music"||b=="Ambience"?0:1);}
        public void SetListener(Float3 at){if(!at.IsFinite)throw new ArgumentException("Listener");lock(gate){Check();listener=at;}}
        public void SetBus(string bus,float gain){if(!AudioCatalog.ValidBus(bus)||!Finite(gain)||gain<0||gain>1)throw new ArgumentException("Bus");lock(gate){Check();buses[bus]=gain;}}
        public IAudioVoice Start(AudioAsset a,bool spatial,float gain,float pitch,Float3 at){return Start(a,CueProfiles.For(a.Route.Substring(a.Route.LastIndexOf('/')+1),a.Route.StartsWith("weapons/",StringComparison.Ordinal)),spatial,gain,pitch,at);}
        public IAudioVoice Start(AudioAsset a,CueProfile p,bool spatial,float gain,float pitch,Float3 at)
        {
            Validate(gain,pitch,at);if(p==null)throw new ArgumentNullException("profile");
            // IO occurs on control lane under the same ownership lock; no DSP IO.
            lock(gate)
            {
                Check();Prune();if(voices.Count+Proximity.PeerCount>=128)throw new InvalidOperationException("Voice budget exhausted");
                PcmWave pcm;
                if(!cache.TryGetValue(a.Hash,out pcm))
                {
                    long required=(long)a.Frames*a.Channels*4;
                    if(required>retainedLimit-RetainedSampleBytes)throw new InvalidOperationException("Session PCM budget exhausted");
                    pcm=VerifiedPcm.Load(a,bytes);cache.Add(a.Hash,pcm);retainedPcmBytes+=required;
                }
                else VerifiedPcm.VerifyCached(a,bytes);
                if(pcm.Frames!=a.Frames||pcm.Channels!=a.Channels||pcm.Rate!=a.Rate)throw new InvalidOperationException("Hash shape mismatch");
                var v=new Voice{Mixer=this,Pcm=pcm,Asset=a,Profile=p,Spatial=spatial,Gain=gain,Pitch=pitch,At=at,Owner=EmissionOwner};voices.Add(v);return v;
            }
        }

        public void PauseOwner(string owner,bool pause){lock(gate){Check();if(pause)pausedOwners.Add(owner);else pausedOwners.Remove(owner);foreach(var v in voices)if(v.Owner==owner&&!v.Asset.Loop&&pause)v.Stopped=true;Prune();}}
        public void ReleaseOwner(string owner){lock(gate){Check();foreach(var v in voices)if(v.Owner==owner)v.Stopped=true;pausedOwners.Remove(owner);Prune();}}
        public void StopTransients(){lock(gate){foreach(var v in voices)if(!v.Asset.Loop)v.Stopped=true;Prune();}}
        public double CursorFrames(IAudioVoice voice){lock(gate){var v=voice as Voice;if(v==null||v.Mixer!=this)throw new ArgumentException("Foreign voice");return v.Cursor;}}
        public double OwnerCursorTotal(string owner){lock(gate){double sum=0;foreach(var v in voices)if(v.Owner==owner)sum+=v.Cursor;return sum;}}
        public void Render(float[] output,int channels,int rate)
        {
            if(output==null||(channels!=1&&channels!=2)||rate<8000||rate>192000||output.Length%channels!=0)throw new ArgumentException("Output PCM");
            lock(gate)
            {
                Array.Clear(output,0,output.Length);if(disposed)return;
                foreach(var v in voices)
                {
                    if(v.Stopped||v.Paused||(v.Owner!=null&&pausedOwners.Contains(v.Owner)))continue;
                    double dx=(double)v.At.X-listener.X,dy=(double)v.At.Y-listener.Y,dz=(double)v.At.Z-listener.Z;
                    double distance=Math.Sqrt(dx*dx+dy*dy+dz*dz);
                    float attenuation=v.Spatial?(distance>=v.Profile.MaxMeters?0:v.Profile.Attenuation((float)distance)):1;
                    float gain=AudioMix.Gain(v.Gain,v.Profile,buses[v.Asset.Bus],buses["Master"],v.Asset.Bus=="Master")*attenuation;
                    for(int i=0;i<output.Length;i+=channels)
                    {
                        if(v.Cursor>=v.Pcm.Frames){if(v.Asset.Loop)v.Cursor%=v.Pcm.Frames;else{v.Stopped=true;break;}}
                        int frame=(int)v.Cursor,next=frame+1;float fraction=(float)(v.Cursor-frame);
                        if(next==v.Pcm.Frames)next=v.Asset.Loop?0:frame;
                        for(int c=0;c<channels;c++)
                        {
                            float s=0;int count=channels==1?v.Pcm.Channels:1;
                            for(int k=0;k<count;k++){int pc=channels==1?k:Math.Min(c,v.Pcm.Channels-1);float a=v.Pcm.Samples[frame*v.Pcm.Channels+pc],b=v.Pcm.Samples[next*v.Pcm.Channels+pc];s+=a+(b-a)*fraction;}
                            output[i+c]+=s/count*gain;
                        }
                        v.Cursor+=(double)v.Pcm.Rate/rate*v.Pitch;
                    }
                }
                Proximity.Mix(output,channels,rate,listener,buses["Voice"]*buses["Master"]);
                SumPeak=Peak(output);limiter.Process(output,channels,rate);OutputPeak=Peak(output);RenderedFrames+=output.Length/channels;Prune();
            }
        }
        private static float Peak(float[] a){float p=0;foreach(float v in a)p=Math.Max(p,Math.Abs(v));return p;}
        private void Prune()
        {
            int old=voices.Count;
            for(int i=voices.Count-1;i>=0;i--)if(voices[i].Stopped){voices[i].Pcm=null;voices.RemoveAt(i);}
            // At most128 entries; remove without allocating on DSP lane.
            while(true){string unused=null;foreach(var item in cache){bool used=false;foreach(var v in voices)if(v.Asset.Hash.Equals(item.Key,StringComparison.OrdinalIgnoreCase)){used=true;break;}if(!used){unused=item.Key;break;}}if(unused==null)break;retainedPcmBytes-=(long)cache[unused].Samples.Length*4;cache.Remove(unused);}
            if(old>0&&voices.Count==0)limiter=new PeakLimiter();
        }
        public void Dispose(){lock(gate){disposed=true;Proximity.Clear();foreach(var v in voices)v.Pcm=null;voices.Clear();cache.Clear();retainedPcmBytes=0;limiter=new PeakLimiter();}}
        private void Check(){if(disposed)throw new ObjectDisposedException("RuntimeMixer");}
        private static bool Finite(float f){return !float.IsNaN(f)&&!float.IsInfinity(f);}
        private static void Validate(float g,float p,Float3 a){if(!Finite(g)||g<0||g>1||!Finite(p)||p<.1f||p>3||!a.IsFinite)throw new ArgumentException("Voice");}
    }
}
