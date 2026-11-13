using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Audio;
using Ashfall.Modules.Persistence;
using Ashfall.Integration.AudioSettings;
class AudioR3Tests
{
    static int checks;
    static void A(bool b,string m){checks++;if(!b)throw new Exception(m);}
    static void Near(float a,float b,string m){A(Math.Abs(a-b)<.00001,m);}
    sealed class Voice:IProfiledAudioVoice
    {
        public int Stops,Updates;public CueProfile Profile;
        public void SetProfile(CueProfile p){Profile=p;Updates++;}
        public void Set(float g,float p,Float3 at){}public void Pause(bool p){}public void Stop(){Stops++;}
    }
    sealed class Playback:IProfiledAudioPlayback
    {
        public List<Voice> Voices=new List<Voice>();
        public IAudioVoice Start(AudioAsset a,bool s,float g,float p,Float3 at){throw new Exception("Profile bypass");}
        public IAudioVoice Start(AudioAsset a,CueProfile p,bool s,float g,float pitch,Float3 at){var v=new Voice{Profile=p};Voices.Add(v);return v;}
        public void SetBus(string bus,float g){}public void StopTransients(){}
    }
    static void Main(string[] args)
    {
        string dir=args[0];string[] fields=File.ReadAllLines(Path.Combine(dir,"fixtures/fixture.tsv"))[1].Split('\t');
        var rows=new List<string>();
        foreach(string route in new[]{"weapons/uzis/shot","generic/bolt","generic/dry"}){fields[0]=route;fields[7]="SFX";fields[8]=route=="generic/bolt"?"1":"0";rows.Add(string.Join("\t",fields));}
        string manifest=Path.Combine(dir,"r3-test-only.tsv");File.WriteAllText(manifest,"ASHFALL-AUDIO/1\n"+string.Join("\n",rows));
        var playback=new Playback();var zero=new Float3(0,0,0);
        using(var router=new AudioRouter(AudioCatalog.Load(manifest),playback))
        {
            router.Once("s/local","shot","uzi",true,0,zero,true,1,1,AudioEmitterRole.Local);
            router.Once("s/remote","shot","uzi",true,0,zero,true,1,1,AudioEmitterRole.Remote);
            router.Once("s/monitor","shot","uzi",true,0,zero,false,1,1,AudioEmitterRole.Local);
            router.Once("s/compat","shot","uzi",true,0,zero,true,1,1);
            A(playback.Voices[0].Profile==CueProfiles.LocalShot&&playback.Voices[1].Profile==CueProfiles.Shot&&playback.Voices[2].Profile==CueProfiles.LocalShot&&playback.Voices[3].Profile==CueProfiles.Shot,"Local, remote, nonspatial monitor, compatibility selection");
            A(CueProfiles.LocalShot.UnitMeters==2&&CueProfiles.LocalShot.MaxMeters==85&&CueProfiles.LocalReload.UnitMeters==10&&CueProfiles.LocalReload.MaxMeters==14,"Source local distances");
            Near(CueProfiles.LocalShot.SourceGain,(float)Math.Pow(10,-5.0/20),"Local shot -5dB");
            Near(CueProfiles.LocalReload.SourceGain,(float)Math.Pow(10,-10.0/20),"Local reload -10dB");
            A(CueProfiles.For("dry",true,AudioEmitterRole.Local)==CueProfiles.LocalReload,"Local dry emitter");
            router.SetLoop("a","s",true,"bolt",null,false,zero,true,1,1);
            Voice loop=playback.Voices[4];
            router.SetLoop("a","s",true,"bolt","uzi",true,zero,true,.8f,1);
            A(playback.Voices.Count==5&&loop.Updates==1&&loop.Stops==0&&loop.Profile==CueProfiles.Reload,"Same asset default->remote profile refresh retains voice");
            router.Pause(true);router.SetLoop("a","s",true,"bolt","uzi",true,zero,true,.7f,1,AudioEmitterRole.Local);
            A(loop.Profile==CueProfiles.LocalReload&&loop.Updates==2&&loop.Stops==0,"Paused local transition retains voice");router.Pause(false);
            for(int i=0;i<600;i++)router.SetLoop("a","s",true,"bolt","uzi",true,zero,true,.7f,1,AudioEmitterRole.Local);
            A(loop.Updates==2&&playback.Voices.Count==5,"Stable profile does not refresh/restart");
            router.SetLoop("a","s",true,"bolt","uzi",true,zero,false,.7f,1,AudioEmitterRole.Local);
            A(loop.Stops==1&&playback.Voices.Count==6,"Spatial transition replaces voice");
            string path=Path.Combine(dir,"settings.json");var store=new AtomicSettingsStore(path);A(store.Load(false).Accepted,"Real store load");
            store.Write("master_volume","0.5");store.Write("audio_defaults_revision","993");store.Write("audio_volumes","{\"SFX\":0.25,\"Music\":0.3,\"Voice\":0.4,\"Ambience\":0.2,\"FutureBus\":7e-2}");store.Write("unknown","opaque retained");A(store.Flush().Accepted,"Real store flush");
            var binding=new AudioSettingsBinding(new AtomicSettingsStore(path),router);A(binding.Load().Accepted,"Binding load");Near(router.BusGain("SFX"),.25f,"SFX source key");Near(router.BusGain("Music"),.3f,"Current music");
            Near(AudioMix.Gain(.8f,CueProfiles.LocalShot,router.BusGain("SFX"),router.BusGain("Master"),false),.8f*CueProfiles.LocalShot.SourceGain*.25f*.5f,"Mixer local monitoring gain exactly once");
            Near(AudioMix.Gain(1,CueProfiles.Shot,.25f,.5f,false),.125f,"Remote bus/master");Near(AudioMix.Gain(1,CueProfiles.Default,.5f,.5f,true),.5f,"Master not squared");
            A(binding.SaveBus("SFX",.6f).Accepted,"Durable setting update");Near(router.BusGain("SFX"),.6f,"Live update");
            var reopened=new AtomicSettingsStore(path);A(reopened.Load(false).Accepted,"Reopen");string value;A(reopened.TryRead("audio_volumes",out value)&&value.Contains("\"FutureBus\":7e-2"),"Unknown numeric bus raw token preserved");A(reopened.TryRead("unknown",out value)&&value=="opaque retained","Unrelated settings retained");
            byte[] before=File.ReadAllBytes(path);A(!binding.SaveBus("SFX",float.NaN).Accepted,"Nonfinite rejected");A(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(path)),"Rejected save unchanged");
            using(new FileStream(path+".lock",FileMode.Open,FileAccess.ReadWrite,FileShare.None)){A(!binding.SaveBus("SFX",.9f).Accepted,"Real family lock blocks commit");Near(router.BusGain("SFX"),.6f,"Failed save not applied");}
            A(binding.SaveBus("Voice",0).Accepted,"Mute save");Near(router.BusGain("Voice"),0,"Muted channel");A(binding.SaveBus("Master",0).Accepted,"Master zero save");Near(router.BusGain("Master"),.0001f,"Source master floor");
            A(reopened.Load(false).Accepted,"Reload prior to old revision");reopened.Write("audio_defaults_revision","992");A(reopened.Flush().Accepted,"Old revision");A(binding.Load().Accepted,"Old defaults load");Near(router.BusGain("Music"),0,"Old revision music reset");Near(router.BusGain("Ambience"),0,"Old ambience reset");
            A(reopened.Load(false).Accepted,"Reload invalid");reopened.Write("audio_volumes","{\"SFX\":1,\"Future\":{\"x\":2}}");A(reopened.Flush().Accepted,"Unsupported fixture commit");before=File.ReadAllBytes(path);A(!binding.Load().Accepted&&!binding.SaveBus("SFX",.5f).Accepted,"Unsupported future member rejected");A(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(path)),"Unsupported data never rewritten");
        }
        string migrated=Path.Combine(dir,"migrated.json"),legacy=Path.Combine(dir,"legacy-test-only.json");
        File.WriteAllText(legacy,"{\"master_volume\":0.7,\"audio_defaults_revision\":993,\"audio_volumes\":{\"SFX\":0.8,\"Music\":0.2},\"unknown\":{\"retained\":true}}");
        byte[] original=File.ReadAllBytes(legacy);var migratedStore=new AtomicSettingsStore(migrated);A(migratedStore.Load(false).Accepted,"Migration target load");A(migratedStore.ImportGodotSettings(legacy).Accepted&&migratedStore.Flush().Accepted,"Actual accepted Godot migration");
        using(var r=new AudioRouter(AudioCatalog.Load(manifest),new Playback())){var b=new AudioSettingsBinding(migratedStore,r);A(b.Load().Accepted,"Imported source settings bound");Near(r.BusGain("Master"),.7f,"Imported master");Near(r.BusGain("SFX"),.8f,"Imported nested volume");A(b.SaveBus("Ambience",.2f).Accepted,"Imported setting durable update");}
        A(Convert.ToBase64String(original)==Convert.ToBase64String(File.ReadAllBytes(legacy)),"Legacy source unchanged");
        int clips=0;
        foreach(string path in File.ReadAllLines(args[1]))
        {
            var pcm=PcmWave.Decode(File.ReadAllBytes(path));int count=Math.Min(pcm.Samples.Length,pcm.Channels*8192);var mixed=new float[count];
            for(int i=0;i<count;i++){var profile=(i/pcm.Channels/1024)%2==0?CueProfiles.LocalShot:CueProfiles.Shot;mixed[i]=pcm.Samples[i]*8*AudioMix.Gain(1,profile,.8f,.9f,false);}
            var whole=(float[])mixed.Clone();var split=(float[])mixed.Clone();new PeakLimiter().Process(whole,pcm.Channels,pcm.Rate);var continuous=new PeakLimiter();
            int at=0,block=1;while(at<count){int n=Math.Min(count-at,pcm.Channels*block);var chunk=new float[n];Array.Copy(split,at,chunk,0,n);continuous.Process(chunk,pcm.Channels,pcm.Rate);Array.Copy(chunk,0,split,at,n);at+=n;block=block%137+1;}
            for(int i=0;i<count;i++){A(whole[i]==split[i],"Real PCM block continuity");A(Math.Abs(whole[i])<=PeakLimiter.Ceiling+.00001,"Real PCM overload bounded");}
            clips++;
        }
        Console.WriteLine("PASS r3 checks="+checks+" real read-only clips="+clips+"; no playback/listening/Unity claim");
    }
}
