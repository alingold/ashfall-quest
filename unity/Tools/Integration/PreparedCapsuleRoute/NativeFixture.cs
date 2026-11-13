using System;
using UnityEngine;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.MotionRoutes;
// Compile only. Root may explicitly schedule; no automatic engine entrypoint.
public static class PreparedCapsuleRouteNativeFixture
{
 public static void Run()
 {
  var o=new SyntheticCapsuleRoutes("native-isolated",1);o.RegisterActor("a","body",1,1);
  var plan=new CapsulePlan("pod",0,0,new Float3(),new Float3(0,1,0),new Float3(0,2,0),new Float3(0,0,2),new Float3(0,0,3),new Float3(0,0,-1),2);
  ReservedCapsuleRoute r;PreparedRouteUpdate p;
  if(!o.Reserve("a",plan,ReservedCapsuleRoute.Encode(plan),20,out r).Accepted||!o.PrepareBegin(new CommandContext("native-isolated","a","begin",0,1,true),r,out p).Accepted||!p.Validate().Accepted)throw new Exception("prepare");
  p.CommitValidated();Debug.Log("CPU capsule participant only; no physical root publication");
 }
}
