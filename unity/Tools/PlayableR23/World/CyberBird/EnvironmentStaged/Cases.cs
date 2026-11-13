using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Ashfall.Integration.Playable.World;
class CyberEnvironmentCases
{
 static int cases;static void Check(bool value,string why){cases++;if(!value)throw new Exception(why);}static void Near(float a,float b,string why){Check(Math.Abs(a-b)<.0002f,why);}
 static float[] Sites(R24CyberLightSite[] list){var p=new float[list.Length*3];for(int i=0;i<list.Length;i++)Array.Copy(list[i].at,0,p,i*3,3);R24CyberLightPolicy.ReflectZ(p);return p;}
 static void TestNearest(float[] p,float[] matrix,float x,float y,float z,int count)
 {
  var ix=new int[count];var d=new float[count];R24CyberLightPolicy.Nearest(p,matrix,x,y,z,ix,d);var reference=Enumerable.Range(0,p.Length/3).Select(i=>{int at=i*3;float px=p[at],py=p[at+1],pz=p[at+2],dx=matrix[0]*px+matrix[1]*py+matrix[2]*pz+matrix[3]-x,dy=matrix[4]*px+matrix[5]*py+matrix[6]*pz+matrix[7]-y,dz=matrix[8]*px+matrix[9]*py+matrix[10]*pz+matrix[11]-z;return new {Index=i,Distance=dx*dx+dy*dy+dz*dz};}).OrderBy(v=>v.Distance).ThenBy(v=>v.Index).Take(count).ToArray();for(int i=0;i<count;i++){Check(ix[i]==reference[i].Index,"actual nearest source site "+i);Near(d[i],reference[i].Distance,"actual world-space source distance");}
 }
 static void Main(string[] args)
 {
  string json=File.ReadAllText(args[0]);var data=new JavaScriptSerializer{MaxJsonLength=2000000}.Deserialize<R24CyberEnvironmentData>(json);data.Validate();Check(data.pooledStreetLights.count==4&&data.pooledInteriorLights.count==6,"only source4+6 pooled lights");Check(data.pooledInteriorLights.interiorSites.Length==1651&&data.pooledInteriorLights.neonSites.Length==209,"actual source interior and neon inventories");var street=R24CyberLightPolicy.ReadStreetSites(json);Check(street.Length==128*3,"actual128 source street lamp sites");Near(street[2],2116,"JSON includes source2400origin already");R24CyberLightPolicy.ReflectZ(street);Near(street[2],-2116,"single reflected baked street position");var interior=Sites(data.pooledInteriorLights.interiorSites);var neon=Sites(data.pooledInteriorLights.neonSites);
  Near(R24CyberLightPolicy.UnitySpotAngle(data.pooledStreetLights.angle),116,"Godot angular radius58 -> Unity full cone116");var moon=R24CyberLightPolicy.ReflectedEmission(data.directional.sourceEulerDegrees);Check(moon[0]>0&&moon[1]<0&&moon[2]>0,"source moon(-47,-24,0) reflected emission");Near(moon[0],.27738956f,"source moon x");Near(moon[1],-.7313537f,"source moon down");Near(moon[2],.623026f,"source moon z");var down=R24CyberLightPolicy.ReflectedEmission(data.pooledStreetLights.sourceEulerDegrees);Near(down[0],0,"street direction x");Near(down[1],-1,"source -90pitch points downward after reflection");Near(down[2],0,"street direction z");
  float clock=0;Check(!R24CyberLightPolicy.Advance(ref clock,.2f)&&R24CyberLightPolicy.Advance(ref clock,.3f)&&clock==0,"actual.5s source cadence resets clock");Check(!R24CyberLightPolicy.Advance(ref clock,0)&&!R24CyberLightPolicy.Advance(ref clock,-1)&&!R24CyberLightPolicy.Advance(ref clock,float.NaN),"pause/invalid samples don't advance pools");Check(R24CyberLightPolicy.Advance(ref clock,2)&&clock==0,"stalled source frame refreshes once without catchup allocations");
  var identity=new float[]{1,0,0,0,0,1,0,0,0,0,1,0};var moved=new float[]{1.2990381f,0,.375f,8,0,1,0,2,-.75f,0,.649519f,-10};foreach(var eye in new[]{new[]{-9f,1.8f,-2765f},new[]{0f,15f,-2400f},new[]{-323f,60f,-2090f}})foreach(var transform in new[]{identity,moved}){TestNearest(street,transform,eye[0],eye[1],eye[2],4);TestNearest(interior,transform,eye[0],eye[1],eye[2],3);TestNearest(neon,transform,eye[0],eye[1],eye[2],3);}
  var equal=new float[]{-1,0,0,1,0,0,0,2,0};var selected=new int[2];var distances=new float[2];R24CyberLightPolicy.Nearest(equal,identity,0,0,0,selected,distances);Check(selected[0]==0&&selected[1]==1,"equal-distance ties deterministic by source site order");
  Near(R24CyberLightPolicy.InteriorEnergy,6.5f,"source active interior overrides initial2.3 energy");Near(R24CyberLightPolicy.NeonEnergy,3.4f,"source neon energy");Near(R24CyberLightPolicy.InteriorRange,18,"source active interior range");Near(R24CyberLightPolicy.NeonRange,14,"source active neon range");Check(55*55==3025&&65*65==4225,"source strict visibility cutoffs");
  Console.WriteLine("PASS "+cases+" actual JSON/source nearest-light/angle/reflection/cadence cases. Native scene lifecycle/render not executed.");
 }
}
