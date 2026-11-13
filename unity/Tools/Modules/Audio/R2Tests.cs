using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Audio;
class R2Tests
{
    static void Assert(bool b,string m){if(!b)throw new Exception(m);}
    static void Bad(Action a){try{a();}catch(InvalidDataException){return;}throw new Exception("Invalid data accepted");}
    static byte[] Chunk(byte[] wave,byte[] tag,byte[] payload,bool pad)
    {
        var s=new MemoryStream();s.Write(wave,0,12);s.Write(tag,0,tag.Length);s.Write(BitConverter.GetBytes(payload.Length),0,4);s.Write(payload,0,payload.Length);if(pad)s.WriteByte(0);s.Write(wave,12,wave.Length-12);byte[] b=s.ToArray();Array.Copy(BitConverter.GetBytes(b.Length-8),0,b,4,4);return b;
    }
    sealed class Voice:IAudioVoice{public void Set(float g,float p,Float3 at){}public void Pause(bool p){}public void Stop(){}}
    sealed class Playback:IProfiledAudioPlayback
    {
        public List<CueProfile> Profiles=new List<CueProfile>();
        public IAudioVoice Start(AudioAsset a,bool s,float g,float p,Float3 at){throw new Exception("Profile routing bypassed");}
        public IAudioVoice Start(AudioAsset a,CueProfile profile,bool s,float g,float p,Float3 at){Profiles.Add(profile);return new Voice();}
        public void SetBus(string b,float g){}public void StopTransients(){}
    }
    static void Main(string[] args)
    {
        string dir=args[0];byte[] wave=File.ReadAllBytes(Path.Combine(dir,"fixtures/stereo.wav"));
        for(int i=0;i<12;i++){byte[] b=new byte[i];Array.Copy(wave,b,i);Bad(delegate{PcmWave.Decode(b);});}
        for(int length=0;length<=5;length++)
        {
            byte[] payload=new byte[length];byte[] valid=Chunk(wave,new byte[]{0xff,0xc3,0xa9,0x80},payload,length%2==1);
            Assert(PcmWave.Decode(valid).Frames==2,"Raw binary tag alignment");
            if(length%2==1){byte[] noPad=Chunk(wave,Encoding.ASCII.GetBytes("JUNK"),payload,false);Bad(delegate{PcmWave.Decode(noPad);});}
        }
        for(int n=1;n<8;n++){var s=new MemoryStream();s.Write(wave,0,wave.Length);s.Write(new byte[n],0,n);byte[] tail=s.ToArray();Array.Copy(BitConverter.GetBytes(tail.Length-8),0,tail,4,4);Bad(delegate{PcmWave.Decode(tail);});}
        Console.WriteLine("PASS short headers, raw nonASCII tags, 0..5 byte chunks, missing odd pads, truncated chunk headers");
        string full=Path.Combine(dir,"fixtures/stereo.wav");
        foreach(string path in new[]{full.Substring(2),full[0]+":"+full.Substring(3),"stereo.wav","/Users/aling/file.wav","\\\\?\\C:\\file.wav","C:\\file.wav:stream","\\\\server"})Bad(delegate{AudioPaths.Canonical(path);});
        string cwd=Environment.CurrentDirectory;
        try{Environment.CurrentDirectory=Path.GetPathRoot(full);Assert(AudioPaths.Canonical(full)==Path.GetFullPath(full),"Drive-independent identity");}finally{Environment.CurrentDirectory=cwd;}
        Assert(AudioPaths.Canonical(Path.Combine(dir,"fixtures/../fixtures/stereo.wav"))==Path.GetFullPath(full),"Canonical dot segments");
        Console.WriteLine("PASS drive/root-relative rejection and canonical identity across current-directory changes");
        string row=File.ReadAllLines(Path.Combine(dir,"fixtures/fixture.tsv"))[1];string[] f=row.Split('\t');f[7]="SFX";f[8]="0";
        var rows=new List<string>();foreach(string route in new[]{"weapons/uzis/shot","generic/bolt","generic/concrete_step","generic/climb"}){f[0]=route;rows.Add(string.Join("\t",f));}
        string manifest=Path.Combine(dir,"profiles.tsv");File.WriteAllText(manifest,"ASHFALL-AUDIO/1\n"+string.Join("\n",rows));
        var p=new Playback();using(var r=new AudioRouter(AudioCatalog.Load(manifest),p)){var z=new Float3(0,0,0);r.Once("s/1","shot","uzi",true,0,z,true,1,1);r.Once("s/2","bolt","uzi",true,0,z,true,1,1);r.Once("s/3","concrete_step",null,false,0,z,true,1,1);r.Once("s/4","climb",null,false,0,z,true,1,1);}
        Assert(p.Profiles[0]==CueProfiles.Shot&&p.Profiles[1]==CueProfiles.Reload&&p.Profiles[2]==CueProfiles.Footstep&&p.Profiles[3]==CueProfiles.Climb,"Production profile selection");
        Assert(CueProfiles.Shot.UnitMeters==12&&CueProfiles.Shot.MaxMeters==140&&CueProfiles.Reload.UnitMeters==4&&CueProfiles.Reload.MaxMeters==30,"Remote policies");
        foreach(var profile in new[]{CueProfiles.Shot,CueProfiles.Reload,CueProfiles.Footstep,CueProfiles.Climb,CueProfiles.Creak,CueProfiles.Turbine,CueProfiles.Impact})
        {float last=1;for(int i=0;i<=1000;i++){float a=profile.Attenuation(profile.MaxMeters*i/1000);Assert(a>=0&&a<=last,"Bounded monotone attenuation");last=a;}Assert(last==0&&profile.Attenuation(0)==1,"Distance endpoints");}
        Console.WriteLine("PASS router forwards requested shot/reload/foley policy, bounded attenuation including max-distance silence");
        var limiter=new PeakLimiter();float[] impulse={4,-2};limiter.Process(impulse,2,48000);Assert(Math.Abs(impulse[0]-PeakLimiter.Ceiling)<.00001&&Math.Abs(impulse[1]+PeakLimiter.Ceiling/2)<.00001,"Linked stereo ceiling");
        float[] quiet=new float[5760];for(int i=0;i<quiet.Length;i++)quiet[i]=.1f;limiter.Process(quiet,1,48000);float initial=PeakLimiter.Ceiling/4;float expected=.1f*(1-(1-initial)/(float)Math.E);Assert(Math.Abs(quiet[quiet.Length-1]-expected)<.0001,"120ms release");
        float[] invalid={float.NaN,float.PositiveInfinity};limiter.Process(invalid,2,48000);Assert(invalid[0]==0&&invalid[1]==0,"Finite DSP");
        float[] overload=new float[20000];for(int i=0;i<overload.Length;i++)overload[i]=(i%3-1)*20;limiter.Process(overload,2,48000);foreach(float sample in overload)Assert(Math.Abs(sample)<=PeakLimiter.Ceiling+.00001,"Sustained ceiling");
        Console.WriteLine("PASS linked-channel limiter: impulse, sustained overload, source -1dB ceiling, 120ms release, nonfinite input");
    }
}
