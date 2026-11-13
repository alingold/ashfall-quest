using System;
using UnityEngine;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Player;
using Ashfall.Integration.FlightMotor;
// Callable only by root in its serialized native lane. No automatic activation.
// Synthetic authorization/input; no physical tracking or root motion certification.
public sealed class FlightSteeringNativeFixture:IAuthorityGate
{
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public static void Run()
 {
  var host=new FlightSteeringNativeFixture();float previous=-1;
  foreach(float gain in new[]{.25f,.875f,2.5f})
  {
   var motor=new PlayerFlightMotor("native-steering","actor",1,host,new PlayerTuning(),1);
   var settings=new CommandContext("native-steering","actor","settings",1,1,true);
   if(!motor.PublishFlightSettings(settings,0,1,new FlightSettingsSnapshot(gain)).Accepted)throw new InvalidOperationException("settings publication");
   MotionStep step;
   var frame=new FlightFrame{DeltaSeconds=.1f,InputEnabled=true,HandsReady=true,Forward=new Float3(Vector3.forward.x,0,Vector3.forward.z),LeftHand=new Float3(-1,.4f,0),RightHand=new Float3(1,0,0)};
   if(!motor.Step(new CommandContext("native-steering","actor","step",1,1,true),new InputFrame{Tick=1,Sequence=1,Tracked=true},new MotionEnvironment{IsXr=true},frame,out step).Accepted||motor.Velocity.X<=previous)throw new InvalidOperationException("native settings heading");
   previous=motor.Velocity.X;
  }
 }
}
