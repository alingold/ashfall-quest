using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
class IntegrationR5Tests
{
 static int pass;static long tick;
 static void C(bool b,string name) {if(!b)throw new Exception(name);pass++;}
 static InputFrame F() {tick++;return new InputFrame{Tick=tick,Sequence=tick,Tracked=true,MoveY=1};}
 static bool Near(float a,float b) {return Math.Abs(a-b)<.0001;}
 sealed class Settings:IPlayerSettingsPort {public ForwardReference ForwardReference {get {return ForwardReference.Hand;}} public float GroundSpeedGain(float gain) {return gain*.5f;}}
 sealed class Contacts:IPlayerInteractionPort {public SprintContacts Value;public SprintContacts ReadContacts(){return Value;}}
 sealed class Geometry:IApprovedPlayerGeometry {public long Revision {get;set;} public bool Approved {get;set;} public int Calls;public int Deny; public bool Drift;
 RuleResult Query(){Calls++;if(Drift)Revision++;return Calls==Deny?RuleResult.Reject("blocked"):RuleResult.Accept();}
 public RuleResult HeadSweep(Float3 h,Float3 d){return Query();} public RuleResult RootSweep(Float3 r,Float3 d){return Query();}public RuleResult HeightSupportObstruction(Float3 r,Float3 h){return Query();}public RuleResult PivotClearance(RootMotionProposal p){return Query();}}
 static void Main()
 {
 foreach(var tuning in new[]{new PlayerTuning(),PlayerTuning.SkinRuntime()})
 {
 var h=new MovementHeading();var l=new Locomotion(tuning);Float3 forward=new Float3(0,0,1),right=new Float3(1,0,0),up=new Float3(0,1,0);
 float heading=h.Read(ForwardReference.Hand,true,true,forward,right,up,0);
 var step=l.Step(F(),new MotionEnvironment{Grounded=true,IsXr=true,HeadingRadians=heading},.02f);C(step.Velocity.X>0&&Math.Abs(step.Velocity.Z)<.001,"hand differs head actual movement");
 heading=h.Read(ForwardReference.Hand,true,true,forward,new Float3(0,1,0),new Float3(-1,0,0),0);C(Near(heading,(float)Math.PI/2),"vertical continuous");
 heading=h.Read(ForwardReference.Hand,true,true,forward,new Float3(-1,0,0),up,0);C(Near(heading,(float)Math.PI/2),"pole sign continuity");
 heading=h.Read(ForwardReference.Hand,true,false,forward,right,up,0);C(heading==0,"eligibility fallback");
 heading=h.Read(ForwardReference.Hand,true,true,forward,new Float3(-1,0,0),up,0);C(Near(heading,-(float)Math.PI/2),"recovery reseeds");
 h.Reset();heading=h.Read(ForwardReference.Hand,true,true,forward,new Float3(0,1,0),new Float3(-1,0,0),0);C(Near(heading,(float)Math.PI/2),"initial top fallback");
 h.Reset();heading=h.Read(ForwardReference.Hand,true,true,forward,right,new Float3(0,-1,0),0);C(Near(heading,-(float)Math.PI/2),"initial inverted up");
 l.Settings=new Settings();step=l.Step(F(),new MotionEnvironment{Grounded=true},.02f);C(Near(step.Velocity.Z,PlayerTuning.WalkSpeed*tuning.WalkMultiplier*.5f),"ground speed provider");
 var contacts=new Contacts();l.Interaction=contacts;l.OwnershipSprint=SprintBinding.LeftGrip;
 var f=F();f.Sprint=true;step=l.Step(f,new MotionEnvironment{Grounded=true,IsXr=true},.02f);C(l.SprintTier==1,"sprint begins");
 contacts.Value=new SprintContacts{LeftSurfaceContact=true};step=l.Step(Fheld(),new MotionEnvironment{Grounded=true,IsXr=true},.02f);C(l.SprintTier==0,"contact cancels");
 contacts.Value=new SprintContacts();step=l.Step(Fheld(),new MotionEnvironment{Grounded=true,IsXr=true},.02f);C(l.SprintTier==0,"contact release held no edge");
 l.Step(F(),new MotionEnvironment{Grounded=true,IsXr=true},.02f);l.Step(Fheld(),new MotionEnvironment{Grounded=true,IsXr=true},.02f);C(l.SprintTier==1,"release press rearm");
 float stamina=l.SprintStamina;l.CancelMotion();C(l.SprintStamina==stamina,"collision no refill");l.ResetMotion();C(l.SprintStamina==6,"reset refill");
 }
 foreach(var tuning in new[]{new PlayerTuning(),PlayerTuning.SkinRuntime()})
 {
  var heading=new MovementHeading();var motion=new Locomotion(tuning);
  foreach(int scenario in new[]{0,1,2,3,4})
  {
   bool ready=scenario!=3;Float3 hand=scenario==0?new Float3(1,0,0):scenario==1?new Float3(.01f,1,0):new Float3(-1,0,0);
   float angle=heading.Read(ForwardReference.Hand,true,ready,new Float3(0,0,1),hand,new Float3(0,1,0),0);
   var moved=motion.Step(F(),new MotionEnvironment{Grounded=true,IsXr=true,HeadingRadians=angle},.02f);
   C(scenario==3?moved.Velocity.Z>0:scenario==4?moved.Velocity.X<0:moved.Velocity.X>0,"heading to actual movement transition "+scenario);
  }
 }
 var input=new PlayerInput();var owner=new PlayerFrameOwner(input);var state=new PlayerLifecycleState{Enabled=true,Focused=true,RigValid=true,Session="s",Actor="a",Epoch=1};owner.Observe(state);var frame=F();frame.Jump=true;owner.Publish(frame);C(owner.Read(frame.Tick).Jump,"publication");
 for(int i=0;i<6;i++) {state.Xr=!state.Xr;state.SessionFocused=state.Present=true;owner.Observe(state);C(owner.Current.Paused&&!owner.Current.Jump,"mode immediate");C(!input.LeftReady&&!input.DesktopReady,"rearm invalidation");}
 state.SessionFocused=false;owner.Observe(state);C(owner.Current.Paused,"focus invalidation");state.Xr=false;state.Present=true;state.Focused=true;owner.Observe(state);owner.Publish(F());state.Epoch++;owner.Observe(state);C(owner.Current.Paused,"epoch invalidation");
 foreach(int denied in new[]{1,2,3,4}) {var geometry=new Geometry{Approved=true,Deny=denied};var query=new PlayerGeometryPreflight(geometry);var p=RootMotionSafety.Plan(new Float3(),new Float3(1,1,0),new Float3(0,0,1),1,true);C(!RootMotionSafety.Approve(p,true,query).Accepted&&geometry.Calls==denied,"geometry rejects stage "+denied);}
 var drift=new Geometry{Approved=true,Drift=true};C(!new PlayerGeometryPreflight(drift).Validate(new RootMotionProposal()).Accepted,"geometry revision drift");
 var look=new DesktopLook();look.InitializeCapture();var ls=look.Read(new DesktopLookSample{Focused=true,Enabled=true,InitializationLook=true,InputRequested=true,DeltaX=10});C(Near(ls.YawRadians,.02f),"init gaze with gameplay off");C(!look.Read(new DesktopLookSample{Focused=true,Enabled=true,DeltaX=10}).YawRadians.Equals(.02f),"init opt in only");
 var proposal=RootMotionSafety.Plan(new Float3(),new Float3(1,1,0),new Float3(0,0,.1f),(float)Math.PI/2,true);
 var partial=PlayerMotorFeedback.Resolve(proposal,new Float3(.01f,0,.02f),.02f,false,false);C(!partial.Complete,"partial move denies yaw");C(Near(partial.Velocity.X,.5f)&&Near(partial.Velocity.Z,1),"partial actual root momentum");
 var full=PlayerMotorFeedback.Resolve(proposal,new Float3(proposal.PivotCorrection.X,0,proposal.PivotCorrection.Z+.1f),.02f,false,false);C(full.Complete&&Near(full.Velocity.Z,5)&&Near(full.Velocity.X,0),"full pivot correction excluded from momentum");
 var ceiling=PlayerMotorFeedback.Resolve(new RootMotionProposal{Translation=new Float3(0,.1f,0)},new Float3(0,.01f,0),.02f,false,true);C(ceiling.Velocity.Y==0&&!ceiling.Complete,"ceiling authoritative clip");
 Console.WriteLine("PASS "+pass+" FAIL 0");
 }
 static InputFrame Fheld(){var f=F();f.Sprint=true;return f;}
}
