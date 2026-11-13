using System;using Ashfall.Core;using Ashfall.Integration.Playable.Match;
class ExploreCases
{
 static int n;static void N(bool v,string m){n++;if(!v)throw new Exception(m);}
 sealed class World:IR24ArchiveRecoveryWorld{public bool Floor(Float3 f,float depth,out Float3 p){p=new Float3(f.X,0,f.Z);return f.Y>=0&&f.Y-depth<=0;}public bool Outside(Float3 p){return false;}public bool Room(Float3 p,bool initial){return true;}}
 static void Main()
 {
  var context=new R24ArchiveWorldContext();var frame=new object();var geometry=new object();var geometry2=new object();var geometry3=new object();N(context.Update(true,"archive:actual",geometry,frame,false,1),"first actual Explore context");long first=context.Generation;
  for(int i=0;i<100;i++)N(!context.Update(true,"archive:actual",geometry,frame,false,i)&&context.Generation==first,"Explore ignores unrelated match epochs, no perframe rebuild");
  N(context.Update(true,"archive:actual",geometry,frame,true,7)&&context.Generation==first+1,"actual match context switch");N(!context.Update(true,"archive:actual",geometry,frame,true,7),"same match no rebuild");long old=context.Generation;
  N(context.Update(true,"archive:actual",geometry,frame,true,8)&&context.Generation!=old,"new real round invalidates old generation");N(context.Update(true,"archive:actual",geometry,frame,false,9),"return Explore is new stage context");
  N(context.Update(true,"archive:actual",geometry2,frame,false,9),"same map reloaded geometry invalidates");N(context.Update(true,"archive:actual",geometry2,new object(),false,9),"new exact source-frame record invalidates");
  N(context.Update(true,"archive:other",geometry2,frame,false,9),"actual map switch invalidates");N(context.Update(true,"archive:other",geometry2,frame,false,9,true),"map frame transform change invalidates");
  N(context.Update(false,null,null,null,false,0)&&!context.Active,"load/lobby cancels active context");old=context.Generation;for(int i=0;i<10;i++)N(!context.Update(false,null,null,null,false,0)&&context.Generation==old,"inactive updates stable");
  N(context.Update(true,"archive:actual",geometry3,frame,false,10)&&context.Active,"new active world after cancellation");
  var info=new R24ArchiveRecoveryInfo("archive:actual","s","m","c","a",new Float3(-100,-5,-100),new Float3(100,100,100),new Float3(-100,0,-100),new Float3(100,0,100),new Float3(),new[]{new Float3(0,.2f,0)},new Float3[0],new[]{new Float3(-30,2,0),new Float3(30,2,0),new Float3(0,1000,0)},new R24ArchiveBox[0],new R24ArchiveBox[0],new R24ArchiveHazard[0]);
  var plan=new R24ArchiveSpawnPlan(info,new World());N(!plan.ContainsWarmup(new Float3(-30,.12f,0)),"unvalidated sites grant no warmup exemption");var routine=plan.Build();int bound=0;while(routine.MoveNext())N(++bound<1000,"bounded source grid coroutine");
  N(plan.Ready&&plan.WarmupSites.Length==2,"all actual floor/room-validated warmups retained; unreachable hint rejected");N(plan.SourceWarmup.X==-30&&Math.Abs(plan.SourceWarmup.Y-.12f)<.0001f,"original first warmup output unchanged");
  N(plan.ContainsWarmup(new Float3(-30,.12f,0))&&plan.ContainsWarmup(new Float3(30,.12f,0)),"both validated source sites, not single map fallback circle");
  N(plan.ContainsWarmup(new Float3(38,.12f,0))&&!plan.ContainsWarmup(new Float3(38.001f,.12f,0)),"exact source8m inclusive radius");
  N(plan.ContainsWarmup(new Float3(30,4.12f,0))&&!plan.ContainsWarmup(new Float3(30,4.121f,0)),"exact source4m inclusive vertical band");
  N(!plan.ContainsWarmup(new Float3(0,.12f,0)),"space between actual sites not broadened into platform/map region");N(!plan.ContainsWarmup(new Float3(float.NaN,0,0)),"nonfinite no exemption");
  var copy=plan.WarmupSites;copy[0]=new Float3(999,0,999);N(plan.ContainsWarmup(new Float3(-30,.12f,0)),"caller cannot mutate validated source site list");
  Console.WriteLine(n+" shared world-context/cancellation/stage and literal all-site4m8m warmup cases PASS");
 }
}
