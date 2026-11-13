#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using UnityEngine;
// Root-only, callable under engine_lane.py. No static activation or approval minting.
public static class FlightMotorR7NativeGates
{
 static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
 static void Check(bool b,string n){if(!b)throw new InvalidOperationException(n);}
 public static void RunStatic(Func<PlayerR6NativeGates.IFixture> create)
 {
  // This also exercises actual floor/wall/ceiling/offset head refusals through the
  // real source-convex backend. Missing continuous angular clearance stays refused.
  PlayerR6NativeGates.RunEditor(create);
  foreach(string scenario in new[]{"floor-touch","tangent-floor"})
  using(var f=create())
  {
   f.Configure(scenario);Physics.SyncTransforms();
   var geometry=new PlayerGeometryPreflight(new UnityPlayerGeometry(f.Body,f.Head,1<<8));
   var motor=new UnityExclusivePlayerMotor(f.Body,geometry);
   var plan=RootMotionSafety.Plan(F(f.Body.transform.position),f.HeadPosition,new Float3(.1f,-.00648f,0),0,true);
   var result=motor.Apply(plan,.02f);
   Check(result.HasPhaseFeedback,"actual motor omitted phase observations");
   Check(result.AppliedPhysicalTranslation.X>.09f,"physical phase stalled");
   Check(result.AppliedPivotCorrection.X==0&&result.AppliedPivotCorrection.Z==0,"zero yaw acquired pivot delta");
   Check(Math.Abs(result.Velocity.X-result.AppliedPhysicalTranslation.X/.02f)<.001f,"velocity contains pivot correction");
  }
 }
 // Coordinator supplies real stable and moving geometry scenarios, never an accepting
 // preflight double. A moving scenario must update/reseal the real approved backend.
 // Production angular hull support is required for a successful nonzero pivot case.
 public interface IPartialFixture:IDisposable
 {
  CharacterController Body {get;}
  IRootCollisionPreflight ApprovedPreflight {get;}
  RootMotionProposal Proposal {get;}
  void Configure(string scenario);
 }
 public static void RunPartial(Func<IPartialFixture> create)
 {
  foreach(string scenario in new[]{"stable-partial-wall","moving-partial-wall","partial-ceiling","partial-offset-head-pivot"})
  using(var f=create())
  {
   f.Configure(scenario);Physics.SyncTransforms();
   var result=new UnityExclusivePlayerMotor(f.Body,f.ApprovedPreflight).Apply(f.Proposal,.02f);
   Check(result.HasPhaseFeedback,"scenario refused or missing phase result: "+scenario);
   Check(!result.Complete,"fixture did not produce partial solve: "+scenario);
   var expected=PlayerMotorFeedback.ResolvePhases(f.Proposal,result.AppliedPhysicalTranslation,result.AppliedPivotCorrection,.02f,result.Grounded,result.CeilingBlocked);
   Check(Math.Abs(expected.Velocity.X-result.Velocity.X)<.001f&&Math.Abs(expected.Velocity.Z-result.Velocity.Z)<.001f,"partial momentum attribution "+scenario);
  }
 }
}
#endif
