using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Integration.AudioRuntime
{
    // Receive/playback only. Root authenticates canonical sender/epoch/connection,
    // consent, distance and packet sequence before Push. No microphone/network IO.
    // Source: pinned voice_r4.gd HZ/FRAMES/BYTES, queue5, unit6/max40.
    internal sealed class ProximityVoicePcm
    {
        private sealed class Peer
        {internal readonly float[] Ring=new float[1600];internal int Read,Count;internal double Phase;internal Float3 At;internal float Gain;internal bool Paused;}
        private readonly Dictionary<string,Peer> peers=new Dictionary<string,Peer>();
        private readonly object gate=new object();
        internal const int BytesPerPeer=1600*4;
        internal long RetainedSampleBytes {get{lock(gate){return (long)peers.Count*BytesPerPeer;}}}
        internal bool Contains(string owner){lock(gate){return peers.ContainsKey(owner);}}
        public int PeerCount {get{lock(gate){return peers.Count;}}}
        public void Push(string owner,byte[] pcm16,Float3 at,float gain)
        {
            if(string.IsNullOrEmpty(owner)||pcm16==null||pcm16.Length!=640||!at.IsFinite||float.IsNaN(gain)||gain<0||gain>1)throw new ArgumentException("Voice packet");
            lock(gate)
            {
                Peer p;if(!peers.TryGetValue(owner,out p)){if(peers.Count>=32)throw new InvalidOperationException("Voice peer budget");p=new Peer();peers.Add(owner,p);}
                p.At=at;p.Gain=gain;if(p.Paused)return;
                if(p.Count>1280){int drop=p.Count-1280;p.Read=(p.Read+drop)%1600;p.Count-=drop;}
                for(int i=0;i<320;i++){p.Ring[(p.Read+p.Count)%1600]=(short)(pcm16[i*2]|pcm16[i*2+1]<<8)/32767f;p.Count++;}
            }
        }
        public void Pause(string owner,bool pause){lock(gate){Peer p;if(peers.TryGetValue(owner,out p)){p.Paused=pause;if(pause){p.Read=0;p.Count=0;p.Phase=0;}}}}
        public void Release(string owner){lock(gate){peers.Remove(owner);}}
        public void Clear(){lock(gate){peers.Clear();}}
        internal void Mix(float[] output,int channels,int rate,Float3 listener,float busGain)
        {
            lock(gate)foreach(var p in peers.Values)
            {
                double x=(double)p.At.X-listener.X,y=(double)p.At.Y-listener.Y,z=(double)p.At.Z-listener.Z,d=Math.Sqrt(x*x+y*y+z*z);
                float gain=d>=40?0:(float)(Math.Min(1,1/(d/6+.00001))*(1-d/40))*p.Gain*busGain;
                if(p.Paused)continue;
                for(int i=0;i<output.Length;i+=channels)
                {
                    if(p.Count==0){p.Phase=0;break;}
                    float a=p.Ring[p.Read],b=p.Count>1?p.Ring[(p.Read+1)%1600]:a;
                    float sample=(a+(b-a)*(float)p.Phase)*gain;for(int c=0;c<channels;c++)output[i+c]+=sample;
                    p.Phase+=16000.0/rate;int advance=Math.Min((int)p.Phase,p.Count);p.Read=(p.Read+advance)%1600;p.Count-=advance;p.Phase-=advance;if(p.Count==0)p.Phase=0;
                }
            }
        }
    }
}
