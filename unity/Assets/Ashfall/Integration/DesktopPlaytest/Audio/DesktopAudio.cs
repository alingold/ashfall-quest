#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
namespace Ashfall.Integration.DesktopPlaytest.Audio
{
    [DisallowMultipleComponent]
    public sealed class DesktopAudio : MonoBehaviour, IDesktopPlaytestModule
    {
        DesktopPlaytestContext context;
        readonly DesktopCueGate gate=new DesktopCueGate();
        AudioSource source;
        AudioClip hit, death;
        AudioListener ownedListener;
        Camera view;
        bool focused=true, suspended, disposed;
        public int CuesStarted { get; private set; }
        public bool OwnsListener { get { return ownedListener!=null; } }
        public void Initialize(DesktopPlaytestContext value)
        {
            if(disposed) throw new ObjectDisposedException("DesktopAudio");
            if(value==null) throw new ArgumentNullException("value");
            if(value.Retired) throw new InvalidOperationException("Context retired");
            if(context!=null) throw new InvalidOperationException("Already initialized");
            context=value;
            try
            {
                hit=MakeClip(DesktopCue.Hit); death=MakeClip(DesktopCue.Death);
                source=gameObject.AddComponent<AudioSource>();
                source.playOnAwake=false; source.loop=false; source.spatialBlend=0;
                source.volume=1; source.pitch=1; source.dopplerLevel=0;
                source.bypassEffects=true; source.bypassListenerEffects=true;
                source.spatialize=false;
                context.ResetRequested+=ResetLocal;
                UnityEngine.AudioSettings.OnAudioConfigurationChanged+=ConfigurationChanged;
                ResetLocal();
            }
            catch { DisposeAudio(); throw; }
        }
        static AudioClip MakeClip(DesktopCue cue)
        {
            float[] pcm=DesktopCuePcm.Create(cue);
            AudioClip clip=AudioClip.Create("Synthetic desktop "+cue,pcm.Length,1,DesktopCuePcm.SampleRate,false);
            if(clip==null) throw new InvalidOperationException("AudioClip creation failed");
            try { if(!clip.SetData(pcm,0)) throw new InvalidOperationException("AudioClip SetData failed"); }
            catch { Destroy(clip); throw; }
            return clip;
        }
        void Update() { Pump(Time.unscaledTimeAsDouble); }
        // Main-lane hook for the root's native fixture. Never writes gameplay counters.
        public void Pump(double now)
        {
            if(context==null||disposed) return;
            if(context.Retired) { DisposeAudio(); return; }
            if(!isActiveAndEnabled) { ResetLocal(); ReleaseListener(); return; }
            bool listener=BindListener();
            bool muted=context.Paused||!focused||suspended||!isActiveAndEnabled||!listener;
            if(muted && source!=null) { source.Stop(); source.clip=null; }
            DesktopCue cue=gate.Observe(context.Hits,context.RemainingTargets,muted,now);
            if(cue==DesktopCue.None||source==null) return;
            // Play(), never PlayOneShot(): at most one voice, including interrupted death/hit.
            source.Stop(); source.clip=cue==DesktopCue.Death?death:hit; source.Play(); CuesStarted++;
        }
        bool BindListener()
        {
            if(view!=context.View) { ReleaseListener(); view=context.View; }
            if(view==null||!view.isActiveAndEnabled) { ReleaseListener(); return false; }
            // Only the actual view may listen. Do not create alongside any other enabled listener.
            AudioListener existing=null; bool foreign=false;
            foreach(var listener in FindObjectsByType<AudioListener>())
            {
                if(listener==ownedListener||!listener.isActiveAndEnabled) continue;
                if(listener.gameObject==view.gameObject && existing==null) existing=listener;
                else foreign=true;
            }
            if(existing!=null||foreign) ReleaseListener();
            if(foreign) return false;
            if(existing!=null) return true;
            if(ownedListener==null)
            {
                // A disabled view listener is external policy: never override it or duplicate it.
                if(view.GetComponent<AudioListener>()!=null) return false;
                ownedListener=view.gameObject.AddComponent<AudioListener>();
            }
            return ownedListener!=null && ownedListener.isActiveAndEnabled;
        }
        void ReleaseListener()
        { if(ownedListener!=null) { ownedListener.enabled=false; Destroy(ownedListener); ownedListener=null; } }
        void ResetLocal()
        {
            if(source!=null) { source.Stop(); source.clip=null; }
            if(context!=null) gate.Reset(context.Hits,context.RemainingTargets);
        }
        public void NotifyFocus(bool value) { focused=value; ResetLocal(); }
        public void NotifySuspension(bool value) { suspended=value; ResetLocal(); }
        void OnApplicationFocus(bool value) { NotifyFocus(value); }
        void OnApplicationPause(bool value) { NotifySuspension(value); }
        void ConfigurationChanged(bool changed) { ResetLocal(); }
        void OnEnable() { ResetLocal(); }
        void OnDisable() { ResetLocal(); ReleaseListener(); }
        public void DisposeAudio()
        {
            if(disposed) return;
            disposed=true;
            UnityEngine.AudioSettings.OnAudioConfigurationChanged-=ConfigurationChanged;
            if(context!=null) context.ResetRequested-=ResetLocal;
            ResetLocal(); ReleaseListener();
            if(source!=null) { source.enabled=false; Destroy(source); source=null; }
            if(hit!=null) { Destroy(hit); hit=null; }
            if(death!=null) { Destroy(death); death=null; }
            context=null; view=null;
        }
        void OnDestroy() { DisposeAudio(); }
    }
}
#endif
