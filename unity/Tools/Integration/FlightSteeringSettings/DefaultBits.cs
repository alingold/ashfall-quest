using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
public sealed class DefaultBits:IAuthorityGate
{
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public static int Main()
 {
  var f=new FlightMachine("s","a",3,new DefaultBits(),PlayerTuning.WalkSpeed*2.5f,1);
  for(int n=0;n<101;n++)
  {
   var frame=new FlightFrame{DeltaSeconds=.01f,InputEnabled=true,Tracked=true,XR=true,HandsReady=true,Forward=new Float3(.2f,0,1),MoveDirection=new Float3(.1f,0,.1f),LeftHand=new Float3(-1,(n-50)*.01f,0),RightHand=new Float3(1,0,0)};
   if(!f.Step(new CommandContext("s","a","c"+n,n,3,true),frame).Accepted)return 1;
   foreach(float v in new[]{f.Velocity.X,f.Velocity.Y,f.Velocity.Z})Console.Write(BitConverter.ToInt32(BitConverter.GetBytes(v),0)+",");
  }
  return 0;
 }
}
