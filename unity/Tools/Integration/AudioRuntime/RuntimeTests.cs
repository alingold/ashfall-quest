using System;
using System.IO;
using System.Security.Cryptography;
using Ashfall.Core;
using Ashfall.Modules.Audio;
using Ashfall.Modules.Persistence;
using Ashfall.Integration.AudioSettings;
using Ashfall.Integration.AudioRuntime;
public static class RuntimeTests
{
    static int checks;
    static readonly Float3 Zero=new Float3(0,0,0);
    static void A(bool v,string name){checks++;if(!v)throw new Exception(name);}
    static void Near(double a,double b,string n){A(Math.Abs(a-b)<.00002,n+" "+a+" != "+b);}
    sealed class Bytes:IApprovedAudioBytes
    {
        public byte[] Value;public bool Fail;
        public byte[] Read(AudioAsset a){if(Fail)throw new IOException("External approved bytes unavailable");return (byte[])Value.Clone();}
    }
    sealed class Lease:IAudioOwnerLease {public bool Current=true;public bool IsCurrent(string s,string o,long e){return Current&&s=="s"&&e==4;}}
    // Failure injection surrounds the ACTUAL mixer, never replaces a successful module.
    sealed class Fault:IProfiledAudioPlayback
    {
        public RuntimeMixer Actual;public string Fail;
        public void SetBus(string b,float g){Actual.SetBus(b,g);if(Fail==b)throw new IOException("post-apply fault");}
        public IAudioVoice Start(AudioAsset a,bool s,float g,float p,Float3 at){return Actual.Start(a,s,g,p,at);}
        public IAudioVoice Start(AudioAsset a,CueProfile c,bool s,float g,float p,Float3 at){return Actual.Start(a,c,s,g,p,at);}
        public void StopTransients(){Actual.StopTransients();}
    }
    static byte[] Wave(int channels,int rate)
    {
        using(var m=new MemoryStream())using(var w=new BinaryWriter(m))
        {int frames=32;w.Write(0x46464952);w.Write(36+frames*channels*2);w.Write(0x45564157);w.Write(0x20746d66);w.Write(16);w.Write((short)1);w.Write((short)channels);w.Write(rate);w.Write(rate*channels*2);w.Write((short)(channels*2));w.Write((short)16);w.Write(0x61746164);w.Write(frames*channels*2);for(int i=0;i<frames;i++)for(int c=0;c<channels;c++)w.Write((short)(c==0?8192:-4096));return m.ToArray();}
    }
    static string Hash(byte[] b){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","");}
    public static int Main(string[] args)
    {
        string dir=args[0];Directory.CreateDirectory(dir);byte[] wav=Wave(1,32000);string file=Path.Combine(dir,"test-only.wav");File.WriteAllBytes(file,wav);
        string manifest=Path.Combine(dir,"test-only.tsv"),header="ASHFALL-AUDIO/1\n",rows="";
        foreach(string cue in new[]{"wind","glide","climb","pad","shot","master"})rows+="generic/"+cue+"\t0\t"+file+"\t"+Hash(wav)+"\ttest-only-v1\taccepted:TEST-FIXTURE-NOT-CONTENT\toriginal-test\t"+(cue=="master"?"Master":"SFX")+"\t"+(cue=="wind"||cue=="glide"?"1":"0")+"\n";
        File.WriteAllText(manifest,header+rows);var catalog=AudioCatalog.Load(manifest);var bytes=new Bytes{Value=wav};var asset=catalog.Resolve("wind",null,false,0,false);
        var block=new float[128];
        using(var mixer=new RuntimeMixer(bytes))
        {
            var voice=mixer.Start(asset,CueProfiles.Default,false,1,1,Zero);mixer.Render(block,2,48000);Near(mixer.CursorFrames(voice),(64.0*32000/48000)%32,"Source-rate cursor across loop block (modulo occurs before each frame)");
        }
        using(var mixer=new RuntimeMixer(bytes))
        {
            var voice=mixer.Start(asset,CueProfiles.LocalShot,false,.8f,1,Zero);mixer.SetBus("SFX",.25f);mixer.SetBus("Master",.5f);mixer.Render(new float[2],2,48000);Near(mixer.OutputPeak,.25*.8*CueProfiles.LocalShot.SourceGain*.25*.5,"Authored source/bus/master once");
            double cursor=mixer.CursorFrames(voice);voice.Pause(true);mixer.Render(block,2,48000);Near(mixer.CursorFrames(voice),cursor,"Pause cursor frozen");Near(mixer.OutputPeak,0,"Pause silent");
            ((IProfiledAudioVoice)voice).SetProfile(CueProfiles.Shot);voice.Pause(false);mixer.Render(new float[2],2,48000);Near(mixer.OutputPeak,.25*.8*.25*.5,"Profile changes live without restart");
            voice.Stop();mixer.Render(block,2,48000);A(mixer.VoiceCount==0,"Stop releases voice");
            mixer.Start(catalog.Resolve("master",null,false,0,false),CueProfiles.Default,false,1,1,Zero);mixer.Render(new float[2],2,48000);Near(mixer.OutputPeak,.125,"Master route not squared");mixer.StopTransients();
            for(int i=0;i<16;i++)mixer.Start(asset,CueProfiles.Default,false,1,1,Zero);mixer.SetBus("Master",1);mixer.SetBus("SFX",1);mixer.Render(block,2,48000);Near(mixer.SumPeak,4,"Real 16-voice sum exceeds individual ceiling");A(mixer.OutputPeak<=PeakLimiter.Ceiling+.00001,"Mixed master bounded");
        }
        using(var mixer=new RuntimeMixer(bytes))
        {
            var v=mixer.Start(asset,CueProfiles.Shot,true,1,1,new Float3(0,0,10));mixer.Render(new float[2],2,32000);Near(mixer.OutputPeak,.25*CueProfiles.Shot.Attenuation(10),"Remote authored attenuation once");v.Set(1,1,new Float3(0,0,141));mixer.Render(block,2,48000);Near(mixer.OutputPeak,0,"Max distance silent");
        }
        using(var mixer=new RuntimeMixer(bytes))
        {bytes.Value=(byte[])wav.Clone();bytes.Value[44]^=1;bool rejected=false;try{mixer.Start(asset,false,1,1,Zero);}catch(InvalidDataException){rejected=true;}A(rejected&&mixer.VoiceCount==0,"Changed immutable bytes reject without voice");bytes.Value=wav;bytes.Fail=true;rejected=false;try{mixer.Start(asset,false,1,1,Zero);}catch(IOException){rejected=true;}A(rejected&&mixer.VoiceCount==0,"Missing approved bytes explicit failure");bytes.Fail=false;}
        using(var whole=new RuntimeMixer(bytes))using(var split=new RuntimeMixer(bytes))
        {
            for(int i=0;i<12;i++){whole.Start(asset,false,1,1.1f,Zero);split.Start(asset,false,1,1.1f,Zero);}
            var expected=new float[2048];whole.Render(expected,2,44100);var actual=new float[2048];int offset=0;
            while(offset<actual.Length){int n=Math.Min(actual.Length-offset,46);var chunk=new float[n];split.Render(chunk,2,44100);Array.Copy(chunk,0,actual,offset,n);offset+=n;}
            for(int i=0;i<actual.Length;i++)Near(actual[i],expected[i],"Summed limiter/resampling continuity across DSP block partitions");
        }
        using(var budget=new RuntimeMixer(bytes))
        {
            for(int i=0;i<128;i++)budget.Start(asset,false,0,1,Zero);bool rejected=false;try{budget.Start(asset,false,1,1,Zero);}catch(InvalidOperationException){rejected=true;}A(rejected&&budget.VoiceCount==128,"Voice capacity fails closed preserving active voices");budget.Dispose();budget.Render(block,2,48000);foreach(float f in block)Near(f,0,"Late DSP after disposal silent");
        }
        var lease=new Lease();var id=new CueIdentity("s","player",4,"one");var other=new CueIdentity("s","peer",4,"one");
        using(var s=new AudioRuntimeSession(catalog,bytes,lease))
        {
            var flightBlock=new float[1600];for(int i=0;i<3600;i++){s.Flight(id,1f/60,true,10,40,false,Zero);s.Mixer.Render(flightBlock,2,48000);}A(s.Mixer.VoiceCount==2&&s.Mixer.RenderedFrames==2880000,"60s sustained flight exactly two loops");
            for(int i=0;i<180;i++){s.Flight(id,1f/60,i%2==0,i%3==0?40:5,40,i%4==0,Zero);s.Once(new CueIdentity("s","player",4,"pad"+i),"pad",null,false,0,Zero,false,.2f,1,AudioEmitterRole.Local);s.Mixer.Render(block,2,48000);}A(s.Mixer.VoiceCount==2,"Rapid glide/dive/pad no voice leak");
            A(s.Once(id,"climb",null,false,0,Zero,true,1,1,AudioEmitterRole.Local),"Climb actual router starts transient");A(!s.Once(id,"climb",null,false,0,Zero,true,1,1,AudioEmitterRole.Local),"Duplicate exact event suppressed");
            s.Loop(other,"loop",true,"wind",null,false,Zero,false,1,1,AudioEmitterRole.Remote);s.PauseOwner(id,true);A(s.Mixer.VoiceCount==3,"Owner pause cancels owned transient only");s.Mixer.Render(block,2,48000);A(s.Mixer.OutputPeak>0,"Other owner remains audible");A(!s.Once(new CueIdentity("s","player",4,"paused"),"climb",null,false,0,Zero,false,1,1,AudioEmitterRole.Local),"Paused owner cannot emit");
            s.PauseOwner(id,false);s.Despawn(id);A(s.Mixer.VoiceCount==1,"Despawn preserves remote owner");lease.Current=false;bool reject=false;try{s.Flight(id,.1f,true,10,1,false,Zero);}catch(InvalidOperationException){reject=true;}A(reject,"Revoked owner rejected");s.Despawn(other);A(s.Mixer.VoiceCount==0,"Cleanup works after lease revoke");
        }
        using(var mixer=new RuntimeMixer(bytes))
        {
            var fault=new Fault{Actual=mixer};using(var router=new AudioRouter(catalog,fault))
            {
                string path=Path.Combine(dir,"settings.json");var binding=new AudioSettingsBinding(new AtomicSettingsStore(path),router);A(binding.LoadPublication().Accepted,"Actual settings load");fault.Fail="SFX";var r=binding.PublishBus("SFX",.2f);A(r.Committed&&r.LivePending,"Real commit then failed apply explicit");byte[] committed=File.ReadAllBytes(path);A(!binding.PublishBus("Master",.9f).Accepted,"New publication blocked while pending");fault.Fail=null;var retry=binding.RetryPublication();A(retry.Accepted&&retry.SnapshotId==r.SnapshotId,"Pending exact snapshot retry");A(Hash(committed)==Hash(File.ReadAllBytes(path)),"Retry writes no durable settings");router.SetLoop("a","l",true,"wind",null,false,Zero,false,1,1);mixer.Render(block,2,48000);Near(mixer.OutputPeak,.05,"Committed live setting actually changes mixed samples");
            }
        }
        // Real stereo PCM conversion/rate and output channel checks, separate catalog identity.
        byte[] stereo=Wave(2,44100);File.WriteAllBytes(file,stereo);File.WriteAllText(manifest,header+"generic/wind\t0\t"+file+"\t"+Hash(stereo)+"\ttest\taccepted:TEST-ONLY\toriginal-test\tSFX\t1\n");bytes.Value=stereo;var cat2=AudioCatalog.Load(manifest);
        using(var m=new RuntimeMixer(bytes)){var v=m.Start(cat2.Resolve("wind",null,false,0,false),false,1,1,Zero);var two=new float[2];m.Render(two,2,48000);Near(two[0],.25,"Stereo left preserved");Near(two[1],-.125,"Stereo right preserved");var mono=new float[1];m.Render(mono,1,48000);Near(mono[0],.0625,"Stereo downmix");Near(m.CursorFrames(v),2.0*44100/48000,"Rate conversion cursor");}
        Console.WriteLine("PASS AudioRuntime checks="+checks+"; actual neutral modules and DSP, TEST PCM only; native/listening OPEN");return 0;
    }
}

