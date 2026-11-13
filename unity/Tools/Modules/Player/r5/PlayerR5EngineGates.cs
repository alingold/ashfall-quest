#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Modules.Player;
using UnityEngine;
// Coordinator calls in isolated Editor fixture, or yields RunPlayMode from its PlayMode harness.
// Supplied geometry must be real approved convex queries; no test substitute closes this gate.
public static class PlayerR5EngineGates
{
 public interface IFixture:IDisposable
 {
  UnityPlayerDriver Driver {get;} CharacterController Controller {get;}
  IRootCollisionPreflight Geometry {get;}
  void Configure(string scenario);
  RootMotionProposal Proposal {get;}
  void LoseXrFocus(); void HoldControls(); void ReleaseControls();
 }
 public static void RunEditor(Func<IFixture> create)
 {
  foreach(string scenario in new[]{"wall","corner","low-ceiling","offset-head","blocked-pivot","partial-move"})
  using(IFixture f=create())
  {
   f.Configure(scenario);Physics.SyncTransforms();
   var before=f.Controller.transform.rotation;
   var result=new UnityExclusivePlayerMotor(f.Controller,f.Geometry).Apply(f.Proposal,.02f);
   if(!result.Complete&&f.Controller.transform.rotation!=before)throw new Exception("Partial/denied pivot rotated: "+scenario);
   if(!result.Velocity.IsFinite)throw new Exception("Nonfinite motor result");
  }
 }
 public static IEnumerator RunPlayMode(Func<IFixture> create)
 {
  using(IFixture f=create())
  {
   f.Configure("held-controls-callback-order");f.HoldControls();yield return null;
   f.Driver.UseXr=!f.Driver.UseXr;
   if(!f.Driver.CurrentFrame.Paused)throw new Exception("Source transition published stale frame");
   f.LoseXrFocus();if(!f.Driver.CurrentFrame.Paused)throw new Exception("Focus transition stale frame");
   f.Driver.Paused=true;if(!f.Driver.CurrentFrame.Paused)throw new Exception("Pause stale frame");
   f.ReleaseControls();yield return null;
  }
 }
}
#endif
