using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Player;
using Ashfall.Integration.FlightMotor;
public sealed class PreparedPadTests:IAuthorityGate,IFlightWorld,IFlightLifecycleAuthorization
{
 int checks,queries;bool deny,blocked; int calls;
 public RuleResult Validate(CommandContext c){calls++;return deny?RuleResult.Reject("retired life/connection/source lease"):RuleResult.Accept();}
 public bool ClearPadContact(string id,Float3 face,Float3 contact){queries++;return !blocked;}
 public bool ClearCapsuleExit(string a,Float3 p){return !blocked;}
 public RuleResult ValidateTransition(CommandContext c,long g,FlightLifecycle r){return RuleResult.Accept();}
 static CommandContext C(string id,long tick=1){return new CommandContext("s","a",id,tick,3,true);}
 static FlightFrame F(){return new FlightFrame{DeltaSeconds=.1f,InputEnabled=true,Tracked=true,Forward=new Float3(0,0,1)};}
 static InputFrame I(long tick=1){return new InputFrame{Tick=tick,Sequence=tick,Tracked=true};}
 static LaunchPad Pad(){return new LaunchPad("pad",new Float3(),new Float3(0,1,0),2,20);}
 FlightMachine Flight(){return new FlightMachine("s","a",3,this,6.5f,1);}
 PlayerFlightMotor Motor(){return new PlayerFlightMotor("s","a",3,this,new PlayerTuning(),1);}
 void Check(bool b,string s){if(!b)throw new Exception(s);checks++;Console.WriteLine("PASS "+s);}
 void Near(float x,float y,string s){Check(Math.Abs(x-y)<.001f,s+" "+x+" expected "+y);}
 bool Prep(PadContacts p,FlightMachine f,out PreparedPadLaunch plan,double now=10,string id="launch"){return p.PrepareLaunch(C(id),f,Pad(),this,new Float3(),new Float3(),false,now,true,false,false,false,out plan).Accepted;}
 bool Compose(PlayerFlightMotor m,out PreparedPadFlight p,double now=10){return m.PrepareStepAfterPad(C("launch"),C("step"),I(),new MotionEnvironment(),F(),Pad(),this,new Float3(),new Float3(),false,now,true,false,false,false,out p).Accepted;}
 public void Run()
 {
  var contact=new PadContacts();var f=Flight();PreparedPadLaunch p,q;
  Check(Prep(contact,f,out p),"prepare launch");Near(f.Velocity.Y,0,"no early launch");Check(!Prep(contact,f,out q),"contact pressure");
  Check(!Prep(new PadContacts(),f,out q),"shared Flight pressure");p.Abort();p.Abort();Check(Prep(contact,f,out p),"same frame retry");
  Check(p.Validate(10).Accepted,"validate launch");int before=calls,world=queries;p.CommitValidated();Check(calls==before&&queries==world,"commit no gate/world");Near(f.Velocity.Y,20,"launch once");
  p.Abort();Near(f.Velocity.Y,20,"abort after commit preserves");Check(!Prep(contact,f,out q,10.5,"other"),"real cooldown");Check(!Prep(new PadContacts(),f,out q,11.2),"real launch replay");
  foreach(int bad in new[]{0,1,2,3,4})
  {
   contact=new PadContacts();f=Flight();Check(Prep(contact,f,out p),"refusal prepared "+bad);
   if(bad==0)deny=true;if(bad==1)blocked=true;
   if(bad==2)Check(f.ResetActor(C("death",2),1,FlightLifecycle.Death,this).Accepted,"actual life mutation");
   if(bad==3)Check(f.Launch(C("other"),new Float3(1,0,0),10,true,false,false).Accepted,"actual Flight mutation");
   Check(!p.Validate(bad==4?10.101:10).Accepted,"final refusal "+bad);p.Abort();deny=blocked=false;
   if(bad!=2&&bad!=3){Check(Prep(contact,f,out p),"retry refusal "+bad);p.Abort();}
  }
  contact=new PadContacts();f=Flight();Check(Prep(contact,f,out p),"local mutation prepared");Check(contact.TryLaunch(C("legacy"),f,Pad(),this,new Float3(),new Float3(),false,10,true,false,false,false).Accepted,"legacy unchanged while reserved");Check(!p.Validate(10).Accepted,"legacy invalidates token");p.Abort();
  contact=new PadContacts();f=Flight();for(int j=0;j<4096;j++)Check(f.Launch(C("full"+j),new Float3(0,1,0),1,true,false,false).Accepted,"fill replay "+j);
  Check(!Prep(contact,f,out p),"replay pressure no mutation");Near(f.Velocity.Y,1,"pressure retained velocity");
  var m=Motor();PreparedPadFlight composed;Check(Compose(m,out composed),"composed pad and Flight");Near(m.Velocity.Y,0,"compose before velocity");Near(composed.Motion.Velocity.Y,20,"clearance no doubled gravity");
  Check(composed.Validate(10).Accepted,"validate composition");before=calls;world=queries;composed.CommitValidated();Check(calls==before&&queries==world,"composed callback free commit");Near(m.Velocity.Y,20,"composed publication");
  Check(!Compose(m,out composed),"composed frame replay");MotionStep motion;
  Check(m.Step(C("next",2),I(2),new MotionEnvironment(),F(),out motion).Accepted,"next actual Flight frame");Near(m.Velocity.Y,20,"clearance progresses once");
  for(int k=0;k<4;k++)
  {
   m=Motor();Check(Compose(m,out composed),"composed refusal prepare "+k);
   if(k==0)blocked=true;if(k==1)deny=true;
   if(k==2)Check(m.ResetActor(C("death",2),1,FlightLifecycle.Death,this).Accepted,"composed lifecycle mutation");
   Check(!composed.Validate(k==3?10.2:10).Accepted,"composed refuses "+k);Near(m.Velocity.Y,0,"no launch leak "+k);composed.Abort();composed.Abort();deny=blocked=false;
   if(k!=2){Check(Compose(m,out composed),"composed same frame retry "+k);Check(composed.Validate(10).Accepted,"retry validates "+k);composed.CommitValidated();Near(m.Velocity.Y,20,"retry launch once "+k);}
  }
  m=Motor();Check(Compose(m,out composed),"collision preparation");Check(composed.PrepareCollision(new FlightCollisionResult{Velocity=new Float3(3,0,0),CeilingBlocked=true}).Accepted,"fold actual collision feedback");Check(composed.Validate(10).Accepted,"collision final validation");composed.CommitValidated();Near(m.Velocity.Y,0,"ceiling clears ascent");Near(m.Velocity.X,3,"collision handoff");Check(m.PendingMotion==null,"collision receipt consumed");
  contact=new PadContacts();f=Flight();Check(Prep(contact,f,out p),"unvalidated token");bool threw=false;try{p.CommitValidated();}catch(InvalidOperationException){threw=true;}Check(threw,"commit requires final validation");Check(!p.Validate(Double.NaN).Accepted&&!p.Validate(9.9).Accepted,"invalid/backwards validation clock");Check(p.Validate(10.1).Accepted,"inclusive expiry boundary");p.Abort();
  contact=new PadContacts();f=Flight();Check(contact.PrepareLaunch(C("sweep"),f,Pad(),this,new Float3(0,1,0),new Float3(0,-1,0),true,10,true,false,false,false,out p).Accepted,"actual swept contact");p.Abort();
  foreach(bool paused in new[]{false,true})
  {
   m=Motor();var input=I();input.Paused=paused;input.Tracked=paused;
   Check(m.PrepareStepAfterPad(C("launch"),C("step"),input,new MotionEnvironment(),F(),Pad(),this,new Float3(),new Float3(),false,10,true,false,false,false,out composed).Accepted,"suspended prepared "+paused);
   Check(!composed.Motion.OwnsMotion,"suspension no displacement "+paused);Check(composed.Validate(10).Accepted,"suspension validated "+paused);composed.CommitValidated();Near(m.Velocity.Y,0,"suspension zero velocity "+paused);
  }
  m=Motor();Check(Compose(m,out composed),"validated stale commit setup");Check(composed.Validate(10).Accepted,"stale initial validation");Check(m.LaunchPad(C("competing"),new LaunchPad("other",new Float3(),new Float3(1,0,0),2,5),this,new Float3(),new Float3(),false,10,true,false,false,false).Accepted,"competing real launch");threw=false;try{composed.CommitValidated();}catch(InvalidOperationException){threw=true;}Check(threw,"stale commit before Player writes");Near(m.Velocity.X,5,"competing velocity preserved");composed.Abort();
  foreach(float ny in new[]{1f,0f,-1f})
  {
   var testPad=new LaunchPad("oriented",new Float3(),new Float3(1,ny,0),1,20);var actual=Motor();var legacy=Motor();var input=I();input.TurnX=.7f;
   var env=new MotionEnvironment{IsXr=true};
   Check(actual.PrepareStepAfterPad(C("launch"),C("step"),input,env,F(),testPad,this,new Float3(),new Float3(),false,10,true,false,false,false,out composed).Accepted,"oriented preparation "+ny);
   Check(legacy.LaunchPad(C("launch"),testPad,this,new Float3(),new Float3(),false,10,true,false,false,false).Accepted,"legacy oriented launch "+ny);
   Check(legacy.Step(C("step"),input,env,F(),out motion).Accepted,"legacy oriented step "+ny);Check(composed.Validate(10).Accepted,"oriented validate "+ny);composed.CommitValidated();
   Near(actual.Velocity.X,legacy.Velocity.X,"actual legacy launch x "+ny);Near(actual.Velocity.Y,legacy.Velocity.Y,"actual legacy launch y "+ny);Near(composed.Motion.YawRadians,motion.YawRadians,"actual legacy yaw "+ny);
   for(int t=2;t<=5;t++){Check(actual.Step(C("later"+t,t),I(t),env,F(),out motion).Accepted,"later clearance step "+ny+"/"+t);Check(legacy.Step(C("later"+t,t),I(t),env,F(),out motion).Accepted,"later legacy step "+ny+"/"+t);Near(actual.Velocity.Y,legacy.Velocity.Y,"gravity integrated once "+ny+"/"+t);}
  }
  Console.WriteLine("TOTAL "+checks);
 }
 public static int Main(){try{new PreparedPadTests().Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
