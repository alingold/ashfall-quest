using System;
using System.IO;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.Audio;
using Ashfall.Integration.AudioRuntime;
public static class R4Tests
{
    static int checks;static void A(bool b,string n){checks++;if(!b)throw new Exception(n);}
    sealed class Authority:IApprovedAudioCatalogAuthority
    {
        public bool Allow=true;public int Entries,Reads;public string Path;
        public bool IsCatalogApproved(string p,string h){return Allow&&h.Length==64;}
        public bool IsEntryApproved(string[] f,string p,long n){Entries++;return Allow&&p==Path&&n<100000;}
        public bool IsApproved(AudioAsset a,string p,long n){Reads++;return Allow&&p==Path;}
    }
    sealed class Lease:IAudioOwnerLease{public bool Live=true;public bool IsCurrent(string s,string o,long e){return Live&&s=="s"&&e==1;}}
    static void Denied(Action action,string n){bool failed=false;try{action();}catch(InvalidDataException){failed=true;}catch(InvalidOperationException){failed=true;}A(failed,n);}
    public static void Main(string[] args)
    {
        Directory.CreateDirectory(args[0]);string wave=System.IO.Path.Combine(args[0],"test.wav");
        using(var f=new BinaryWriter(File.Create(wave))){f.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));f.Write(676);f.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));f.Write(16);f.Write((short)1);f.Write((short)1);f.Write(16000);f.Write(32000);f.Write((short)2);f.Write((short)16);f.Write(System.Text.Encoding.ASCII.GetBytes("data"));f.Write(640);for(int i=0;i<320;i++)f.Write((short)(1000+i*30));}
        string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(wave))).Replace("-","");
        string manifest=System.IO.Path.Combine(args[0],"catalog.tsv");File.WriteAllText(manifest,"ASHFALL-AUDIO/1\ngeneric/wind\t0\t"+wave+"\t"+hash+"\tfixture-v1\taccepted:TEST-ONLY\toriginal-test\tSFX\t1\n");
        var auth=new Authority{Path=wave};auth.Allow=false;Denied(delegate{ApprovedCatalogBinding.Load(manifest,auth);},"catalog authority");auth.Allow=true;
        var binding=ApprovedCatalogBinding.Load(manifest,auth);A(auth.Entries==1,"entry attestation");var lease=new Lease();var s=binding.CreateSession(lease);var owner=new CueIdentity("s","a",1,"event");
        s.Loop(owner,"wind",true,"wind",null,false,new Float3(0,0,0),false,1,1,AudioEmitterRole.Local);
        long retained=s.Mixer.RetainedSampleBytes;auth.Allow=false;Denied(delegate{s.Loop(owner,"other",true,"wind",null,false,new Float3(0,0,0),false,1,1,AudioEmitterRole.Local);},"cache hit revocation");A(s.Mixer.VoiceCount==1&&s.Mixer.RetainedSampleBytes==retained,"rejection preserves cache");auth.Allow=true;
        var packet=new byte[640];for(int i=0;i<320;i++){packet[i*2]=0;packet[i*2+1]=64;}
        s.ReceiveVoice(owner,packet,new Float3(0,0,0),1);var output=new float[640];s.Mixer.Render(output,2,16000);A(output[40]>.4,"voice enters actual global mixer");A(s.Mixer.OutputPeak<=PeakLimiter.Ceiling+.00001,"global cap");
        s.PauseOwner(owner,true);double cursor=s.Mixer.OwnerCursorTotal(owner.OwnerKey);s.ReceiveVoice(owner,packet,new Float3(0,0,0),1);s.Mixer.Render(output,2,16000);foreach(float v in output)A(v==0,"pause silence");A(cursor==s.Mixer.OwnerCursorTotal(owner.OwnerKey),"owner cursor freeze");
        s.PauseOwner(owner,false);s.Mixer.SetBus("SFX",0);s.Mixer.Render(output,2,16000);foreach(float v in output)A(v==0,"paused voice queue discarded");
        s.ReceiveVoice(owner,packet,new Float3(40,0,0),1);s.Mixer.Render(output,2,16000);foreach(float v in output)A(v==0,"source range40");
        s.ReceiveVoice(owner,packet,new Float3(0,0,0),1);s.Mixer.SetBus("Voice",0);s.Mixer.Render(output,2,16000);foreach(float v in output)A(v==0,"voice bus mute");
        lease.Live=false;Denied(delegate{s.ReceiveVoice(owner,packet,new Float3(0,0,0),1);},"revoked owner voice");lease.Live=true;
        Exception error=null;var worker=new Thread(delegate(){try{s.Dispose();}catch(Exception e){error=e;}});worker.Start();worker.Join();A(error is InvalidOperationException&&s.Mixer.VoiceCount==1,"worker disposal rejected intact");
        s.Despawn(owner);A(s.Mixer.VoiceCount==0&&s.Mixer.RetainedSampleBytes==0,"despawn releases");s.Dispose();
        using(var small=new RuntimeMixer(binding.Files,128)){Denied(delegate{small.ReceiveVoice("p",packet,new Float3(0,0,0),1);},"voice budget before allocation");A(small.RetainedSampleBytes==0,"voice allocation rejection unchanged");}
        using(var voiceMixer=new RuntimeMixer(binding.Files,12800)){voiceMixer.ReceiveVoice("p",packet,new Float3(0,0,0),1);A(voiceMixer.RetainedSampleBytes==6400,"voice storage accounted");voiceMixer.ReceiveVoice("p",packet,new Float3(0,0,0),1);A(voiceMixer.RetainedSampleBytes==6400,"same peer counted once");voiceMixer.ReceiveVoice("q",packet,new Float3(0,0,0),1);A(voiceMixer.RetainedSampleBytes==12800,"combined voice budget");Denied(delegate{voiceMixer.ReceiveVoice("r",packet,new Float3(0,0,0),1);},"voice total budget");}
        var voice=new ProximityVoicePcm();for(int i=0;i<32;i++)voice.Push("p"+i,packet,new Float3(0,0,0),1);Denied(delegate{voice.Push("overflow",packet,new Float3(0,0,0),1);},"voice capacity");A(voice.PeerCount==32,"peer count bounded");
        // More than5 queued packets must retain only newest bounded content.
        voice.Clear();for(int i=0;i<8;i++)voice.Push("p",packet,new Float3(0,0,0),1);output=new float[3200];voice.Mix(output,2,16000,new Float3(0,0,0),1);A(output[3198]>.4,"five packets retained");Array.Clear(output,0,output.Length);voice.Mix(output,2,16000,new Float3(0,0,0),1);foreach(float v in output)A(v==0,"queue exhausted bounded");
        File.WriteAllText(manifest,new string('x',1048577));Denied(delegate{ApprovedCatalogBinding.Load(manifest,auth);},"manifest size before read");
        Console.WriteLine("PASS R4 "+checks+" assertions; fixtures only, native/oracle OPEN");
    }
}
