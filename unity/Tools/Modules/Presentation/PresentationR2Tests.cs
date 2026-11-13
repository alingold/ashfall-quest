using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Presentation;

sealed class Store : ISettingsStore {
    public readonly Dictionary<string,string> Data=new Dictionary<string,string>();
    public int Writes,Flushes;
    public bool TryRead(string key,out string value){return Data.TryGetValue(key,out value);}
    public void Write(string key,string value){Writes++;Data[key]=value;}
    public RuleResult Flush(){Flushes++;return RuleResult.Accept();}
}
sealed class TypedHost : IPresentationHost, IPresentationSettingsHost {
    public readonly Store Store=new Store();
    public readonly Dictionary<string,string> Overrides=new Dictionary<string,string>();
    public readonly HashSet<string> Locked=new HashSet<string>();
    public bool Paused,ReadFailure;
    public int Commits;
    public long Token;
    public SettingsSnapshot CaptureSettings(){if(ReadFailure)throw new Exception("private detail");return new SettingsStorePresentationPort(Store).Capture(Overrides,null);}
    public bool TryReadSetting(string key,out string value){throw new Exception("legacy path must not be used");}
    public bool CanEdit(string key){return !Locked.Contains(key);}
    public RuleResult CommitSettings(IDictionary<string,string> changes){
        Commits++;foreach(var p in changes)if(Locked.Contains(p.Key)||Overrides.ContainsKey(p.Key))return RuleResult.Reject("private detail");
        foreach(var p in changes)Store.Data[p.Key]=p.Value;return RuleResult.Accept();
    }
    public void SetGameplayPaused(bool value){Paused=value;}
    public bool CanRequest(string action){return true;}
    public void Request(string action,long token){Token=token;}
}
static class R2Tests {
    static int passed;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Test(string name,Action test){test();passed++;Console.WriteLine("PASS "+name);}
    static PresentationModel Page(TypedHost h,string page){var m=new PresentationModel(h);m.Open();m.Activate("root");m.Activate(page);return m;}
    public static int Main(){try {
        Test("immutable store snapshot separates personal host and map",delegate{
            var store=new Store();store.Data["walk_speed_multiplier"]="1";store.Data["audio_volumes.SFX"]="0.4";store.Data["unknown"]="keep";
            var host=new Dictionary<string,string>{{"walk_speed_multiplier","3"}};var map=new Dictionary<string,string>{{"walk_speed_multiplier","2"}};
            var snap=new SettingsStorePresentationPort(store).Capture(host,map);host["walk_speed_multiplier"]="4";store.Data["walk_speed_multiplier"]="5";
            string value;Check(snap.TryReadPersonal("walk_speed_multiplier",out value)&&value=="1","immutable personal");
            Check(snap.TryReadEffective("walk_speed_multiplier",out value)&&value=="3"&&snap.Origin("walk_speed_multiplier")=="host","immutable effective precedence");
            Check(snap.TryReadPersonal("audio_volumes.SFX",out value)&&value=="0.4","exact source dotted path");
            Check(store.Writes==0&&store.Flushes==0&&store.Data["unknown"]=="keep","reader never persists");
        });
        Test("overrides display effective values without writing personal state",delegate{
            var h=new TypedHost();h.Store.Data["walk_speed_multiplier"]="1";h.Overrides["walk_speed_multiplier"]="3";var m=Page(h,"movement");
            Check(m.Value("walk_speed_multiplier")=="1"&&m.EffectiveValue("walk_speed_multiplier")=="3","separate views");
            Check(!m.SetNumeric("walk_speed_multiplier",4),"override locked");m.Resume();Check(h.Commits==0&&h.Store.Data["walk_speed_multiplier"]=="1","no override flattened");
        });
        Test("refresh preserves only drafts and reloads other personal values",delegate{
            var h=new TypedHost();var m=Page(h,"movement");m.SetNumeric("walk_speed_multiplier",2);h.Store.Data["walk_speed_multiplier"]="4";h.Store.Data["snap_degrees"]="90";
            m.RefreshSettings();Check(m.Value("walk_speed_multiplier")=="2"&&m.Value("snap_degrees")=="90","draft conflict policy");
            m.DiscardAndResume();Check(m.Value("walk_speed_multiplier")=="4"&&!h.Paused,"discard reloads current host");
        });
        Test("revocation allows back resume retry and explicit discard",delegate{
            var h=new TypedHost();var m=Page(h,"movement");m.SetNumeric("walk_speed_multiplier",2);h.Locked.Add("walk_speed_multiplier");m.Back();
            Check(m.Page=="root"&&m.HasChanges&&m.Error.Length>0,"back preserved rejected edit");m.Resume();
            Check(!m.IsOpen&&!h.Paused&&m.HasChanges&&h.Store.Data.Count==0,"safe resume keeps draft");
            m.Open();Check(m.HasChanges&&m.Value("walk_speed_multiplier")=="2","reopen keeps draft");
            h.Locked.Clear();m.Activate("retry");Check(!m.HasChanges&&h.Store.Data["walk_speed_multiplier"]=="2","retry after permission restored");
            m.Activate("root");m.Activate("movement");m.SetNumeric("walk_speed_multiplier",3);h.Locked.Add("walk_speed_multiplier");m.DiscardAndResume();
            Check(!h.Paused&&!m.HasChanges&&m.Value("walk_speed_multiplier")=="2","permission independent discard");
        });
        Test("readiness errors sanitize disable edits and recover without pause trap",delegate{
            var h=new TypedHost();h.ReadFailure=true;var m=Page(h,"movement");Check(!m.SettingsReady&&!m.SetNumeric("walk_speed_multiplier",2),"unready locked");
            Check(m.Error.Length>0&&!m.Error.Contains("private"),"sanitized visible error");m.Resume();Check(!h.Paused&&!m.IsOpen,"unready exit");
            h.ReadFailure=false;h.Store.Data["walk_speed_multiplier"]="3";m.Open();Check(m.SettingsReady&&m.Value("walk_speed_multiplier")=="3","ready refresh");
        });
        Test("loading has safe resume and stale completion cannot reclaim UI",delegate{
            var h=new TypedHost();var m=new PresentationModel(h);m.Open();m.Activate("action:home");long token=h.Token;
            Check(m.Loading,"loading");m.Resume();Check(!m.Loading&&!m.IsOpen&&!h.Paused,"exit pending operation");
            m.Open();m.Complete(token,true,"",true);Check(m.IsOpen&&h.Paused,"late result ignored");m.Suspend();m.Suspend();Check(!h.Paused,"idempotent teardown");
        });
        Test("unsupported HUD controls disabled retain saved preferences",delegate{
            var h=new TypedHost();h.Store.Data["hud_size"]="1.5";h.Store.Data["hud_style"]="reference";var m=Page(h,"general");m.Activate("game");
            foreach(var row in m.Rows())if(row.Id=="hud_size"||row.Id=="hud_style")Check(!row.Enabled,"unsupported disabled");
            Check(!m.SetNumeric("hud_size",1),"slider disabled");m.Activate("hud_style");m.Resume();Check(h.Commits==0&&h.Store.Data["hud_style"]=="reference","no misleading persistence");
        });
        Test("numeric selection focus-only directional edits and finite extremes",delegate{
            var h=new TypedHost();var m=Page(h,"movement");m.Activate("walk_speed_multiplier");Check(!m.HasChanges&&m.Focus=="walk_speed_multiplier","selection");
            m.Adjust("walk_speed_multiplier",1);Check(m.Value("walk_speed_multiplier")=="1.05","explicit edit");m.SetNumeric("walk_speed_multiplier",double.MaxValue);Check(m.Value("walk_speed_multiplier")=="5","max clamp");
            m.SetNumeric("walk_speed_multiplier",-double.MaxValue);Check(m.Value("walk_speed_multiplier")=="0.25","min clamp");
        });
        Test("hidden reset key permission checked atomically and preserved unknown values",delegate{
            var h=new TypedHost();h.Store.Data["sprint_double_press_window"]="0.6";h.Store.Data["unknown"]="keep";var m=Page(h,"movement");m.SetNumeric("walk_speed_multiplier",3);m.Activate("turning");
            h.Locked.Add("sprint_double_press_window");m.Activate("reset:movement");Check(m.Value("walk_speed_multiplier")=="3","no partial reset");
            h.Locked.Clear();m.Activate("reset:movement");m.Back();Check(h.Store.Data["sprint_double_press_window"]=="0.32"&&h.Store.Data["unknown"]=="keep","source reset and unknown retention");
        });
        Console.WriteLine("PASS "+passed+" r2 regression scenarios");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
