#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Integration.DesktopPlaytest;
using Ashfall.Integration.DesktopPlaytest.Audio;
using UnityEngine;
namespace DesktopAudioR1
{
    public static class NativeGate
    {
        static void Check(bool value,string message) { if(!value) throw new InvalidOperationException("DesktopAudio: "+message); }
        // Root only: run in an isolated synthetic scene with no other enabled listeners.
        // Calls real Unity objects. Clip round-trip/play scheduling is not listening acceptance.
        public static IEnumerator Run()
        {
            var host=new GameObject("desktop-audio-gate"); var view=new GameObject("desktop-audio-view");
            var foreign=new GameObject("desktop-audio-foreign");
            DesktopAudio audio=null;
            try
            {
                Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Length==0,"isolated scene listener precondition");
                var camera=view.AddComponent<Camera>(); var existing=view.AddComponent<AudioListener>();
                var context=new DesktopPlaytestContext{View=camera,RemainingTargets=5};
                audio=host.AddComponent<DesktopAudio>();audio.Initialize(context);audio.Pump(0);
                Check(!audio.OwnsListener&&view.GetComponents<AudioListener>().Length==1,"reuse exact view listener");
                var source=host.GetComponent<AudioSource>();
                Check(host.GetComponents<AudioSource>().Length==1&&!source.loop&&source.spatialBlend==0&&!source.spatialize,"one serial nonspatial source");
                context.Hits=1; audio.Pump(1);Check(audio.CuesStarted==1&&source.clip.samples==2160,"hit scheduling");
                var retainedHit=source.clip;
                var pcm=new float[source.clip.samples];Check(source.clip.GetData(pcm,0)&&pcm[0]==0&&pcm[pcm.Length-1]==0,"actual clip PCM");
                var expected=DesktopCuePcm.Create(DesktopCue.Hit);for(int i=0;i<pcm.Length;i++)Check(Math.Abs(pcm[i]-expected[i])<.000001,"native PCM roundtrip");
                context.Hits=2;context.RemainingTargets=4;audio.Pump(2);Check(audio.CuesStarted==2&&source.clip.samples==5280,"death priority");
                var retainedDeath=source.clip;
                context.Paused=true;context.Hits=3;audio.Pump(3);Check(source.clip==null&&!source.isPlaying,"pause stops");
                context.Paused=false;audio.Pump(4);Check(audio.CuesStarted==2,"resume no stale event");
                audio.NotifyFocus(false);context.Hits=4;audio.Pump(5);audio.NotifyFocus(true);audio.Pump(6);Check(audio.CuesStarted==2,"focus drops backlog");
                audio.NotifySuspension(true);context.Hits=5;audio.Pump(7);audio.NotifySuspension(false);audio.Pump(8);Check(audio.CuesStarted==2,"suspension drops backlog");
                context.RequestReset();audio.Pump(9);Check(audio.CuesStarted==2&&context.Hits==5&&context.RemainingTargets==4,"reset reads only");
                var other=foreign.AddComponent<AudioListener>();context.Hits=6;audio.Pump(10);Check(source.clip==null&&!audio.OwnsListener,"foreign conflict silences; preserves external");
                other.enabled=false;existing.enabled=false;context.Hits=7;audio.Pump(11);Check(audio.CuesStarted==2&&!audio.OwnsListener,"external disabled listener not overridden");
                UnityEngine.Object.Destroy(existing);yield return null;
                audio.Pump(12);Check(audio.OwnsListener&&view.GetComponents<AudioListener>().Length==1,"own exactly one when absent");
                audio.enabled=false;audio.Pump(12.5);Check(!view.GetComponent<AudioListener>().enabled,"disabled Pump cannot recreate owned listener");yield return null;
                audio.enabled=true;audio.Pump(13);Check(audio.OwnsListener,"reenable binds fresh owned listener");
                var replacement=new GameObject("desktop-audio-replacement-view");
                try { context.View=replacement.AddComponent<Camera>();audio.Pump(14);yield return null;Check(view.GetComponents<AudioListener>().Length==0&&replacement.GetComponents<AudioListener>().Length==1,"view replacement cleanup"); }
                finally { context.View=camera;UnityEngine.Object.Destroy(replacement); }
                audio.DisposeAudio();audio.DisposeAudio();yield return null;
                Check(host.GetComponents<AudioSource>().Length==0&&retainedHit==null&&retainedDeath==null,"idempotent native cleanup both clips");
                context.RequestReset();Check(context.Hits==7,"disposed reset unsubscribed");
                Debug.Log("PASS DesktopAudioR1 native fixture; synthetic scheduling/ownership only; listening NOT established");
            }
            finally { if(audio!=null)audio.DisposeAudio();UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(view);UnityEngine.Object.Destroy(foreign); }
        }
    }
}
#endif
