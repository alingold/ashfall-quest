using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Persistence;
using Ashfall.Modules.Player;
using Ashfall.Modules.Presentation;
using Ashfall.Integration.PersonalSettings;
sealed class Actions : ISettingsUiActions {
    public bool Paused;
    public void SetPaused(bool value){Paused=value;}
    public bool CanRequest(string value){return false;}
    public void Request(string value,long token){throw new InvalidOperationException();}
}
sealed class FailingFiles : IAtomicSettingsFiles {
    readonly PhysicalSettingsFiles inner=new PhysicalSettingsFiles();
    public bool Fail;
    public string ExternalWrite;
    public IDisposable AcquireFamily(string p){return inner.AcquireFamily(p);}
    public bool Exists(string p){return inner.Exists(p);}
    public byte[] Read(string p,int max){return inner.Read(p,max);}
    public void CreateDurable(string p,byte[] b){inner.CreateDurable(p,b);if(ExternalWrite!=null){File.WriteAllText(ExternalWrite,"{\"schema\":1,\"values\":{\"dominant_hand\":\"right\",\"external\":\"keep\"}}");ExternalWrite=null;}}
    public void Commit(string p,string d,string b,bool r){if(Fail)throw new IOException("injected failure before replace");inner.Commit(p,d,b,r);}
}
static class R3Tests {
    static string root;static int passed;
    static void Check(bool c,string m){if(!c)throw new Exception(m);}
    static void Near(float a,float b){Check(Math.Abs(a-b)<.0001,"numeric "+a+" != "+b);}
    static string PathFor(string n){return Path.Combine(root,n+".ashfall-settings.json");}
    static Dictionary<string,string> D(string key,string value){return new Dictionary<string,string>{{key,value}};}
    static void Seed(string p,IDictionary<string,string> values){var s=new AtomicSettingsStore(p);Check(s.Load(false).Accepted,"seed load");foreach(var v in values)s.Write(v.Key,v.Value);Check(s.Flush().Accepted,"seed flush");}
    static PersonalSettingsAdapter Bridge(string p,Actions a){return new PersonalSettingsAdapter(p,SettingsSource.Quest,a);}
    static PresentationModel Flight(PersonalSettingsAdapter h){var m=new PresentationModel(h);m.Open();m.Activate("root");m.Activate("movement");m.Activate("turning");m.Activate("flight");return m;}
    static void Test(string n,Action test){test();passed++;Console.WriteLine("PASS "+n);}
    public static int Main(string[] args){try{root=args[0];Directory.CreateDirectory(root);
        Test("partial store defaults are not written by capture",delegate{
            string p=PathFor("partial");var h=Bridge(p,new Actions());h.CaptureSettings();Near(h.Effective.Number("walk_speed_multiplier"),1);Check(!File.Exists(p),"read wrote disk");
            Near(h.Effective.AudioLinear("Music"),0);Near(h.Effective.AudioLinear("SFX"),1);
        });
        Test("source profiles reconcile defaults limits and UI",delegate{
            string p=PathFor("profiles");Seed(p,new Dictionary<string,string>{{"movement_settings_version","3"},{"walk_speed_multiplier","9"}});
            var q=Bridge(p,new Actions());q.CaptureSettings();Near(q.Effective.CreatePlayerTuning().WalkMultiplier,5);
            var s=new PersonalSettingsAdapter(p,SettingsSource.SkinRuntime,new Actions());s.CaptureSettings();Near(s.Effective.CreatePlayerTuning().WalkMultiplier,3);
            var m=new PresentationModel(s);m.Open();m.Activate("root");m.Activate("movement");m.SetNumeric("walk_speed_multiplier",5);Check(m.Value("walk_speed_multiplier")=="3","UI profile clamp");
        });
        Test("nested migration actual import unknown tokens preserved",delegate{
            string p=PathFor("import"),src=Path.Combine(root,"source-godot.json");
            string raw="{\"movement_settings_version\":1,\"walk_speed_multiplier\":2.4,\"controls_tuning_revision\":0,\"glide_steering_gain\":2,\"audio_defaults_revision\":993,\"audio_volumes\":{\"SFX\":0.4,\"Voice\":0.7,\"future\":123456789012345678901234567890},\"weapon_settings\":{\"grip_toggle\":true,\"future\":{\"x\":[1,2]}},\"future_top\":{\"opaque\":true}}";
            File.WriteAllText(src,raw);var store=new AtomicSettingsStore(p);Check(store.Load(false).Accepted&&store.ImportGodotSettings(src).Accepted&&store.Flush().Accepted,"real import");
            var h=Bridge(p,new Actions());h.CaptureSettings();Near(h.Effective.Number("walk_speed_multiplier"),2);Near(h.Effective.Number("glide_steering_gain"),1);Near(h.Effective.AudioLinear("SFX"),.4f);Check(h.Effective.Flag("weapon_settings.grip_toggle"),"nested boolean");
            Check(h.CommitSettings(D("dominant_hand","left")).Accepted,"commit");h.CaptureSettings();Near(h.Effective.Number("walk_speed_multiplier"),2);Near(h.Effective.Number("glide_steering_gain"),1);
            var reload=new AtomicSettingsStore(p);Check(reload.Load(false).Accepted,"reload");string v;Check(reload.TryRead("audio_volumes",out v)&&v.Contains("123456789012345678901234567890"),"unknown numeric precision");Check(reload.TryRead("weapon_settings",out v)&&v.Contains("\"x\":[1,2]"),"unknown nested");Check(reload.TryRead("future_top",out v)&&v=="{\"opaque\":true}","unknown top");Check(File.ReadAllText(src)==raw,"source unchanged");
        });
        Test("typed handedness comfort vignette audio and Player public mapping",delegate{
            string p=PathFor("typed");var h=Bridge(p,new Actions());
            Check(h.CommitSettings(new Dictionary<string,string>{{"dominant_hand","left"},{"comfort_speed","true"},{"vignette_enabled","true"},{"turning_style","auto_snap"},{"snap_degrees","90"},{"smooth_turn_speed","180"},{"audio_volumes.Voice","0.5"}}).Accepted,"typed commit");
            var e=h.Effective;Check(e.LeftHanded&&e.Vignette&&e.Text("forward_reference")=="head","typed flags");Near(e.WalkBaseMetersPerSecond,2.6f);Near(e.AudioDecibels("Voice"),-6.0206f);Near(e.AudioDecibels("Music"),-80);
            var t=e.CreatePlayerTuning();Check(t.Turning==TurnStyle.AutoSnap&&t.SmoothAnalog,"turn flags");Near(t.SnapRadians,(float)Math.PI/2);Near(t.SmoothRadiansPerSecond,(float)Math.PI);t.WalkMultiplier=4;Near(e.CreatePlayerTuning().WalkMultiplier,1);
        });
        Test("host map precedence personal restoration and immutable snapshots",delegate{
            var h=Bridge(PathFor("overrides"),new Actions());Check(h.CommitSettings(D("walk_speed_multiplier","1.5")).Accepted,"seed");
            h.SetContext(D("walk_speed_multiplier","3"),D("walk_speed_multiplier","2"),null);var s=h.CaptureSettings();var old=h.Effective;Near(old.Number("walk_speed_multiplier"),3);Check(!h.CommitSettings(D("walk_speed_multiplier","4")).Accepted,"override write rejected");
            Check(h.CommitSettings(D("dominant_hand","left")).Accepted,"unrelated edit");h.SetContext(null,null,null);h.CaptureSettings();Near(h.Effective.Number("walk_speed_multiplier"),1.5f);Near(old.Number("walk_speed_multiplier"),3);string v;Check(s.TryReadPersonal("walk_speed_multiplier",out v)&&v=="1.5","personal preserved");
        });
        Test("commit failure leaves disk and effective values intact retry reload",delegate{
            string p=PathFor("failure");var f=new FailingFiles();var h=new PersonalSettingsAdapter(p,SettingsSource.Quest,new Actions(),f);Check(h.CommitSettings(D("dominant_hand","right")).Accepted,"initial");byte[] before=File.ReadAllBytes(p);f.Fail=true;
            Check(!h.CommitSettings(D("dominant_hand","left")).Accepted&&!h.Effective.LeftHanded,"failed leaked into runtime");Check(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(p)),"failed disk changed");h.CaptureSettings();Check(!h.Effective.LeftHanded,"reload failure");f.Fail=false;Check(h.CommitSettings(D("dominant_hand","left")).Accepted,"retry");Check(Bridge(p,new Actions()).CaptureSettings()!=null,"reopen");
        });
        Test("refresh dirty fast clean slow regression",delegate{
            string p=PathFor("fast");var h=Bridge(p,new Actions());var m=Flight(h);m.SetNumeric("sprint_fast_gain",1.5);Seed(p,D("sprint_slow_gain","2"));m.RefreshSettings();Check(m.Value("sprint_fast_gain")=="2","fast reconciliation");m.Resume();h.CaptureSettings();Near(h.Effective.Number("sprint_fast_gain"),2);Check(!m.HasChanges,"draft committed");
        });
        Test("refresh dirty slow clean fast plus precommit refresh",delegate{
            string p=PathFor("slow");var h=Bridge(p,new Actions());var m=Flight(h);m.SetNumeric("sprint_slow_gain",2);Seed(p,D("sprint_fast_gain","1.2"));m.Resume();h.CaptureSettings();Near(h.Effective.Number("sprint_fast_gain"),2);Near(h.Effective.Number("sprint_slow_gain"),2);
        });
        Test("permission revocation exits pause retains draft retries",delegate{
            var a=new Actions();var h=Bridge(PathFor("revoked"),a);var m=Flight(h);m.SetNumeric("sprint_fast_gain",1.5);h.SetContext(null,null,new[]{"sprint_fast_gain"});m.RefreshSettings();m.Resume();Check(!a.Paused&&!m.IsOpen&&m.HasChanges,"pause trap");h.SetContext(null,null,null);m.Open();m.Resume();Check(!m.HasChanges,"retry");
        });
        Test("dependent locked refresh has conflict and safe discard",delegate{
            string p=PathFor("lockedpair");var a=new Actions();var h=Bridge(p,a);var m=Flight(h);m.SetNumeric("sprint_fast_gain",1.5);Seed(p,D("sprint_slow_gain","2"));h.SetContext(null,null,new[]{"sprint_fast_gain"});m.RefreshSettings();Check(m.Error.Contains("conflict"),"conflict absent");m.Resume();Check(!a.Paused&&m.HasChanges,"safe exit");m.DiscardAndResume();Check(!m.HasChanges&&!a.Paused,"discard");
        });
        Test("override arriving after edit never flattens personal",delegate{
            string p=PathFor("lateoverride");var a=new Actions();var h=Bridge(p,a);var m=Flight(h);m.SetNumeric("sprint_fast_gain",2);h.SetContext(D("sprint_fast_gain","2.5"),null,null);m.RefreshSettings();m.Resume();Check(m.HasChanges&&!a.Paused,"draft retained");h.SetContext(null,null,null);m.DiscardAndResume();h.CaptureSettings();Check(h.Effective.Number("sprint_fast_gain")<2,"override flattened");
        });
        Test("invalid partial batch schema corruption and composed conflict reject",delegate{
            string p=PathFor("reject");var h=Bridge(p,new Actions());h.CaptureSettings();Check(!h.CommitSettings(new Dictionary<string,string>{{"dominant_hand","left"},{"master_volume","NaN"}}).Accepted&&!File.Exists(p),"partial commit");Check(!h.CommitSettings(D("sprint_fast_gain","1")).Accepted,"invalid sprint");h.SetContext(D("sprint_slow_gain","2"),null,null);bool bad=false;try{var e=h.Effective;}catch(InvalidOperationException){bad=true;}Check(bad,"conflicting override hidden");
            File.WriteAllText(p,"{\"schema\":99,\"values\":{}}");Check(!h.CommitSettings(D("dominant_hand","left")).Accepted,"future overwritten");File.WriteAllText(p,"broken");Check(!h.CommitSettings(D("dominant_hand","left")).Accepted,"corrupt overwritten");
        });
        Test("external disk reload and mid-flush conflict preserve writer",delegate{
            string p=PathFor("cas");var f=new FailingFiles();var h=new PersonalSettingsAdapter(p,SettingsSource.Quest,new Actions(),f);
            Check(h.CommitSettings(D("dominant_hand","right")).Accepted,"initial");f.ExternalWrite=p;
            Check(!h.CommitSettings(D("dominant_hand","left")).Accepted,"external writer clobbered");Check(!h.Effective.LeftHanded,"rejected state leaked");h.CaptureSettings();Check(!h.Effective.LeftHanded&&File.ReadAllText(p).Contains("external"),"reload external");
            Seed(p,D("dominant_hand","left"));h.CaptureSettings();Check(h.Effective.LeftHanded,"actual disk reload");
        });
        Test("newer semantic revisions fail closed without downgrade",delegate{
            foreach(string key in new[]{"movement_settings_version","controls_tuning_revision","audio_defaults_revision"}) {
                string p=PathFor("revision-"+key);Seed(p,D(key,"9999"));string before=File.ReadAllText(p);var h=Bridge(p,new Actions());
                Check(!h.CommitSettings(D("dominant_hand","left")).Accepted&&File.ReadAllText(p)==before,"future revision overwritten");
            }
        });
        Test("actual PlayerInput handedness disarm pause and held action neutral",delegate{
            var h=Bridge(PathFor("input"),new Actions());h.CaptureSettings();var input=new PlayerInput();var binding=new PlayerSettingsBinding(input);binding.Apply(h.Effective);
            var sample=new DeviceSample { Focused=true,HeadPosition=new Float3(1,2,3) };
            binding.Read(1,.1f,sample);sample.Left.Grip=1;Check(binding.Read(2,.02f,sample).GripLeft,"initial action");
            Check(h.CommitSettings(D("dominant_hand","left")).Accepted,"left commit");binding.Apply(h.Effective);
            var disarmed=binding.Read(3,.02f,sample);Check(!disarmed.GripLeft&&disarmed.HeadPosition.X==1,"hand change did not disarm or moved head");
            sample.Left.Grip=0;binding.Read(4,.1f,sample);sample.Left.Grip=1;Check(binding.Read(5,.02f,sample).GripLeft,"rearm");
            binding.SetPresentationPaused(true);var paused=binding.Read(6,.02f,sample);Check(paused.Paused&&!paused.GripLeft&&paused.Tracked,"pause tracking/action");
            binding.SetPresentationPaused(false);Check(!binding.Read(7,.02f,sample).GripLeft,"held input resumed");
        });
        Console.WriteLine("PASS "+passed+" behavior groups");return 0;
    }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
