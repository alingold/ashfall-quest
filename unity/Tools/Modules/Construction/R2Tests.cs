using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;

sealed class R2Actors:IConstructionActors {
 public BuilderState State;
 public bool TryRead(string id,out BuilderState state){state=State;return id=="a";}
}
sealed class R2Gate:IAuthorityGate {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
sealed class R2World:IConstructionWorld {
 public bool Ground=true,Top=false,Branch=false,Visible=true,GroundTopContact=false,BlockGroundFloor=false;
 public string Block="";
 public bool HasStaticContact(Panel p,float tolerance){return (Ground&&(Math.Abs(p.Center.Y-Geometry.HalfSize(p).Y)<=tolerance||(GroundTopContact&&Math.Abs(p.Center.Y+Geometry.HalfSize(p).Y)<=tolerance)))||(Top&&p.Center.Y>2*Geometry.Side)||(Branch&&p.Center.X>Geometry.Side);}
 public bool IsBuried(Panel p){return false;}
 public string Clearance(Panel p,string actor){return BlockGroundFloor&&p.Kind=="floor"&&p.Center.Y<Geometry.Side/2?"ground_floor_clearance":Block;}
 public bool HasLineOfSight(string actor,Float3 origin,Float3 target){return Visible;}
}
static class R2Tests {
 static int checks,failures;static float S=Geometry.Side;static R2Actors a;static R2World w;static ConstructionService s;
 static CommandContext C(string id,long t){return new CommandContext("r","a",id,t,1,true);}
 static ConstructionCommand Q(string id,long t,long seq){return new ConstructionCommand(C(id,t),seq);}
 static void Check(bool pass,string name){checks++;if(!pass)failures++;Console.WriteLine((pass?"PASS ":"FAIL ")+name);}
 static void Start(){a=new R2Actors();w=new R2World();s=new ConstructionService("r",1,new R2Gate(),a,w,false);}
 static void Floor(float z){a.State=new BuilderState(new Float3(S/2,1,z),new Float3(0,-1,0),new Float3(S/2,1,z),true,true);}
 static void Wall(int level){a.State=new BuilderState(new Float3(S/2,(level+.5f)*S,-1),new Float3(0,0,1),new Float3(S/2,(level+.5f)*S,-1),true,true);}
 static void Stack(){Start();for(int i=0;i<3;i++){Wall(i);Check(s.Place(C("stack"+i,i)).Accepted,"stack setup "+i);}}
 static float Health(string id){foreach(var p in s.Snapshot())if(p.Id==id)return p.Health;return -1;}
 static void Main(){
  Start();Floor(0);var p=s.Preview("a").Panels[0];Check(p.Slot=="floor:1:0:1:0"&&p.Center.Z<0,"r1 exact zero-Z ray boundary");
  Start();Floor(-S);p=s.Preview("a").Panels[0];Check(p.Slot=="floor:1:0:3:0","r1 exact negative-Z boundary");
  Start();Floor(S);Check(s.Preview("a").Panels[0].Slot=="floor:1:0:-1:0","positive-Z boundary uses source convention");
  foreach(float jitter in new float[]{-.000002f,0,.000002f}){
   Start();a.State=new BuilderState(new Float3(S-1,1-.000001f+jitter,S/2),new Float3(1,-1,0),new Float3(S-1,1-.000001f+jitter,S/2),true,true);
   Check(Geometry.Intersections(a.State.Origin,a.State.Direction)[0].Axis==0,"r1 exact near-corner source candidate priority "+jitter);
   Check(s.Preview("a").Panels[0].Axis==1,"bottom-only fake contact rejects below-ground wall "+jitter);
   s.CancelPreview("a");w.GroundTopContact=true;
   Check(s.Preview("a").Panels[0].Axis==0,"fresh near-corner preview with top-face contact "+jitter);
  }
  Start();w.GroundTopContact=true;string slot=null;foreach(float jitter in new float[]{-.000002f,.000002f,-.000002f}){
   a.State=new BuilderState(new Float3(S-1,1-.000001f+jitter,S/2),new Float3(1,-1,0),new Float3(S-1,1-.000001f+jitter,S/2),true,true);
   p=s.Preview("a").Panels[0];if(slot==null)slot=p.Slot;Check(p.Slot==slot,"approach/retreat hysteresis near tie");
  }
  Stack();w.Ground=false;s.Advance(0,false);s.Advance(.31f,false);Check(s.Snapshot().Length==2,"r1 static root loss at .31 retains upper two");
  s.Advance(.28f,false);Check(s.Snapshot().Length==2,"upper deadline not reset or shortened");s.Advance(.02f,false);Check(s.Snapshot().Length==1,"second depth deadline .60");s.Advance(.30f,false);Check(s.Snapshot().Length==0,"third depth deadline .90");
  Stack();w.Top=true;w.Ground=false;s.Advance(1,false);Check(s.Snapshot().Length==3,"alternate surviving anchor preserves component");
  Stack();w.Ground=false;s.Advance(0,false);s.Advance(.29f,false);w.Ground=true;s.Advance(.02f,false);Check(s.Snapshot().Length==3,"root restoration just before deadline rescues all");
  Stack();w.Ground=false;s.Advance(0,false);s.Advance(.31f,false);w.Ground=true;s.Advance(.30f,false);Check(s.Snapshot().Length==1,"restoring removed root does not resurrect or anchor upper wall");
  foreach(float when in new float[]{.29f,.31f,.59f,.61f,.89f}){
   Stack();w.Ground=false;s.Advance(0,false);s.Advance(when,false);
   // Newly supported adjacent branch at the top rescues remaining live chain.
   w.Branch=true;a.State=new BuilderState(new Float3(1.5f*S,2.5f*S,-1),new Float3(0,0,1),new Float3(1.5f*S,2.5f*S,-1),true,true);
   Check(s.Place(C("branch",4)).Accepted,"branch attachment at "+when);
   int expected=when<.30f?4:when<.60f?3:2;s.Advance(2,false);Check(s.Snapshot().Length==expected,"branch rescue preserves survivors at "+when);
  }
  Stack();w.Ground=false;s.Advance(0,false);s.Advance(.91f,false);w.Branch=true;s.Advance(1,false);Check(s.Snapshot().Length==0,"late branch cannot resurrect expired chain");
  Start();Floor(S/2);Check(s.Place(Q("p1",10,0)).Accepted,"sequenced initial floor");string first=s.Snapshot()[0].Id;
  a.State=new BuilderState(new Float3(1.5f*S,1,S/2),new Float3(0,-1,0),new Float3(1.5f*S,1,S/2),true,true);
  Check(s.Place(Q("p2",10,1)).Accepted,"second distinct build same simulation tick");string second=s.Snapshot()[1].Id;
  Check(s.Damage(Q("hit1",10,2),first,10).Accepted&&s.Damage(Q("hit2",10,3),second,10).Accepted&&Health(first)==130&&Health(second)==130,"two independent hits on different panels same tick");
  Check(s.AwardSupply(Q("pickup",10,4),3).Accepted,"supply shares hits simulation tick");
  a.State=new BuilderState(new Float3(2.5f*S,1,S/2),new Float3(0,-1,0),new Float3(2.5f*S,1,S/2),true,true);
  Check(s.Place(Q("p3",10,5)).Accepted&&s.Charges("a")==999&&s.Snapshot().Length==3,"build after two hits and pickup same tick atomics");
  Check(s.AwardSupply(Q("retry-new-id",10,4),3).Reason=="stale_sequence","sequence identity cannot be relabeled");
  Check(s.AwardSupply(Q("pickup",10,6),3).Reason=="replay","retained commandID cannot be relabeled sequence");
  Check(s.AwardSupply(Q("old-tick",9,6),3).Reason=="stale_tick","simulation time cannot decrease");
  Check(s.AwardSupply(C("mixed",10),3).Reason=="sequence_mode","actor cannot downgrade sequence protection");
  for(int i=6;i<2060;i++)if(!s.AwardSupply(Q("fill"+i,10,i),1).Accepted)throw new Exception("same tick eviction fill "+i);
  int balance=s.Charges("a");Check(s.AwardSupply(Q("pickup",10,4),3).Reason=="stale_sequence"&&s.Charges("a")==balance,"evicted original identity/sequence rejected at same tick");
  Check(s.AwardSupply(Q("pickup",11,4),3).Reason=="stale_sequence","fabricated newer timestamp cannot bypass sequence watermark");
  Check(s.AwardSupply(Q("next",10,2060),1).Accepted,"same-tick stream continues past replay capacity");
  Start();Floor(S/2);s.Place(C("floor",1));first=s.Snapshot()[0].Id;
  Check(s.Damage(C("pellet1",2),first,10).Accepted&&s.Damage(C("pellet2",2),first,10).Accepted&&Health(first)==120,"compatibility port two distinct same-tick hits");
  Check(s.AwardSupply(C("pickup",2),3).Accepted,"compatibility same-tick supply");
  for(int i=0;i<2050;i++)s.AwardSupply(C("fill"+i,2),1);
  balance=s.Charges("a");Check(s.AwardSupply(C("pellet1",2),3).Reason=="replay_window"&&s.Charges("a")==balance,"compatibility eviction fails closed at retired tick");
  Check(s.AwardSupply(C("fresh",3),1).Accepted,"compatibility advances with real next simulation tick");
  Start();Floor(S/2);w.Block="player_clearance";Check(!s.Place(Q("blocked",1,0)).Accepted,"failed placement validated");w.Block="";
  Check(s.Place(Q("retry",1,1)).Accepted,"failed command allows distinct same-tick retry");
  Check(s.Damage(new ConstructionCommand(new CommandContext("r","a","client",1,1,false),2),s.Snapshot()[0].Id,140).Reason=="authority"&&s.Snapshot()[0].Health==140,"sequenced client damage denied atomically");
  w.Visible=false;a.State=new BuilderState(new Float3(1.5f*S,1,S/2),new Float3(0,-1,0),new Float3(1.5f*S,1,S/2),true,true);balance=s.Charges("a");Check(s.Place(Q("los",1,2)).Reason=="occluded"&&s.Charges("a")==balance&&s.Snapshot().Length==1,"sequenced LOS failure has no cost/spawn");
  Start();Floor(S/2);
  for(int i=0;i<998;i++){
   if(!s.Place(Q("spend"+i,1,2*i)).Accepted)throw new Exception("spend setup");
   if(!s.Damage(Q("remove"+i,1,2*i+1),s.Snapshot()[0].Id,140).Accepted)throw new Exception("spend cleanup");
  }
  w.BlockGroundFloor=true;a.State=new BuilderState(new Float3(S/2,S+1,S/2),new Float3(0,-1,0),new Float3(S/2,S+1,S/2),true,true);
  Check(s.Place(Q("poor-pair",1,1996)).Reason=="capacity"&&s.Charges("a")==1&&s.Snapshot().Length==0,"insufficient pair balance rejects both panels atomically");
  Start();for(int i=0;i<255;i++){
   a.State=new BuilderState(new Float3((i+.5f)*S,1,S/2),new Float3(0,-1,0),new Float3((i+.5f)*S,1,S/2),true,true);
   if(!s.Place(Q("cap"+i,1,i)).Accepted)throw new Exception("capacity setup");
  }
  w.BlockGroundFloor=true;a.State=new BuilderState(new Float3(300.5f*S,S+1,S/2),new Float3(0,-1,0),new Float3(300.5f*S,S+1,S/2),true,true);
  balance=s.Charges("a");Check(s.Place(Q("pair-at-255",1,255)).Reason=="capacity"&&s.Snapshot().Length==255&&s.Charges("a")==balance,"pair at 255 capacity cannot partially spawn or charge");
  Console.WriteLine("TOTAL "+checks+" checks; "+failures+" failures");Environment.ExitCode=failures==0?0:1;
 }
}
