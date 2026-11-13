#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.XR;
namespace Ashfall.Integration.Playable.Diagnostics
{
    // Opt-in device diagnostic. Normal player launches create no probe and do
    // not change maps, input, quality, tracking, inventory or frame-rate settings.
    public sealed class R24QuestPerformanceProbe:MonoBehaviour
    {
        [Serializable] sealed class TextureRow {public string name,format;public int width,height,mips;public long bytes;}
        [Serializable] sealed class AudioVoice {public string name,clip,loadState;public bool virtualized,muted;public float gain,spatialBlend,distanceFromHead;}
        [Serializable] sealed class AudioSnapshot {
            public double dspTime,mixRms,mixPeak;public int outputRate,dspBufferSize,realVoices,virtualVoices,listeners,enabledListeners,sources,activeSources,playingSources,mutedPlayingSources;
            public float masterVolume;public bool listenerPaused,mixSampled,headsetAudibilityVerified=false;
            public long lastAcceptedShot;public string speakerMode,error="",hardwareRoute="unavailable";
            public List<AudioVoice> playing=new List<AudioVoice>();
        }
        [Serializable] sealed class Snapshot {
            public string phase,map,calibration,weapon;public double clock;public int textures,materials,meshes,renderers,enabledRenderers,visibleRenderers,colliders;
            public long textureBytes,meshBytes,allocatedBytes,reservedBytes;public bool headTracked,xrRunning,xrFocused,applicationFocused;
            public Vector3 rawHead,trackingOrigin;public int ownedWeapons,worldWeapons,eyeWidth,eyeHeight,aa,targetRate;public float viewportScale;
            public List<TextureRow> largestTextures=new List<TextureRow>();
            public AudioSnapshot audio;
        }
        [Serializable] sealed class Sample {public string phase,map;public double seconds,fps,cpuMs,gpuMs;public bool timingAvailable,tracked,focused;public long allocatedBytes;public AudioSnapshot audio;}
        [Serializable] sealed class Result {public string requestedMap,error="",output;public bool complete,headsetAcceptance=false;public List<Snapshot> snapshots=new List<Snapshot>();public List<Sample> samples=new List<Sample>();}
        R23PlayableLoop root;Result result;string output;float deadline;
        readonly FrameTiming[] timings=new FrameTiming[1];
        readonly float[] mixedAudio=new float[256];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BeginIfRequested()
        {
            string map=null,audio=null;
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))using(var intent=activity.Call<AndroidJavaObject>("getIntent"))
            {map=intent.Call<string>("getStringExtra","ashfall_performance_map");audio=intent.Call<string>("getStringExtra","ashfall_audio_probe");}
#else
            var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++){if(args[i]=="-ashfallPerformanceMap")map=args[i+1];if(args[i]=="-ashfallAudioProbe")audio=args[i+1];}
