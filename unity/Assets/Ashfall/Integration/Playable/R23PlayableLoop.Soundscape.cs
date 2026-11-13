#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Soundscape;
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        public R24SourceSoundscape Soundscape {get;private set;}
        void InitializeSoundscape()
        {
            Soundscape=new GameObject("Original music and environmental ambience").AddComponent<R24SourceSoundscape>();Soundscape.transform.SetParent(transform,false);Soundscape.Initialize(Rig);Soundscape.SetVolumes(UI.Settings.MusicVolume,UI.Settings.AmbienceVolume);
        }
        public void StepSoundscape(float dt,bool hardware)
        {
            if(Soundscape==null)return;
            bool world=Ready&&roundStarted&&!switchingMap&&Map!=null&&Map.IsLoaded&&Lobby!=null&&!Lobby.Visible;
            bool active=world&&hardware&&!PageBlocksGameplay&&!Rig.Paused&&Gameplay!=null&&Gameplay.Health>0;
            float detection=ValleyBots!=null&&ValleyBots.EncounterActive?ValleyBots.MaximumDetection:0;
            Soundscape.Step(Mathf.Clamp(dt,0,.25f),active,world?Map.MapId:null,world?Map.transform:null,detection,world&&Map.MapId=="valley"&&ValleyBots!=null&&ValleyBots.Mission.Chapter>=3);
        }
    }
}
#endif
