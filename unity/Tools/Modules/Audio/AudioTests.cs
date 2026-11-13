using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using Ashfall.Core;
using Ashfall.Modules.Audio;

internal static class AudioTests
{
    private sealed class Voice : IAudioVoice
    {
        internal int Stops,Pauses;internal float Gain,Pitch; internal bool Paused;
        public void Set(float gain,float pitch,Float3 p){Gain=gain;Pitch=pitch;}
        public void Stop(){Stops++;}
        public void Pause(bool p){Pauses++;Paused=p;}
    }
    private sealed class Playback : IAudioPlayback
    {
        internal readonly List<Voice> Voices=new List<Voice>();
        internal readonly List<AudioAsset> Assets=new List<AudioAsset>();
        internal int TransientStops;
        public IAudioVoice Start(AudioAsset asset,bool spatial,float gain,float pitch,Float3 p){var v=new Voice();v.Set(gain,pitch,p);Voices.Add(v);Assets.Add(asset);return v;}
        public void SetBus(string bus,float gain){}
        public void StopTransients(){TransientStops++;}
    }
    private static int passed;
    private static string dir,header="ASHFALL-AUDIO/1\n",hash,wav;
    private static readonly Float3 Origin=new Float3(0,0,0);
    private static void Assert(bool value,string label){if(!value)throw new Exception(label);}
    private static void Near(float a,float b){Assert(Math.Abs(a-b)<.00001,"Expected "+b+", got "+a);}
    private static void Reject(Action action){bool failed=false;try{action();}catch(InvalidDataException){failed=true;}Assert(failed,"Invalid import accepted");}
    private static void Test(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
    private static string Row(string route,int variant,string bus,bool loop){return string.Join("\t",new[]{route,variant.ToString(),wav,hash,"fixture-v1","accepted:test-fixture-only","original deterministic test PCM",bus,loop?"1":"0"})+"\n";}
    private static AudioCatalog Catalog(string rows){string p=Path.Combine(dir,"fixture.tsv");File.WriteAllText(p,header+rows);return AudioCatalog.Load(p);}
    private static void WriteWave()
    {
        wav=Path.Combine(dir,"stereo.wav");using(var w=new BinaryWriter(File.Create(wav)))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(44);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(32000);w.Write(128000);w.Write((short)4);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(8);w.Write((short)-32768);w.Write((short)32767);w.Write((short)0);w.Write((short)16384);
        }
        using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(wav))).Replace("-","");
    }
    public static int Main(string[] args)
    {
        try
        {
            if(args[0]=="--wave-list")
            {
                int count=0;foreach(string path in File.ReadAllLines(args[1])){var pcm=PcmWave.Decode(File.ReadAllBytes(path));Assert(pcm.Frames>0,"Empty source audio");count++;}
                Console.WriteLine("PASS production PCM decoder: "+count+" unique source WAVs (reference only, no acceptance)");return 0;
            }
            dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);WriteWave();
            Test("stereo frame count and signed PCM decode",delegate{var p=PcmWave.Decode(File.ReadAllBytes(wav));Assert(p.Frames==2&&p.Channels==2&&p.Rate==32000,"PCM metadata");Near(p.Samples[0],-1);Near(p.Samples[3],.5f);});
            Test("reject truncated and malformed WAV",delegate{byte[] b=File.ReadAllBytes(wav);Array.Resize(ref b,b.Length-1);Reject(delegate{PcmWave.Decode(b);});b=File.ReadAllBytes(wav);b[32]=2;Reject(delegate{PcmWave.Decode(b);});});
            Test("odd ancillary RIFF chunks preserve alignment",delegate{byte[] b=File.ReadAllBytes(wav);var s=new MemoryStream();s.Write(b,0,12);s.Write(Encoding.ASCII.GetBytes("JUNK"),0,4);s.Write(BitConverter.GetBytes(1),0,4);s.WriteByte(7);s.WriteByte(0);s.Write(b,12,b.Length-12);b=s.ToArray();Array.Copy(BitConverter.GetBytes(b.Length-8),0,b,4,4);Assert(PcmWave.Decode(b).Frames==2,"Chunk padding");});
            Test("production gate rejects unapproved, hash drift, variant gaps, invalid buses",delegate{
                string row=Row("generic/glide",0,"SFX",true);
                Reject(delegate{Catalog(row.Replace("accepted:test-fixture-only","unreviewed"));});
                Reject(delegate{Catalog(row.Replace(hash,new string('0',64)));});
                Reject(delegate{Catalog(Row("generic/glide",1,"SFX",true));});
                Reject(delegate{Catalog(Row("generic/glide",0,"Typo",true));});
                Reject(delegate{Catalog(row+row);});
            });
            Test("source variant wrap, uzi alias, AWP bolt exception, authored fallback",delegate{
                var c=Catalog(Row("generic/bolt",0,"SFX",false)+Row("weapons/awp/bolt",0,"SFX",false)+Row("weapons/uzis/shot",0,"SFX",false)+Row("weapons/uzis/shot",1,"SFX",false)+Row("generic/authored/dry",0,"SFX",false));
                Assert(c.Resolve("bolt","awp",true,0,false).Route=="generic/bolt","AWP routing");
                Assert(c.Resolve("shot","uzi",true,-1,false).Variant==1,"posmod/alias");
                Assert(c.Resolve("dry",null,false,0,false).Route=="generic/authored/dry","Fallback");
            });
            var catalog=Catalog(Row("generic/wind",0,"Ambience",true)+Row("generic/glide",0,"SFX",true)+Row("generic/menu_click",0,"SFX",false));
            Test("600 flight frames use two continuous beds; silence does not retrigger",delegate{
                var p=new Playback();using(var r=new AudioRouter(catalog,p)){var f=new FlightAudio();for(int i=0;i<600;i++)f.Tick(r,"local",1f/60,true,10.5f,0,false,Origin);Assert(p.Voices.Count==2,"Loop restart");Near(f.GlideGain,1);Near(p.Voices[1].Gain,.27f);Near(p.Voices[1].Pitch,1.15f);Assert(p.Assets[1].Bus=="SFX","Glide bus");Near(r.BusGain("Ambience"),0);Near(r.BusGain("SFX"),1);for(int i=0;i<60;i++)f.Tick(r,"local",1f/60,false,0,0,true,Origin);Near(f.GlideGain,0);Near(p.Voices[1].Gain,.0005f);Assert(p.Voices.Count==2,"Silent loop restarted");}
            });
            Test("flight envelope follows source attack and shelter ramp",delegate{var p=new Playback();using(var r=new AudioRouter(catalog,p)){var f=new FlightAudio();f.Tick(r,"p",.125f,true,10.5f,0,true,Origin);Near(f.GlideGain,.25f);Near(f.WindDb,-25.375f);Near(p.Voices[1].Gain,.0675f);}});
            Test("stop is idempotent, restart creates one voice, owners isolated",delegate{var p=new Playback();using(var r=new AudioRouter(catalog,p)){for(int i=0;i<3;i++)r.SetLoop("a","flight",true,"glide",null,false,Origin,false,.2f,1);r.SetLoop("b","flight",true,"glide",null,false,Origin,false,.2f,1);r.ReleaseOwner("a");Assert(p.Voices[0].Stops==1&&p.Voices[1].Stops==0,"Owner leak");r.SetLoop("a","flight",false,"glide",null,false,Origin,false,0,1);r.SetLoop("a","flight",true,"glide",null,false,Origin,false,.2f,1);Assert(p.Voices.Count==3,"Restart");}});
            Test("pause preserves loop cursor port and cancels transients",delegate{var p=new Playback();using(var r=new AudioRouter(catalog,p)){r.SetLoop("p","flight",true,"glide",null,false,Origin,false,.2f,1);r.Pause(true);r.Pause(true);Assert(p.Voices[0].Pauses==1&&p.Voices[0].Stops==0&&p.TransientStops==1,"Pause lifecycle");Assert(!r.Once("s/1","menu_click",null,false,0,Origin,false,1,1),"Paused one-shot");r.Pause(false);Assert(!p.Voices[0].Paused&&p.Voices.Count==1,"Resume restart");}});
            Test("network/local duplicate event suppressed; legitimate later event plays",delegate{var p=new Playback();using(var r=new AudioRouter(catalog,p)){Assert(r.Once("session/a/12","menu_click",null,false,0,Origin,false,1,1),"First event");Assert(!r.Once("session/a/12","menu_click",null,false,0,Origin,false,1,1),"Duplicate played");Assert(r.Once("session/a/13","menu_click",null,false,0,Origin,false,1,1),"Later event");Assert(p.Voices.Count==2,"Event count");}});
            Test("NaN rejected before playback or envelope mutation",delegate{var p=new Playback();using(var r=new AudioRouter(catalog,p)){bool rejected=false;try{new FlightAudio().Tick(r,"p",float.NaN,true,10,0,false,Origin);}catch(ArgumentException){rejected=true;}Assert(rejected&&p.Voices.Count==0,"NaN frame");}});
            Console.WriteLine("PASS total="+passed);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
