using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Climbing;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.ClimbingMotor;
using Ashfall.Integration.RuntimeLoop;
class MotionR5 : IAuthorityGate,IRuntimeMotionLifetime,IClimbCollision
{
 object id=new object();Action handlers;int subscriptions;bool deny,grab;int checks;
 public object Identity{get{return id;}}
 public event Action Invalidated{add{handlers+=value;subscriptions++;}remove{handlers-=value;subscriptions--;}}
 public RuleResult Validate(CommandContext c){return deny?RuleResult.Reject("authority"):RuleResult.Accept();}
 public bool TryGrab(Float3 p,float r,out SurfaceHit h){h=new SurfaceHit{Point=p,Normal=new Float3(0,0,-1),AuthoredClimbable=true};return grab;}
 public bool Raycast(Float3 a,Float3 b,QueryKind k,out SurfaceHit h){h=new SurfaceHit();return false;}
 public bool HeadMotionClear(Float3 h,Float3 m){return true;}
 public MotionResult SweepHead(Float3 h,Float3 m,float s,bool slide){return new MotionResult(m,new Float3());}
 void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 MotorFrame Frame(){return new MotorFrame{Context=new CommandContext("s","a","draft",1,1,true),LifecycleGeneration=1,Ready=true,
 Input=new InputFrame{Tick=1,Sequence=1,Tracked=true,HeadPosition=new Float3(0,1.7f,0)},Climb=ClimbOptions.Default,
 Flight=new FlightFrame{DeltaSeconds=.1f,Forward=new Float3(0,0,1)},DesktopForward=new Float3(0,0,1)};}
 void Run(){var motor=new PlayerFlightMotor("s","a",1,this,new PlayerTuning(),1);var climb=new ClimbController(this);
 var owner=new RuntimeMotionPreparation("s","a",1,1,this,this,motor,climb);RuntimeMotionDraft d,other;var f=Frame();
 Check(subscriptions==1,"one subscription");Check(owner.Prepare(f,out d).Accepted,"real draft prepares");
 Check(d.MotionOwner==RuntimeMotionOwner.PlayerFlight,"explicit player flight owner");Check(d.Validate().Accepted,"all real reservations validate");
 Check(motor.Velocity.Equals(new Float3())&&!climb.LeftAnchored&&!climb.RightAnchored&&!climb.Mantling,"prepare no live mechanics");
 Check(!climb.CheckpointReady().Accepted,"actual climb reservation retained");Check(!owner.Prepare(f,out other).Accepted,"one outstanding draft");
 Check(!d.Publish().Accepted&&motor.Velocity.Equals(new Float3()),"no certificate no publication");
 var expected=new PlayerFlightMotor("s","a",1,this,new PlayerTuning(),1);PreparedPlayerFlightStep baseline;
 Check(expected.PrepareStep(f.Context,f.Input,f.Environment,new FlightFrame{DeltaSeconds=.1f,Forward=new Float3(0,0,1),InputEnabled=true},out baseline).Accepted,"independent actual flight baseline");
 Check(d.Motion.Displacement.Equals(baseline.Motion.Displacement)&&d.Motion.Velocity.Equals(baseline.Motion.Velocity),"one gravity result matches actual motor");baseline.Abort();
 d.Dispose();d.Dispose();Check(climb.CheckpointReady().Accepted&&!d.Validate().Accepted,"abort releases actual reservation");
 Check(owner.Prepare(f,out d).Accepted,"same input retry unspent");d.Dispose();
 grab=true;f.Environment.IsXr=true;f.Input.GripLeft=true;f.Input.LeftHandPosition=new Float3(0,1.7f,.2f);f.Climb.LeftContact=f.Input.LeftHandPosition;
 Check(owner.Prepare(f,out d).Accepted&&d.Climbing.Climbing&&d.MotionOwner==RuntimeMotionOwner.Climb,"actual climb acquisition draft sole owner");
 Check(!climb.LeftAnchored&&d.Motion.YawRadians==0,"draft anchor remains unpublished and suppresses yaw");d.Dispose();grab=false;f=Frame();
 f.Environment.ExternalMotionOwner=true;Check(!owner.Prepare(f,out d).Accepted&&climb.CheckpointReady().Accepted,"foreign root owner rejects before reservation");f=Frame();
 f.LifecycleGeneration=2;Check(!owner.Prepare(f,out d).Accepted,"respawn generation cannot follow live accessor");f=Frame();
 deny=true;Check(!owner.Prepare(f,out d).Accepted&&climb.CheckpointReady().Accepted,"authority refusal pure");deny=false;
 f.Flight.DeltaSeconds=float.NaN;Check(!owner.Prepare(f,out d).Accepted,"nonfinite frame refused");f=Frame();
 f.Input.Paused=true;Check(owner.Prepare(f,out d).Accepted&&d.MotionOwner==RuntimeMotionOwner.Climb&&d.Motion.Displacement.Equals(new Float3()),"pause suspended draft no second gravity");d.Dispose();f=Frame();
 f.Ready=false;Check(owner.Prepare(f,out d).Accepted&&d.Motion.Displacement.Equals(new Float3()),"focus loss suspended draft");d.Dispose();f=Frame();
 Check(owner.Prepare(f,out d).Accepted,"prepare before invalidation");handlers();Check(!d.Validate().Accepted&&climb.CheckpointReady().Accepted,"event immediately aborts real reservations");
 Check(!owner.Prepare(f,out other).Accepted,"event retirement terminal");owner.Dispose();owner.Dispose();Check(subscriptions==0,"unsubscribe exactly once");
 owner=new RuntimeMotionPreparation("s","a",1,1,this,this,motor,climb);Check(owner.Prepare(f,out d).Accepted,"replacement owner uses same unspent input");
 id=new object();Check(!d.Validate().Accepted&&climb.CheckpointReady().Accepted,"same name reconnect identity retires");owner.Dispose();
 Console.WriteLine("MotionR5 "+checks+" PASS; certified root/native/lifecycle adoption UNKNOWN");}
 static int Main(){try{new MotionR5().Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}

