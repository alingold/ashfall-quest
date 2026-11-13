using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using UnityEngine;
// Root-only fixture. Compile/stage here; root supplies an empty and then obstructed
// real collision scene under engine_lane.py. No activation hooks and no native run.
public sealed class PreparedPadNativeFixture : IFlightWorld, IAuthorityGate
{
 readonly int mask;
 public PreparedPadNativeFixture(int collisionMask){mask=collisionMask;}
 static Vector3 U(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
 public bool ClearPadContact(string id,Float3 face,Float3 contact){return !Physics.Linecast(U(face),U(contact),mask,QueryTriggerInteraction.Ignore);}
 public bool ClearCapsuleExit(string actor,Float3 root){throw new NotSupportedException("not a capsule fixture");}
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();} // Synthetic authority only.
 public void Run(Vector3 center,bool expectedClear)
 {
  var at=new Float3(center.x,center.y,center.z);
  var pad=new LaunchPad("native-pad",at,new Float3(0,1,0),1,20);
  var f=new FlightMachine("fixture","actor",1,this,6.5f,1);var contacts=new PadContacts();PreparedPadLaunch p;
  var result=contacts.PrepareLaunch(new CommandContext("fixture","actor","launch",1,1,true),f,pad,this,at,at,false,10,true,false,false,false,out p);
  if(result.Accepted!=expectedClear)throw new InvalidOperationException("actual physics contact admission");
  if(!result.Accepted)return;
  if(f.Velocity.Y!=0||!p.Validate(10).Accepted)throw new InvalidOperationException("native preparation mutated or changed");
  p.CommitValidated();if(f.Velocity.Y!=20)throw new InvalidOperationException("native launch publication");
 }
}
