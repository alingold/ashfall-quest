using System;
namespace Ashfall.Integration.AudioRuntime
{
    // Captured by one native activation's event and PCM closures. Never resolves a replacement.
    public sealed class AudioActivation
    {
        private readonly object gate=new object();
        private readonly AudioRuntimeSession session;
        private bool pending,closed;
        public AudioActivation(AudioRuntimeSession value){if(value==null)throw new ArgumentNullException("value");session=value;}
        public bool Pending {get{lock(gate){return pending;}}}
        public void Invalidate(){lock(gate){pending=true;}}
        public void Render(float[] data,int rate){lock(gate){if(pending||closed){Array.Clear(data,0,data.Length);return;}session.Mixer.Render(data,2,rate);}}
        // Root control lane only. No native/managed disposal from event/audio callbacks.
        public void Close(){lock(gate){if(closed)return;pending=true;closed=true;session.Dispose();}}
    }
}
