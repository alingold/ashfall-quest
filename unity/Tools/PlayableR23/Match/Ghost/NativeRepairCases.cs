using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
class NativeRepairCases
{
 static int checks;static long tick;
 static void N(bool value,string why){checks++;if(!value)throw new Exception(why);}
 static DeviceSample Sample(bool held=false,bool paused=false)
 {
  return new DeviceSample{IsXr=true,Focused=true,HeadTracked=true,HeadPosition=new Float3(0,1.7088f,0),Paused=paused,
   Left=new HandSample{Tracked=true,Position=new Float3(-.24f,1.16f,.24f),Grip=held?.8f:0},
   Right=new HandSample{Tracked=true,Position=new Float3(.24f,1.16f,.24f),Trigger=held?.9f:0}};
 }
 static void Step(PlayerInput owner,DeviceSample sample){owner.Read(++tick,.02f,sample);}
 static void Main()
 {
  var input=new PlayerInput();input.Disarm();Step(input,Sample(true));N(!input.LeftReady&&!input.RightReady,"Held controls cannot rearm actual ghost transition");
  Step(input,Sample());Step(input,Sample());N(!input.LeftReady&&!input.RightReady,"Reproduce native fixture failure:40ms is below actual80ms neutral gate");
  Step(input,Sample());N(!input.LeftReady&&!input.RightReady,"Actual60ms still correctly unarmed");
  Step(input,Sample());N(input.LeftReady&&input.RightReady,"Actual80ms re-arms source owner without threshold change");
  input.Disarm();for(int i=0;i<5;i++)Step(input,Sample());N(input.LeftReady&&input.RightReady,"Corrected fixture100ms positive path uses same real PlayerInput");
  input.Disarm();for(int i=0;i<20;i++)Step(input,Sample(true));N(!input.LeftReady&&!input.RightReady,"Held controls still never rearm after repeated frames");
  Step(input,Sample());Step(input,Sample());Step(input,Sample(true));N(!input.LeftReady&&!input.RightReady,"Nonneutral interruption restarts actual dwell");
  for(int i=0;i<5;i++)Step(input,Sample());N(input.LeftReady&&input.RightReady,"Fresh neutral succeeds after held interruption");
  Step(input,Sample(false,true));N(!input.LeftReady&&!input.RightReady,"Actual pause still disarms ghost movement controls");
  for(int i=0;i<5;i++)Step(input,Sample());N(input.LeftReady&&input.RightReady,"Actual unpaused fresh-neutral path restores both");
  Console.WriteLine(checks+" actual PlayerInput native-fixture failure reproduction/80ms dwell/100ms repair/held/pause cases PASS");
 }
}