#endif
            if(audio=="true"){var audioProbe=new GameObject("Explicit passive audio diagnostic").AddComponent<R24QuestPerformanceProbe>();audioProbe.StartCoroutine(audioProbe.RunAudio());return;}
            if(String.IsNullOrEmpty(map))return;
            // An explicit launch may select only a real, packaged map.
            if(World.R23MapCatalog.Find(map)==null){Debug.LogError("ASHFALL PERF invalid requested map "+map);return;}
            var probe=new GameObject("Explicit Quest performance diagnostic").AddComponent<R24QuestPerformanceProbe>();probe.StartCoroutine(probe.Run(map));
        }
        // Passive mode never selects a map or synthesizes input/tones. It records
        // the real current route and audio graph only after an explicit opt-in.
        IEnumerator RunAudio()
        {
            deadline=Time.realtimeSinceStartup+150;output=Path.Combine(Application.persistentDataPath,"audio-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json");result=new Result{requestedMap="passive-current-route",output=output};
            while(Time.realtimeSinceStartup<deadline){root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(root!=null&&(root.Ready||root.Failure!=null))break;yield return null;}
            if(root==null||!root.Ready){result.error="Root not ready: "+(root==null?"missing":root.Failure);Save();yield break;}
            Capture("passive-audio-start");yield return Observe("passive-audio",30);Capture("passive-audio-final");result.complete=true;Save();Debug.Log("ASHFALL AUDIO DIAGNOSTIC COMPLETE "+output);
        }
        IEnumerator Run(string map)
        {
            deadline=Time.realtimeSinceStartup+150;output=Path.Combine(Application.persistentDataPath,"performance-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json");result=new Result{requestedMap=map,output=output};
            while(Time.realtimeSinceStartup<deadline){root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(root!=null&&(root.Ready||root.Failure!=null))break;yield return null;}
            if(root==null||!root.Ready){result.error="Root not ready: "+(root==null?"missing":root.Failure);Save();yield break;}
            Capture("initial-lobby");yield return Observe("initial-lobby",3);
            root.ReturnToLobby();root.SelectMap(map,true);
            while((!root.Ready||root.Map.MapId!=map)&&root.Failure==null&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!root.Ready||root.Map.MapId!=map){result.error="Actual map failed: "+root.Failure;Save();yield break;}
            root.UI.ClearMatchSelection();root.StartPlaying();
            // First samples deliberately include activation/settlement; the later
            // window is separately labelled rather than averaging load stalls away.
            Capture("map-enter");yield return Observe("map-activation",8);Capture("map-settled");yield return Observe("map-steady",15);Capture("map-final");
            result.complete=true;Save();Debug.Log("ASHFALL PERF COMPLETE "+output);
            // Leave the current app/route alone. Only the diagnostic owner may
            // subsequently stop its explicitly launched process.
        }
        IEnumerator Observe(string phase,float duration)
        {
            float end=Mathf.Min(deadline,Time.realtimeSinceStartup+duration),start=Time.realtimeSinceStartup;int frames=0;
            while(Time.realtimeSinceStartup<end){yield return null;frames++;float now=Time.realtimeSinceStartup;if(now-start<1)continue;
                FrameTimingManager.CaptureFrameTimings();uint count=FrameTimingManager.GetLatestTimings(1,timings);
                var row=new Sample{phase=phase,map=root.Map.MapId,seconds=now,fps=frames/(double)(now-start),timingAvailable=count>0&&timings[0].cpuFrameTime>0,cpuMs=count>0?timings[0].cpuFrameTime:0,gpuMs=count>0?timings[0].gpuFrameTime:0,tracked=root.Rig.Tracking,focused=root.Rig.SessionFocused,allocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),audio=ReadAudio()};result.samples.Add(row);Save();Debug.Log("ASHFALL PERF SAMPLE "+JsonUtility.ToJson(row));frames=0;start=now;
            }
        }
        void Capture(string phase)
        {
            var row=new Snapshot{phase=phase,map=root.Map.MapId,clock=Time.realtimeSinceStartup,calibration=root.Calibration.Status,weapon=root.Gameplay.ActiveWeaponId,headTracked=root.Rig.Tracking,xrRunning=root.Rig.DisplayRunning,xrFocused=root.Rig.SessionFocused,applicationFocused=Application.isFocused,rawHead=root.Rig.Head.transform.localPosition,trackingOrigin=root.Rig.TrackingOrigin.localPosition,ownedWeapons=root.Gameplay.OwnedWeaponActionIds.Length,worldWeapons=root.Gameplay.WorldWeaponActionIds.Length,allocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),reservedBytes=Profiler.GetTotalReservedMemoryLong(),eyeWidth=XRSettings.eyeTextureWidth,eyeHeight=XRSettings.eyeTextureHeight,viewportScale=XRSettings.renderViewportScale,aa=QualitySettings.antiAliasing,targetRate=Application.targetFrameRate};
            foreach(var texture in Resources.FindObjectsOfTypeAll<Texture2D>()){long bytes=Profiler.GetRuntimeMemorySizeLong(texture);row.textures++;row.textureBytes+=bytes;row.largestTextures.Add(new TextureRow{name=texture.name,format=texture.format.ToString(),width=texture.width,height=texture.height,mips=texture.mipmapCount,bytes=bytes});}
            row.largestTextures.Sort((a,b)=>b.bytes.CompareTo(a.bytes));if(row.largestTextures.Count>40)row.largestTextures.RemoveRange(40,row.largestTextures.Count-40);
            foreach(var mesh in Resources.FindObjectsOfTypeAll<Mesh>()){row.meshes++;row.meshBytes+=Profiler.GetRuntimeMemorySizeLong(mesh);}
            row.materials=Resources.FindObjectsOfTypeAll<Material>().Length;
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)){row.renderers++;if(renderer.enabled&&renderer.gameObject.activeInHierarchy)row.enabledRenderers++;if(renderer.isVisible)row.visibleRenderers++;}
            row.colliders=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length;row.audio=ReadAudio();result.snapshots.Add(row);Save();Debug.Log("ASHFALL PERF SNAPSHOT "+JsonUtility.ToJson(row));
        }
        AudioSnapshot ReadAudio()
        {
            var state=new AudioSnapshot();
            try{
                var config=UnityEngine.AudioSettings.GetConfiguration();state.dspTime=UnityEngine.AudioSettings.dspTime;state.outputRate=UnityEngine.AudioSettings.outputSampleRate;state.dspBufferSize=config.dspBufferSize;state.realVoices=config.numRealVoices;state.virtualVoices=config.numVirtualVoices;state.speakerMode=config.speakerMode.ToString();state.masterVolume=AudioListener.volume;state.listenerPaused=AudioListener.pause;
                foreach(var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include)){state.listeners++;if(listener.enabled&&listener.gameObject.activeInHierarchy)state.enabledListeners++;}
                foreach(var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include)){
                    state.sources++;if(source.isActiveAndEnabled)state.activeSources++;if(!source.isPlaying)continue;state.playingSources++;if(source.mute)state.mutedPlayingSources++;
                    if(state.playing.Count<16)state.playing.Add(new AudioVoice{name=source.name,clip=source.clip==null?"":source.clip.name,loadState=source.clip==null?"none":source.clip.loadState.ToString(),virtualized=source.isVirtual,muted=source.mute,gain=source.volume,spatialBlend=source.spatialBlend,distanceFromHead=root==null?0:Vector3.Distance(source.transform.position,root.Rig.Head.transform.position)});
                }
                state.lastAcceptedShot=root==null?0:root.Gameplay.LastAcceptedNativeShot;
                if(state.enabledListeners>0&&state.outputRate>0&&!state.listenerPaused){AudioListener.GetOutputData(mixedAudio,0);double sum=0;foreach(float value in mixedAudio){double sample=value;sum+=sample*sample;state.mixPeak=Math.Max(state.mixPeak,Math.Abs(sample));}state.mixRms=Math.Sqrt(sum/mixedAudio.Length);state.mixSampled=true;}
            }catch(Exception error){state.error=error.ToString();}
            // A nonzero Unity mix proves software samples only. Hardware routing
            // and human audibility remain explicitly unobserved.
            return state;
        }
        void Save(){File.WriteAllText(output,JsonUtility.ToJson(result,true));}
    }
}
#endif
