using System;
using System.Collections.Generic;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.PersonalSettings;
public sealed class SteeringTests:IAuthorityGate,IFlightWorld,IFlightLifecycleAuthorization,ISettingsUiActions
{
 int count;bool deny;Action callback;
 public RuleResult Validate(CommandContext c){var a=callback;callback=null;if(a!=null)a();return deny?RuleResult.Reject("denied"):RuleResult.Accept();}
 public RuleResult ValidateTransition(CommandContext c,long g,FlightLifecycle r){return RuleResult.Accept();}
 public bool ClearPadContact(string id,Float3 f,Float3 p){return true;}
 public bool ClearCapsuleExit(string id,Float3 p){return true;}
 public void SetPaused(bool p){} public bool CanRequest(string a){return false;} public void Request(string a,long o){}
 static CommandContext C(string id,long t=1){return new CommandContext("s","a",id,t,3,true);}
 void Check(bool b,string n){if(!b)throw new Exception(n);count++;}
 void Near(float a,float b,string n){Check(Math.Abs(a-b)<.00001f,n+" "+a+" vs "+b);}
 static FlightFrame F(){return new FlightFrame{DeltaSeconds=.1f,InputEnabled=true,Tracked=true,XR=true,HandsReady=true,Forward=new Float3(0,0,1),LeftHand=new Float3(-1,.4f,0),RightHand=new Float3(1,0,0)};}
 static InputFrame I(long t=1){return new InputFrame{Tick=t,Sequence=t,Tracked=true};}
 PlayerFlightMotor Motor(){return new PlayerFlightMotor("s","a",3,this,new PlayerTuning(),1);}
 FlightMachine Flight(){return new FlightMachine("s","a",3,this,PlayerTuning.WalkSpeed*2.5f,1);}
 void Run(string dir)
 {
  foreach(float bad in new[]{Single.NaN,Single.PositiveInfinity,Single.NegativeInfinity,.249f,2.501f}){bool refused=false;try{new FlightSettingsSnapshot(bad);}catch(ArgumentOutOfRangeException){refused=true;}Check(refused,"invalid gain");}
  float last=-1;
  foreach(float gain in new[]{.25f,.875f,2.5f})
  {
   var m=Motor();Check(m.PublishFlightSettings(C("settings"),0,1,new FlightSettingsSnapshot(gain)).Accepted,"publish");MotionStep motion;
   Check(m.Step(C("step"),I(),new MotionEnvironment{IsXr=true},F(),out motion).Accepted,"real motor glide");
   float bank=.715f*gain;float speed=PlayerTuning.WalkSpeed*2.5f*(12f/6.5f);float expected=speed*bank/(float)Math.Sqrt(1+bank*bank);
   Near(m.Velocity.X,expected,"heading numeric oracle");Near(m.Velocity.Y,-speed/7,"ratio");Check(m.Velocity.X>last,"gain changes heading");last=m.Velocity.X;
   var direct=Flight();Check(direct.PublishSettings(C("settings"),0,1,new FlightSettingsSnapshot(gain)).Accepted,"direct publish");Check(direct.Step(C("step"),F()).Accepted,"direct step");Near(direct.Velocity.X,m.Velocity.X,"direct/composed");
  }
  foreach(float gain in new[]{.25f,.875f,2.5f})foreach(float diff in new[]{-.4f,-.08f,0,.08f,.4f})
  {
   var f=Flight();Check(f.PublishSettings(C("s"),0,1,new FlightSettingsSnapshot(gain)).Accepted,"bank publish");var frame=F();frame.LeftHand=new Float3(-1,diff,0);
   Check(f.Step(C("b"),frame).Accepted,"bank step");float bank=Math.Sign(diff)*Math.Max(0,Math.Min(1,(Math.Abs(diff)-.08f)/.32f))*.715f*gain;
   Near(f.Velocity.X,PlayerTuning.WalkSpeed*2.5f*(12f/6.5f)*bank/(float)Math.Sqrt(1+bank*bank),"signed deadzone bank");
  }
  var defaultMotor=Motor();MotionStep output;Check(defaultMotor.Step(C("d"),I(),new MotionEnvironment{IsXr=true},F(),out output).Accepted,"default");
  var explicitMotor=Motor();Check(explicitMotor.PublishFlightSettings(C("s"),0,1,FlightSettingsSnapshot.Default).Accepted,"explicit default");Check(explicitMotor.Step(C("d"),I(),new MotionEnvironment{IsXr=true},F(),out output).Accepted,"explicit step");
  Check(BitConverter.ToInt32(BitConverter.GetBytes(defaultMotor.Velocity.X),0)==BitConverter.ToInt32(BitConverter.GetBytes(explicitMotor.Velocity.X),0),"default exact bits");
  foreach(bool resolved in new[]{false,true})
  {
   var f=Flight();PreparedFlightStep p;Check(f.PrepareStep(C("step"),F(),out p).Accepted,"prepare");Float3 v;if(resolved)Check(f.ResolvePreparedStep(p,0,new Float3(),out v).Accepted,"resolve");
   Check(f.PublishSettings(C("settings"),0,1,new FlightSettingsSnapshot(2.5f)).Accepted,"replacement");Check(!f.ValidatePreparedStep(p).Accepted,"stale invalidated");Check(!f.ResolvePreparedStep(p,0,new Float3(),out v).Accepted,"stale cannot recalculate");p.Abort();
   Check(f.PrepareStep(C("step"),F(),out p).Accepted,"same command retry");Check(f.ResolvePreparedStep(p,0,new Float3(),out v).Accepted,"retry resolve");f.CommitPreparedStepValidated(p);Check(v.X>10,"retry uses new gain");
  }
  var motor=Motor();PreparedPlayerFlightStep plan;Check(motor.PrepareStep(C("step"),I(),new MotionEnvironment{IsXr=true},F(),out plan).Accepted,"composed prepare");
  Check(motor.PublishFlightSettings(C("settings"),0,1,new FlightSettingsSnapshot(.25f)).Accepted,"replace composed");Check(!plan.Validate().Accepted,"composed invalidated");bool threw=false;try{plan.CommitValidated();}catch(InvalidOperationException){threw=true;}Check(threw,"refuses before Player effects");Near(motor.Velocity.Y,0,"no gravity consumed");plan.Abort();
  Check(motor.Step(C("step"),I(),new MotionEnvironment{IsXr=true},F(),out output).Accepted,"exact input retry");
  var flight=Flight();Check(!flight.PublishSettings(C("null"),0,1,null).Accepted,"null");Check(!flight.PublishSettings(C("gen"),1,1,FlightSettingsSnapshot.Default).Accepted,"wrong generation");deny=true;Check(!flight.PublishSettings(C("deny"),0,1,FlightSettingsSnapshot.Default).Accepted,"authority refusal");deny=false;
  Check(flight.PublishSettings(C("deny"),0,1,FlightSettingsSnapshot.Default).Accepted,"publication retry");Check(!flight.PublishSettings(C("stale"),0,1,FlightSettingsSnapshot.Default).Accepted,"stale revision");Check(!flight.PublishSettings(C("deny"),0,2,FlightSettingsSnapshot.Default).Accepted,"publication replay");
  flight=Flight();var reentrant=flight;callback=()=>Check(reentrant.PublishSettings(C("inner"),0,1,new FlightSettingsSnapshot(2.5f)).Accepted,"inner");Check(!flight.Step(C("outer"),F()).Accepted,"reentrant step refuses");Check(flight.Step(C("outer"),F()).Accepted,"reentrant exact retry");Check(flight.Velocity.X>10,"retry new gain");
  foreach(int state in new[]{0,1,2,3,4}){motor=Motor();Check(motor.PublishFlightSettings(C("settings"),0,1,new FlightSettingsSnapshot(2.5f)).Accepted,"suspension settings");var input=I();var env=new MotionEnvironment{IsXr=true};var frame=F();if(state==0)input.Paused=true;if(state==1)input.Tracked=false;if(state==2)env.ExternalMotionOwner=true;if(state==3)frame.Climbing=true;if(state==4)frame.Mantling=true;Check(motor.Step(C("step"),input,env,frame,out output).Accepted,"suspended step");Near(motor.Velocity.X,0,"suspension no steering");}
  motor=Motor();var pad=new LaunchPad("p",new Float3(),new Float3(0,1,0),1,20);PreparedPadFlight padPlan;
  Check(motor.PrepareStepAfterPad(C("launch"),C("step"),I(),new MotionEnvironment{IsXr=true},F(),pad,this,new Float3(),new Float3(),false,10,true,false,false,false,out padPlan).Accepted,"pad prepare");
  Check(motor.PublishFlightSettings(C("settings"),0,1,new FlightSettingsSnapshot(2.5f)).Accepted,"pad settings");Check(!padPlan.Validate(10).Accepted,"pad invalidated");threw=false;try{padPlan.CommitValidated();}catch(InvalidOperationException){threw=true;}Check(threw,"pad no partial commit");padPlan.Abort();
  Check(motor.PrepareStepAfterPad(C("launch"),C("step"),I(),new MotionEnvironment{IsXr=true},F(),pad,this,new Float3(),new Float3(),false,10,true,false,false,false,out padPlan).Accepted,"pad exact cooldown/input retry");Check(padPlan.Validate(10).Accepted,"pad validation");padPlan.CommitValidated();Near(motor.Velocity.Y,20,"one launch");
  Check(motor.ResetActor(C("death",2),1,FlightLifecycle.Death,this).Accepted,"death");Check(!motor.PublishFlightSettings(C("dead",3),1,2,FlightSettingsSnapshot.Default).Accepted,"inactive refuses");Check(motor.ResetActor(C("respawn",3),2,FlightLifecycle.Respawn,this).Accepted,"respawn");Check(!motor.PublishFlightSettings(C("old",4),0,2,FlightSettingsSnapshot.Default).Accepted,"old generation");Check(motor.PublishFlightSettings(C("new",4),2,2,FlightSettingsSnapshot.Default).Accepted,"fresh generation");
  foreach(SettingsSource profile in new[]{SettingsSource.Quest,SettingsSource.SkinRuntime})
  {
   var adapter=new PersonalSettingsAdapter(Path.Combine(dir,profile+".ashfall-settings.json"),profile,this);adapter.CaptureSettings();var binding=new PlayerSettingsBinding(new PlayerInput());binding.Apply(adapter.Effective);motor=Motor();Check(Array.IndexOf(binding.UnsupportedTuningFields,"glide_steering_gain")>=0,"unbound stays unsupported");
   Check(binding.PublishFlightSettings(motor,C("default"),0,1).Accepted,"real binding default");Check(binding.UnsupportedTuningFields.Length==1&&binding.UnsupportedTuningFields[0]=="sprint_double_press_window","only actual bound field supported");
   var source=new Dictionary<string,string>{{"glide_steering_gain","2.5"}};adapter.SetContext(source,null,null);var immutable=adapter.Effective;var snap=FlightSettingsPort.Capture(immutable);source["glide_steering_gain"]="0.25";Near(snap.GlideSteeringGain,2.5f,"source dictionary immutable");
   foreach(string invalid in new[]{"NaN","Infinity","0.249","2.501"}){bool rejected=false;try{adapter.SetContext(new Dictionary<string,string>{{"glide_steering_gain",invalid}},null,null);}catch(ArgumentException){rejected=true;}Check(rejected,"actual adapter invalid context refusal");Near(adapter.Effective.Number("glide_steering_gain"),2.5f,"invalid context preserves profile");}
   binding.Apply(immutable);Check(binding.UnsupportedTuningFields.Length==2,"new profile needs publication");Check(binding.PublishFlightSettings(motor,C("changed"),0,2).Accepted,"actual settings adapter to owner");Check(motor.Step(C("flight"),I(),new MotionEnvironment{IsXr=true},F(),out output).Accepted,"actual bound consumption");Check(motor.Velocity.X>10,"real context heading");
  }
  Check(!Flight().SettingsCheckpointReady().Accepted,"checkpoint explicit blocker");Console.WriteLine("PASS "+count+" steering observations");
 }
 public static int Main(string[] args){try{Directory.CreateDirectory(args[0]);new SteeringTests().Run(args[0]);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}


