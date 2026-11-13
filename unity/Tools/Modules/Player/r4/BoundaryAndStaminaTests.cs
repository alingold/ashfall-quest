using System;using Ashfall.Core;using Ashfall.Modules.Player;
static class BoundaryAndStaminaTests {
 static int n;static void C(bool ok,string s){if(!ok)throw new Exception(s);n++;}
 sealed class Query:IRootCollisionPreflight {public int Calls;public RuleResult Validate(RootMotionProposal p){Calls++;return RuleResult.Accept();}}
 static Float3 V(int axis,float value){return new Float3(axis==0?value:0,axis==1?value:0,axis==2?value:0);}
 static void Main(){
  for(int axis=0;axis<3;axis++)foreach(float sign in new[]{-1f,1f}){
   var q=new Query();Float3 root=V(axis,sign*float.MaxValue);var p=RootMotionSafety.Plan(root,root,V(axis,sign*float.MaxValue),0,true);
   C(!RootMotionSafety.Approve(p,true,q).Accepted&&q.Calls==0,"signed destination overflow rejects before query");
   root=V(axis,sign*(float.MaxValue*.25f));p=RootMotionSafety.Plan(root,root,root,0,true);C(RootMotionSafety.Approve(p,true,q).Accepted&&q.Calls==1,"large representable destination retained");
   q=new Query();p=RootMotionSafety.Plan(new Float3(),V(axis,sign*float.MaxValue),V(axis,sign*float.MaxValue),0,true);C(!RootMotionSafety.Approve(p,true,q).Accepted&&q.Calls==0,"head-only destination overflow rejects");
  }
  var locomotion=new Locomotion(new PlayerTuning());locomotion.Step(new InputFrame{Tick=1,Sequence=1,Tracked=true,MoveY=1,Sprint=true},new MotionEnvironment{Grounded=true,IsXr=true},.1f);float before=locomotion.SprintStamina;var velocity=locomotion.Velocity;
  C(before<6,"actual sprint depleted stamina");C(Math.Abs(locomotion.RestoreStamina(.04f)-.04f)<.00001,"partial host gain reports real amount");
  C(locomotion.Velocity.X==velocity.X&&locomotion.Velocity.Y==velocity.Y&&locomotion.Velocity.Z==velocity.Z,"gain leaves motion unchanged");
  C(locomotion.RestoreStamina(6)>0&&locomotion.SprintStamina==6,"fill caps at source6");C(locomotion.RestoreStamina(6)==0,"already full reports capped zero");
  foreach(float bad in new[]{-1f,7f,float.NaN,float.PositiveInfinity}){bool rejected=false;try{locomotion.RestoreStamina(bad);}catch(ArgumentOutOfRangeException){rejected=true;}C(rejected&&locomotion.SprintStamina==6,"invalid gain no mutation");}
  Console.WriteLine("PASS "+n+" destination/stamina checks");
 }
}
