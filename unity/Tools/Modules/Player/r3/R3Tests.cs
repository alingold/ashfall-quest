using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
public static class R3Tests
{
 static int n; static long tick;
 static void C(bool b,string s){if(!b)throw new Exception(s);n++;}
 static bool Near(float a,float b){return Math.Abs(a-b)<.0002f;}
 static InputFrame F(){tick++;return new InputFrame{Tick=tick,Sequence=tick,Tracked=true};}
 sealed class Query:IRootCollisionPreflight { public int Calls; public bool Allow; public RootMotionProposal Last; public RuleResult Validate(RootMotionProposal p){Calls++;Last=p;return Allow?RuleResult.Accept():RuleResult.Reject("wall");} }
 static void Main()
 {
  foreach(var t in new[]{new PlayerTuning(),PlayerTuning.SkinRuntime()}) {
   foreach(float h in new[]{0f,1e6f,1e10f,1e20f,float.MaxValue,-float.MaxValue}) {
    var l=new Locomotion(t);var f=F();f.MoveY=1;
    var s=l.Step(f,new MotionEnvironment{HeadingRadians=h,Grounded=true},.02f);
    C(s.Velocity.IsFinite&&s.Displacement.IsFinite,"finite large heading");
    C(Near((float)Math.Sqrt(s.Velocity.X*s.Velocity.X+s.Velocity.Z*s.Velocity.Z),PlayerTuning.WalkSpeed*t.WalkMultiplier),"bounded horizontal norm");
   }
   var sim=new Locomotion(t);var frame=F();frame.MoveY=1;frame.Sprint=true;
   sim.Step(frame,new MotionEnvironment{Grounded=true,IsXr=true},.1f);
   C(sim.SprintStamina<6,"depleted");float prior=sim.SprintStamina;
   frame=F();frame.Paused=true;sim.Step(frame,new MotionEnvironment(),.02f);
   C(Near(sim.SprintStamina,prior),"pause preserves stamina");sim.CancelMotion();C(Near(sim.SprintStamina,prior),"cancel preserves stamina");
   sim.ResetMotion();C(sim.SprintStamina==6,"explicit source reset full");
   sim=new Locomotion(t);frame=F();frame.MoveY=1;frame.Sprint=true;sim.Step(frame,new MotionEnvironment{Grounded=true,IsXr=true},.1f);
   frame=F();frame.Tracked=false;sim.Step(frame,new MotionEnvironment(),.02f);C(sim.SprintStamina==6,"tracking reset full");
  }
  var look=new DesktopLook();var sample=new DesktopLookSample{Focused=true,Enabled=true,CapturePressed=true,GameplayReady=true,DeltaX=100,DeltaY=100};
  var a=look.Read(sample);C(a.Captured&&a.CaptureChanged&&a.YawRadians==0&&a.PitchRadians==0,"capture click consumes delta");
  sample.CapturePressed=false;a=look.Read(sample);C(Near(a.YawRadians,.2f)&&Near(a.PitchRadians,-.2f),"mouse sensitivity and sign");
  sample.DeltaY=10000;a=look.Read(sample);C(Near(a.PitchRadians,-1.45f),"lower pitch clamp");sample.DeltaY=-10000;a=look.Read(sample);C(Near(a.PitchRadians,1.45f),"upper pitch clamp");
  sample.DeltaX=float.MaxValue;a=look.Read(sample);C(Near(a.YawRadians,(float)Math.PI),"bounded huge finite mouse");
  sample.Focused=false;a=look.Read(sample);C(!a.Captured&&a.YawRadians==0,"focus releases");sample.Focused=true;sample.DeltaX=100;a=look.Read(sample);C(!a.Captured,"focus gain cannot recapture");
  sample.CapturePressed=true;look.Read(sample);sample.CapturePressed=false;sample.Paused=true;a=look.Read(sample);C(!a.Captured&&a.YawRadians==0,"pause releases");
  sample.Paused=false;sample.CapturePressed=true;look.Read(sample);sample.CapturePressed=false;sample.EscapePressed=true;C(!look.Read(sample).Captured,"escape release");
  sample.EscapePressed=false;sample.CapturePressed=true;look.Read(sample);sample.DeltaX=float.NaN;C(!look.Read(sample).Captured,"invalid delta releases");
  foreach(var dominant in new[]{DominantHand.Left,DominantHand.Right}) {
   var input=new PlayerInput();input.Ownership.Dominant=dominant;
   var raw=new DeviceSample{IsXr=true,Focused=true,HeadTracked=true,Left=new HandSample{Tracked=true},Right=new HandSample{Tracked=true}};
   for(int i=0;i<4;i++)input.Read(++tick,.02f,HandActionAdapter.Bind(raw,input.Ownership));
   if(dominant==DominantHand.Left){raw.Right.Primary=true;raw.Left.Secondary=true;}else{raw.Left.Primary=true;raw.Right.Secondary=true;}
   raw.Left.StickY=1;raw.Right.StickX=.9f;raw.Left.Click=true;
   var f=input.Read(++tick,.02f,HandActionAdapter.Bind(raw,input.Ownership));C(f.Jump&&f.Reload&&f.Sprint&&f.MoveY==1&&f.TurnX==.9f,"explicit source hand roles "+dominant);
   if(dominant==DominantHand.Left)raw.Right.Tracked=false;else raw.Left.Tracked=false;
   f=input.Read(++tick,.02f,HandActionAdapter.Bind(raw,input.Ownership));C(!f.Jump,"lost support hand disables jump");
   input.Disarm();for(int i=0;i<8;i++)C(!input.Read(++tick,.02f,HandActionAdapter.Bind(raw,input.Ownership)).Jump,"held hand cannot rearm");
  }
  var changed=new PlayerInput();var held=new DeviceSample{IsXr=true,Focused=true,HeadTracked=true,Left=new HandSample{Tracked=true},Right=new HandSample{Tracked=true}};
  for(int i=0;i<4;i++)changed.Read(++tick,.02f,held);
  held.Jump=true;C(changed.Read(++tick,.02f,held).Jump,"setup ready right dominance");
  changed.Ownership.Dominant=DominantHand.Left;
  for(int i=0;i<6;i++)C(!changed.Read(++tick,.02f,held).Jump,"changing handedness held jump disarms new support");
  held.Jump=false;for(int i=0;i<4;i++)changed.Read(++tick,.02f,held);held.Jump=true;C(changed.Read(++tick,.02f,held).Jump,"new support neutral rearms");
  var gripOwner=new HandActionOwnership{Sprint=SprintBinding.LeftGrip};var physical=new DeviceSample{IsXr=true,Left=new HandSample{Grip=.7f}};
  C(HandActionAdapter.Bind(physical,gripOwner).Sprint,"left grip sprint binding explicit");
  gripOwner.Sprint=SprintBinding.LeftClick;C(!HandActionAdapter.Bind(physical,gripOwner).Sprint,"click binding does not infer grip sprint");
  var blocked=new Locomotion(PlayerTuning.SkinRuntime()).Step(F(),new MotionEnvironment{DesktopYawRadians=.2f,TurningBlocked=true,ExternalMotionOwner=true},.02f);
  C(!blocked.OwnsYaw&&blocked.YawRadians==0&&!blocked.OwnsMotion,"external turning block suppresses desktop yaw");
  var desktop=new PlayerInput();var d=new DeviceSample{Focused=true};for(int i=0;i<4;i++)desktop.Read(++tick,.02f,d);
  d.Focused=false;desktop.Read(++tick,.02f,d);d.Focused=true;d.MoveY=1;d.Jump=true;
  for(int i=0;i<6;i++){var f=desktop.Read(++tick,.02f,d);C(f.MoveY==0&&!f.Jump,"focus held movement gated");}
  d.MoveY=0;d.Jump=false;for(int i=0;i<4;i++)desktop.Read(++tick,.02f,d);d.MoveY=1;
  var df=desktop.Read(++tick,.02f,d);var movement=new Locomotion(PlayerTuning.SkinRuntime()).Step(df,new MotionEnvironment{Grounded=true,DesktopYawRadians=(float)(Math.PI/2)},.02f);
  C(Near(movement.Displacement.X,PlayerTuning.WalkSpeed*.02f)&&Near(movement.Displacement.Z,0),"desktop mouse yaw moves same tick");
  var ext=new Locomotion(PlayerTuning.SkinRuntime()).Step(F(),new MotionEnvironment{ExternalMotionOwner=true,DesktopYawRadians=.2f},.02f);
  C(ext.OwnsYaw&&!ext.OwnsMotion&&Near(ext.YawRadians,.2f),"desktop external translation preserves yaw");
  var p=RootMotionSafety.Plan(new Float3(2,0,3),new Float3(3,1.6f,3),new Float3(0,0,.1f),(float)(Math.PI/2),true);
  C(Near(p.PivotCorrection.X,1)&&Near(p.PivotCorrection.Z,1),"offset head pivot correction exact");
  var q=new Query();C(!RootMotionSafety.Approve(p,true,q).Accepted&&q.Calls==1,"wall denies complete proposal");q.Allow=true;C(RootMotionSafety.Approve(p,true,q).Accepted&&Near(q.Last.HeadPosition.Y,1.6f),"measured head proposal forwarded");
  p.OwnsTranslation=false;C(!RootMotionSafety.Approve(p,true,q).Accepted&&q.Calls==2,"external translation rejected before geometry");
  foreach(float height in new[]{.7f,1.6f,2.1f}) {
   var m=new RigMeasurements{Radius=.2f,Height=.5f,SkinWidth=.02f,RootUp=new Float3(0,1,0),TrackingUp=new Float3(0,1,0),CurrentHeadPosition=new Float3(.3f,height,.2f),IsXr=true,HierarchyValid=true,UnitScales=true,ExclusivePoseWriter=true};
   C(PlayerRigSafety.Valid(m),"current crouched/standing head accepted without resizing tracked pose");
   m.CurrentHeadPosition=new Float3(0,float.NaN,0);C(!PlayerRigSafety.Valid(m),"nonfinite measured head rejected");
   m.CurrentHeadPosition=new Float3(0,height,0);m.Height=.3f;C(!PlayerRigSafety.Valid(m),"capsule height below diameter rejected");
   m.Height=.5f;m.SkinWidth=.2f;C(!PlayerRigSafety.Valid(m),"skin width radius rejected");m.SkinWidth=.02f;m.UnitScales=false;C(!PlayerRigSafety.Valid(m),"nonunit hierarchy rejected");
   m.UnitScales=true;m.ExclusivePoseWriter=false;C(!PlayerRigSafety.Valid(m),"exclusive pose ownership required XR");
  }
  var overflow=new RootMotionProposal{OwnsTranslation=true,Translation=new Float3(float.MaxValue,0,0),PivotCorrection=new Float3(float.MaxValue,0,0)};
  int calls=q.Calls;C(!RootMotionSafety.Approve(overflow,true,q).Accepted&&q.Calls==calls,"combined correction overflow rejects before geometry and mutation");
  foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}) {
   var sim=new Locomotion(PlayerTuning.SkinRuntime());var f=F();bool rejected=false;
   try{sim.Step(f,new MotionEnvironment{HeadingRadians=invalid},.02f);}catch(ArgumentException){rejected=true;}
   C(rejected&&sim.Step(f,new MotionEnvironment(),.02f).OwnsMotion,"invalid heading does not consume frame");
  }
  Console.WriteLine("PASS "+n+" r3 behavior checks");
 }
}
