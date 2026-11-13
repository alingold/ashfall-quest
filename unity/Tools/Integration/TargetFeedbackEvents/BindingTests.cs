using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Presentation;
using Ashfall.Integration.PersonalSettings;
using Ashfall.Integration.PersonalSettings.Engine;
public static class BindingTests
{
 sealed class Sink:ITargetFeedbackPort { public NativeTargetFeedbackPool Pool=new NativeTargetFeedbackPool(48);public void Emit(string t,float a,bool s){Pool.Emit(t,a,s);} }
 sealed class Host:IAuthorityGate,IHealthWorld,IInteractionQuery {
 public double Now{get{return 0;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
 public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("fixture");}
 public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}
 public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return false;}}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static int Main(){try{
 var w=new Host();var h=new HealthService("r10",0,0,true,w,w,w);var hud=new HudModel();hud.Update(new HudInput(),false,0);var sink=new Sink();
 Check(h.Register(new CommandContext("r10","t","register",1,0,true),"t",-1,false).Accepted,"register");h.DrainTransitions();
 var binding=new TargetFeedbackBinding(h,"r10",0,"t",hud,sink);
 for(int i=0;i<3;i++) { var before=h.Read("t");Check(h.ApplyLoss(new CommandContext("r10","t","d"+i,2+i,0,true),"t",before.Revision,i==0?7:i==1?.25f:2.5f,0,false).Accepted,"damage");foreach(var e in h.DrainTransitions()){binding.ObserveTransition(e);if(i>0){binding.OnAuthorizedOutgoingTransition(e);binding.OnAuthorizedOutgoingTransition(e);}} }
 Check(sink.Pool.Read(0).Text=="1"&&sink.Pool.Read(1).Text=="3"&&!sink.Pool.Read(2).Active,"actual binding exact foreign/local concurrent duplicate");
 hud.Update(new HudInput(),true,0);var b=h.Read("t");Check(h.ApplyLoss(new CommandContext("r10","t","hidden",5,0,true),"t",b.Revision,4,0,false).Accepted,"hidden damage");var changes=h.DrainTransitions();foreach(var e in changes)binding.ObserveTransition(e);hud.Update(new HudInput(),false,0);foreach(var e in changes)binding.OnAuthorizedOutgoingTransition(e);Check(!sink.Pool.Read(2).Active,"hidden event cannot reappear");
 Console.WriteLine("PASS actual HealthService/TargetFeedbackBinding -> public pool, foreign/local duplicate and hidden authorization");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}

