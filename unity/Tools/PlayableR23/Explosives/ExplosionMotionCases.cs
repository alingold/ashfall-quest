using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Explosives;
class ExplosionMotionCases
{
 static int count;static void Check(bool yes,string why){count++;if(!yes)throw new Exception(why);}static void Near(float a,float b,string why){Check(Math.Abs(a-b)<.00002f,why);}
 static float Length(Float3 p){return (float)Math.Sqrt(p.X*p.X+p.Y*p.Y+p.Z*p.Z);}
 static void Main()
 {
  Near(R24ExplosionMotion.CoreScale(0),.32f,"source initial core");Near(R24ExplosionMotion.CoreScale(0,true),.3584f,"rocket source size1.12");Near(R24ExplosionMotion.LightEnergy(0),3,"source energy3");Near(R24ExplosionMotion.LightEnergy(1f/3),0,"source light extinguishes after1/3s");
  Near(R24ExplosionMotion.ZoneRingScale(0,0),.2f,"source ring initial scale");Near(R24ExplosionMotion.ZoneRingScale(1,2),6.7f,"source third ring expansion");Near(R24ExplosionMotion.ZoneFade(1.8f),0,"source zone1.8s fade");Near(R24ExplosionMotion.EmberFade(1.65f),0,"source ember1.65s fade");Near(R24ExplosionMotion.DebrisFade(.55f),1,"source debris full early opacity");Near(R24ExplosionMotion.DebrisFade(2.25f),0,"source debris2.25s fade");
  for(int i=0;i<28;i++){var d=R24ExplosionMotion.ZoneDirection(i);Near(Length(d),1,"source fibonacci zone shard unit direction");Near(d.Y,1-2*(i+.5f)/28,"source zone shard latitude");Near(d.Z,-(float)Math.Sin(i*2.399963f)*(float)Math.Sqrt(1-d.Y*d.Y),"one source Z reflection");}
  for(int i=0;i<20;i++){Near(Length(R24ExplosionMotion.EmberDirection(i)),1,"source ember unit direction");Near(Length(R24ExplosionMotion.EmberPosition(0,i)),0,"source ember begins at accepted blast center");}
  for(int frame=0;frame<=250;frame++)
  {
   float t=frame*.01f;float expansion=1-(float)Math.Exp(-t*9);Near(R24ExplosionMotion.CoreScale(t),.32f+expansion*1.75f,"actual source expansion curve");
   for(int i=0;i<9;i++){Check(R24ExplosionMotion.SmokePosition(t,i).IsFinite&&R24ExplosionMotion.SmokeScale(t,i).Y>=0&&R24ExplosionMotion.DebrisPosition(t,i).IsFinite&&R24ExplosionMotion.DebrisEuler(t,i).IsFinite,"source smoke and debris finite through lifetime");}
   for(int i=0;i<7;i++){var a=R24ExplosionMotion.FirePosition(t,i);var b=R24ExplosionMotion.FirePosition(t,i,true);Near(b.X,a.X*1.12f,"rocket x source gain");Near(b.Y,a.Y*1.12f,"rocket y source gain");Near(b.Z,a.Z*1.12f,"rocket z source gain");}
  }
  Console.WriteLine("PASS "+count+" source blast/zone geometry-motion cases. Shader render/engine not run.");
 }
}
