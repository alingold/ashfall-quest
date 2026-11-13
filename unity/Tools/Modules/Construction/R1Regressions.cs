// r1 independent fixtures retained; three old-policy assertions updated for authorized r2 same-tick contract.
using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
sealed class ReviewActors:IConstructionActors {
 public BuilderState State;
 public bool TryRead(string id,out BuilderState state){state=State;return id=="a";}
}
sealed class ReviewGate:IAuthorityGate {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
sealed class ReviewWorld:IConstructionWorld {
 public bool Ground=true,Blocked=false;
 public bool HasStaticContact(Panel p,float tolerance){return Ground&&Math.Abs(p.Center.Y-Geometry.HalfSize(p).Y)<=tolerance;}
 public bool IsBuried(Panel p){return false;}
 public string Clearance(Panel p,string actor){return Blocked?"player_clearance":"";}
 public bool HasLineOfSight(string actor,Float3 origin,Float3 target){return true;}
}
static class Adversarial {
 static int failures,checks; static float S=Geometry.Side; static long tick;
 static ReviewActors a;static ReviewWorld w;static ConstructionService s;
 static void Check(bool ok,string name,string detail){checks++;Console.WriteLine((ok?"PASS ":"FAIL ")+name+" | "+detail);if(!ok)failures++;}
 static CommandContext C(string id,long t){return new CommandContext("r","a",id,t,1,true);}
 static void Start(){a=new ReviewActors();w=new ReviewWorld();s=new ConstructionService("r",1,new ReviewGate(),a,w,false);tick=0;}
 static void Wall(int level){a.State=new BuilderState(new Float3(S/2,(level+.5f)*S,-1),new Float3(0,0,1),new Float3(S/2,(level+.5f)*S,-1),true,true);}
 static void Floor(float z){a.State=new BuilderState(new Float3(S/2,1,z),new Float3(0,-1,0),new Float3(S/2,1,z),true,true);}
 static void Main(){
  Start();Floor(0);var p=s.Preview("a").Panels[0];
  Check(p.Slot=="floor:1:0:1:0","reflected zero-Z boundary", "expected source slot floor:1:0:1:0, actual "+p.Slot+" center Z="+p.Center.Z);
  Start();Floor(-S);p=s.Preview("a").Panels[0];
  Check(p.Slot=="floor:1:0:3:0","reflected negative-Z boundary","expected source slot floor:1:0:3:0, actual "+p.Slot);
  Start();a.State=new BuilderState(new Float3(S-1,1-.000001f,S/2),new Float3(1,-1,0),new Float3(S-1,1-.000001f,S/2),true,true);p=s.Preview("a").Panels[0];
  Check(Geometry.Intersections(a.State.Origin,a.State.Direction)[0].Axis==0&&p.Axis==1,"near-corner source priority and unsupported-wall fallback","source X candidate first; bottom-only fake contact forces floor preview; actual "+p.Kind+" axis="+p.Axis);
  Start();for(int i=0;i<3;i++){Wall(i);var d=s.Place(C("stack"+i,++tick));Check(d.Accepted,"stack setup "+i,d.Reason);}
  w.Ground=false;s.Advance(0,false);s.Advance(.31f,false);
  Check(s.Snapshot().Length==2,"static-anchor loss keeps per-edge cascade","after detection +.31 expected 2 upper walls; actual "+s.Snapshot().Length);
  Start();for(int i=0;i<3;i++){Wall(i);s.Place(C("stack"+i,++tick));}
  var root=s.Snapshot()[0].Id;s.Damage(C("destroy",++tick),root,180);s.Advance(.31f,false);
  Check(s.Snapshot().Length==1,"damage severing per-edge cascade","actual "+s.Snapshot().Length);
  Start();Floor(S/2);s.Place(C("place",1));var id=s.Snapshot()[0].Id;
  var d1=s.Damage(C("pellet1",2),id,10);var d2=s.Damage(C("pellet2",2),id,10);
  Check(d1.Accepted&&d2.Accepted&&s.Snapshot()[0].Health==120,"two same-tick distinct commands are accepted","second same-tick damage: "+d2.Reason+"; distinct IDs share actual simulation time");
  Check(s.AwardSupply(C("pickup",2),3).Accepted,"damage and supply accept distinct IDs at same tick","distinct same-tick events accepted");
  Start();Floor(S/2);w.Blocked=true;s.Place(C("blocked",1));w.Blocked=false;
  Check(s.Place(C("retry",1)).Accepted,"failed placement consumes identity but allows distinct same-tick retry","identity consumed; tick remains available to fresh commands");
  Start();Floor(S/2);s.Place(C("place",1));id=s.Snapshot()[0].Id;
  Check(s.Damage(new CommandContext("r","a","client",2,1,false),id,140).Reason=="authority"&&s.Snapshot()[0].Health==140,"client damage rejected","health preserved");
  Check(s.AwardSupply(new CommandContext("r","a","client",3,1,false),3).Reason=="authority"&&s.Charges("a")==998,"client supply rejected","balance preserved");
  var snap=s.Snapshot();snap[0]=null;Check(s.Snapshot()[0]!=null,"snapshot array isolation","stored state intact");
  Start();Floor(S/2);Check(s.AwardSupply(C("max",1),Int32.MaxValue-999).Accepted&&s.Charges("a")==Int32.MaxValue,"max supply legal","uncapped until integer boundary");
  Check(!s.AwardSupply(C("overflow",2),1).Accepted&&s.Charges("a")==Int32.MaxValue,"overflow leaves state intact","balance unchanged");
  Console.WriteLine("TOTAL "+checks+" checks; "+failures+" failures");Environment.ExitCode=failures==0?0:1;
 }
}


