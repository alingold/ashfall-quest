using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
sealed class R6Tests : IAuthorityGate,IFlightCollisionAuthorization,IFlightLifecycleAuthorization
{
 int pass;bool allow=true;
 public RuleResult Validate(CommandContext c){return allow?RuleResult.Accept():RuleResult.Reject("host");}
 public RuleResult ValidateCollision(CommandContext c,FlightMotionReceipt p,FlightCollisionResult r){return RuleResult.Accept();}
 public RuleResult ValidateTransition(CommandContext c,long g,FlightLifecycle r){return RuleResult.Accept();}
 static CommandContext C(string id,long t){return new CommandContext("s","a",id,t,3,true);}
 static InputFrame I(long t,float turn){return new InputFrame{Tick=t,Sequence=t,Tracked=true,TurnX=turn};}
 static FlightFrame F(){return new FlightFrame{InputEnabled=true,Tracked=true,DeltaSeconds=.1f,Forward=new Float3(0,0,1),DesktopGlide=true};}
 void Check(bool b,string n){if(!b)throw new Exception(n);pass++;Console.WriteLine("PASS "+n);}
 void Near(float x,float y,string n){Check(Math.Abs(x-y)<.001,n+" actual="+x+" expected="+y);}
 void Run()
 {
  foreach(TurnStyle style in new[]{TurnStyle.Smooth,TurnStyle.Snap,TurnStyle.AutoSnap})foreach(bool xr in new[]{false,true})foreach(float sign in new[]{-1f,1f})
  {
   var tuning=new PlayerTuning{Turning=style};var motor=new PlayerFlightMotor("s","a",3,this,tuning,1);var reference=new Locomotion(tuning);
   for(int t=1;t<=8;t++)
   {
    var input=I(t,t==5?0:sign);var env=new MotionEnvironment{IsXr=xr,DesktopYawRadians=xr?0:.07f};var f=F();f.HandsReady=true;f.LeftHand=new Float3(-1,0,0);f.RightHand=new Float3(1,0,0);
    MotionStep motion;Check(motor.Step(C("turn"+t,t),input,env,f,out motion).Accepted,"accepted turn "+style+xr+sign+t);
    env.ExternalMotionOwner=true;var expected=reference.Step(input,env,.1f);Near(motion.YawRadians,expected.YawRadians,"actual Player yaw");
    float speed=(float)Math.Sqrt(motion.Velocity.X*motion.Velocity.X+motion.Velocity.Z*motion.Velocity.Z);
    Near(motion.Velocity.X,speed*(float)Math.Sin(expected.YawRadians),"same tick signed heading x");Near(motion.Velocity.Z,speed*(float)Math.Cos(expected.YawRadians),"same tick signed heading z");
    Near(motion.Displacement.X,motion.Velocity.X*.1f,"single plan displacement");Near(motor.PendingMotion.ProposedVelocity.X,motion.Velocity.X,"receipt final heading");
   }
  }
  var machine=new FlightMachine("s","a",3,this,6,1);PreparedFlightStep p;Float3 v;
  allow=false;Check(!machine.PrepareStep(C("prepare",1),F(),out p).Accepted,"denied prepare");allow=true;
  Check(machine.PrepareStep(C("prepare",1),F(),out p).Accepted,"prepare same command after denial");Check(machine.PendingMotion==null&&machine.Mode==FlightMode.Ground,"prepare no publication");
  Check(!machine.PublishPreparedStep(new CommandContext("x","a","prepare",1,3,true),p,0,new Float3(),out v).Accepted,"forged session");
  Check(!machine.PublishPreparedStep(C("prepare",2),p,0,new Float3(),out v).Accepted,"expiry exact tick");
  Check(!machine.PublishPreparedStep(C("prepare",1),p,Single.NaN,new Float3(),out v).Accepted,"invalid yaw");
  var other=new FlightMachine("s","a",3,this,6,1);Check(!other.PublishPreparedStep(C("prepare",1),p,0,new Float3(),out v).Accepted,"foreign machine");
  Check(machine.PublishPreparedStep(C("prepare",1),p,.5f,new Float3(),out v).Accepted,"authorized publication");Check(v.X>0,"yaw resolved");
  Check(!machine.PublishPreparedStep(C("prepare",1),p,0,new Float3(),out v).Accepted,"one use");p.Abort();p.Abort();Check(machine.PendingMotion!=null,"abort after commit preserves publication");
  Check(machine.PrepareStep(C("stale",2),F(),out p).Accepted,"prepare before launch");Check(machine.Launch(C("launch",2),new Float3(0,1,0),20,true,false,false).Accepted,"intervening real launch");
  Check(!machine.PublishPreparedStep(C("stale",2),p,0,new Float3(),out v).Accepted,"source revision invalidates plan");Near(machine.Velocity.Y,20,"new launch survives");
  Check(machine.PrepareStep(C("life-plan",3),F(),out p).Accepted,"prepare before lifecycle");Check(machine.ResetActor(C("life",3),1,FlightLifecycle.Respawn,this).Accepted,"real generation transition");Check(!machine.PublishPreparedStep(C("life-plan",3),p,0,new Float3(),out v).Accepted,"generation invalidates plan");
  Check(machine.PrepareStep(C("abort",4),F(),out p).Accepted,"prepare for abort");p.Abort();p.Abort();Check(!machine.PublishPreparedStep(C("abort",4),p,0,new Float3(),out v).Accepted,"abort idempotent");
  var motor2=new PlayerFlightMotor("s","a",3,this,new PlayerTuning(),1);MotionStep m;
  var bad=F();bad.Forward=new Float3(Single.NaN,0,1);Check(!motor2.Step(C("retry",1),I(1,1),new MotionEnvironment(),bad,out m).Accepted,"invalid frame rejects before Player");
  Check(motor2.Step(C("retry",1),I(1,1),new MotionEnvironment(),F(),out m).Accepted,"rejected step consumed no Player input");
  var hit=FlightPhysicalFeedback.Resolve(new Float3(0,.2f,.3f),.1f,false,true,false);Near(hit.Velocity.X,0,"wall removes normal momentum");Near(hit.Velocity.Y,0,"ceiling clips ascent");Near(hit.Velocity.Z,3,"partial slide physical momentum");
  Check(motor2.ApplyCollisionResult(C("solver",1),motor2.PendingMotion,hit,this).Accepted,"actual resolved feedback publication");
  Check(motor2.Step(C("released",2),I(2,0),new MotionEnvironment(),new FlightFrame{InputEnabled=true,DeltaSeconds=.1f,Forward=new Float3(0,0,1)},out m).Accepted,"released collision");Near(m.Velocity.Y,-1.62f,"fixed one gravity oracle");Near(m.Velocity.Z,2.92f,"slide persists next frame");
  // Pivot never enters this API: supplied physical delta is .3m even if root also moved
  // .4m for turning. A partial pivot similarly cannot become physical momentum.
  Near(FlightPhysicalFeedback.Resolve(new Float3(.3f,0,0),.1f,false,false,false).Velocity.X,3,"full pivot excluded by explicit physical delta");
  Near(FlightPhysicalFeedback.Resolve(new Float3(.1f,0,0),.1f,false,false,false).Velocity.X,1,"partial pivot excluded by explicit physical delta");
  Console.WriteLine("TOTAL "+pass+" PASS");
 }
 static int Main(){try{new R6Tests().Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
