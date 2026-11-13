#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Player;
using Ashfall.Integration.FlightMotor;
using UnityEngine;
// Callable fixture only. Coordinator stages with exact real sources, invokes under
// engine_lane.py. This leaf never runs Unity. No scene/bootstrap dependencies.
public static class FlightMotorR6NativeTests
{
 sealed class Gate:IAuthorityGate {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
 public static void Run()
 {
  GameObject go=new GameObject("FlightR6QuarantinedFixture");
  try
  {
   var adapter=go.AddComponent<FlightRootAdapter>();var machine=new FlightMachine("s","a",3,new Gate(),6,1);
   var c=new CommandContext("s","a","legacy",1,3,true);int published=0;
   adapter.FlightVelocity+=delegate(Float3 v){published++;throw new Exception("unsafe subscriber invoked");};
   adapter.RootRelocation+=delegate(Float3 v){published++;throw new Exception("unsafe relocation invoked");};
   var f=new FlightFrame{DeltaSeconds=.1f,InputEnabled=true,Tracked=true,DesktopGlide=true,Forward=new Float3(0,0,1)};
   if(adapter.ApplyStep(machine,c,f).Accepted||machine.PendingMotion!=null||machine.Mode!=FlightMode.Ground||published!=0)throw new Exception("Legacy Step partial publication");
   if(adapter.ApplyPad(null,machine,null,c,new Float3(),new Float3(),false,0,true,false,false,false).Accepted||published!=0)throw new Exception("Legacy Pad migration not enforced");
   if(adapter.ApplyCapsule(null,c,0,true,true,false).Accepted||adapter.BeginCapsule(null,c,new CapsuleEligibility(),null,0,null).Accepted||published!=0)throw new Exception("Legacy capsule migration not enforced");
   if(!machine.Step(c,f).Accepted)throw new Exception("Rejected legacy call consumed command");
   var motor=new PlayerFlightMotor("s","a",3,new Gate(),new PlayerTuning(),1);
   MotionStep motion;
   if(!motor.Step(c,new InputFrame{Tick=1,Sequence=1,Tracked=true,TurnX=1},new MotionEnvironment(),f,out motion).Accepted||motion.YawRadians<=0||motion.Velocity.X<=0)throw new Exception("Real composed same tick heading");
   Debug.Log("FlightMotorR6NativeTests PASS legacy rejection without mutation; no scene/physics/runtime approval");
  }
  finally {UnityEngine.Object.DestroyImmediate(go);}
 }
}
#endif
