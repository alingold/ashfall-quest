#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Modules.Audio;
using UnityEngine;
namespace Ashfall.Integration.AudioRuntime.Engine
{
    public static class NativeAudioGates
    {
        // Coordinator explicitly calls in Editor with an approved asset and reader.
        // Creation/data round trip only; not playback/listening approval.
        public static void ImportRoundTrip(AudioAsset asset,BoundedApprovedAudioFiles bytes)
        {
            using(var resolver=new NativeClipResolver(bytes))
            {
                var pcm=VerifiedPcm.Load(asset,bytes);var clip=resolver.Import(asset);
                if(clip.channels!=pcm.Channels||clip.frequency!=pcm.Rate||clip.samples!=pcm.Frames)throw new InvalidOperationException("Native clip shape");
                var read=new float[pcm.Samples.Length];if(!clip.GetData(read,0))throw new InvalidOperationException("Native clip read");
                for(int i=0;i<read.Length;i++)if(Math.Abs(read[i]-pcm.Samples[i])>.00001)throw new InvalidOperationException("Native clip PCM mismatch");
            }
        }
        // Coordinator isolated PlayMode only, through engine_lane.py. No leaf invokes this.
        public static IEnumerator Lifetime(Func<AudioRuntimeSession> createSession,Action<string> receipt)
        {
            int clips=Resources.FindObjectsOfTypeAll<AudioClip>().Length;
            int sources=Resources.FindObjectsOfTypeAll<AudioSource>().Length;
            var go=new GameObject("ASHFALL isolated audio lifecycle gate");
            var runtime=go.AddComponent<NativeAudioRuntime>();
            var failed=createSession();bool rejected=false;
            try{runtime.ActivateCore(failed,true);}catch(InvalidOperationException){rejected=true;}
            if(!rejected||runtime.Session!=null||failed.Mixer.RetainedSampleBytes!=0)throw new InvalidOperationException("Failed activation cleanup");
            runtime.Deactivate();runtime.Deactivate();
            runtime.Activate(createSession());var stale=runtime.CapturedHandlerForGate;
            // Invoke the captured real subscription delegate, not a replacement lifecycle algorithm.
            stale(false);if(!runtime.PendingInvalidation)throw new InvalidOperationException("Event did not invalidate generation");
            runtime.DrainPendingInvalidation();if(runtime.Session!=null)throw new InvalidOperationException("Pending cleanup");
            runtime.Activate(createSession());var replacement=runtime.Session;stale(true);
            runtime.DrainPendingInvalidation();if(runtime.Session!=replacement)throw new InvalidOperationException("Old callback disposed replacement");
            // Actual Unity static event delivery: only coordinator executes this device operation.
            if(!UnityEngine.AudioSettings.Reset(UnityEngine.AudioSettings.GetConfiguration()))throw new InvalidOperationException("Actual configuration reset failed");
            yield return null;runtime.DrainPendingInvalidation();
            if(runtime.Session!=null)throw new InvalidOperationException("Actual reset event not delivered");
            runtime.Activate(createSession());UnityEngine.Object.Destroy(go);yield return null;yield return null;
            if(Resources.FindObjectsOfTypeAll<AudioClip>().Length!=clips||Resources.FindObjectsOfTypeAll<AudioSource>().Length!=sources)throw new InvalidOperationException("Isolated native resource census differs");
            if(receipt!=null)receipt("Actual reset/lifecycle/census gate passed; no listening approval");
        }
        // Root activates runtime, installs/configures final-listener DSP and starts
        // coroutine in an isolated PlayMode rig. Requires approved wind/glide/climb.
        // Caller supplies profile/settings edits through its actual binding separately.
        public static IEnumerator Transitions(NativeAudioRuntime runtime,FinalMasterDsp final,CueIdentity owner,Action<string> receipt)
        {
            if(!runtime||!final||runtime.Session==null)throw new InvalidOperationException("Explicit native wiring required");
            var session=runtime.Session;long before=final.Blocks;float observedPeak=0;double start=Time.realtimeSinceStartupAsDouble;
            while(Time.realtimeSinceStartupAsDouble-start<60)
            {
                session.Flight(owner,Time.unscaledDeltaTime,true,12,40,false,new Float3(0,0,0));
                observedPeak=Math.Max(observedPeak,final.OutputPeak);
                if(session.Mixer.VoiceCount!=2)throw new InvalidOperationException("Sustained loop count");yield return null;
            }
            for(int i=0;i<120;i++)
            {
                session.Flight(owner,Time.unscaledDeltaTime,i%2==0,i%3==0?40:5,40,false,new Float3(0,0,0));
                // High-speed non-glide frames represent pad/dive handoff. Source bank
                // has no universal "pad" cue; never invent an authored sound route.
                if(i%2!=0)session.Once(new CueIdentity(owner.Session,owner.Owner,owner.Epoch,"native-transition-"+i),"climb",null,false,0,new Float3(0,0,0),false,.2f,1,AudioEmitterRole.Local);
                observedPeak=Math.Max(observedPeak,final.OutputPeak);
                if(i==30)
                {
                    session.PauseOwner(owner,true);double cursor=session.Mixer.OwnerCursorTotal(owner.OwnerKey);long blocks=final.Blocks;
                    // Allow queued output to drain before checking isolated final-listener silence.
                    for(int settle=0;settle<30;settle++)yield return null;
                    final.BeginWindow();for(int observe=0;observe<10;observe++)yield return null;
                    if(final.Blocks<=blocks||final.WindowPeak>.00001||session.Mixer.OwnerCursorTotal(owner.OwnerKey)!=cursor)throw new InvalidOperationException("Native paused output/cursor");
                }
                if(i==60)session.PauseOwner(owner,false);yield return null;
            }
            if(final.Blocks<=before)throw new InvalidOperationException("No actual final listener DSP callbacks observed");
            if(observedPeak<=0)throw new InvalidOperationException("No nonzero native output observed");
            if(final.CeilingViolations!=0||final.MaximumOutputPeak>PeakLimiter.Ceiling+.00001)throw new InvalidOperationException("Final listener ceiling");
            session.Despawn(owner);if(session.Mixer.VoiceCount!=0)throw new InvalidOperationException("Owned voices survive despawn");
            if(receipt!=null)receipt("Native callback/transition gate passed; listening/asset/performance approval remains separate");
        }
    }
}
#endif
