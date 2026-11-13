#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using Ashfall.Integration.Playable.Bots;

namespace Ashfall.Integration.Playable.Soundscape
{
    // Original music.gd + missing environmental portion of ambience.gd. Existing
    // traversal retains ownership of footsteps, climbing, impacts and glide SFX.
    // No listener, audio-driver, input or movement ownership is introduced.
    public sealed class R24SourceSoundscape:MonoBehaviour
    {
        [Serializable] sealed class Manifest {public int version=0;public ClipRow[] clips=null;}
        [Serializable] sealed class ClipRow {public string kind=null,file=null,sha256=null,pcmSha256=null;public int variant=0,channels=0,rate=0,frames=0,bytes=0;}
        readonly R24MusicState musicState=new R24MusicState();readonly R24AmbienceState ambienceState=new R24AmbienceState();
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();readonly List<AudioClip> resources=new List<AudioClip>();
        readonly AudioSource[] music=new AudioSource[3],turbines=new AudioSource[2];AudioSource wind,creak;R23VRRig rig;Manifest manifest;
        readonly System.Random random=new System.Random(71731);
        bool disposed,musicLoading,ambienceLoading,musicStarted,musicPaused,wasActive;
        float musicVolume,ambienceVolume;double scheduledStart;int creakVariant;UnityWebRequest pending;string currentMap;
        public string Failure {get;private set;}
        public bool MusicReady {get;private set;}
        public bool AmbienceReady {get;private set;}
        public bool ManifestReady {get{return manifest!=null;}}
        public int LoadedClipCount {get{return resources.Count;}}
        public int MusicStarts {get;private set;}
        public int CreaksPlayed {get;private set;}
        public int ShelterQueries {get;private set;}
        public bool Sheltered {get;private set;}
        public bool Active {get;private set;}
        public float MusicVolume {get{return musicVolume;}}
        public float AmbienceVolume {get{return ambienceVolume;}}
        public float WindDb {get;private set;}=-25;
        public R24MusicFrame MusicMix {get;private set;}
        public double MusicScheduledStart {get{return scheduledStart;}}
        public double VirtualMusicSeconds {get{return musicState.PlaybackSeconds;}}
        public int ActiveTurbines {get{int n=0;foreach(var voice in turbines)if(voice!=null&&voice.isPlaying)n++;return n;}}
        public void Initialize(R23VRRig player)
        {
            if(rig!=null||player==null)throw new ArgumentException("One soundscape rig");rig=player;
            wind=Voice("Original world wind",false);wind.loop=true;
            creak=Voice("Original environmental creak",true);creak.minDistance=0;creak.maxDistance=35;creak.rolloffMode=AudioRolloffMode.Custom;creak.SetCustomCurve(AudioSourceCurveType.CustomRolloff,R24KestrelAudio.CreateAttenuationCurve(10,35,-21,-21));
            for(int i=0;i<3;i++){music[i]=Voice("Original synchronized music stem "+i,false);music[i].loop=true;}
            for(int i=0;i<2;i++){turbines[i]=Voice("Original Valley turbine "+i,true);turbines[i].loop=true;turbines[i].minDistance=0;turbines[i].maxDistance=48;turbines[i].rolloffMode=AudioRolloffMode.Custom;turbines[i].SetCustomCurve(AudioSourceCurveType.CustomRolloff,R24KestrelAudio.CreateAttenuationCurve(7,48,-24,-24));turbines[i].pitch=i==0?1:.94f;}
            StartCoroutine(Guard(ReadManifest()));
        }
        AudioSource Voice(string name,bool spatial)
        {var go=new GameObject(name);go.transform.SetParent(transform,false);var voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=spatial?1:0;voice.dopplerLevel=0;voice.volume=0;return voice;}
        public void SetVolumes(float musicGain,float ambienceGain)
        {
            if(!R24MusicState.Finite(musicGain)||!R24MusicState.Finite(ambienceGain))throw new ArgumentOutOfRangeException("source bus volume");
            musicVolume=Mathf.Clamp01(musicGain);ambienceVolume=Mathf.Clamp01(ambienceGain);ApplyGains();MaybeLoad();
        }
        public void Step(float dt,bool active,string mapId,Transform mapFrame,float detection,bool extracting=false)
        {
            R24MusicState.Validate(dt);if(disposed||rig==null)return;Active=active;
            // Actual Valley mission owner supplies extraction; no fabricated radio
            // or multiplayer round-end state is used for the source mix.
            MusicMix=musicState.Step(dt,detection,extracting,!active,false);
            MaybeLoad();
            if(active&&ambienceState.ShelterQueryDue(dt)){Sheltered=Physics.Raycast(rig.transform.position+Vector3.up*1.7f,Vector3.up,7,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore);ShelterQueries++;}
            var environment=ambienceState.Step(dt,active,rig.transform.position.y,Sheltered);WindDb=environment.WindDb;
            if(MusicReady){
                if(active&&!musicStarted)StartMusic();
                if(musicStarted){if(!MusicMix.PauseStreams&&musicPaused){foreach(var voice in music)voice.UnPause();musicPaused=false;}else if(MusicMix.PauseStreams&&!musicPaused){foreach(var voice in music)voice.Pause();musicPaused=true;}}
            }
            if(currentMap!=mapId){currentMap=mapId;foreach(var voice in turbines)voice.Stop();}
            if(AmbienceReady&&active){
                if(!wind.isPlaying)wind.Play();
                bool valley=mapId=="valley"&&mapFrame!=null;
                for(int i=0;i<2;i++){var voice=turbines[i];if(valley){voice.transform.position=mapFrame.TransformPoint(new Vector3(-18+(i==0?-5:5),7,40));if(!voice.isPlaying)voice.Play();}else voice.Stop();}
                if(environment.Creak&&ambienceVolume>0){creak.Stop();creak.transform.position=rig.transform.position+new Vector3(Random(-12,12),Random(3,9),-Random(-16,8));creak.pitch=Random(.85f,1.12f);creak.clip=clips["creak/"+creakVariant++%3];creak.Play();CreaksPlayed++;}
            }else if(wasActive||!active){wind.Stop();creak.Stop();creak.clip=null;foreach(var voice in turbines)voice.Stop();}
            wasActive=active;ApplyGains();
        }
        void StartMusic()
        {
            int rate=music[0].clip.frequency,frames=music[0].clip.samples;
            foreach(var voice in music)if(voice.clip.frequency!=rate||voice.clip.samples!=frames)throw new InvalidOperationException("Source music stems must share tempo/duration/sample clock");
            int phase=(int)(musicState.PlaybackSeconds*rate%frames);scheduledStart=UnityEngine.AudioSettings.dspTime+.08;
            foreach(var voice in music){voice.timeSamples=phase;voice.PlayScheduled(scheduledStart);}musicStarted=true;musicPaused=false;MusicStarts++;
        }
        void ApplyGains()
        {
            if(wind!=null)wind.volume=Mathf.Pow(10,WindDb/20)*ambienceVolume;if(creak!=null)creak.volume=Mathf.Pow(10,-21/20f)*ambienceVolume;
            foreach(var voice in turbines)if(voice!=null)voice.volume=Mathf.Pow(10,-24/20f)*ambienceVolume;
            if(music[0]!=null){music[0].volume=MusicMix.Exploration*musicVolume;music[1].volume=MusicMix.Combat*musicVolume;music[2].volume=MusicMix.Extraction*musicVolume;}
        }
        IEnumerator ReadManifest()
        {
            byte[] bytes=null;yield return Read("audio.json",b=>bytes=b);manifest=JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(bytes));
            if(manifest==null||manifest.version!=1||manifest.clips==null||manifest.clips.Length!=8)throw new InvalidOperationException("Source soundscape manifest");MaybeLoad();
        }
        void MaybeLoad()
        {
            if(disposed||rig==null||manifest==null||Failure!=null)return;
            // Start preparing when the user enables a channel, including while
            // its settings page is paused. Zero/default channels allocate no PCM.
            if(musicVolume>0&&!MusicReady&&!musicLoading){musicLoading=true;StartCoroutine(Guard(LoadGroup(true)));}
            if(ambienceVolume>0&&!AmbienceReady&&!ambienceLoading){ambienceLoading=true;StartCoroutine(Guard(LoadGroup(false)));}
        }
        IEnumerator LoadGroup(bool score)
        {
            foreach(var row in manifest.clips){if((row.kind=="music")!=score)continue;byte[] bytes=null;yield return Read(row.file,b=>bytes=b);
                if(bytes.Length!=row.bytes||Sha(bytes,0,bytes.Length)!=row.sha256)throw new InvalidOperationException("Source soundscape file identity: "+row.file);
                var decoded=R24Pcm16Wave.Decode(bytes,false);if(decoded.Channels!=row.channels||decoded.Rate!=row.rate||decoded.Frames!=row.frames||Sha(bytes,decoded.DataOffset,decoded.DataBytes)!=row.pcmSha256)throw new InvalidOperationException("Source soundscape PCM identity: "+row.file);
                var clip=AudioClip.Create(row.file,decoded.Frames,decoded.Channels,decoded.Rate,false);resources.Add(clip);
                // Bound conversion/upload per loader frame rather than decoding
                // and copying an entire stereo score stem in a gameplay tick.
                const int chunkFrames=32768;float[] samples=null;
                for(int frame=0;frame<decoded.Frames;){int count=Mathf.Min(chunkFrames,decoded.Frames-frame);if(samples==null||samples.Length!=count*decoded.Channels)samples=new float[count*decoded.Channels];decoded.ReadFrames(bytes,frame,samples);if(!clip.SetData(samples,frame))throw new InvalidOperationException("Source soundscape PCM upload");frame+=count;yield return null;}
                clips.Add(row.kind+"/"+row.variant,clip);
            }
            if(score){for(int i=0;i<3;i++)music[i].clip=clips["music/"+i];MusicReady=true;musicLoading=false;}
            else{wind.clip=clips["wind/0"];foreach(var voice in turbines)voice.clip=clips["turbine/0"];AmbienceReady=true;ambienceLoading=false;}
        }
        IEnumerator Read(string file,Action<byte[]> accept)
        {
            if(file.IndexOf('/')>=0||file.IndexOf('\\')>=0||file.Contains(".."))throw new ArgumentException("Source soundscape basename");string url=Application.streamingAssetsPath+"/PlayableR23/Soundscape/"+file;if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            using(var request=UnityWebRequest.Get(url)){request.timeout=30;pending=request;try{yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success)throw new InvalidOperationException(file+": "+request.error);accept(request.downloadHandler.data);}finally{if(pending==request)pending=null;}}
        }
        IEnumerator Guard(IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);try{while(stack.Count>0){object value;try{var top=stack.Peek();if(!top.MoveNext()){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}value=top.Current;}catch(Exception e){Failure=e.ToString();Debug.LogWarning("Source soundscape unavailable: "+Failure,this);yield break;}var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;}}
            finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}
        }
        static string Sha(byte[] bytes,int offset,int count){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes,offset,count)).Replace("-","").ToLowerInvariant();}
        float Random(float minimum,float maximum){return minimum+(float)random.NextDouble()*(maximum-minimum);}
        public bool TryReadClip(string key,out AudioClip clip){return clips.TryGetValue(key,out clip);}
        public AudioSource ReadMusicVoice(int index){return index>=0&&index<3?music[index]:null;}
        public AudioSource ReadWindVoice(){return wind;}
        public AudioSource ReadCreakVoice(){return creak;}
        public AudioSource ReadTurbineVoice(int index){return index>=0&&index<2?turbines[index]:null;}
        public void SuspendEnvironment(){Active=wasActive=false;if(wind!=null)wind.Stop();if(creak!=null){creak.Stop();creak.clip=null;}foreach(var voice in turbines)if(voice!=null)voice.Stop();}
        public void Shutdown()
        {
            if(disposed)return;disposed=true;StopAllCoroutines();foreach(var voice in GetComponentsInChildren<AudioSource>(true)){voice.Stop();voice.clip=null;}
            foreach(var clip in resources)if(clip!=null)Destroy(clip);resources.Clear();clips.Clear();MusicReady=AmbienceReady=Active=false;
        }
        void OnDestroy(){Shutdown();}
    }
}
#endif
