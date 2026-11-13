using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Integration.ConstructionGeometry;

sealed class Occupants:IConstructionOccupants {
 public ProtectedBox[] Players=new ProtectedBox[0];public Float3[] Objectives=new Float3[0];public bool Available=true;
 public bool TrySnapshot(out ProtectedBox[] p,out Float3[] o){p=Players;o=Objectives;return Available;}
}
sealed class Actors:IConstructionActors {public BuilderState State;public bool TryRead(string id,out BuilderState s){s=State;return true;}}
sealed class Gate:IAuthorityGate {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
static class GeometryTests {
 static int checks,failed;static float S=Geometry.Side;static string H=new string('a',64);static int serial;
 static void Check(bool b,string s){checks++;if(!b)failed++;Console.WriteLine((b?"PASS ":"FAIL ")+s);}
 static void Throws(Action a,string s){bool caught=false;try{a();}catch(ArgumentException){caught=true;}Check(caught,s);}
 static SourceTriangle Tri(Float3 a,Float3 b,Float3 c,SurfaceRole role){return new SourceTriangle("surface/"+(serial++),"fixture",H,role,role==SurfaceRole.Decoration?11:role==SurfaceRole.Loot?9:8,a,b,c);}
 static List<SourceTriangle> Deck(float x0,float x1,float z0,float z1,float y,SurfaceRole role){return new List<SourceTriangle>{Tri(new Float3(x0,y,z0),new Float3(x0,y,z1),new Float3(x1,y,z0),role),Tri(new Float3(x1,y,z0),new Float3(x0,y,z1),new Float3(x1,y,z1),role)};}
 static StructuralWorld World(List<SourceTriangle> ts,Occupants o,bool archive=false,FloorBurialPolicy policy=FloorBurialPolicy.RemoteAnySamples150){return new StructuralWorld(new CollisionAllocation(8,9,10,11),ts,o,archive,policy);}
 static Panel Floor(float y){return new Panel("floor",new Float3(S/2,y,S/2),1,false,1);}
 static ConstructionService Service(StructuralWorld w,Actors a){return new ConstructionService("s",1,new Gate(),a,w,false);}
 static void Pose(Actors a,float x,float y,float z,Float3 direction){a.State=new BuilderState(new Float3(x,y,z),direction,new Float3(x,y,z),true,true);}
 static ConstructionCommand Q(int i){return new ConstructionCommand(new CommandContext("s","a","event"+i,42,1,true),i);}
 static void Main(){
  var o=new Occupants();var ground=Deck(-20,20,-20,20,-.08f,SurfaceRole.Structure);var w=World(ground,o,false,FloorBurialPolicy.LocalAllSamples025);var a=new Actors();var s=Service(w,a);
  foreach(float magnitude in new float[]{.0316227f,.0316227766f,.0316229f,1f}){
   Pose(a,S/2,1,S/2,new Float3(0,-magnitude,0));bool expected=magnitude*magnitude>=.001f;
   Check((Geometry.Intersections(a.State.Origin,a.State.Direction).Count>0)==expected,"squared threshold geometry "+magnitude);
   Check(s.Preview("a").Accepted==expected,"squared threshold actual service "+magnitude);
  }
  Check(Geometry.Intersections(new Float3(),new Float3(Single.MaxValue,0,0)).Count==0,"overflow direction rejected");
  Check(Geometry.Intersections(new Float3(),new Float3(Single.NaN,0,0)).Count==0,"NaN direction rejected");
  var exact=new Float3(.01f,-.030000001192092896f,0);float squared=exact.X*exact.X+exact.Y*exact.Y;
  Pose(a,S/2,1,S/2,exact);Check(squared==.001f&&s.Preview("a").Accepted,"exact representable squared threshold accepted by service");
  var floor=Floor(0);
  Check(!World(Deck(-20,20,-20,20,-.2f,SurfaceRole.Structure),o).HasStaticContact(floor,.045f),"finite contact gap excludes separated deck");
  ground=Deck(-20,20,-20,20,-.08f,SurfaceRole.Structure);w=World(ground,o,false,FloorBurialPolicy.LocalAllSamples025);
  Check(w.HasStaticContact(floor,.045f)&&!w.HasStaticContact(floor,.039f),"contact tolerance boundary");
  Check(!w.HasStaticContact(new Panel("floor",new Float3(100,0,100),1,false,1),.045f),"finite deck has no infinite plane support");
  var diagonal=new List<SourceTriangle>{Tri(new Float3(0,0,0),new Float3(0,0,2),new Float3(2,0,0),SurfaceRole.Structure)};
  Check(!World(diagonal,o).HasStaticContact(new Panel("wall",new Float3(3.5f,0,3.5f),0,false,1),0),"triangle SAT rejects bounds-only false contact");
  var roof=Deck(-20,20,-20,20,0,SurfaceRole.ConstructionTerrain);var wall=new Panel("wall",new Float3(S/2,-S/2,0),2,false,1);
  Check(World(roof,o,true).HasStaticContact(wall,.045f),"roof top-side contact remains valid");
  Check(!World(roof,o,true).IsBuried(wall)&&World(roof,o,false).IsBuried(wall),"archive construction terrain excluded from wall burial only");
  var terrain=Deck(-20,20,-20,20,1,SurfaceRole.TerrainSurface);
  Check(World(terrain,o,true).IsBuried(wall),"explicit terrain buries archive wall");
  Check(!World(Deck(0,S/2,-20,20,1,SurfaceRole.TerrainSurface),o).IsBuried(wall),"one exposed wall corner preserves eligibility");
  var shallow=Deck(-20,20,-20,20,-.10f,SurfaceRole.Structure);
  Check(World(shallow,o,false,FloorBurialPolicy.RemoteAnySamples150).IsBuried(floor)&&!World(shallow,o,false,FloorBurialPolicy.LocalAllSamples025).IsBuried(floor),"explicit local versus remote floor threshold");
  var partial=Deck(S*.4f,S*.6f,S*.4f,S*.6f,0,SurfaceRole.Structure);
  Check(World(partial,o).IsBuried(floor)&&!World(partial,o,false,FloorBurialPolicy.LocalAllSamples025).IsBuried(floor),"remote any versus local all nine samples");
  var seam=Deck(-S*.001f,S*1.001f,-S*.001f,S*1.001f,0,SurfaceRole.Structure);
  Check(World(seam,o,false,FloorBurialPolicy.LocalAllSamples025).IsBuried(floor),"seam inclusive finite floor sample boundary");
  foreach(var role in new SurfaceRole[]{SurfaceRole.Decoration,SurfaceRole.Loot,SurfaceRole.Construction})Check(!World(Deck(-20,20,-20,20,0,role),o).HasStaticContact(floor,.045f),"nonstatic role not permanent anchor "+role);
  o.Players=new ProtectedBox[]{new ProtectedBox("other",new Float3(S/2,.1f,S/2),new Float3(.3f,.8f,.3f))};
  Check(w.Clearance(floor,"a")=="player_clearance","all players checked including other actor");
  o.Players=new ProtectedBox[]{new ProtectedBox("a",new Float3(S/2,.1f,S/2),new Float3(.3f,.8f,.3f))};
  Check(w.Clearance(floor,"a")=="player_clearance","requester included in panel collision");
  o.Players=new ProtectedBox[0];o.Objectives=new Float3[]{new Float3(S/2,.44f,S/2)};
  Check(w.Clearance(floor,"a")=="objective_clearance","objective expanded panel boundary");
  o.Objectives=new Float3[]{new Float3(S/2,.441f,S/2)};Check(w.Clearance(floor,"a")=="","objective outside safety boundary");o.Objectives=new Float3[0];
  o.Available=false;Check(w.Clearance(floor,"a")=="clearance_unavailable","missing occupancy fails closed");o.Available=true;
  var blocker=new List<SourceTriangle>{Tri(new Float3(-20,.5f,-20),new Float3(20,.5f,-20),new Float3(0,.5f,20),SurfaceRole.Structure)};
  blocker.AddRange(ground);var blocked=World(blocker,o);Pose(a,S/2,1,S/2,new Float3(0,-1,0));s=Service(blocked,a);
  Check(!blocked.HasLineOfSight("a",a.State.Origin,floor.Center),"structural LOS rejects crossed wall");
  Check(s.Place(Q(0)).Reason=="occluded"&&s.Snapshot().Length==0&&s.Charges("a")==999,"actual service no through-wall mutation");
  Check(w.HasLineOfSight("a",new Float3(100,1,100),new Float3(100,-1,100)),"finite LOS outside deck");
  var vertical=new List<SourceTriangle>{Tri(new Float3(S-.5f,-20,-20),new Float3(S-.5f,20,-20),new Float3(S-.5f,0,20),SurfaceRole.Structure)};
  vertical.AddRange(ground);var vw=World(vertical,o,false,FloorBurialPolicy.LocalAllSamples025);
  Pose(a,S-1,S/2,S/2,new Float3(1,0,0));var vs=Service(vw,a);
  Check(vs.Place(Q(0)).Reason=="occluded"&&vs.Snapshot().Length==0,"vertical structural wall blocks actual service placement");
  Check(!vw.HasLineOfSight("a",new Float3(S-.5f,0,0),new Float3(S-.5f,1,0)),"coplanar segment within wall blocked");
  Check(vw.HasLineOfSight("a",new Float3(S-.5f,40,40),new Float3(S-.5f,41,40)),"coplanar segment outside finite wall clear");
  Check(!vw.HasLineOfSight("a",new Float3(S-1,1,0),new Float3(S-.5f,1,0)),"structural endpoint receives no owner-wide exemption");
  Pose(a,S/2,1,S/2,new Float3(0,-1,0));
  s=Service(w,a);Check(s.Place(Q(0)).Accepted,"real finite contact placement");var id=s.Snapshot()[0].Id;
  Check(s.Damage(Q(1),id,10).Accepted&&s.AwardSupply(Q(2),3).Accepted&&s.Snapshot()[0].Health==130&&s.Charges("a")==1001,"multiple same tick events through geometry service");
  // Replace complete immutable worlds at host boundary to model authoritative map contact loss/rescue.
  var switcher=new SwitchWorld(w);s=new ConstructionService("s",1,new Gate(),a,switcher,false);Check(s.Place(Q(0)).Accepted,"rescue setup");
  switcher.Current=World(new List<SourceTriangle>(),o);s.Advance(0,false);s.Advance(.29f,false);Check(s.Snapshot().Length==1,"unsupported pending deadline");switcher.Current=w;s.Advance(.02f,false);s.Advance(1,false);Check(s.Snapshot().Length==1,"finite structural anchor rescue before deadline");
  Throws(delegate{new CollisionAllocation(0,9,10,11);},"central layer zero rejected");
  Throws(delegate{World(new List<SourceTriangle>{Tri(new Float3(),new Float3(1,0,0),new Float3(0,0,1),SurfaceRole.Unknown)},o);},"unclassified source blocked");
  var bad=new SourceTriangle("bad","owner",H,SurfaceRole.Structure,0,new Float3(),new Float3(1,0,0),new Float3(0,0,1));Throws(delegate{World(new List<SourceTriangle>{bad},o);},"wrong source layer blocked");
  var sample=SampleData.Triangles();var real=World(sample,o,true);var t=sample[0];var mid=new Float3((t.A.X+t.B.X+t.C.X)/3,(t.A.Y+t.B.Y+t.C.Y)/3,(t.A.Z+t.B.Z+t.C.Z)/3);
  Check(!real.HasLineOfSight("a",new Float3(mid.X,mid.Y+1,mid.Z),new Float3(mid.X,mid.Y-1,mid.Z)),"real Paintball source triangle occludes across centroid");
  Check(real.HasLineOfSight("a",new Float3(mid.X+100,mid.Y+1,mid.Z),new Float3(mid.X+100,mid.Y-1,mid.Z)),"real finite Paintball surface does not extend infinitely");
  Pose(a,mid.X-1,mid.Y,mid.Z-1,new Float3(1,0,0));var actual=Service(real,a);var realDecision=actual.Place(Q(0));
  Check(realDecision.Accepted&&actual.Snapshot().Length==1&&realDecision.Panels[0].Slot=="wall:-212:31:213:1","actual service known finite Paintball source placement and slot");
  var original=real.HasStaticContact(realDecision.Panels[0],.045f);sample.Clear();Check(original&&real.HasStaticContact(realDecision.Panels[0],.045f),"caller collection mutation cannot replace frozen surfaces");
  Console.WriteLine("TOTAL "+checks+" checks; "+failed+" failures");Environment.ExitCode=failed==0?0:1;
 }
}
sealed class SwitchWorld:IConstructionWorld {
 public IConstructionWorld Current;public SwitchWorld(IConstructionWorld w){Current=w;}
 public bool HasStaticContact(Panel p,float t){return Current.HasStaticContact(p,t);}public bool IsBuried(Panel p){return Current.IsBuried(p);}public string Clearance(Panel p,string a){return Current.Clearance(p,a);}public bool HasLineOfSight(string a,Float3 o,Float3 t){return Current.HasLineOfSight(a,o,t);}
}
