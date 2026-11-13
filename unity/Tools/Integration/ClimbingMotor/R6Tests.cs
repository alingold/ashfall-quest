using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.ClimbingMotor;
public static class R6Tests
{
 static int count;static void Check(bool b,string s){if(!b)throw new Exception(s);count++;}
 sealed class Auth:IAuthorityGate,IClimbFrameAuthorization,IFlightWorld,ICapsuleRouteValidation,IFlightLifecycleAuthorization
 {
  public RuleResult Validate(CommandContext c){return RuleResult.Accept();}public RuleResult Validate(CommandContext c,long s,long g){return RuleResult.Accept();}
  public RuleResult ValidateTransition(CommandContext c,long g,FlightLifecycle l){return RuleResult.Accept();}
  public bool ClearPadContact(string id,Float3 a,Float3 b){return true;}
  public bool ClearCapsuleExit(string id,Float3 p){return true;}
  public bool ValidateRoute(string id,CapsulePlan p){return true;}
 }
 sealed class Pose:IConvexHeadSource{public bool TryRead(out ConvexHeadPose p){p=new ConvexHeadPose{Qw=1};return true;}}
 sealed class Scene:IConvexClimbScene
 {
  public long Revision{get{return 1;}}
  public bool Penetrating(ConvexHeadQuery q,out bool b){b=false;return true;}
  public bool Cast(ConvexHeadQuery q,out bool b,out ClimbQueryHit h){b=false;h=new ClimbQueryHit();return true;}
  public bool Ray(ClimbQuery q,out bool b,out ClimbQueryHit h){b=q.Motion.Y < -3;h=new ClimbQueryHit{Distance=1.765f,Surface=new SurfaceHit{Point=new Float3(q.Origin.X,0,q.Origin.Z),Normal=new Float3(0,1,0)}};return true;}
  public bool Grab(ClimbQuery q,out bool b,out ClimbQueryHit h){b=true;h=new ClimbQueryHit{Surface=new SurfaceHit{Point=q.Origin,Normal=new Float3(0,1,0),AuthoredClimbable=true}};return true;}
 }
 sealed class Root:IExclusivePlayerMotor
 {public int Calls;public bool Ground,Ceiling,Partial;public PlayerMotorResult Apply(RootMotionProposal p,float dt){Calls++;var r=PlayerMotorFeedback.Resolve(p,p.Translation,dt,Ground,Ceiling);if(Partial)r.Complete=false;return r;}}
 sealed class Rig
 {
  public ClimbController C;public PlayerFlightMotor F;public ClimbingRootMotor M;public Root R=new Root();
  public Rig(){var a=new Auth();var col=new ConvexClimbCollision(new Scene(),new Pose(),new CollisionLayers{HeadMask=256,SupportMask=256,StructureMask=256},.70710678f);C=new ClimbController(col);F=new PlayerFlightMotor("s","a",2,a,new PlayerTuning(),1);M=new ClimbingRootMotor("s","a",2,C,F,R,a,new SourceSupport(col,.70710678f));}
  public bool Go(MotorFrame f,out ClimbStep s){PlayerMotorResult r;return M.Step(f,out s,out r).Accepted;}
 }
 static MotorFrame Frame(int t){return new MotorFrame{Context=new CommandContext("s","a","r6-"+t,t,2,true),Input=new InputFrame{Tick=t,Sequence=t,Tracked=true,HeadPosition=new Float3(0,1.75f,0),LeftHandPosition=new Float3(0,1,0)},Environment=new MotionEnvironment{Grounded=true,IsXr=true},Flight=new FlightFrame{DeltaSeconds=.02f,Forward=new Float3(0,0,1)},Climb=ClimbOptions.Default,Ready=true,LifecycleGeneration=1};}
 static void Pull(Rig r,float y){ClimbStep s;Check(r.Go(Frame(1),out s),"init");var f=Frame(2);f.Input.GripLeft=true;f.Climb.LeftContact=new Float3(0,1,0);Check(r.Go(f,out s)&&r.C.LeftAnchored,"grab");f=Frame(3);f.Input.GripLeft=true;f.Input.LeftHandPosition=new Float3(0,y,0);Check(r.Go(f,out s),"pull");}
 public static int Main(){try{
  ClimbStep s;
  // All meaningful completed-move contacts through actual Climbing + Flight + Player.
  for(int mode=0;mode<5;mode++){
   var r=new Rig();Pull(r,mode==3?1.2f:mode==4?1:.8f);r.R.Ground=mode!=1;r.R.Ceiling=mode==2;
   Check(r.Go(Frame(4),out s)&&s.Launch==(mode!=4),"release launch or zero impulse");int calls=r.R.Calls;
   Check(mode==2?r.F.Velocity.Y==0:Math.Abs(r.F.Velocity.Y-s.Velocity.Y)<1e-5,"release feedback policy");
   if(mode<2){float before=r.F.Velocity.Y;r.R.Ground=false;var next=Frame(5);next.Environment.Grounded=false;Check(r.Go(next,out s),"airborne next handoff");Console.WriteLine("OBS next velocity "+before+" -> "+r.F.Velocity.Y);Check(r.F.Velocity.Y<before&&r.F.Velocity.Y>before-1,"one gravity next frame");}
   Check(r.R.Calls==(mode<2?calls+1:calls),"one physical apply per frame");
  }
  var partial=new Rig();Pull(partial,.8f);partial.R.Partial=true;Check(partial.Go(Frame(4),out s)&&partial.F.Velocity.Y==0,"partial result refuses fling");
  for(int state=0;state<3;state++){
   var r=new Rig();if(state>0)Pull(r,.8f);int t=state==0?1:4;var f=Frame(t);f.Environment.ExternalMotionOwner=true;f.Input.GripLeft=true;f.Input.LeftHandPosition=new Float3(0,.5f,0);
   var receipt=r.F.PendingMotion;var v=r.F.Velocity;int calls=r.R.Calls;bool anchor=r.C.LeftAnchored;
   Check(!r.Go(f,out s)&&r.R.Calls==calls&&r.C.LeftAnchored==anchor,"external owner rejects with no local/root mutation");
   Check(Object.ReferenceEquals(receipt,r.F.PendingMotion)&&r.F.Velocity.Y==v.Y,"no Flight publication on rejection");
  }
  var auth=new Auth();var padRig=new Rig();Pull(padRig,.8f);
  Check(padRig.F.LaunchPad(new CommandContext("s","a","pad",4,2,true),new LaunchPad("p",new Float3(),new Float3(1,1,0),1,45),auth,new Float3(),new Float3(),false,0,true,false,false,false).Accepted,"real external pad launch mutation");
  float padY=padRig.F.Velocity.Y;var occupied=Frame(5);occupied.Environment.ExternalMotionOwner=true;int padCalls=padRig.R.Calls;
  Check(!padRig.Go(occupied,out s)&&padRig.F.Velocity.Y==padY&&padRig.R.Calls==padCalls,"climbing cannot overwrite real pad momentum");
  var fleet=new CapsuleFleet("s",2,auth);CapsuleUpdate ride;
  Check(fleet.Begin(new CommandContext("s","a","ride",6,2,true),new CapsuleEligibility{MapId="alpha",Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,Team=0,TeamCount=2},new CapsulePlan("pod",0,0,new Float3(),new Float3(0,10,0),new Float3(10,10,0),new Float3(10,0,0),new Float3(12,0,0),new Float3(0,0,-1),3),0,auth,out ride).Accepted,"real capsule ride begins");
  Check(fleet.Advance(new CommandContext("s","a","ride-step",7,2,true),1,true,true,false,auth,out ride).Accepted&&ride.State==CapsuleState.Flying,"real capsule route advances");
  occupied=Frame(8);occupied.Environment.ExternalMotionOwner=true;Check(!padRig.Go(occupied,out s)&&padRig.R.Calls==padCalls,"external capsule ownership refuses root publication");
  // Actual sibling life/connection cleanup retires motion receipts; the external
  // authorization fixture must still map canonical life AND connection to one generation.
  foreach(FlightLifecycle reason in new[]{FlightLifecycle.Disconnect,FlightLifecycle.Respawn}){
   var life=new Rig();Pull(life,.8f);Check(life.F.ResetActor(new CommandContext("s","a","life",4,2,true),1,reason,auth).Accepted,"actual sibling lifecycle mutation");
   int calls=life.R.Calls;Check(!life.Go(Frame(3),out s)&&life.R.Calls==calls,"retired frame cannot write root");
  }
  var traj=new SourceHeadTrajectory(new Float3(1,2,3),new ConvexHeadPose{Qw=1},new Float3(2,.1f,-1),.7f);
  Check(traj.VertexCount==168,"all source vertices retained");
  for(int i=0;i<168;i++)for(int j=0;j<40;j++){
   double a=j*.05,b=(j+1)*.05;var x=traj.Vertex(i,a);var y=traj.Vertex(i,b);double dx=x.X-y.X,dy=x.Y-y.Y,dz=x.Z-y.Z;
   Check(Math.Sqrt(dx*dx+dy*dy+dz*dz)<=traj.TravelBound(a,b)+1e-6,"continuous travel bound dominates vertex interval");
  }
  var p0=traj.Vertex(0,0);var p1=traj.Vertex(0,1);Check(Math.Abs(p1.X-p0.X-2)<1e-6&&Math.Abs(p1.Z-p0.Z+1)<1e-6,"move completes before yaw");
  var p2=traj.Vertex(0,2);Check(Math.Abs(p2.Y-p1.Y)<1e-6&&Math.Abs(p2.X-p1.X)>.001,"yaw moves actual faceted source vertex");
  Console.WriteLine(count+" PASS 0 FAIL (numerical samples are not native clearance)");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
