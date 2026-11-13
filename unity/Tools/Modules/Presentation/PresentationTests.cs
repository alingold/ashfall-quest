using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Presentation;

internal sealed class Host : IPresentationHost
{
    public readonly Dictionary<string,string> Saved=new Dictionary<string,string>();
    public readonly HashSet<string> Locked=new HashSet<string>();
    public bool FailSave,ThrowSave,Paused;
    public int Commits,Requests;
    public long Token;
    public bool TryReadSetting(string key,out string value){return Saved.TryGetValue(key,out value);}
    public bool CanEdit(string key){return !Locked.Contains(key);}
    public RuleResult CommitSettings(IDictionary<string,string> changes){
        Commits++;if(ThrowSave)throw new InvalidOperationException("private path");
        if(FailSave)return RuleResult.Reject("private path");
        foreach(var pair in changes)Saved[pair.Key]=pair.Value;return RuleResult.Accept();
    }
    public void SetGameplayPaused(bool value){Paused=value;}
    public bool CanRequest(string action){return action=="home" || action=="calibrate";}
    public void Request(string action,long operation){Requests++;Token=operation;}
}
internal static class PresentationTests
{
    private static int passed;
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Test(string name,Action test){test();passed++;Console.WriteLine("PASS "+name);}
    private static PresentationModel Page(Host host,params string[] path){var model=new PresentationModel(host);model.Open();model.Activate("root");foreach(string p in path)model.Activate(p);return model;}
    public static int Main(){try{
        Test("source defaults and invalid saved values",delegate{
            var h=new Host();h.Saved["smooth_turn_speed"]="NaN";h.Saved["hud_size"]="999";h.Saved["turning_style"]="unrecognized";
            var m=new PresentationModel(h);Check(m.Value("smooth_turn_speed")=="360","finite fallback");Check(m.Value("hud_size")=="1.75","clamp");
            Check(m.Value("turning_style")=="smooth" && m.Value("audio_volumes.Music")=="0" && m.Value("audio_volumes.Ambience")=="0","accepted defaults");
        });
        Test("navigation restores parent focus and releases gameplay only on close",delegate{
            var h=new Host();var m=Page(h,"movement","turning","flight");Check(h.Paused,"pause acquired");
            m.Back();Check(m.Page=="turning"&&m.Focus=="flight","flight parent");m.Back();Check(m.Focus=="turning","movement parent");
            m.Back();Check(m.Focus=="movement","root parent");m.Back();Check(m.Page=="pause"&&h.Paused,"pause parent");m.Back();Check(!m.IsOpen&&!h.Paused,"resume");
        });
        Test("host-controlled controls skip focus and reject direct activation",delegate{
            var h=new Host();h.Locked.Add("fling_power");h.Locked.Add("walk_speed_multiplier");var m=Page(h,"movement");
            m.FocusRow("smooth_turn_speed");m.Move(1);Check(m.Focus=="turning","disabled skipped");
            Check(!m.SetNumeric("walk_speed_multiplier",5),"locked slider");m.Activate("fling_power");Check(m.Value("fling_power")=="1","locked direct activate");
        });
        Test("actual source slider range quantization and invalid command rejection",delegate{
            var h=new Host();var m=Page(h,"movement");m.SetNumeric("walk_speed_multiplier",8);Check(m.Value("walk_speed_multiplier")=="5","R22 max");
            m.SetNumeric("snap_degrees",42);Check(m.Value("snap_degrees")=="40","snap step");Check(!m.SetNumeric("snap_degrees",double.NaN),"NaN");
            m.Back();Check(h.Saved["snap_degrees"]=="40"&&h.Saved["walk_speed_multiplier"]=="5","durable batch");
            var reload=new PresentationModel(h);Check(reload.Value("snap_degrees")=="40","reloaded saved setting");
        });
        Test("failed durability allows back, keeps drafts and retries",delegate{
            var h=new Host();var m=Page(h,"general");m.SetNumeric("master_volume",.234);h.FailSave=true;m.Back();
            Check(m.Page=="root"&&h.Paused&&m.HasChanges&&h.Saved.Count==0,"no exit on failure");Check(!m.Error.Contains("private"),"player error sanitation");
            h.FailSave=false;m.Activate("retry");Check(!m.HasChanges&&m.Error==""&&h.Saved["master_volume"]=="0.23","retry persisted");m.Back();Check(m.Page=="pause","can leave after retry");
        });
        Test("persistence exceptions preserve a recoverable UI",delegate{
            var h=new Host();var m=Page(h,"weapons");m.Activate("dominant_hand");h.ThrowSave=true;m.Back();Check(m.HasChanges&&m.Page=="root","exception held draft");
            h.ThrowSave=false;m.Back();Check(h.Saved["dominant_hand"]=="left","exception retry");
        });
        Test("dependent sprint constraints remain atomic",delegate{
            var h=new Host();var m=Page(h,"movement","turning","flight");m.SetNumeric("sprint_slow_gain",1.9);Check(m.Value("sprint_fast_gain")=="1.9","fast raised");
            m.SetNumeric("sprint_fast_gain",1.1);Check(m.Value("sprint_fast_gain")=="1.9","fast never below slow");
            h.Locked.Add("sprint_fast_gain");Check(!m.SetNumeric("sprint_slow_gain",2),"dependent lock rejects");Check(m.Value("sprint_slow_gain")=="1.9","no partial mutation");
        });
        Test("reset restores movement defaults and respects host locks",delegate{
            var h=new Host();var m=Page(h,"movement");m.SetNumeric("walk_speed_multiplier",4);m.Activate("turning");
            h.Locked.Add("fling_power");m.Activate("reset:movement");Check(m.Value("walk_speed_multiplier")=="4","no partial reset");
            h.Locked.Clear();m.Activate("reset:movement");Check(m.Value("walk_speed_multiplier")=="1"&&m.Value("turning_style")=="smooth","accepted reset");
            m.Back();Check(h.Saved["walk_speed_multiplier"]=="1","reset persisted");
        });
        Test("loading suppresses duplicate actions and stale completion",delegate{
            var h=new Host();var m=new PresentationModel(h);m.Open();m.Activate("action:home");long first=h.Token;
            m.Activate("action:home");Check(h.Requests==1&&m.Loading&&h.Paused,"duplicate suppressed");
            m.Complete(first-1,true,"",true);Check(m.Loading,"stale ignored");m.Complete(first,false,"secret",false);Check(!m.Loading&&!m.Error.Contains("secret"),"failure shown");
            m.Activate("retry");Check(h.Requests==2&&h.Token>first,"new retry operation");m.Complete(first,true,"",true);Check(m.Loading,"old retry ignored");
            m.Complete(h.Token,true,"",true);Check(!m.IsOpen&&!h.Paused,"successful action closes");
        });
        Test("HUD source visibility ghost and meter rules",delegate{
            var hud=new HudModel();var input=new HudInput{Health=150,Shield=-2,Stamina=-1,StaminaMaximum=6};
            hud.Update(input,false,0);Check(hud.Visible&&hud.Health==1&&hud.Shield==0&&hud.Sprint==1,"meters");
            input.Dead=true;hud.Update(input,false,0);Check(!hud.Visible,"dead hidden");input.NetworkGhost=true;hud.Update(input,false,0);Check(hud.Visible,"ghost visible");
            input.Complete=true;hud.Update(input,false,0);Check(!hud.Visible,"complete hidden");input.Complete=false;hud.Update(input,true,0);Check(!hud.Visible,"paused hidden");
        });
        Test("HUD damage aggregation expiry and no resurfacing after pause",delegate{
            var hud=new HudModel();var input=new HudInput{Health=100,Stamina=3,StaminaMaximum=6};hud.Update(input,false,0);hud.Damage(10);hud.Update(input,false,.5f);hud.Damage(5);
            Check(hud.Incoming==15&&hud.Sprint==.5f,"aggregate");hud.Update(input,false,-10);Check(hud.IncomingSeconds==1.15f,"negative delta ignored");
            hud.Update(input,false,1.2f);Check(hud.Incoming==0,"expired");hud.Damage(10);hud.Update(input,true,0);hud.Update(input,false,0);Check(hud.Incoming==0,"pause clears damage");
            hud.Damage(float.NaN);hud.Update(input,false,float.NaN);Check(hud.Incoming==0,"nonfinite safe");
        });
        Console.WriteLine("PASS "+passed+" production behavior scenarios");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
