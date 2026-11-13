#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Threading;
using UnityEngine;
namespace Ashfall.Integration.AudioRuntime.Engine
{
    // ROOT ONLY, isolated PlayMode, engine_lane.py. Never auto-started.
    public static class NativeDeliveryGatesR4
    {
        public static IEnumerator ResetAndQueuedSilence(NativeAudioRuntime runtime,FinalMasterDsp final,Func<AudioRuntimeSession> factory,Action<AudioRuntimeSession> start,Action<string> receipt)
        {
            NativeMainLane.Require();int main=Thread.CurrentThread.ManagedThreadId;
            RequireListener(final);runtime.ProvisionMainLane();
            try
            {
            var session=factory();runtime.Activate(session);start(session);
            long blocks=final.Blocks;double deadline=Time.realtimeSinceStartupAsDouble+5;
            final.BeginWindow();
            while(final.Blocks-blocks<8&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(final.Blocks-blocks<8||final.WindowPeak<=0)throw new InvalidOperationException("No real audible DSP before reset");
            long events=runtime.DeliveredEvents;
            if(!UnityEngine.AudioSettings.Reset(UnityEngine.AudioSettings.GetConfiguration()))throw new InvalidOperationException("Real UnityEngine.AudioSettings.Reset failed");
            deadline=Time.realtimeSinceStartupAsDouble+5;
            while((runtime.DeliveredEvents==events||runtime.Session!=null)&&Time.realtimeSinceStartupAsDouble<deadline){runtime.DrainPendingInvalidation();yield return null;}
            if(runtime.DeliveredEvents<=events||runtime.Session!=null||runtime.LastDisposalThread!=main||session.Mixer.RetainedSampleBytes!=0)throw new InvalidOperationException("Real event/main-lane disposal failed");
            // Measure silence after actual queued hardware/DSP buffers have drained.
            int length,count;UnityEngine.AudioSettings.GetDSPBufferSize(out length,out count);
            double drain=(double)length*count/UnityEngine.AudioSettings.outputSampleRate+.25;
            deadline=Time.realtimeSinceStartupAsDouble+drain;
            while(Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            blocks=final.Blocks;final.BeginWindow();deadline=Time.realtimeSinceStartupAsDouble+5;
            while(final.Blocks-blocks<8&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(final.Blocks-blocks<8||final.WindowPeak>.00001)throw new InvalidOperationException("Queued native output not silent");
            runtime.Deactivate();if(runtime.DeliveredEvents<=events)throw new InvalidOperationException("Event evidence lost");
            if(receipt!=null)receipt("R4 real Reset/event delivery/main-lane disposal/queued DSP silence PASS; output device/listening separate");
            }
            finally{runtime.Deactivate();}
        }
        public static void RequireListener(FinalMasterDsp final)
        {
            NativeMainLane.Require();if(!final||!final.GetComponent<AudioListener>()||!final.GetComponent<AudioListener>().enabled||AudioListener.volume!=1)throw new InvalidOperationException("Final enabled unity-gain listener required");
            int enabled=0;foreach(var listener in UnityEngine.Object.FindObjectsByType<AudioListener>())if(listener.enabled)enabled++;
            if(enabled!=1)throw new InvalidOperationException("One isolated listener required");
            bool seen=false;foreach(var component in final.GetComponents<MonoBehaviour>())
            {
                if(component==final){seen=true;continue;}
                if(seen&&component&&component.enabled&&component.GetType().GetMethod("OnAudioFilterRead",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)!=null)throw new InvalidOperationException("A DSP filter follows final master");
            }
            final.Configure(UnityEngine.AudioSettings.outputSampleRate);
        }
        public static IEnumerator GlobalMixer(FinalMasterDsp final,Action<string> receipt)
        {
            RequireListener(final);var go=new GameObject("R4 isolated global sum adversary");AudioClip clip=null;
            try
            {
                int rate=UnityEngine.AudioSettings.outputSampleRate;var pcm=new float[rate];for(int i=0;i<pcm.Length;i++)pcm[i]=.8f;
                clip=AudioClip.Create("R4 TEST ONLY sum",rate,1,rate,false);if(!clip.SetData(pcm,0))throw new InvalidOperationException("Gate PCM allocation");
                for(int i=0;i<2;i++){var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.clip=clip;source.loop=true;source.volume=1;source.spatialBlend=0;source.Play();}
                long before=final.Blocks;double deadline=Time.realtimeSinceStartupAsDouble+5;bool over=false;
                while(final.Blocks-before<16&&Time.realtimeSinceStartupAsDouble<deadline){over|=final.SumPeak>1;yield return null;}
                if(final.Blocks-before<16||!over||final.CeilingViolations!=0||final.MaximumOutputPeak>Ashfall.Modules.Audio.PeakLimiter.Ceiling+.00001)throw new InvalidOperationException("Actual summed global mixer ceiling gate failed");
                if(receipt!=null)receipt("R4 actual two-source overload/global listener ceiling PASS; synthetic content, no listening approval");
            }
            finally{UnityEngine.Object.Destroy(go);if(clip)UnityEngine.Object.Destroy(clip);}
        }
    }
}
#endif
