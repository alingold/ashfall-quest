using System;
using Ashfall.Integration.Playable.HandsWeapons;
class RulesCases
{
 static void Main(){int observations=0;
  foreach(string id in new[]{"mk18","akm","spr","m1014","dt11","awp","m60","px4","1911","mp9","tec9","magnum","matadors","knife","katana","cyberblade","excalibur","smaw"})
  foreach(bool sword in new[]{false,true})foreach(bool index in new[]{false,true})foreach(float requested in new[]{-1f,0,.2f,.5f,1,2}){
   // Independent literal transcription of reference grip_hand_086 closure order.
   float expected=id=="matadors"?(index?.80f:1.05f):sword||id=="magnum"||id=="tec9"?(index?.86f:1.12f):id=="knife"?(index?.80f:1.05f):id=="px4"||id=="1911"||id=="mp9"?(index?.62f:.85f):(index?.54f:.74f);
   if(R23GlovePoseRules.Closure(id,sword,index,requested,true)!=expected)throw new Exception("Held finger policy "+id);
   if(R23GlovePoseRules.Closure(id,sword,index,requested,false)!=Math.Max(0,Math.Min(1,requested)))throw new Exception("Free grip/index independence");observations+=2;
   int pose=sword?2:id=="px4"||id=="1911"||id=="tec9"||id=="mp9"||id=="magnum"||id=="matadors"?1:0;
   if(R23GlovePoseRules.Pose(id,sword)!=pose)throw new Exception("Authored grip resource choice");observations++;
  }
  foreach(float dt in new[]{0f,.001f,.01f,.02f,.1f,.25f}){if(Math.Abs(R23GlovePoseRules.Blend(dt)-(1-Math.Exp(-22*dt)))>1e-7)throw new Exception("Source22/s smoothing");observations++;}
  foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-.01f,.251f}){try{R23GlovePoseRules.Blend(invalid);throw new Exception("Invalid dt accepted");}catch(ArgumentOutOfRangeException){observations++;}}
  foreach(float delta in new[]{.001f,.0003f,.00001f}){if(R23GlovePoseRules.Settled(delta,0,0,0))throw new Exception("Premature approximate quaternion stop");observations++;}
  if(!R23GlovePoseRules.Settled(.0000004f,.0000004f,.0000004f,.0000004f)||!R23GlovePoseRules.Settled(0,0,0,0))throw new Exception("Exact component convergence");observations+=2;
  Console.WriteLine("PASS "+observations+" authored pose/closure, free grip-index independence, extrapolation and smoothing observations.");
 }
}
