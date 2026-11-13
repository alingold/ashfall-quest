#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Bots;
using Ashfall.Integration.Playable.UI;
using Ashfall.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        [Serializable] sealed class SourcePcmRows {public SourcePcmRow[] clips=null,files=null;}
        [Serializable] sealed class SourcePcmRow {public string file=null,kind=null,pcmSha256=null,sha256=null;public int variant=0,channels=0,rate=0,frames=0;}
        static object AudioSlider(R23VrUI ui,string helpPrefix)
        {foreach(var button in (IEnumerable)Read(ui,"buttons"))if(((string)Read(button,"Help")??"").StartsWith(helpPrefix,StringComparison.Ordinal)&&Read(button,"SliderChanged")!=null)return button;throw new InvalidOperationException("Actual audio slider missing: "+helpPrefix);}
        static bool AudioRay(R23VrUI ui,object button,float fraction,bool held,bool valid=true,float y=0)
        {var surface=(Transform)Read(button,"Surface");var size=(Vector2)Read(button,"Size");Vector3 origin=surface.TransformPoint(new Vector3((fraction-.5f)*size.x,y,-.4f));return ui.UpdateLoadoutPointer(1,origin,surface.forward,held,false,valid);}
        static void DragAudio(R23VrUI ui,object button,float fraction)
        {AudioRay(ui,button,fraction,false);AudioRay(ui,button,fraction,true);AudioRay(ui,button,fraction,false);}
        static IEnumerator SourceSoundscape(R23PlayableLoop loop,Action<string> note)
        {
            const string preferencesKey="ashfall.r23.personal-ui-settings.v1";bool hadPreferences=PlayerPrefs.HasKey(preferencesKey);string savedPreferences=PlayerPrefs.GetString(preferencesKey,"");
            var original=loop.UI.Settings;float listener=AudioListener.volume;bool listenerPaused=AudioListener.pause;int callbacks=0;Action<R23UiSettings> count=delegate{callbacks++;};loop.UI.SettingsChanged+=count;
            try
            {
                loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card for source audio");float end=Time.realtimeSinceStartup+90;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");loop.StartPlaying();loop.SmokeUiUpdate();
                end=Time.realtimeSinceStartup+30;while(!loop.Soundscape.ManifestReady&&loop.Soundscape.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Soundscape.ManifestReady&&loop.Soundscape.Failure==null,"Actual source music/environment manifest");
                Need(loop.Soundscape.MusicVolume==original.MusicVolume&&loop.Soundscape.AmbienceVolume==original.AmbienceVolume,"Saved music/ambience settings bound without changing defaults");if(original.MusicVolume==0&&original.AmbienceVolume==0)Need(loop.Soundscape.LoadedClipCount==0,"Muted defaults allocate no score/environment PCM");
                loop.UI.OpenSettings();Pointer(loop.UI,Button(loop.UI,"GENERAL"),false);var music=AudioSlider(loop.UI,"Volume of the original synchronized music.");var ambient=AudioSlider(loop.UI,"World wind and environmental sounds.");var voice=AudioSlider(loop.UI,"Unavailable: player voice");
                Need((bool)Read(music,"Enabled")&&(bool)Read(ambient,"Enabled")&&!(bool)Read(voice,"Enabled"),"Actual connected music/ambience sliders active; unsupported voice locked");
                DragAudio(loop.UI,music,.35f);Need(Mathf.Abs(loop.UI.Settings.MusicVolume-.35f)<.001f&&Mathf.Abs(loop.Soundscape.MusicVolume-.35f)<.001f,"Actual music pointer callback reaches consumer");Need(loop.UI.CurrentSettingsHelp.Contains("music"),"Actual music hover help");
                AudioRay(loop.UI,music,.35f,false);AudioRay(loop.UI,music,.35f,true);AudioRay(loop.UI,music,.35f,true,true,.4f);Need(loop.UI.CurrentSettingsHelp.Contains("music"),"Held slider pins music explanation outside row");int beforeInvalid=callbacks;AudioRay(loop.UI,music,.35f,true,false);Need(callbacks==beforeInvalid&&!loop.UI.CurrentSettingsHelp.Contains("original synchronized music"),"Invalid/claimed hand cannot act or retain hover explanation");AudioRay(loop.UI,music,.35f,false);
                int beforeVoice=callbacks;float savedVoice=loop.UI.Settings.VoiceVolume;bool reserved=AudioRay(loop.UI,voice,.5f,false);AudioRay(loop.UI,voice,.8f,true);AudioRay(loop.UI,voice,.8f,false);Need(!reserved&&callbacks==beforeVoice&&loop.UI.Settings.VoiceVolume==savedVoice&&loop.UI.CurrentSettingsHelp.Contains("not connected"),"Locked voice explains itself without actions/hand reservation/preference loss");
                DragAudio(loop.UI,ambient,.4f);Need(Mathf.Abs(loop.UI.Settings.AmbienceVolume-.4f)<.001f&&Mathf.Abs(loop.Soundscape.AmbienceVolume-.4f)<.001f,"Actual ambience pointer callback reaches consumer");Need(loop.UI.Settings.MasterVolume==original.MasterVolume&&loop.UI.Settings.SfxVolume==original.SfxVolume,"New channels do not rewrite master/SFX preferences");
                AudioRay(loop.UI,music,.35f,false);CaptureAudioSettings(loop);loop.UI.SetPlaying();
                end=Time.realtimeSinceStartup+60;while((!loop.Soundscape.MusicReady||!loop.Soundscape.AmbienceReady)&&loop.Soundscape.Failure==null&&Time.realtimeSinceStartup<end){loop.StepSoundscape(.02f,true);yield return null;}Need(loop.Soundscape.MusicReady&&loop.Soundscape.AmbienceReady&&loop.Soundscape.Failure==null,"Eight actual PCM clips loaded/uploaded asynchronously");
                var rows=JsonUtility.FromJson<SourcePcmRows>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"PlayableR23/Soundscape/audio.json")));Need(rows.clips.Length==8&&loop.Soundscape.LoadedClipCount==8,"Bounded source music/environment bank");
                foreach(var row in rows.clips){AudioClip clip;Need(loop.Soundscape.TryReadClip(row.kind+"/"+row.variant,out clip),"Actual decoded clip "+row.file);VerifySourcePcm(clip,Path.Combine(Application.streamingAssetsPath,"PlayableR23/Soundscape",row.file),note);}
                for(int i=0;i<200;i++)loop.StepSoundscape(.02f,true);var mix=loop.Soundscape.MusicMix;Need(Mathf.Abs(mix.Level-.65f)<.0001f&&mix.Combat==0&&mix.Extraction==0,"Original exploration score/internal gain; no invented extraction/network cues");
                Need(Mathf.Abs(loop.Soundscape.ReadMusicVoice(0).volume-.65f*.85f*.35f)<.0001f&&loop.Soundscape.ReadMusicVoice(1).volume==0&&loop.Soundscape.ReadMusicVoice(2).volume==0,"Music personal bus applied exactly once");
                Need(loop.Soundscape.ReadWindVoice().clip!=null&&loop.Soundscape.ReadWindVoice().loop&&loop.Soundscape.ActiveTurbines==0,"Actual Range wind without relocating Valley turbines into another map");Need(Mathf.Abs(loop.Soundscape.ReadWindVoice().volume-Mathf.Pow(10,loop.Soundscape.WindDb/20)*.4f)<.00001f,"Actual wind source dB gain times ambience bus");
                int starts=loop.Soundscape.MusicStarts;double target=loop.Soundscape.MusicScheduledStart;end=Time.realtimeSinceStartup+3;while(UnityEngine.AudioSettings.dspTime<target+.1&&Time.realtimeSinceStartup<end)yield return null;
                int minimum=int.MaxValue,maximum=0;for(int i=0;i<3;i++){var v=loop.Soundscape.ReadMusicVoice(i);Need(v.loop&&v.clip.channels==2&&v.clip.frequency==48000&&v.clip.samples==1920000,"Three actual synchronized40s source stems");minimum=Mathf.Min(minimum,v.timeSamples);maximum=Mathf.Max(maximum,v.timeSamples);}int buffer,countBuffers;UnityEngine.AudioSettings.GetDSPBufferSize(out buffer,out countBuffers);bool clockAvailable=UnityEngine.AudioSettings.dspTime>=target+.1;
                if(clockAvailable)Need(maximum-minimum<=buffer,"Actual source stem sample clocks stay within one reported DSP buffer");note("Actual music commonDSP="+target+" now="+UnityEngine.AudioSettings.dspTime+" phaseRange="+minimum+".."+maximum+" buffer="+buffer+" clockAvailable="+clockAvailable+". No headset/listening claim.");
                loop.UI.OpenSettings();Pointer(loop.UI,Button(loop.UI,"GENERAL"),false);music=AudioSlider(loop.UI,"Volume of the original synchronized music.");ambient=AudioSlider(loop.UI,"World wind and environmental sounds.");DragAudio(loop.UI,music,0);DragAudio(loop.UI,ambient,0);Need(loop.Soundscape.ReadMusicVoice(0).volume==0&&loop.Soundscape.ReadWindVoice().volume==0,"Actual bus zero mutes both channels immediately");
                DragAudio(loop.UI,music,.35f);DragAudio(loop.UI,ambient,.4f);loop.UI.SetPlaying();loop.StepSoundscape(.02f,true);Need(loop.Soundscape.MusicStarts==starts,"Volume changes do not restart/reallocate synchronized score");
                loop.ReturnToLobby();for(int i=0;i<4;i++)loop.StepSoundscape(.1f,true);Need(loop.Soundscape.MusicMix.PauseStreams&&!loop.Soundscape.ReadWindVoice().isPlaying&&loop.Soundscape.ActiveTurbines==0,"Actual Home stops environment and source-fades/pauses music");
                note("Live settings callback/hover/drag/disabled-voice semantics,8actualPCMfiles,shared musicDSP/range wind,independent bus mute,resume without restart andHome cleanup passed. Extraction/radio mission bindings and headset audibility/performance remain unqualified. Listener before="+listener+" after="+AudioListener.volume+" pauseBefore="+listenerPaused+" after="+AudioListener.pause+" (no ad hoc listener/device writes).");
            }
            finally{loop.UI.SettingsChanged-=count;loop.UI.SetSettings(original);Call(loop,"ApplySettings",original);loop.ReturnToLobby();for(int i=0;i<4;i++)loop.StepSoundscape(.1f,true);if(hadPreferences)PlayerPrefs.SetString(preferencesKey,savedPreferences);else PlayerPrefs.DeleteKey(preferencesKey);PlayerPrefs.Save();}
        }
        static void CaptureAudioSettings(R23PlayableLoop loop)
        {
            var panel=(Transform)Read(loop.UI,"panel");foreach(var r in panel.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(m!=null)foreach(var e in ShaderUtil.GetShaderMessages(m.shader))Need(e.severity.ToString()!="Error",e.message);
            // General UI children are layer0, unlike the tactical-map board on5.
            // Isolate only these existing renderers for the capture and restore
            // every original layer; do not change production UI/world routing.
            var layers=new Dictionary<GameObject,int>();var go=new GameObject("UNMATCHED connected audio settings camera");
            try{loop.UI.RefreshTextLayout();var camera=go.AddComponent<Camera>();camera.enabled=false;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.fieldOfView=50;camera.nearClipPlane=.03f;camera.farClipPlane=10;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.09f,.13f);camera.transform.SetPositionAndRotation(panel.position-panel.forward*1.65f,Quaternion.LookRotation(panel.forward,panel.up));int active=0,frustum=0;var planes=GeometryUtility.CalculateFrustumPlanes(camera);
                foreach(var r in panel.GetComponentsInChildren<Renderer>(true)){if(!layers.ContainsKey(r.gameObject)){layers.Add(r.gameObject,r.gameObject.layer);r.gameObject.layer=31;}if(r.enabled&&r.gameObject.activeInHierarchy){active++;if(GeometryUtility.TestPlanesAABB(planes,r.bounds))frustum++;}}
                Need(active>20&&frustum>20,"Actual General UI renderers active/in camera frustum: "+active+"/"+frustum);string path=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),"audio-settings-unmatched.png");CaptureTacticalLandscape(camera,path);
                var texture=new Texture2D(2,2,TextureFormat.RGB24,false);try{Need(texture.LoadImage(File.ReadAllBytes(path)),"Saved General UI PNG readable");var pixels=texture.GetPixels32();int min=765,max=0;foreach(var pixel in pixels){int luma=pixel.r+pixel.g+pixel.b;min=Math.Min(min,luma);max=Math.Max(max,luma);}Need(max-min>100,"Actual UI spatial pixels, reject uniform clear: "+min+".."+max);}finally{UnityEngine.Object.Destroy(texture);}
            }finally{foreach(var entry in layers)if(entry.Key!=null)entry.Key.layer=entry.Value;UnityEngine.Object.Destroy(go);}
        }
        static IEnumerator SourceSoundscapeCapture(R23PlayableLoop loop,Action<string> note)
        {
            try{loop.UI.OpenSettings();Pointer(loop.UI,Button(loop.UI,"GENERAL"),false);var music=AudioSlider(loop.UI,"Volume of the original synchronized music.");AudioRay(loop.UI,music,.35f,false);yield return null;Need(loop.UI.Page==R23UiPage.Settings,"Actual General page retained");CaptureAudioSettings(loop);note("Actual General UI renderer/frustum and nonuniform saved pixels; UNMATCHED Unity-only capture. Temporary capture layers restored; no audio/preferences edits or replay.");}
            finally{loop.ReturnToLobby();}
        }
        static void VerifySourcePcm(AudioClip clip,string path,Action<string> note)
        {
            var pcm=Ashfall.Integration.Playable.Soundscape.R24Pcm16Wave.Decode(File.ReadAllBytes(path));Need(clip!=null&&clip.channels==pcm.Channels&&clip.frequency==pcm.Rate&&clip.samples==pcm.Frames,"Actual decoded source layout "+Path.GetFileName(path));var samples=new float[clip.samples*clip.channels];Need(clip.GetData(samples,0),"Actual decoded PCM readable");float maxError=0,peak=0;for(int i=0;i<samples.Length;i++){NeedFiniteSample(samples[i]);peak=Mathf.Max(peak,Mathf.Abs(samples[i]));maxError=Mathf.Max(maxError,Mathf.Abs(samples[i]-pcm.Samples[i]));}Need(peak>0&&maxError<=1/32768f,"Actual PCM within one source16bit LSB: "+Path.GetFileName(path)+" error="+maxError);note("Actual decoded "+Path.GetFileName(path)+" frames="+clip.samples+" channels="+clip.channels+" Hz="+clip.frequency+" maxPCMError="+maxError+" peak="+peak);
        }
        static void NeedFiniteSample(float v){if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("Nonfinite decoded source sample");}
        static IEnumerator KestrelSourceAudio(R23PlayableLoop loop,Action<string> note)
        {
            try
            {
                var load=LoadBotCombatMap(loop);while(load.MoveNext())yield return load.Current;var squad=loop.ValleyBots;var actors=new R24KestrelAudio[4];var bodies=new CharacterController[4];
                foreach(var body in squad.EncounterTransform.GetComponentsInChildren<CharacterController>()){R24KestrelBrain b;Vector3 p;bool supported;for(int i=0;i<4;i++)if(squad.TryRead(i,out b,out p,out supported)&&body.name=="KESTREL "+b.Variant+" / "+i){bodies[i]=body;actors[i]=body.GetComponent<R24KestrelAudio>();}}
                float end=Time.realtimeSinceStartup+30;bool ready=false;while(Time.realtimeSinceStartup<end){ready=true;foreach(var actor in actors)ready&=actor!=null&&actor.Ready&&actor.Failure==null;if(ready)break;yield return null;}Need(ready,"All four optional source cue banks decoded without gating combat");
                string[] kinds={"charge","fire","hit","death"};foreach(string kind in kinds)for(int i=0;i<R24KestrelAudioRules.Count(kind);i++){AudioClip clip,shared;Need(actors[0].TryReadClip(kind,i,out clip)&&actors[1].TryReadClip(kind,i,out shared)&&clip==shared,"Actual decoded clips shared across actor owners");string semantic=R24KestrelAudioRules.Semantic(kind);VerifySourcePcm(clip,Path.Combine(Application.streamingAssetsPath,"PlayableR23/KestrelAudio",semantic+"_"+i.ToString("D2")+".wav"),note);}
                foreach(var actor in actors){var voices=actor.GetComponentsInChildren<AudioSource>(true);Need(voices.Length==4,"Bounded two source channels/polyphony2");foreach(var voice in voices){bool impact=voice.name.Contains("impact");Need(Mathf.Abs(voice.transform.localPosition.y-1.2f)<.0001f&&Mathf.Abs(voice.volume-loop.UI.Settings.SfxVolume)<.0001f,"Actual source actor placement/bus gain");var curve=voice.GetCustomCurve(AudioSourceCurveType.CustomRolloff);foreach(float distance in new[]{0f,.1f,1f,9f,voice.maxDistance*.5f,voice.maxDistance}){float expected=(float)(R24KestrelAudioRules.EvaluateAttenuation(distance,9,voice.maxDistance,impact?-12:-9,3)/R24KestrelAudioRules.DbToLinear(3));Need(Mathf.Abs(curve.Evaluate(distance/voice.maxDistance)-expected)<.0001f,"Actual native attenuation curve source match");}}}
                string hitId=squad.TargetId(bodies[0]),deathId=squad.TargetId(bodies[3]);int hitBefore=actors[0].PlaysAccepted;Need(loop.ApplyTargetDamage(hitId,1,new Float3(0,1,-8)),"Actual Root damage accepts nonlethal enemy hit");Need(actors[0].PlaysAccepted==hitBefore+1&&actors[0].LastClip.Contains("impact_metal"),"Actual Health hit drives source impact channel");
                int deathBefore=actors[3].PlaysAccepted;Need(loop.ApplyTargetDamage(deathId,1000,new Float3(0,1,-8)),"Actual Root enemy elimination");Need(actors[3].PlaysAccepted==deathBefore+2&&actors[3].LastClip.Contains("enemy_break")&&actors[3].gameObject.activeInHierarchy,"Source hit+death emitted, audio retained through fallback lifetime");
                R24KestrelBrain first;Vector3 at;bool floor;squad.TryRead(0,out first,out at,out floor);PlaceNearRealBot(loop,at);int beforeCharge=actors[0].PlaysAccepted;
                for(int i=0;i<500&&first.State!=R24KestrelState.Windup;i++){loop.StepValleyBots(.02f,true);if(i%20==0)yield return null;}Need(first.State==R24KestrelState.Windup&&actors[0].PlaysAccepted==beforeCharge+1&&actors[0].LastClip.Contains("enemy_charge"),"Actual source perception/state transition emits charge");
                int beforeFire=actors[0].PlaysAccepted;for(int i=0;i<100&&squad.ReadDischarge(0)==null;i++)loop.StepValleyBots(.02f,true);Need(squad.ReadDischarge(0)!=null&&actors[0].PlaysAccepted==beforeFire+1&&actors[0].LastClip.Contains("enemy_fire"),"Actual real bot discharge emits source fire");note("Actual bot cue lifecycle: hit/charge/fire on live0, hit+death on3, all11PCMsourceclips/4sharedowners/16boundedvoices; no fake cue callback or listener reset.");
                var owned=actors[0].GetComponentsInChildren<AudioSource>();loop.UI.OpenSettings();loop.StepValleyBots(.02f,true);foreach(var voice in owned)Need(!voice.isPlaying&&voice.clip==null,"Source pause cancels bot transients");loop.ReturnToLobby();yield return null;yield return null;Need(UnityEngine.Object.FindObjectsByType<R24KestrelAudio>(FindObjectsInactive.Include).Length==0,"Map withdrawal retires all actor voice owners/shared leases");
            }
            finally{loop.ReturnToLobby();}
        }
    }
}
#endif
