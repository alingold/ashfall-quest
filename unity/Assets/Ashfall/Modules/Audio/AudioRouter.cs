using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.Audio
{
    public sealed class AudioRouter : IDisposable
    {
        private sealed class LoopState { public AudioAsset Asset; public IAudioVoice Voice; public string Owner; public bool Spatial; public CueProfile Profile; }
        private readonly AudioCatalog catalog;
        private readonly IAudioPlayback playback;
        private readonly Dictionary<string,LoopState> loops=new Dictionary<string,LoopState>(StringComparer.Ordinal);
        private readonly HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> order=new Queue<string>();
        private readonly Dictionary<string,float> gains=new Dictionary<string,float>(StringComparer.Ordinal);
        private bool paused,disposed;
        public AudioRouter(AudioCatalog catalog,IAudioPlayback playback)
        {
            if(catalog==null||playback==null)throw new ArgumentNullException();this.catalog=catalog;this.playback=playback;
            SetBus("Master",1);SetBus("SFX",1);SetBus("Music",0);SetBus("Ambience",0);SetBus("Voice",1);
        }
        public void SetBus(string bus,float gain){Check();if(!AudioCatalog.ValidBus(bus)||!Finite(gain)||gain<0||gain>1)throw new ArgumentException("Bus gain");playback.SetBus(bus,gain);gains[bus]=gain;}
        public float BusGain(string bus){return gains[bus];}
        public bool Once(string eventId,string kind,string subject,bool weapon,int variant,Float3 at,bool spatial,float gain,float pitch)
        { return Once(eventId,kind,subject,weapon,variant,at,spatial,gain,pitch,AudioEmitterRole.Remote); }
        public bool Once(string eventId,string kind,string subject,bool weapon,int variant,Float3 at,bool spatial,float gain,float pitch,AudioEmitterRole role)
        {
            var profile=CueProfiles.For(kind,weapon,role);
            Check();Validate(at,gain,pitch);if(string.IsNullOrWhiteSpace(eventId))throw new ArgumentException("Session-qualified event id required");
            if(paused||seen.Contains(eventId))return false;
            var asset=catalog.Resolve(kind,subject,weapon,variant,false);if(asset.Loop)throw new InvalidOperationException("Loop cue requires SetLoop");
            Start(asset,profile,spatial,gain,pitch,at);
            seen.Add(eventId);order.Enqueue(eventId);if(order.Count>2048)seen.Remove(order.Dequeue());return true;
        }
        public void SetLoop(string owner,string slot,bool active,string kind,string subject,bool weapon,Float3 at,bool spatial,float gain,float pitch)
        { SetLoop(owner,slot,active,kind,subject,weapon,at,spatial,gain,pitch,AudioEmitterRole.Remote); }
        public void SetLoop(string owner,string slot,bool active,string kind,string subject,bool weapon,Float3 at,bool spatial,float gain,float pitch,AudioEmitterRole role)
        {
            var profile=CueProfiles.For(kind,weapon,role);
            Check();Validate(at,gain,pitch);if(string.IsNullOrWhiteSpace(owner)||string.IsNullOrWhiteSpace(slot)||owner.Contains("\n")||slot.Contains("\n"))throw new ArgumentException("Loop identity");
            string key=owner+"\n"+slot;LoopState state;loops.TryGetValue(key,out state);
            if(!active){if(state!=null){state.Voice.Stop();loops.Remove(key);}return;}
            var asset=catalog.Resolve(kind,subject,weapon,0,false);if(!asset.Loop)throw new InvalidOperationException("One-shot cue requires Once");
            if(state!=null && (state.Asset.Hash!=asset.Hash || state.Asset.Bus!=asset.Bus || state.Asset.Route!=asset.Route || state.Spatial!=spatial)){state.Voice.Stop();loops.Remove(key);state=null;}
            if(state!=null && state.Profile!=profile)
            {
                var adjustable=state.Voice as IProfiledAudioVoice;
                if(adjustable!=null){adjustable.SetProfile(profile);state.Profile=profile;}
                else {state.Voice.Stop();loops.Remove(key);state=null;}
            }
            if(state==null){state=new LoopState{Asset=asset,Owner=owner,Spatial=spatial,Profile=profile,Voice=Start(asset,profile,spatial,gain,pitch,at)};loops.Add(key,state);if(paused)state.Voice.Pause(true);}
            else state.Voice.Set(gain,pitch,at);
        }
        public void Pause(bool value){Check();if(paused==value)return;paused=value;if(value)playback.StopTransients();foreach(var state in loops.Values)state.Voice.Pause(value);}
        public void ReleaseOwner(string owner){Check();var keys=new List<string>();foreach(var pair in loops)if(pair.Value.Owner==owner){pair.Value.Voice.Stop();keys.Add(pair.Key);}foreach(string key in keys)loops.Remove(key);}
        public void Dispose(){if(disposed)return;foreach(var state in loops.Values)state.Voice.Stop();playback.StopTransients();loops.Clear();seen.Clear();order.Clear();disposed=true;}
        private void Check(){if(disposed)throw new ObjectDisposedException("AudioRouter");}
        private IAudioVoice Start(AudioAsset asset,CueProfile profile,bool spatial,float gain,float pitch,Float3 at)
        {
            var profiled=playback as IProfiledAudioPlayback;
            return profiled==null?playback.Start(asset,spatial,gain,pitch,at):profiled.Start(asset,profile,spatial,gain,pitch,at);
        }
        private static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
        private static void Validate(Float3 at,float gain,float pitch){if(!at.IsFinite||!Finite(gain)||gain<0||gain>1||!Finite(pitch)||pitch<.1f||pitch>3)throw new ArgumentException("Voice parameters");}
    }
    // Own one instance per local player. Feed horizontal speed, never camera velocity.
    public sealed class FlightAudio
    {
        public float GlideGain {get;private set;}
        public float WindDb {get;private set;}
        public FlightAudio(){WindDb=-25;}
        public void Tick(AudioRouter router,string owner,float dt,bool gliding,float speed,float altitude,bool sheltered,Float3 position)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||float.IsNaN(speed)||float.IsInfinity(speed)||speed<0||float.IsNaN(altitude)||float.IsInfinity(altitude))throw new ArgumentException("Flight frame");
            GlideGain=Move(GlideGain,gliding?Math.Max(0,Math.Min(1,(speed-2.5f)/8)):0,dt*2);
            WindDb=Move(WindDb,sheltered?-34:-26+Math.Max(0,Math.Min(4,altitude/12)),dt*3);
            router.SetLoop(owner,"wind",true,"wind",null,false,position,false,(float)Math.Pow(10,WindDb/20),1);
            router.SetLoop(owner,"glide",true,"glide",null,false,position,false,Math.Max(GlideGain*.27f,.0005f),.85f+GlideGain*.3f);
        }
        private static float Move(float a,float b,float d){return a<b?Math.Min(a+d,b):Math.Max(a-d,b);}
    }
}
