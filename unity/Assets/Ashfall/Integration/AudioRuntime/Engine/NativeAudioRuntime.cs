#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Audio;
using UnityEngine;
namespace Ashfall.Integration.AudioRuntime.Engine
{
    public static class NativeMainLane
    {
        public static void Require()
        {
            var context=System.Threading.SynchronizationContext.Current;
            if(context==null||context.GetType().FullName!="UnityEngine.UnitySynchronizationContext")throw new InvalidOperationException("Unity main synchronization context required");
        }
    }
    // No Awake/OnEnable/import initializer: coordinator must explicitly Activate.
    public sealed class NativeAudioRuntime : MonoBehaviour
    {
        private AudioSource output;
        private AudioClip stream;
        private AudioRuntimeSession session;
        private AudioActivation activation;
        private UnityEngine.AudioSettings.AudioConfigurationChangeHandler configurationHandler;
        private int controlThread;
        private long deliveredEvents;
        public long DeliveredEvents {get{return System.Threading.Interlocked.Read(ref deliveredEvents);}}
        public int LastDisposalThread {get;private set;}
        public void ProvisionMainLane(){NativeMainLane.Require();Control();controlThread=System.Threading.Thread.CurrentThread.ManagedThreadId;}
        public AudioRuntimeSession Session {get{return session;}}
        public bool PendingInvalidation {get{return activation!=null&&activation.Pending;}}
        private void Control(){NativeMainLane.Require();if(controlThread!=0&&controlThread!=System.Threading.Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("Root main control lane required");}
        public void Activate(AudioRuntimeSession value){ActivateCore(value,false);}
        internal UnityEngine.AudioSettings.AudioConfigurationChangeHandler CapturedHandlerForGate {get{return configurationHandler;}}
        internal void ActivateCore(AudioRuntimeSession value,bool failAfterSubscribe)
        {
            NativeMainLane.Require();Control();
            if(activation!=null||value==null||!value.UsesBoundedApprovedFiles)throw new InvalidOperationException("Explicit bounded single activation required");
            controlThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
            int rate=UnityEngine.AudioSettings.outputSampleRate;
            var captured=new AudioActivation(value);activation=captured;session=value;
            try
            {
                // Subscribe BEFORE allocation/Play; a synchronous reset also silences this generation.
                configurationHandler=delegate(bool changed){System.Threading.Interlocked.Increment(ref deliveredEvents);captured.Invalidate();};
                UnityEngine.AudioSettings.OnAudioConfigurationChanged+=configurationHandler;
                if(failAfterSubscribe)throw new InvalidOperationException("Injected native activation failure");
                stream=AudioClip.Create("ASHFALL owned mixed output",rate,2,rate,true,delegate(float[] data){captured.Render(data,rate);});
                output=gameObject.AddComponent<AudioSource>();output.playOnAwake=false;output.loop=true;output.spatialBlend=0;output.volume=1;output.pitch=1;output.dopplerLevel=0;
                output.clip=stream;output.Play();
                if(captured.Pending)DrainPendingInvalidation();
            }
            catch{Deactivate();throw;}
        }
        public void DrainPendingInvalidation(){Control();if(activation!=null&&activation.Pending)Deactivate();}
        // No automatic activation. Update is only a main-thread pending-cleanup pump.
        private void Update(){DrainPendingInvalidation();}
        public void SetListenerPosition(Vector3 position){Control();if(session==null||PendingInvalidation)throw new InvalidOperationException("Inactive");session.Mixer.SetListener(new Float3(position.x,position.y,position.z));}
        private static void DestroyOwned(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public void Deactivate()
        {
            Control();var old=activation;if(old!=null)old.Invalidate();
            if(configurationHandler!=null){UnityEngine.AudioSettings.OnAudioConfigurationChanged-=configurationHandler;configurationHandler=null;}
            activation=null;session=null;
            if(output){output.Stop();output.clip=null;DestroyOwned(output);output=null;}
            if(stream){DestroyOwned(stream);stream=null;}
            if(old!=null){old.Close();LastDisposalThread=System.Threading.Thread.CurrentThread.ManagedThreadId;}
        }
        private void OnDestroy(){Deactivate();}
    }
    // Explicit Editor/PlayMode-callable importer: no AssetDatabase or import side effects.
    public sealed class NativeClipResolver : IDisposable
    {
        private readonly BoundedApprovedAudioFiles bytes;
        private readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>(StringComparer.OrdinalIgnoreCase);
        public long RetainedSampleBytes {get;private set;}
        public int ClipCount {get{return clips.Count;}}
        private bool disposed;
        private readonly int controlThread;
        private void Control(){NativeMainLane.Require();if(controlThread!=System.Threading.Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("Native importer main lane");}
        public NativeClipResolver(BoundedApprovedAudioFiles approved){if(approved==null)throw new ArgumentNullException("approved");NativeMainLane.Require();controlThread=System.Threading.Thread.CurrentThread.ManagedThreadId;bytes=approved;}
        public AudioClip Import(AudioAsset asset)
        {
            Control();if(disposed)throw new ObjectDisposedException("NativeClipResolver");
            AudioClip cached;if(clips.TryGetValue(asset.Hash,out cached)){VerifiedPcm.VerifyCached(asset,bytes);if(cached.channels!=asset.Channels||cached.frequency!=asset.Rate||cached.samples!=asset.Frames)throw new InvalidOperationException("Native hash shape mismatch");return cached;}
            long required=(long)asset.Frames*asset.Channels*4;
            if(required>VerifiedPcm.MaxRetainedBytes-RetainedSampleBytes)throw new InvalidOperationException("Native PCM budget");
            var pcm=VerifiedPcm.Load(asset,bytes);
            var clip=AudioClip.Create(asset.Route,pcm.Frames,pcm.Channels,pcm.Rate,false);
            try{if(!clip.SetData(pcm.Samples,0))throw new InvalidOperationException("Native AudioClip rejected PCM");clips.Add(asset.Hash,clip);RetainedSampleBytes+=required;return clip;}
            catch{if(Application.isPlaying)UnityEngine.Object.Destroy(clip);else UnityEngine.Object.DestroyImmediate(clip);throw;}
        }
        public void Dispose(){Control();if(disposed)return;foreach(var clip in clips.Values)if(clip){if(Application.isPlaying)UnityEngine.Object.Destroy(clip);else UnityEngine.Object.DestroyImmediate(clip);}clips.Clear();RetainedSampleBytes=0;disposed=true;}
    }
    // Coordinator attaches explicitly to the final listener. Measures actual global mix.
    // Must be the last filter; other audio paths and listener gain remain coordinator gates.
    public sealed class FinalMasterDsp : MonoBehaviour
    {
        private readonly object gate=new object();
        private readonly PeakLimiter limiter=new PeakLimiter();
        private int rate;
        private float sumPeak,outputPeak,maximumOutputPeak,windowPeak;
        private long blocks,ceilingViolations;
        public float SumPeak {get{lock(gate){return sumPeak;}}}
        public float OutputPeak {get{lock(gate){return outputPeak;}}}
        public float MaximumOutputPeak {get{lock(gate){return maximumOutputPeak;}}}
        public float WindowPeak {get{lock(gate){return windowPeak;}}}
        public long Blocks {get{lock(gate){return blocks;}}}
        public long CeilingViolations {get{lock(gate){return ceilingViolations;}}}
        public void BeginWindow(){lock(gate){windowPeak=0;}}
        public void Configure(int sampleRate){if(sampleRate<8000||sampleRate>192000)throw new ArgumentException("Rate");lock(gate){rate=sampleRate;}}
        private void OnAudioFilterRead(float[] data,int channels)
        {
            lock(gate)
            {
                if(rate==0){Array.Clear(data,0,data.Length);return;}
                float before=0,after=0;foreach(float s in data)before=Math.Max(before,Math.Abs(s));
                limiter.Process(data,channels,rate);foreach(float s in data)after=Math.Max(after,Math.Abs(s));
                sumPeak=before;outputPeak=after;maximumOutputPeak=Math.Max(maximumOutputPeak,after);windowPeak=Math.Max(windowPeak,after);
                if(after>PeakLimiter.Ceiling+.00001||float.IsNaN(after)||float.IsInfinity(after))ceilingViolations++;blocks++;
            }
        }
    }
}
#endif
