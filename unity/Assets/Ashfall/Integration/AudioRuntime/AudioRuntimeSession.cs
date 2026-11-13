using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Audio;
namespace Ashfall.Integration.AudioRuntime
{
    // Explicit root-controlled lifetime; all control calls on the root's main thread.
    public sealed class AudioRuntimeSession : IDisposable
    {
        public bool UsesBoundedApprovedFiles {get{return Mixer.UsesBoundedApprovedFiles;}}
        public readonly AudioRouter Router;
        public readonly RuntimeMixer Mixer;
        private readonly IAudioOwnerLease leases;
        private readonly Dictionary<string,FlightAudio> flight=new Dictionary<string,FlightAudio>();
        private readonly HashSet<string> paused=new HashSet<string>();
        private bool disposed;
        private readonly int controlThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        private void Control(){if(controlThread!=System.Threading.Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("Session root control lane required");}
        public AudioRuntimeSession(AudioCatalog catalog,IApprovedAudioBytes bytes,IAudioOwnerLease ownerLeases)
        {if(ownerLeases==null)throw new ArgumentNullException("ownerLeases");leases=ownerLeases;Mixer=new RuntimeMixer(bytes);Router=new AudioRouter(catalog,Mixer);}
        private void Check(CueIdentity id){Control();if(disposed)throw new ObjectDisposedException("AudioRuntimeSession");if(id==null||!leases.IsCurrent(id.Session,id.Owner,id.Epoch))throw new InvalidOperationException("Stale/unbound audio owner");}
        public bool Once(CueIdentity id,string kind,string subject,bool weapon,int variant,Float3 at,bool spatial,float gain,float pitch,AudioEmitterRole role)
        {
            Check(id);if(paused.Contains(id.OwnerKey))return false;
            Mixer.EmissionOwner=id.OwnerKey;try{return Router.Once(id.EventKey,kind,subject,weapon,variant,at,spatial,gain,pitch,role);}finally{Mixer.EmissionOwner=null;}
        }
        public void Loop(CueIdentity id,string slot,bool active,string kind,string subject,bool weapon,Float3 at,bool spatial,float gain,float pitch,AudioEmitterRole role)
        {Check(id);Mixer.EmissionOwner=id.OwnerKey;try{Router.SetLoop(id.OwnerKey,slot,active,kind,subject,weapon,at,spatial,gain,pitch,role);}finally{Mixer.EmissionOwner=null;}}
        public void Flight(CueIdentity id,float dt,bool gliding,float speed,float altitude,bool sheltered,Float3 at)
        {
            Check(id);FlightAudio state;if(!flight.TryGetValue(id.OwnerKey,out state)){state=new FlightAudio();flight.Add(id.OwnerKey,state);}
            Mixer.EmissionOwner=id.OwnerKey;try{state.Tick(Router,id.OwnerKey,paused.Contains(id.OwnerKey)?0:dt,gliding,speed,altitude,sheltered,at);}finally{Mixer.EmissionOwner=null;}
        }
        public void ReceiveVoice(CueIdentity id,byte[] authenticatedPcm16,Float3 at,float playerGain){Check(id);if(!paused.Contains(id.OwnerKey))Mixer.ReceiveVoice(id.OwnerKey,authenticatedPcm16,at,playerGain);}
        public void PauseOwner(CueIdentity id,bool pause){Check(id);Mixer.Proximity.Pause(id.OwnerKey,pause);Mixer.PauseOwner(id.OwnerKey,pause);if(pause)paused.Add(id.OwnerKey);else paused.Remove(id.OwnerKey);}
        // Cleanup must remain callable after the lease was revoked.
        public void Despawn(CueIdentity id){Control();if(id==null)throw new ArgumentNullException("id");Mixer.Proximity.Release(id.OwnerKey);Router.ReleaseOwner(id.OwnerKey);Mixer.ReleaseOwner(id.OwnerKey);paused.Remove(id.OwnerKey);flight.Remove(id.OwnerKey);}
        public void Dispose(){Control();if(disposed)return;Router.Dispose();Mixer.Dispose();flight.Clear();paused.Clear();disposed=true;}
    }
}
