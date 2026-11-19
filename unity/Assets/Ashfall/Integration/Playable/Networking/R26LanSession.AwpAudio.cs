#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;

namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        readonly AudioSource[] awpCueVoices=new AudioSource[8];int awpCueCursor;
        bool awpCueBound;
        void ForwardAwpCue(byte cue)
        {
            if(peer==null)return;
            PublishRawTracking();
            peer.Lobby.RequestAwpCue(cue);
        }
        void SyncAwpAudio()
        {
            if(!awpCueBound)awpCueBound=root.Gameplay.BindAwpCue(ForwardAwpCue);
            R24LanLobby.R26RemoteAwpCue cue;
            while(peer.Lobby.TryDequeueAwpCue(out cue)){
                if(!root.Ready||PresenceScene=="menu")continue;
                Vector3 at=new Vector3(cue.At.X,cue.At.Y,cue.At.Z);
                if((root.Rig.Head.transform.position-at).sqrMagnitude>R26AwpAudio.CueRange*R26AwpAudio.CueRange)continue;
                int i=awpCueCursor++%awpCueVoices.Length;
                var voice=awpCueVoices[i];
                if(voice==null){
                    var node=new GameObject("Remote AWP mechanical voice "+i);node.transform.SetParent(transform,false);
                    voice=node.AddComponent<AudioSource>();awpCueVoices[i]=voice;voice.playOnAwake=false;voice.spatialBlend=1;
                    voice.rolloffMode=AudioRolloffMode.Logarithmic;voice.minDistance=2;voice.maxDistance=R26AwpAudio.CueRange;voice.dopplerLevel=0;
                }
                voice.transform.position=at;root.Gameplay.PlayRemoteAwpCue(cue.Actor,cue.Cue,voice);
            }
        }
    }
}
#endif
