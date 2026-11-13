using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.Audio;
using Ashfall.Integration.AudioRuntime;
public static class R2Tests
{
    static int count;
    static void A(bool b,string n){count++;if(!b)throw new Exception(n);}
    static void Reject(Action f,string n){bool rejected=false;try{f();}catch(InvalidDataException){rejected=true;}catch(IOException){rejected=true;}catch(InvalidOperationException){rejected=true;}A(rejected,n);}
    sealed class Receipt:IApprovedAudioFileAttestation {public bool Allow=true;public int Calls;public bool IsApproved(AudioAsset a,string p,long l){Calls++;return Allow&&p==a.Path&&l==108;}}
    sealed class Bytes:IApprovedAudioBytes {public byte[] Data;public int Calls;public byte[] Read(AudioAsset a){Calls++;return Data;}}
    sealed class Lease:IAudioOwnerLease {public bool IsCurrent(string s,string o,long e){return true;}}
    sealed class Changing:MemoryStream
    {
        readonly bool growth;
        public Changing(byte[] b,bool g):base(b){growth=g;}
        public override int Read(byte[] b,int o,int n){return base.Read(b,o,growth?n:Math.Min(n,10));}
        public override long Length {get{return base.Length+(growth?1:0);}}
    }
    public static int Main(string[] args)
    {
        string dir=args[0];Directory.CreateDirectory(dir);var data=new byte[108];
        using(var m=new MemoryStream(data))using(var w=new BinaryWriter(m)){w.Write(0x46464952);w.Write(100);w.Write(0x45564157);w.Write(0x20746d66);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(32000);w.Write(64000);w.Write((short)2);w.Write((short)16);w.Write(0x61746164);w.Write(64);for(int i=0;i<32;i++)w.Write((short)8192);}
        string path=Path.Combine(dir,"test-only.wav");File.WriteAllBytes(path,data);string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");
        string[] fields={"generic/wind","0",path,hash,"test-only","accepted:TEST-ONLY","test-original","SFX","1"};var asset=new AudioAsset(fields,1,32000,32);
        var receipt=new Receipt();var files=new BoundedApprovedAudioFiles(receipt);var pcm=VerifiedPcm.Load(asset,files,108,32);A(pcm.Samples.Length==32&&pcm.Samples[0]==.25f,"actual bounded read/decode at exact budgets");
        Reject(delegate{VerifiedPcm.Load(asset,files,107,32);},"container max+1");int calls=receipt.Calls;Reject(delegate{VerifiedPcm.Load(asset,files,108,31);},"sample max+1 before read");A(receipt.Calls==calls,"no approval callback before sample rejection");
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Write)){file.SetLength((long)VerifiedPcm.MaxContainerBytes+1);}
        calls=receipt.Calls;Reject(delegate{VerifiedPcm.Load(asset,files);},"real file exceeds64MiB before allocation/approval");A(calls==receipt.Calls,"oversize file skips attestation");File.WriteAllBytes(path,data);
        var big=new AudioAsset(fields,2,32000,16000001);Reject(delegate{VerifiedPcm.Load(big,files);},"32M sample policy before allocation");
        receipt.Allow=false;Reject(delegate{VerifiedPcm.Load(asset,files);},"string/existence grants no approval");receipt.Allow=true;
        var legacy=new Bytes{Data=new byte[109]};Reject(delegate{VerifiedPcm.Load(asset,legacy,108,32);},"legacy before owned clone cap");
        using(var s=new MemoryStream(new byte[20]))Reject(delegate{AudioSnapshot.ReadExact(s,21,21);},"truncation during bounded read");
        using(var s=new MemoryStream(new byte[22]))Reject(delegate{AudioSnapshot.ReadExact(s,21,21);},"growth during bounded read");
        using(var s=new Changing(new byte[21],true))Reject(delegate{AudioSnapshot.ReadExact(s,21,21);},"length change despite EOF");
        using(var s=new Changing(data,false)){var snapshot=AudioSnapshot.ReadExact(s,108,108);var copy=snapshot.Copy();copy[44]=0;A(snapshot.Decode(asset,32).Samples[0]==.25f,"snapshot private/partial stream read");}
        legacy.Data=data;
        using(var mixer=new RuntimeMixer(legacy,128))
        {
            var v=mixer.Start(asset,false,1,1,new Float3());var v2=mixer.Start(asset,false,1,1,new Float3());A(mixer.CacheCount==1&&mixer.RetainedSampleBytes==128&&legacy.Calls==3,"shared PCM retained once, single provider read after earlier rejected legacy read");
            var otherFields=(string[])fields.Clone();otherFields[3]=new string('F',64);var other=new AudioAsset(otherFields,1,32000,32);int before=legacy.Calls;
            Reject(delegate{mixer.Start(other,false,1,1,new Float3());},"aggregate budget before provider read");A(legacy.Calls==before&&mixer.VoiceCount==2,"budget failure preserves voices");v.Stop();A(mixer.VoiceCount==1&&mixer.RetainedSampleBytes==128,"one shared owner retained");v2.Stop();A(mixer.VoiceCount==0&&mixer.RetainedSampleBytes==0&&mixer.CacheCount==0,"last shared owner cleanup");
        }
        var manifest=Path.Combine(dir,"test.tsv");File.WriteAllText(manifest,"ASHFALL-AUDIO/1\n"+string.Join("\t",fields)+"\n");var catalog=AudioCatalog.Load(manifest);
        var oldSession=new AudioRuntimeSession(catalog,files,new Lease());var old=new AudioActivation(oldSession);var nextSession=new AudioRuntimeSession(catalog,files,new Lease());var next=new AudioActivation(nextSession);
        oldSession.Mixer.Start(asset,false,1,1,new Float3());nextSession.Mixer.Start(asset,false,1,1,new Float3());var block=new float[64];old.Render(block,32000);A(block[0]>.0,"old DSP real output");
        var callback=new Thread(delegate(){old.Invalidate();});callback.Start();callback.Join();old.Render(block,32000);foreach(float f in block)A(f==0,"pending callback DSP silence");A(oldSession.Mixer.VoiceCount==1,"callback no disposal");
        old.Close();old.Close();old.Invalidate();next.Render(block,32000);A(block[0]>0&&nextSession.Mixer.VoiceCount==1,"late old callback preserves replacement");A(oldSession.Mixer.RetainedSampleBytes==0,"old cleanup releases PCM");next.Close();A(nextSession.Mixer.RetainedSampleBytes==0,"replacement cleanup");
        var noise=new float[9600];for(int i=0;i<noise.Length;i++)noise[i]=(float)Math.Sin(i*.37);
        var climb=Ashfall.Integration.AudioRuntime.Quarantine.ClimbFromOracleNoise.Synthesize(0,noise);
        int peak=0;foreach(short sample in climb)peak=Math.Max(peak,Math.Abs((int)sample));
        A(climb.Length==9600&&climb[0]==0&&peak>=20314&&peak<=20316,"quarantined source equations shape/normalization on TEST noise, no Godot RNG equivalence");
        Console.WriteLine("PASS R2 behavioral assertions="+count+"; native execution OPEN");return 0;
    }
}
