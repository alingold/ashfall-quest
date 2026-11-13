using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Audio;
using Ashfall.Modules.Persistence;
using Ashfall.Integration.AudioSettings;
class AudioR4Tests
{
    static int checks;
    static void A(bool v,string m){checks++;if(!v)throw new Exception(m);}
    static void Near(float a,float b,string m){A(Math.Abs(a-b)<.00001,m+" actual="+a+" expected="+b);}
    static readonly string[] Buses={"Master","SFX","Music","Voice","Ambience"};
    sealed class Playback:IAudioPlayback
    {
        public string Fail;public int Calls;public bool MutateThenFail;
        public Action Callback;
        public Dictionary<string,float> Gains=new Dictionary<string,float>();
        public IAudioVoice Start(AudioAsset a,bool s,float g,float p,Float3 at){throw new NotSupportedException();}
        public void SetBus(string b,float g)
        {
            Calls++;if(Callback!=null)Callback();
            if(Fail==b&&!MutateThenFail)throw new IOException("injected before "+b);
            Gains[b]=g;if(Fail==b)throw new InvalidOperationException("injected after "+b);
        }
        public void StopTransients(){}
    }
    // Fault observer delegates ALL disk work to the actual accepted physical store.
    sealed class Files:IAtomicSettingsFiles
    {
        readonly PhysicalSettingsFiles actual=new PhysicalSettingsFiles();
        public int Commits;public Action BeforeCreate;
        public IDisposable AcquireFamily(string p){return actual.AcquireFamily(p);}
        public bool Exists(string p){return actual.Exists(p);}
        public byte[] Read(string p,int n){return actual.Read(p,n);}
        public void CreateDurable(string p,byte[] b){if(BeforeCreate!=null)BeforeCreate();actual.CreateDurable(p,b);}
        public void Commit(string p,string d,string b,bool r){actual.Commit(p,d,b,r);Commits++;}
    }
    static string Token(string p,string key){var s=new AtomicSettingsStore(p);A(s.Load(false).Accepted,"Reopen actual disk");string v;return s.TryRead(key,out v)?v:null;}
    static void Seed(string p,string revision,string extra)
    {
        var s=new AtomicSettingsStore(p);A(s.Load(false).Accepted,"Seed load");s.Write("master_volume","0.5");s.Write("audio_defaults_revision",revision);
        s.Write("audio_volumes","{\"SFX\":0.25,\"Music\":0.3,\"Voice\":0.4,\"Ambience\":0.2"+extra+"}");
        s.Write("outer","opaque unknown");A(s.Flush().Accepted,"Seed physical flush");
    }
    static string Bytes(string p){return Convert.ToBase64String(File.ReadAllBytes(p));}
    static void Main(string[] args)
    {
        string dir=args[0],manifest=Path.Combine(dir,"fixtures/fixture.tsv");var catalog=AudioCatalog.Load(manifest);
        foreach(bool mutate in new[]{false,true})for(int index=0;index<5;index++)
        {
            string path=Path.Combine(dir,"failure-"+mutate+index+".json");Seed(path,"9007199254740993",",\"FutureBus\":1e100,\"Tiny\":-0.000e-999999999999999999999999");
            var files=new Files();var playback=new Playback();
            using(var router=new AudioRouter(catalog,playback))
            {
                var binding=new AudioSettingsBinding(new AtomicSettingsStore(path,files),router);A(binding.Load().Accepted,"Initial binding load");
                float old=router.BusGain(Buses[index]);playback.Fail=Buses[index];playback.MutateThenFail=mutate;
                var r=binding.PublishBus("SFX",.17f);A(!r.Accepted&&r.Committed&&r.LivePending&&r.FailedBus==Buses[index],"Explicit durable/live pending at every index");
                A(files.Commits==1,"Exactly one real commit");Near(router.BusGain(Buses[index]),old,"Failed bus never recorded as confirmed");
                string committed=Bytes(path),backup=Bytes(path+".backup"),id=r.SnapshotId;
                A(Token(path,"audio_volumes").Contains("\"SFX\":0.17"),"Committed before callback failure");
                A(Token(path,"audio_defaults_revision")=="9007199254740993","Exact large revision preserved");
                A(!binding.SaveBus("Voice",.8f).Accepted&&!binding.SaveBus("Master",.9f).Accepted&&!binding.Load().Accepted,"Multiple unsent edits and Load blocked");
                r=binding.RetryPublication();A(r.LivePending&&r.Committed&&r.SnapshotId==id&&files.Commits==1,"Repeated failure retries exact snapshot without commit");
                playback.Fail=null;int calls=playback.Calls;r=binding.RetryPublication();
                A(r.Accepted&&r.Committed&&r.SnapshotId==id&&playback.Calls==calls+5,"Retry replays entire exact snapshot");Near(router.BusGain("SFX"),.17f,"Recovered SFX");
                A(Bytes(path)==committed&&Bytes(path+".backup")==backup&&files.Commits==1,"Retry unchanged disk and backup; no revision or commit duplication");
                calls=playback.Calls;A(binding.RetryPublication().Accepted&&playback.Calls==calls,"Completed retry is idempotent");
                A(binding.PublishBus("Voice",.8f).Accepted&&files.Commits==2,"Caller resubmits formerly unsent edit explicitly");
                string raw=Token(path,"audio_volumes");A(raw.Contains("\"FutureBus\":1e100")&&raw.Contains("\"Tiny\":-0.000e-999999999999999999999999"),"Unknown raw numeric tokens preserved exactly");
                A(Token(path,"outer")=="opaque unknown","Outer opaque value retained");
                // Reopen after another durable-but-failed publication; Load recovers the latest disk snapshot.
                playback.Fail=Buses[index];r=binding.PublishBus("SFX",.6f);A(r.Committed&&r.LivePending,"Second pending commit");string before=Bytes(path);
                var reopened=new AudioSettingsBinding(new AtomicSettingsStore(path),router);
                var loadReceipt=reopened.LoadPublication();A(!loadReceipt.Accepted&&loadReceipt.LivePending&&!loadReceipt.Committed&&loadReceipt.FailedBus==Buses[index],"Reopen live failure is pending without new commit");
                A(reopened.RetryPublication().LivePending,"Reopen repeated live failure retained");playback.Fail=null;
                A(reopened.RetryPublication().Accepted&&Bytes(path)==before,"New binding reopens and publishes without save");Near(router.BusGain("SFX"),.6f,"Reopen recovery gain");
            }
        }
        string[] revisions={"992.99999999","9.929999999999999999999999e2","993","993.000000000000000000000001","9.93e2","9007199254740993","1e999999999999999999999999","1e-999999999999999999999999","-1e999999999999999999999999","0e99999999999999999999"};
        for(int i=0;i<revisions.Length;i++)
        {
            bool current=i>=2&&i<=6;string path=Path.Combine(dir,"revision"+i+".json");Seed(path,revisions[i],"");
            using(var router=new AudioRouter(catalog,new Playback()))
            {
                var b=new AudioSettingsBinding(new AtomicSettingsStore(path),router);A(b.Load().Accepted,"Exact revision load");Near(router.BusGain("Music"),current?.3f:0,"Exact migration threshold");
                A(b.SaveBus("SFX",.8f).Accepted,"Exact revision save");A(Token(path,"audio_defaults_revision")== (current?revisions[i]:"993"),"Future raw revision or one-time migration");
            }
        }
        string conflict=Path.Combine(dir,"conflict.json");Seed(conflict,"993","");var fp=new Playback();var ff=new Files();
        using(var router=new AudioRouter(catalog,fp))
        {
            var b=new AudioSettingsBinding(new AtomicSettingsStore(conflict,ff),router);A(b.Load().Accepted,"Conflict load");fp.Fail="Music";
            var original=b.PublishBus("SFX",.17f);A(original.Committed,"Conflict initial commit");
            var external=new AtomicSettingsStore(conflict);A(external.Load(false).Accepted,"External writer load");external.Write("audio_volumes","{\"SFX\":0.9}");A(external.Flush().Accepted,"External writer actual commit");
            string before=Bytes(conflict);int calls=fp.Calls;fp.Fail=null;var r=b.RetryPublication();A(r.State==AudioPublicationState.Conflict&&r.Committed&&r.SnapshotId==original.SnapshotId,"Explicit conflict preserves receipt");
            A(fp.Calls==calls&&Bytes(conflict)==before&&ff.Commits==1,"Conflict neither overwrites disk nor publishes stale snapshot");
            A(new AudioSettingsBinding(new AtomicSettingsStore(conflict),router).Load().Accepted,"Explicit new binding loads external settings");Near(router.BusGain("SFX"),.9f,"External settings recovered");
        }
        string rejected=Path.Combine(dir,"rejected.json");Seed(rejected,"993","");var pf=new Playback();var faults=new Files();
        using(var router=new AudioRouter(catalog,pf))
        {
            var b=new AudioSettingsBinding(new AtomicSettingsStore(rejected,faults),router);A(b.Load().Accepted,"Failure load");int calls=pf.Calls;string before=Bytes(rejected);
            using(new FileStream(rejected+".lock",FileMode.Open,FileAccess.ReadWrite,FileShare.None))A(!b.PublishBus("SFX",.9f).Committed,"Real exclusive lock rejection");
            A(pf.Calls==calls&&Bytes(rejected)==before,"Rejected commit no live application");
            faults.BeforeCreate=delegate{throw new IOException("precommit failure");};var r=b.PublishBus("SFX",.9f);A(!r.Committed&&!r.LivePending&&faults.Commits==0&&pf.Calls==calls,"Actual store precommit IO rejection");
            faults.BeforeCreate=null;A(b.SaveBus("SFX",.4f).Accepted,"Rejected staging discarded and retry works");
            // Reentry cannot interleave a second edit during callbacks.
            bool invoked=false;pf.Callback=delegate{if(!invoked){invoked=true;A(!b.PublishBus("Voice",.9f).Accepted,"Reentrant edit rejected");}};
            A(b.PublishBus("Master",.7f).Accepted&&faults.Commits==2,"Reentry no duplicate commit");pf.Callback=null;
        }
        foreach(string raw in new[]{"{\"Future\":01}","{\"Future\":NaN}","{\"Future\":1e}","{\"Future\":{}}","{\"Future\":1,\"Future\":2}","{\"Music\":1e100}"})
        {
            string path=Path.Combine(dir,"invalid"+checks+".json");Seed(path,"993","");var store=new AtomicSettingsStore(path);A(store.Load(false).Accepted,"Invalid fixture load");store.Write("audio_volumes",raw);A(store.Flush().Accepted,"Opaque store retains even unsupported semantic value");
            using(var router=new AudioRouter(catalog,new Playback()))
            {
                string before=Bytes(path);var b=new AudioSettingsBinding(new AtomicSettingsStore(path),router);
                A(!b.LoadPublication().Accepted&&!b.PublishBus("SFX",.1f).Committed&&Bytes(path)==before,"Unsupported or invalid known data rejected without rewrite");
            }
        }
        foreach(var p in new[]{CueProfiles.LocalShot,CueProfiles.LocalReload,CueProfiles.Shot,CueProfiles.Reload,CueProfiles.Footstep,CueProfiles.Sprint,CueProfiles.Climb,CueProfiles.Creak,CueProfiles.Turbine,CueProfiles.Impact})
        {
            Near(p.Attenuation(0)*p.SourceGain,p.MaxGain,"Absolute near-field source cap");
            float last=p.Attenuation(0);for(int i=1;i<=4096;i++){float v=p.Attenuation(p.MaxMeters*i/4096);A(v>=0&&v<=last,"Monotone cap plus fade");last=v;}
            A(last==0&&p.Attenuation(p.MaxMeters+1)==0,"Continuous fade to silence");
            Near(p.Attenuation(p.MaxMeters/2), (float)(Math.Min(p.MaxGain/p.SourceGain,1.0/((p.MaxMeters/2)/p.UnitMeters+.00001))*.5),"Mid distance source policy");
        }
        Near(CueProfiles.Shot.Attenuation(70),.08571414f,"Review counterexample fixed");
        Near(CueProfiles.Footstep.Attenuation(1),.875f,"Source max_db no nearfield boost for foley");
        foreach(float d in new[]{-1,float.NaN,float.PositiveInfinity}){bool bad=false;try{CueProfiles.Shot.Attenuation(d);}catch(ArgumentException){bad=true;}A(bad,"Invalid distance rejected");}
        Console.WriteLine("PASS r4 "+checks+" assertions; real accepted physical disk, every live bus before/after mutation, retries/reopen/conflicts/exact numbers/source attenuation. No engine/listening claim.");
    }
}
