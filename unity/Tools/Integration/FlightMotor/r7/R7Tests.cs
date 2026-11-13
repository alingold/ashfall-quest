using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
sealed class R7Tests:IAuthorityGate,IFlightCollisionAuthorization
{
 int count;
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public RuleResult ValidateCollision(CommandContext c,FlightMotionReceipt r,FlightCollisionResult v){return RuleResult.Accept();}
 static InputFrame I(long t){return new InputFrame{Tick=t,Sequence=t,Tracked=true,TurnX=1,MoveY=1,Sprint=true};}
 static CommandContext C(long t){return new CommandContext("s","a","c"+t,t,3,true);}
 void Check(bool b,string n){if(!b)throw new Exception(n);count++;Console.WriteLine("PASS "+n);}
 void Near(float a,float b,string n){Check(Math.Abs(a-b)<.001f,n+" actual="+a+" expected="+b);}
 void Run()
 {
  var tuning=new PlayerTuning{Turning=TurnStyle.Snap};var p=new Locomotion(tuning);PreparedPlayerStep a,b;
  Check(p.PrepareStep(I(1),new MotionEnvironment{Grounded=true,IsXr=true},.1f,out a).Accepted,"prepare actual sprint/turn");
  Near(p.SprintStamina,6,"prepare stamina untouched");Near(p.Velocity.Y,0,"prepare velocity untouched");
  Check(a.Motion.YawRadians>0,"detached snap computed");a.Abort();a.Abort();Check(!a.Validate().Accepted,"aborted token");
  Check(p.PrepareStep(I(1),new MotionEnvironment{Grounded=true,IsXr=true},.1f,out a).Accepted,"same frame retry");
  Check(p.PrepareStep(I(2),new MotionEnvironment{Grounded=true,IsXr=true},.1f,out b).Accepted,"new frame reserves");
  Check(!a.Validate().Accepted,"new frame invalidates old");b.Abort();
  Check(p.PrepareStep(I(1),new MotionEnvironment{Grounded=true,IsXr=true},.1f,out a).Accepted,"retry unpublished frame");
  Check(a.Validate().Accepted,"validate current token");a.CommitValidated();Near(p.SprintStamina,5.9f,"commit exact stamina");
  a.Abort();Near(p.SprintStamina,5.9f,"abort published token preserves state");
  Check(!p.PrepareStep(I(1),new MotionEnvironment(),.1f,out b).Accepted,"committed input replay");
  foreach(int mutation in new[]{0,1,2,3,4,5})
  {
   var q=new Locomotion(tuning);Check(q.PrepareStep(I(1),new MotionEnvironment(),.1f,out a).Accepted,"prepare mutation "+mutation);
   if(mutation==0)q.ResetMotion();if(mutation==1)q.CancelMotion();if(mutation==2)q.SetVelocity(new Float3(1,2,3));
   if(mutation==3)q.RestoreStamina(0);if(mutation==4)q.Step(I(2),new MotionEnvironment(),.1f);if(mutation==5)tuning.SnapRadians+=.01f;
   Check(!a.Validate().Accepted,"mutation invalidates "+mutation);bool rejected=false;
   try{a.CommitValidated();}catch(InvalidOperationException){rejected=true;}Check(rejected,"stale commit refuses "+mutation);
  }
  var competing=new Locomotion(new PlayerTuning());Check(competing.PrepareStep(I(1),new MotionEnvironment(),.1f,out a).Accepted,"first same frame token");Check(competing.PrepareStep(I(1),new MotionEnvironment(),.1f,out b).Accepted,"competing same frame token");Check(!a.Validate().Accepted&&b.Validate().Accepted,"only latest reservation valid");b.Abort();
  var g=new Locomotion(new PlayerTuning());g.SetVelocity(new Float3(0,4,0));Near(g.Step(I(1),new MotionEnvironment(),.1f).Velocity.Y,2.38f,"actual Player gravity oracle");
  var proposal=RootMotionSafety.Plan(new Float3(),new Float3(1,0,0),new Float3(.3f,0,0),(float)Math.PI/2,true);
  var partial=new Float3(proposal.PivotCorrection.X*.5f,0,proposal.PivotCorrection.Z*.5f);
  var result=PlayerMotorFeedback.ResolvePhases(proposal,new Float3(.15f,0,0),partial,.1f,false,false);
  Near(result.Velocity.X,1.5f,"partial pivot excluded x");Near(result.Velocity.Z,0,"partial pivot excluded z");Check(!result.Complete&&result.HasPhaseFeedback,"partial phases explicit");
  var ambiguous=PlayerMotorFeedback.Resolve(proposal,result.AppliedDisplacement,.1f,false,false);Check(!ambiguous.Complete&&!ambiguous.HasPhaseFeedback&&ambiguous.Velocity.X==0&&ambiguous.Velocity.Z==0,"ambiguous combined partial fails closed");
  foreach(TurnStyle style in new[]{TurnStyle.Snap,TurnStyle.AutoSnap,TurnStyle.Smooth})
  {
   var motor=new PlayerFlightMotor("s","a",3,this,new PlayerTuning{Turning=style},1);MotionStep m;
   var f=new FlightFrame{InputEnabled=true,Tracked=true,DesktopGlide=true,Forward=new Float3(0,0,1),DeltaSeconds=.1f};
   var initial=I(1);initial.TurnX=0;
   Check(motor.Step(C(1),initial,new MotionEnvironment(),f,out m).Accepted,"first "+style);
   Check(motor.ApplyCollisionResult(new CommandContext("s","a","solver",1,3,true),motor.PendingMotion,new FlightCollisionResult{Velocity=new Float3(0,-10000,0)},this).Accepted,"boundary feedback "+style);
   var old=motor.PendingMotion;f.DesktopGlide=false;
   Check(!motor.Step(C(2),I(2),new MotionEnvironment(),f,out m).Accepted,"derived rejection "+style);
   Check(Object.ReferenceEquals(old,motor.PendingMotion),"pending receipt preserved "+style);
   f.DesktopGlide=true;Check(motor.Step(C(2),I(2),new MotionEnvironment(),f,out m).Accepted,"same frame retry "+style);
   // Held snap remains consumed only by accepted first frame; rejection adds no turn edge.
   if(style==TurnStyle.Snap)Near(m.YawRadians,(float)Math.PI/6,"snap edge survives retry");
   var control=new PlayerFlightMotor("s","a",3,this,new PlayerTuning{Turning=style},1);MotionStep expected;
   Check(control.Step(C(1),initial,new MotionEnvironment(),f,out expected).Accepted,"control first "+style);
   Check(control.ApplyCollisionResult(new CommandContext("s","a","solver",1,3,true),control.PendingMotion,new FlightCollisionResult{Velocity=new Float3(0,-10000,0)},this).Accepted,"control feedback "+style);
   Check(control.Step(C(2),I(2),new MotionEnvironment(),f,out expected).Accepted,"control second "+style);
   Near(m.YawRadians,expected.YawRadians,"rejection adds no yaw consumption "+style);Near(m.Velocity.X,expected.Velocity.X,"rejection adds no velocity effect "+style);
  }
  Console.WriteLine("TOTAL "+count+" PASS");
 }
 static int Main(){try{new R7Tests().Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
