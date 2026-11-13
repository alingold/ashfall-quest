using System;
using Ashfall.Editor.Playable;
class CaptureFitCases {
 static int count;
 static void Need(bool v,string why){if(!v)throw new Exception(why);count++;}
 static void Fit(float width,float height,float depth,float aspect,float fov){
  const double margin=.06;float d=R24UiCaptureFit.Distance(width/2,height/2,depth,fov,aspect,(float)margin);
  double tan=Math.Tan(fov*Math.PI/360);for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){double range=d+z*depth,u=.5+x*width/(4*range*tan*aspect),v=.5+y*height/(4*range*tan);Need(range>.03&&u>=margin-1e-6&&u<=1-margin+1e-6&&v>=margin-1e-6&&v<=1-margin+1e-6,"Projected actual canvas corner outside margin");}
 }
 static int Main(){Fit(2.2f,1.375f,0,1280f/720,85);Fit(1.3f,1.48f,0,1280f/720,85);Fit(1.2f,.64f,0,1280f/720,85);Fit(4.4f,2.75f,.12f,1,45);Fit(2.2f,1.375f,.01f,.56f,75);bool rejected=false;try{R24UiCaptureFit.Distance(1,1,0,60,1,.5f);}catch(ArgumentOutOfRangeException){rejected=true;}Need(rejected,"Invalid margin accepted");rejected=false;try{R24UiCaptureFit.Distance(float.PositiveInfinity,1,0,60,1,.06f);}catch(ArgumentOutOfRangeException){rejected=true;}Need(rejected,"Infinite canvas accepted");Console.WriteLine(count+" actual-canvas perspective corner/invalid-domain cases PASS (pure CPU)");return 0;}
}
