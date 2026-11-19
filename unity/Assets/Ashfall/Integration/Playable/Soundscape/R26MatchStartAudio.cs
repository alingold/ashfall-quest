#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.UI;
namespace Ashfall.Integration.Playable.Soundscape
{
    // The LAN countdown owns the downbeat. Offline rounds play from the local live edge.
    public sealed class R26MatchStartAudio:MonoBehaviour
    {
        const double Downbeat=.96, ScheduleAhead=.10;
        R23PlayableLoop root;AudioSource voice;AudioClip clip;R24LanLobby playedLobby;long epoch=-1;int round;
        public int Starts {get;private set;}
        public string Failure {get;private set;}
        public void Initialize(R23PlayableLoop owner)
        {
            root=owner;var child=new GameObject("R26 match start sting");child.transform.SetParent(transform,false);
            voice=child.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=0;voice.dopplerLevel=0;voice.priority=64;
            clip=Resources.Load<AudioClip>("R26MatchStart");
            if(clip==null){Failure="R26MatchStart resource missing";Debug.LogWarning("R26 match start cue unavailable: "+Failure);}
        }
        void LateUpdate()
        {
            if(root==null||voice==null||clip==null)return;
            if(!root.Ready||root.Lobby==null){voice.Stop();return;}
            voice.volume=R23MenuAudioEdges.Gain(root.UI.Settings.SfxVolume);
            var lobby=root.LanSession==null?null:root.LanSession.Lobby;
            if(lobby==null){
                if(!root.MatchActive||root.Lobby.Visible){voice.Stop();return;}
                Play(null,root.MatchEpoch,root.MatchRound,false,0);
                return;
            }
            var selection=lobby.SelectedMap;
            if(selection==null||!selection.Active||selection.Cancelled||selection.Results||selection.Rules==null||!lobby.OwnWorldLoaded||!lobby.ClockSynchronized){voice.Stop();return;}
            double countdown=lobby.RoundCountdown;
            if(countdown>Downbeat+ScheduleAhead)return;
            if(countdown>0||(root.MatchActive&&root.LanRoundBound&&root.MatchRound==selection.Round&&!root.Lobby.Visible))
                Play(lobby,selection.Epoch,selection.Round,true,countdown);
        }
        void Play(R24LanLobby lobby,long nextEpoch,int nextRound,bool synchronized,double countdown)
        {
            if(playedLobby==lobby&&epoch==nextEpoch&&round==nextRound)return;
            playedLobby=lobby;epoch=nextEpoch;round=nextRound;voice.Stop();
            if(voice.volume<=0)return;
            double delay=synchronized?countdown-Downbeat:0;
            voice.clip=clip;voice.timeSamples=delay<0?(int)Math.Round(-delay*clip.frequency):0;
            int offset=voice.timeSamples;
            if(delay>0)voice.PlayScheduled(UnityEngine.AudioSettings.dspTime+delay);else voice.Play();
            Starts++;
            if(R25NetBot.Requested)Debug.LogFormat("R26 MUSIC start clip={0} length={1:F3} channels={2} rate={3} load={4} epoch={5} round={6} countdown={7:F3} delay={8:F3} offset={9} gain={10:F3}",clip.name,clip.length,clip.channels,clip.frequency,clip.loadState,epoch,round,countdown,delay,offset,voice.volume);
        }
        void OnDisable(){if(voice!=null)voice.Stop();}
        void OnDestroy(){if(voice!=null)Destroy(voice.gameObject);}
    }
}
#endif
