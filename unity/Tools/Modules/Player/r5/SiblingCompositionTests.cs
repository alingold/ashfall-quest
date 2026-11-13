using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Climbing;
class SiblingCompositionTests
{
 static void C(bool b,string n){if(!b)throw new Exception(n);Console.WriteLine("PASS "+n);}
 sealed class Gate:IAuthorityGate,IFlightCollisionAuthorization {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}public RuleResult ValidateCollision(CommandContext c,FlightMotionReceipt r,FlightCollisionResult s){return RuleResult.Accept();}}
 sealed class Pose:IConvexHeadSource {public bool TryRead(out ConvexHeadPose p){p=new ConvexHeadPose{Qw=1};return true;}}
 sealed class Scene:IConvexClimbScene
 {public long Revision {get{return 1;}}public bool Block;
 public bool Penetrating(ConvexHeadQuery q,out bool p){p=false;return true;}
 public bool Cast(ConvexHeadQuery q,out bool hit,out ClimbQueryHit h){hit=Block;h=new ClimbQueryHit{Distance=0,Surface=new SurfaceHit{Normal=new Float3(0,0,-1)}};return true;}
 public bool Ray(ClimbQuery q,out bool h,out ClimbQueryHit n){h=false;n=new ClimbQueryHit();return true;}public bool Grab(ClimbQuery q,out bool h,out ClimbQueryHit n){h=false;n=new ClimbQueryHit();return true;}}
 static CommandContext Context(string id){return new CommandContext("s","a",1==1?id:id,1,1,true);}
 static void Main()
 {
 var player=new Locomotion(new PlayerTuning());var flight=new FlightMachine("s","a",1,new Gate(),5,1);
 C(flight.Step(Context("step"),new FlightFrame{DeltaSeconds=.02f,InputEnabled=true,Tracked=true,Velocity=new Float3(2,5,0),Forward=new Float3(0,0,1)}).Accepted,"real Flight step");
 var receipt=flight.PendingMotion;var velocity=new Float3(1,0,0);
 C(flight.ApplyCollisionResult(Context("feedback"),receipt,new FlightCollisionResult{Velocity=velocity,CeilingBlocked=true},new Gate()).Accepted,"real Flight ceiling feedback");
 player.SetVelocity(flight.Velocity);C(player.Velocity.X==1&&player.Velocity.Y==0,"Flight to Player handoff mutates actual Player");
 C(!flight.ApplyCollisionResult(Context("feedback"),receipt,new FlightCollisionResult{Velocity=velocity},new Gate()).Accepted,"receipt replay rejects");
 var scene=new Scene();var collision=new ConvexClimbCollision(scene,new Pose(),new CollisionLayers{StructureMask=256,HeadMask=256,SupportMask=256},.7f);
 var solved=collision.SweepHead(new Float3(0,1,0),new Float3(0,0,.1f),.02f,false);player.SetVelocity(solved.Velocity);C(Math.Abs(player.Velocity.Z-5)<.001,"actual source convex solve to Player momentum");
 scene.Block=true;solved=collision.SweepHead(new Float3(0,1,0),new Float3(0,0,.1f),.02f,false);player.SetVelocity(solved.Velocity);C(player.Velocity.Z==0,"actual blocked source convex cancels momentum");
 }
}
