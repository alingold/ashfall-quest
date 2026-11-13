using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Player;
using Ashfall.Integration.FlightMotor;
sealed class R5Tests : IAuthorityGate,IFlightWorld,IFlightLifecycleAuthorization,IFlightCollisionAuthorization
{
 int pass; bool gate=true, allow=true;
 public RuleResult Validate(CommandContext c){return gate?RuleResult.Accept():RuleResult.Reject("host");}
 public RuleResult ValidateTransition(CommandContext c,long g,FlightLifecycle r){return RuleResult.Accept();}
 public RuleResult ValidateCollision(CommandContext c,FlightMotionReceipt p,FlightCollisionResult r){return allow?RuleResult.Accept():RuleResult.Reject("solver mismatch");}
 public bool ClearPadContact(string s,Float3 a,Float3 b){return true;}
 public bool ClearCapsuleExit(string s,Float3 a){return true;}
 static CommandContext C(string id,long t){return new CommandContext("s","a",id,t,3,true);}
 static InputFrame I(long t){return new InputFrame{Tick=t,Sequence=t,Tracked=true};}
 static FlightFrame F(){return new FlightFrame{DeltaSeconds=.1f,Tracked=true,InputEnabled=true,Forward=new Float3(0,0,1)};}
 PlayerFlightMotor M(){return new PlayerFlightMotor("s","a",3,this,new PlayerTuning(),1);}
 void Check(bool b,string n){if(!b)throw new Exception(n);pass++;Console.WriteLine("PASS "+n);}
 void Near(float a,float b,string n){Check(Math.Abs(a-b)<.001,n+" actual="+a+" expected="+b);}
 void Run()
 {
  var pad=new LaunchPad("p",new Float3(),new Float3(1,1,0),1,45);var m=M();MotionStep motion;
  Check(m.LaunchPad(C("pad",1),pad,this,new Float3(),new Float3(),false,0,true,false,false,false).Accepted,"tilted setup");
  var env=new MotionEnvironment{CeilingBlocked=true};
  Check(m.Step(C("ceiling",2),I(2),env,F(),out motion).Accepted,"ceiling step");
  Near(motion.Velocity.Y,0,"ceiling clips before displacement");Near(motion.Displacement.Y,0,"no positive ceiling displacement");
  var receipt=m.PendingMotion;Check(receipt.Tick==2&&receipt.Generation==0&&receipt.ActorId=="a"&&receipt.SessionId=="s"&&receipt.AuthorityEpoch==3,"receipt scope");
  var hit=new FlightCollisionResult{Velocity=new Float3(0,0,7),CeilingBlocked=true};
  Check(!m.ApplyCollisionResult(C("hit",2),receipt,hit,null).Accepted,"missing solver authorization");
  allow=false;Check(!m.ApplyCollisionResult(C("hit",2),receipt,hit,this).Accepted,"denied solver output");allow=true;
  gate=false;Check(!m.ApplyCollisionResult(C("hit",2),receipt,hit,this).Accepted,"denied host");gate=true;
  Check(!m.ApplyCollisionResult(new CommandContext("other","a","hit",2,3,true),receipt,hit,this).Accepted,"forged session");
  Check(!m.ApplyCollisionResult(new CommandContext("s","b","hit",2,3,true),receipt,hit,this).Accepted,"forged actor");
  Check(!m.ApplyCollisionResult(new CommandContext("s","a","hit",2,4,true),receipt,hit,this).Accepted,"forged epoch");
  Check(!m.ApplyCollisionResult(new CommandContext("s","a","hit",2,3,false),receipt,hit,this).Accepted,"client feedback");
  Check(!m.ApplyCollisionResult(C("hit",3),receipt,hit,this).Accepted,"wrong produced tick");
  foreach(float bad in new[]{Single.NaN,Single.PositiveInfinity,10000.01f,-10000.01f}){
   var invalid=hit;invalid.Velocity=new Float3(bad,0,0);Check(!m.ApplyCollisionResult(C("hit",2),receipt,invalid,this).Accepted,"invalid momentum "+bad);
  }
  var up=hit;up.Velocity=new Float3(0,1,0);Check(!m.ApplyCollisionResult(C("hit",2),receipt,up,this).Accepted,"ceiling upward output rejected");
  Check(Object.ReferenceEquals(receipt,m.PendingMotion),"denials preserve exact proposal");
  Check(m.ApplyCollisionResult(C("hit",2),receipt,hit,this).Accepted,"authorized root resolution");
  Near(m.Velocity.X,0,"wall removed x");Near(m.Velocity.Z,7,"slide retained z");Check(m.PendingMotion==null,"once receipt consumed");
  Check(!m.ApplyCollisionResult(C("again",2),receipt,hit,this).Accepted,"same receipt another command denied");
  Check(!m.Step(C("hit",3),I(3),new MotionEnvironment(),F(),out motion).Accepted,"feedback ID cannot rebind Step");
  Check(m.Step(C("next",3),I(3),new MotionEnvironment(),F(),out motion).Accepted,"release after contact");
  Near(motion.Velocity.Y,-1.62f,"one gravity after root correction");Near(motion.Velocity.Z,6.92f,"actual root slide feeds next frame");
  var old=m.PendingMotion;
  Check(m.Step(C("new",4),I(4),new MotionEnvironment(),F(),out motion).Accepted,"new proposal");
  Check(!m.ApplyCollisionResult(C("stale",3),old,hit,this).Accepted,"stale receipt denied");
  var other=M();Check(other.Step(C("other",4),I(4),new MotionEnvironment(),F(),out motion).Accepted,"other instance proposal");
  Check(!other.ApplyCollisionResult(C("cross",4),m.PendingMotion,hit,this).Accepted,"cross instance denied");
  var floor=new FlightCollisionResult{Velocity=new Float3(3,0,2),Grounded=true};
  Check(m.ApplyCollisionResult(C("floor",4),m.PendingMotion,floor,this).Accepted&&m.Mode==FlightMode.Ground,"floor resolves launch and fall");
  env=new MotionEnvironment{Grounded=true};var input=I(5);input.MoveX=1;
  Check(m.Step(C("walk",5),input,env,F(),out motion).Accepted,"landing walk");
  var reference=new Locomotion(new PlayerTuning());reference.SetVelocity(floor.Velocity);var expected=reference.Step(input,env,.1f);
  Near(motion.Velocity.X,expected.Velocity.X,"actual Player landing x");Near(motion.Velocity.Y,expected.Velocity.Y,"actual Player landing y");
  Near(m.PendingMotion.ProposedVelocity.Y,motion.Velocity.Y,"ground receipt exact composed velocity");
  var ext=new FlightCollisionResult{Velocity=new Float3(2,3,4),ExternalMotionOwner=true};
  Check(m.ApplyCollisionResult(C("climb",5),m.PendingMotion,ext,this).Accepted&&m.Mode==FlightMode.Suspended,"climb handoff without respawn");
  env=new MotionEnvironment{ExternalMotionOwner=true};input=I(6);input.TurnX=1;
  var obs=F();obs.Climbing=true;
  Check(m.Step(C("climbing",6),input,env,obs,out motion).Accepted&&!motion.OwnsMotion,"external owner has sole translation");
  expected=reference.Step(input,env,.1f);Near(motion.YawRadians,expected.YawRadians,"external owner uses actual Player turn");
  Near(m.Velocity.Y,3,"climb no second gravity");
  Check(m.ApplyCollisionResult(C("climb-resolved",6),m.PendingMotion,ext,this).Accepted,"external root velocity recorded");
  Check(m.Step(C("return",7),I(7),new MotionEnvironment(),F(),out motion).Accepted,"climb to flight");Near(m.Velocity.Y,1.38f,"climb velocity next frame one gravity");
  for(int t=8;t<=12;t++){
   Check(m.ApplyCollisionResult(C("block"+t,t-1),m.PendingMotion,new FlightCollisionResult{Velocity=new Float3()},this).Accepted,"full block "+t);
   Check(m.Step(C("frame"+t,t),I(t),new MotionEnvironment(),F(),out motion).Accepted,"repeated collision frame "+t);Near(m.Velocity.Y,-1.62f,"repeated block gravity once "+t);
  }
  old=m.PendingMotion;Check(m.ResetActor(C("life",13),1,FlightLifecycle.Respawn,this).Accepted,"real lifecycle reset");
  Check(!m.ApplyCollisionResult(C("prelife",12),old,hit,this).Accepted,"prior generation receipt rejected");
  Check(m.Step(C("postlife",14),I(14),new MotionEnvironment(),F(),out motion).Accepted&&m.PendingMotion.Generation==1,"generation bound new receipt");
  old=m.PendingMotion;Check(m.LaunchPad(C("launch2",15),pad,this,new Float3(),new Float3(),false,3,true,false,false,false).Accepted,"pad after lifecycle");
  Check(!m.ApplyCollisionResult(C("prelaunch",14),old,hit,this).Accepted,"launch invalidates old step");
  var ceilingOwner=M();env=new MotionEnvironment{ExternalMotionOwner=true,CeilingBlocked=true};
  Check(ceilingOwner.LaunchPad(C("owner-pad",1),pad,this,new Float3(),new Float3(),false,0,true,false,false,false).Accepted,"external ceiling setup");
  Check(ceilingOwner.Step(C("owner-step",2),I(2),env,F(),out motion).Accepted&&!motion.OwnsMotion&&ceilingOwner.Mode==FlightMode.Suspended,"ceiling never steals external translation");
  var machine=new FlightMachine("s","a",3,this,6,1);Check(machine.Step(C("direct",1),F()).Accepted,"direct machine Step");
  Check(machine.ApplyCollisionResult(C("direct-hit",1),machine.PendingMotion,floor,this).Accepted&&machine.Mode==FlightMode.Ground,"direct machine feedback");
  Console.WriteLine("TOTAL "+pass+" PASS");
 }
 static int Main(){try{new R5Tests().Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
