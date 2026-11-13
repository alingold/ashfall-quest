using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Persistence;
using Ashfall.Modules.Player;
using Ashfall.Modules.Presentation;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Modes;
using Ashfall.Integration.PersonalSettings;
sealed class R5Actions:ISettingsUiActions {
 public bool Paused;public void SetPaused(bool p){Paused=p;}public bool CanRequest(string a){return false;}public void Request(string a,long o){throw new Exception();}
}
sealed class EnvironmentPorts:IAuthorityGate,IHealthWorld,IInteractionQuery,ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IModeRoster {
 public double Now{get;set;}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
 public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("authority");}
 public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}
 public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return false;}
 public string Issue(){return "token";}public bool CanManage(CommandContext c){return c.IsAuthority;}
 public SpawnSite[] Sites(string m,bool w){return new[]{new SpawnSite("site",new Float3(),-1,true,false)};}
 public bool IsSafe(string m,string a,Float3 f){return true;}
 public bool TryGet(string id,out ModeActor a){a=new ModeActor(id,id,-1,"alive",true,false);return id=="p";}
 public ModeActor[] Snapshot(){return new[]{new ModeActor("p","p",-1,"alive",true,false)};}
}
static class R5Tests {
 static string root;static int pass;static long tick;
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Near(float a,float b){Check(Math.Abs(a-b)<.0002,"number "+a+" != "+b);}
 static Dictionary<string,string> D(string k,string v){return new Dictionary<string,string>{{k,v}};}
 static CommandContext C(string id){return new CommandContext("s","p",id,++tick,0,true);}
 static PersonalSettingsAdapter H(string n,SettingsSource s,R5Actions a){return new PersonalSettingsAdapter(Path.Combine(root,n+".ashfall-settings.json"),s,a);}
 static PresentationModel M(PersonalSettingsAdapter h){var m=new PresentationModel(h);m.Open();m.Activate("root");m.Activate("movement");m.Activate("turning");return m;}
 static void Test(string n,Action a){a();pass++;Console.WriteLine("PASS "+n);}
 public static int Main(string[] args){try{root=args[0];Directory.CreateDirectory(root);
 Test("canonical fresh and edited reset commit reload both source profiles",delegate{
  foreach(SettingsSource s in Enum.GetValues(typeof(SettingsSource)))foreach(bool edited in new[]{false,true}){
   string n="reset-"+s+edited;var a=new R5Actions();var h=H(n,s,a);if(edited)Check(h.CommitSettings(D("walk_speed_multiplier","3")).Accepted,"seed");
   var m=M(h);m.Activate("reset:movement");m.Resume();Check(!m.HasChanges&&!a.Paused,"reset save");
   var reopen=H(n,s,new R5Actions());reopen.CaptureSettings();Near(reopen.Effective.Number("walk_speed_multiplier"),1);
   Near(reopen.Effective.Number("sprint_slow_gain"),7.5f/6.5f);Near(reopen.Effective.Number("sprint_fast_gain"),9.5f/6.5f);
  }
 });
 Test("hidden reset permission revocation recovery retry discard",delegate{
  var a=new R5Actions();var h=H("revoke",SettingsSource.Quest,a);Check(h.CommitSettings(D("sprint_double_press_window","0.5")).Accepted,"seed");var m=M(h);
  h.SetContext(null,null,new[]{"sprint_double_press_window"});m.Activate("reset:movement");Check(!m.HasChanges,"hidden lock");h.SetContext(null,null,null);m.Activate("reset:movement");h.SetContext(null,null,new[]{"sprint_double_press_window"});m.Resume();Check(m.HasChanges&&!a.Paused,"safe exit retains draft");h.SetContext(null,null,null);m.Open();m.Resume();Check(!m.HasChanges,"retry");
  m=M(h);m.Adjust("comfort_speed",1);h.SetContext(null,null,new[]{"comfort_speed"});m.DiscardAndResume();Check(!m.HasChanges&&!a.Paused,"discard revoked");
 });
 Test("every source bus low nonzero and zero mute separate from dB",delegate{
  foreach(SettingsSource s in Enum.GetValues(typeof(SettingsSource))){var h=H("audio-"+s,s,new R5Actions());foreach(string bus in new[]{"Master","SFX","Music","Voice","Ambience"})foreach(string v in new[]{"0.0005","0"}){
   Check(h.CommitSettings(D(bus=="Master"?"master_volume":"audio_volumes."+bus,v)).Accepted,"bus commit");var e=h.Effective;Near(e.AudioDecibels(bus),bus=="Master"?(v=="0"?-80:-66.0206f):-60);Check(e.AudioMuted(bus)==(v=="0"),"mute");
  }}
 });
 Test("actual current Player physical roles ownership disarm neutral rearm",delegate{
  var h=H("player",SettingsSource.Quest,new R5Actions());h.CaptureSettings();var input=new PlayerInput();var b=new PlayerSettingsBinding(input);b.Apply(h.Effective);
  var s=new DeviceSample{IsXr=true,Focused=true,HeadTracked=true,HeadPosition=new Float3(1,2,3),Left=new HandSample{Tracked=true,Position=new Float3(4,5,6)},Right=new HandSample{Tracked=true,Position=new Float3(7,8,9)}};
  b.Read(1,.1f,s);s.Left.Click=true;Check(b.Read(2,.02f,s).Sprint,"initial click");
  Check(h.CommitSettings(new Dictionary<string,string>{{"dominant_hand","left"},{"sprint_binding","left_grip"}}).Accepted,"ownership commit");b.Apply(h.Effective);s.Left.Grip=1;s.Left.Trigger=1;s.Right.Primary=true;
  var f=b.Read(3,.02f,s);Check(input.Ownership.Dominant==DominantHand.Left&&input.Ownership.Sprint==SprintBinding.LeftGrip,"actual ownership");Check(!f.Sprint&&!f.TriggerLeft&&!f.Jump&&f.LeftHandPosition.X==4&&f.RightHandPosition.X==7,"held blocked poses fixed");
  s.Left.Click=false;s.Left.Grip=0;s.Left.Trigger=0;s.Right.Primary=false;b.Read(4,.1f,s);s.Left.Grip=1;s.Right.Primary=true;s.Left.Secondary=true;s.Left.StickY=.7f;s.Right.StickX=.6f;f=b.Read(5,.02f,s);Check(f.Sprint&&f.Jump&&f.Reload&&f.MoveY==.7f&&f.TurnX==.6f,"real remap");
  Check(h.CommitSettings(D("sprint_binding","left_stick_click")).Accepted,"sprint only");b.Apply(h.Effective);s.Left.Click=true;Check(!b.Read(6,.02f,s).Sprint,"sprint-only disarm");
 });
 Test("menu runtime host persists reloads and applies actual Player ownership",delegate{
  var a=new R5Actions();var h=H("runtime",SettingsSource.Quest,a);var input=new PlayerInput();var player=new PlayerSettingsBinding(input);var host=new PersonalSettingsRuntimeHost(h,player);var menu=new PresentationModel(host);
  menu.Open();menu.Activate("root");menu.Activate("weapons");menu.Adjust("dominant_hand",1);menu.Resume();Check(!menu.HasChanges&&!host.RuntimePending&&input.Ownership.Dominant==DominantHand.Left,"menu end to end");
  var other=H("runtime",SettingsSource.Quest,new R5Actions());Check(other.CommitSettings(D("sprint_binding","left_grip")).Accepted,"external");menu.Open();Check(input.Ownership.Sprint==SprintBinding.LeftGrip,"reopen reload publishes");menu.Resume();Check(!a.Paused,"resume");
 });
 Test("partial invalid finite fallback future schema unknown preservation immutable policies",delegate{
  string p=Path.Combine(root,"partial.ashfall-settings.json");var store=new AtomicSettingsStore(p);Check(store.Load(false).Accepted,"load");store.Write("walk_speed_multiplier","NaN");store.Write("sprint_fast_gain","1e999");store.Write("opaque","{\"future\":[1,2]}");Check(store.Flush().Accepted,"seed");
  var h=H("partial",SettingsSource.Quest,new R5Actions());h.CaptureSettings();var old=h.Personal;Near(old.Number("walk_speed_multiplier"),1);Check(h.CommitSettings(D("dominant_hand","left")).Accepted,"partial commit");h.SetContext(D("walk_speed_multiplier","3"),null,null);Near(h.Effective.Number("walk_speed_multiplier"),3);Near(h.Personal.Number("walk_speed_multiplier"),1);Check(!old.LeftHanded,"immutable");
  var reload=new AtomicSettingsStore(p);Check(reload.Load(false).Accepted,"reload");string raw;Check(reload.TryRead("opaque",out raw)&&raw=="{\"future\":[1,2]}","unknown retained");reload.Write("movement_settings_version","99");Check(reload.Flush().Accepted,"future seed");string bytes=File.ReadAllText(p);Check(!h.CommitSettings(D("dominant_hand","right")).Accepted&&File.ReadAllText(p)==bytes,"future fail closed");
 });
 Test("real Health mutations Session join Modes start drive HUD and reset danger",delegate{
  var w=new EnvironmentPorts();var hp=new HealthService("s",0,0,true,w,w,w);Check(hp.Register(C("register"),"p",-1,false).Accepted,"register");
  var session=new Session("s","content",new SessionRules("map","respawn","hash",0,8,true,false,true,true,5),w,w,w,w,w);string token;Check(session.Join(C("join"),new JoinRequest(Protocol.Name,Protocol.Schema,"content","name",null,false),out token).Accepted,"join");
  var rules=new ModeRules("respawn",0,10,120,1);var round=new RoundController(rules,w,"p","s",0,w,64);Check(round.StartRound(C("start"),0).Accepted,"round start");
  var hud=new HudModel();var binding=new PresentationStateBinding(hp,session,round,"p",delegate{return w.Now;},hud);binding.ZoneActive=true;binding.OutsideZone=true;hud.Update(binding.Capture(),false,0);Check(hud.Visible&&hud.ClockDanger,"danger");
  var before=hp.Read("p");Check(hp.ApplyLoss(C("hit"),"p",before.Revision,10,20,false).Accepted,"actual loss");foreach(var e in hp.DrainTransitions())binding.OnHealthTransition(e);hud.Update(binding.Capture(),false,0);Near(hud.Health,.9f);Near(hud.Shield,.8f);Near(hud.Incoming,30);
  binding.ZoneActive=false;hud.Update(binding.Capture(),false,0);Check(!hud.ClockDanger,"zone removal white");binding.ZoneActive=true;binding.OutsideZone=false;hud.Update(binding.Capture(),false,0);Check(!hud.ClockDanger,"safe white");binding.OutsideZone=true;hud.Update(binding.Capture(),true,0);Check(!hud.ClockDanger&&!hud.Visible,"pause clears");
 });
 Console.WriteLine("TOTAL "+pass+" PASS 0 FAIL");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
