using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
sealed class LiveBox:IHeadBoxSource
{
 public HeadBox Box=new HeadBox {Approved=true,HalfExtents=new Float3(.12f,.08f,.12f),CenterOffset=new Float3(0,.08f,0),Qw=1};
 public bool TryRead(out HeadBox b) {b=Box;return true;}
}
// Independent analytic half-space geometry: solid x>=Wall or y>=Ceiling.
// Query recorder verifies production translation independently of hit computation.
sealed class PlaneScene:IClimbQueryScene
{
 public float Wall=1,Ceiling=10; public bool Available=true,ForcedOverlap; public int Calls,Casts; public ClimbQuery Last,LastRay,HorizontalRay;
 public bool Ray(ClimbQuery q,out bool hit,out ClimbQueryHit h) {LastRay=q;if(q.Motion.Y==0)HorizontalRay=q;return Trace(q,0,0,out hit,out h);}
 public bool Grab(ClimbQuery q,out bool hit,out ClimbQueryHit h) { Last=q;Calls++;hit=Math.Abs(q.Origin.X-Wall)<=q.Radius;h=new ClimbQueryHit {Distance=Math.Abs(q.Origin.X-Wall),Surface=new SurfaceHit {Point=new Float3(Wall,q.Origin.Y,q.Origin.Z),Normal=new Float3(-1,0,0)}};return Available; }
 public bool Overlap(ClimbQuery q,out bool blocked) {Last=q;Calls++;blocked=ForcedOverlap||q.Origin.X+ExtentX(q)>Wall||q.Origin.Y+ExtentY(q)>Ceiling;return Available;}
 static float ExtentX(ClimbQuery q) {var b=q.Shape;return Math.Abs(1-2*(b.Qy*b.Qy+b.Qz*b.Qz))*b.HalfExtents.X+Math.Abs(2*(b.Qx*b.Qy-b.Qz*b.Qw))*b.HalfExtents.Y+Math.Abs(2*(b.Qx*b.Qz+b.Qy*b.Qw))*b.HalfExtents.Z;}
 static float ExtentY(ClimbQuery q) {var b=q.Shape;return Math.Abs(2*(b.Qx*b.Qy+b.Qz*b.Qw))*b.HalfExtents.X+Math.Abs(1-2*(b.Qx*b.Qx+b.Qz*b.Qz))*b.HalfExtents.Y+Math.Abs(2*(b.Qy*b.Qz-b.Qx*b.Qw))*b.HalfExtents.Z;}
 public bool Cast(ClimbQuery q,out bool hit,out ClimbQueryHit h) {Casts++;return Trace(q,ExtentX(q),ExtentY(q),out hit,out h);}
 bool Trace(ClimbQuery q,float ex,float ey,out bool hit,out ClimbQueryHit h)
 {
  Last=q;Calls++;float t=2;Float3 n=new Float3();
  if(q.Motion.X>0) {float v=(Wall-ex-q.Origin.X)/q.Motion.X;if(v>=0&&v<t){t=v;n=new Float3(-1,0,0);}}
  if(q.Motion.Y>0) {float v=(Ceiling-ey-q.Origin.Y)/q.Motion.Y;if(v>=0&&v<t){t=v;n=new Float3(0,-1,0);}}
  hit=t<=1;float len=(float)Math.Sqrt(q.Motion.X*q.Motion.X+q.Motion.Y*q.Motion.Y+q.Motion.Z*q.Motion.Z);
  h=new ClimbQueryHit {Distance=t*len,Surface=new SurfaceHit {Point=new Float3(q.Origin.X+q.Motion.X*t,q.Origin.Y+q.Motion.Y*t,q.Origin.Z+q.Motion.Z*t),Normal=n}};return Available;
 }
}
static class R3Tests
{
 static int count; static void Check(bool b,string s){count++;if(!b)throw new Exception(s);Console.WriteLine("PASS "+s);}
 static bool Near(float a,float b){return Math.Abs(a-b)<.0002f;}
 static int Main(){try{
 Float3[] route;var zero=new Float3();
 foreach(float y in new float[]{.2f,.55f,.75f,1.25f,1.9f,2.2f,2.5f}) {
  float measured=Math.Max(.55f,Math.Min(2.2f,y)); Check(Near(MantlePlanner.MeasuredEyeHeight(zero,new Float3(0,y,0)),measured),"source measured range "+y);
  var w=new Geometry {Ledge=true,Top=Math.Min(y,measured)-.01f};
  foreach(float calibration in new float[]{.55f,.75f,1.75f,2.2f})
   Check(MantlePlanner.TryPlan(w,zero,new Float3(0,y,0),new Float3(99,0,0),calibration,.707f,out route),"live support independent calibration head/standing="+y+"/"+calibration);
 }
 {var w=new Geometry {Ledge=true,Top=2.21f};Check(!MantlePlanner.TryPlan(w,zero,new Float3(0,2.5f,0),zero,2.2f,.707f,out route),"measured upper clamp rejects excessive rise");}
 {var w=new Geometry {Ledge=true,Top=.75f,Ceiling=1.4f};Check(!MantlePlanner.TryPlan(w,zero,new Float3(0,.8f,0),zero,1.75f,.707f,out route),"crouched complete route blocked by low ceiling");}
 var scene=new PlaneScene();var box=new LiveBox();var layers=new CollisionLayers {StructureMask=1<<8,HeadMask=(1<<8)|(1<<9),SupportMask=(1<<8)|(1<<9)};
 var a=new ClimbCollisionAdapter(scene,box,layers);
 Check(a.HeadMotionClear(new Float3(0,1,0),new Float3(.2f,0,0)),"analytic box clear translation");
 Check(Near(scene.Last.Origin.Y,1.08f)&&Near(scene.Last.Shape.HalfExtents.X,.12f)&&scene.Last.Mask==768,"live box offset/extents and central mask preserved");
 Check(!a.HeadMotionClear(new Float3(0,1,0),new Float3(1,0,0)),"full head sweep blocks wall");
 var m=a.SweepHead(new Float3(0,1,0),new Float3(1,.2f,0),.1f,false);
 Check(m.Displacement.X<.88f&&m.Displacement.Y<.2f,"non-slide sweep retains safe partial displacement");
 m=a.SweepHead(new Float3(0,1,0),new Float3(1,.2f,0),.1f,true);
 Check(m.Displacement.X<.88f&&Near(m.Displacement.Y,.2f)&&Near(m.Velocity.Y,2),"climb slide preserves wall tangent and actual velocity");
 scene.Ceiling=1.3f;m=a.SweepHead(new Float3(0,1,0),new Float3(0,.5f,0),.1f,false);
 Check(m.Displacement.Y>0&&m.Displacement.Y<.14f,"goggle upper extent blocks ceiling");
 box.Box.CenterOffset=new Float3(0,.2f,0);Check(!a.HeadMotionClear(new Float3(0,1,0),new Float3(0,.1f,0)),"live center updated between queries");
 box.Box.Qz=(float)Math.Sqrt(.5);box.Box.Qw=box.Box.Qz;box.Box.HalfExtents=new Float3(.4f,.05f,.12f);scene.Ceiling=1.5f;
 Check(!a.HeadMotionClear(new Float3(0,1,0),zero),"oriented head box rotation retained");
 box.Box.Qw=4;int calls=scene.Calls;Check(!a.HeadMotionClear(zero,zero)&&scene.Calls==calls,"invalid quaternion query free");box=new LiveBox();a=new ClimbCollisionAdapter(scene,box,layers);box.Box.Approved=false;calls=scene.Calls;
 Check(!a.HeadMotionClear(zero,zero)&&scene.Calls==calls,"unapproved shape query free");box.Box.Approved=true;
 scene.ForcedOverlap=true;Check(!a.HeadMotionClear(zero,zero)&&Near(a.SweepHead(zero,new Float3(.1f,0,0),.1f,true).Displacement.X,0),"initial penetration fails closed");scene.ForcedOverlap=false;scene.Available=false;
 Check(!a.HeadMotionClear(zero,new Float3(.1f,0,0))&&Near(a.SweepHead(zero,new Float3(.1f,0,0),.1f,true).Displacement.X,0),"unavailable or saturated scene fails closed");scene.Available=true;
 SurfaceHit hit;a.Raycast(zero,new Float3(2,0,0),QueryKind.Support,out hit);Check(scene.LastRay.Mask==768,"support uses explicit separate mask");
 Check(a.TryGrab(new Float3(.9f,0,0),.18f,out hit)&&scene.Last.Mask==256&&Near(scene.Last.Radius,.18f),"grab radius and structure policy translated");
 bool threw=false;try{new ClimbCollisionAdapter(scene,box,new CollisionLayers {StructureMask=1,SupportMask=1});}catch(ArgumentException){threw=true;}Check(threw,"default layer configuration refused");
 var c=new ClimbController(a); var o=ClimbOptions.Default; scene.Wall=1;scene.Ceiling=10;
 var f=new InputFrame {Sequence=1,Tick=1,Tracked=true,HeadPosition=new Float3(0,1,0)};
 Check(c.StepDesktop(f,zero,.1f,o,true,new Float3(1,0,0)).Climbing&&Near(scene.HorizontalRay.Motion.X,1.35f),"desktop E casts source length from live eye with world heading");
 f.Sequence=f.Tick=2;var step=c.StepDesktop(f,zero,.1f,o,true,new Float3(1,0,0));Check(Near(step.RootDisplacement.Y,.22f),"desktop held E uses head-only swept rise");
 calls=scene.Calls;Check(!c.StepDesktop(f,zero,.1f,o,true,new Float3(1,0,0)).Accepted&&scene.Calls==calls,"desktop duplicate frame cannot repeat geometry");
 Check(a.SweepHead(zero,new Float3(.1f,0,0),float.Epsilon,false).Velocity.IsFinite,"subnormal timestep cannot produce infinite velocity");
 Console.WriteLine("RESULT checks="+count+" failures=0 UnityPhysics=UNKNOWN");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
