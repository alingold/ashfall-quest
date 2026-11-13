#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.ClimbingMotor.Engine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
// Coordinator-only callable fixture. No autorun, scene/bootstrap/layer changes,
// approval minting, Unity stubs or accepting query doubles.
public static class PlayerR6NativeGates
{
 public interface IFixture:IDisposable
 {
  CharacterController Body {get;}
  UnityPlayerDriver Driver {get;}
  // Must be the actual current ClimbingMotor adapter, backed by exact source hull,
  // sealed proxy context and coordinator-provided immutable approval capability.
  UnityApprovedConvexHeadQueries Head {get;}
  IDesktopViewPort View {get;}
  float LastPitch {get;}
  // Configure real colliders, then rebuild the context/revision. Do not move sealed
  // source geometry underneath a context or alter tracked head/capsule authority.
  void Configure(string scenario);
  Float3 HeadPosition {get;}
 }
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
 static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
 static RootMotionProposal Plan(IFixture f,Float3 delta,float yaw)
 {return RootMotionSafety.Plan(F(f.Body.transform.position),f.HeadPosition,delta,yaw,true);}
 static PlayerGeometryPreflight Bind(IFixture f)
 {
  Check(f.Head!=null&&f.Head.Approved,"actual source-convex approval required");
  return new PlayerGeometryPreflight(new UnityPlayerGeometry(f.Body,f.Head,1<<8));
 }
 public static void RunEditor(Func<IFixture> create)
 {
  // floor-touch: flat floor top exactly at capsule bottom; tangent-floor: parallel
  // motion while touching floor; penetration: capsule bottom inside the floor.
  // Wall/corner/ceiling are within the requested .1m segment (including capsule radius).
  foreach(string scenario in new[]{"floor-touch","tangent-floor","penetration","wall","corner","ceiling","self-authored-capsule"})
  using(var f=create())
  {
   f.Configure(scenario);Physics.SyncTransforms();var geometry=Bind(f);
   var delta=scenario=="ceiling"?new Float3(0,.1f,0):new Float3(.1f,scenario=="floor-touch"?-.00648f:0,0);
   var proposal=Plan(f,delta,0);var before=f.Body.transform.position;
   bool expected=scenario=="floor-touch"||scenario=="tangent-floor";
   Check(geometry.Validate(proposal).Accepted==expected,"real geometry verdict "+scenario);
   var motor=new UnityExclusivePlayerMotor(f.Body,geometry);var result=motor.Apply(proposal,.02f);
   if(expected)
   {
    Check(result.Complete&&result.AppliedDisplacement.X>.09f,"floor movement stalled "+scenario);
    Check(result.Velocity.IsFinite&&result.Velocity.X>4.5f,"floor momentum "+scenario);
   }
   else Check(f.Body.transform.position==before,"blocked root moved "+scenario);
  }
  using(var f=create())
  {
   f.Configure("offset-head");Physics.SyncTransforms();var geometry=Bind(f);
   var p=Plan(f,new Float3(.1f,0,0),.2f);var before=f.Body.transform.position;var rotation=f.Body.transform.rotation;
   Check(!geometry.Validate(p).Accepted,"missing continuous angular hull query must reject");
   new UnityExclusivePlayerMotor(f.Body,geometry).Apply(p,.02f);
   Check(f.Body.transform.position==before&&f.Body.transform.rotation==rotation,"denied angular proposal mutated root");
  }
 }
 public static IEnumerator RunDriverOrder(Func<IFixture> create)
 {
  using(var f=create())
  {
   f.Configure("desktop-initialization");Physics.SyncTransforms();var driver=f.Driver;
   driver.UseXr=false;driver.DesktopView=f.View;driver.CollisionPreflight=Bind(f);
   driver.Motor=new UnityExclusivePlayerMotor(f.Body,driver.CollisionPreflight);
   Check(Application.isFocused&&Time.timeScale>0,"focused real PlayMode fixture required");
   var mouse=InputSystem.AddDevice<Mouse>();
   try
   {
    driver.BeginDesktopInitializationLook(true);
    // A getter between request and Update used to consume the generation and lose capture.
    var observed=driver.CurrentFrame;Check(observed.Paused,"initialization must disarm gameplay");
    float before=f.LastPitch;
    InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(20,10)});
    yield return null;
    Check(Math.Abs(f.LastPitch-before)>.001f,"real driver lost pending initialization capture");
    Check(driver.CurrentFrame.Paused&&!driver.CurrentFrame.TriggerRight,"initialization armed held action");
    foreach(string callback in new[]{"OnApplicationFocus","OnApplicationPause","OnDisable"})
    {
     driver.BeginDesktopInitializationLook(true);
     if(callback=="OnApplicationFocus")driver.SendMessage(callback,false);
     else if(callback=="OnApplicationPause")driver.SendMessage(callback,true);
     else driver.enabled=false;
     Check(driver.CurrentFrame.Paused&&!driver.CurrentFrame.TriggerRight,"callback stale held action "+callback);
    }
    driver.UseXr=true;Check(driver.CurrentFrame.Paused,"source transition stale action");
   }
   finally {InputSystem.RemoveDevice(mouse);}
  }
 }
}
#endif
