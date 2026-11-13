using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
class R6Tests
{
 static int n;static long tick;
 static void C(bool v){n++;if(!v)throw new Exception("R6 assertion "+n);}
 sealed class Contact:IPlayerInteractionPort {public SprintContacts Value;public SprintContacts ReadContacts(){return Value;}}
 sealed class Gate:IAuthorityGate {public bool Allow=true;public RuleResult Validate(CommandContext c){return Allow?RuleResult.Accept():RuleResult.Reject("fixture denial");}}
 sealed class Settings:IPlayerSettingsPort {public ForwardReference ForwardReference {get{return ForwardReference.Hand;}} public float GroundSpeedGain(float s){return s;}}
 static InputFrame F(bool sprint){tick++;return new InputFrame{Tick=tick,Sequence=tick,Tracked=true,Sprint=sprint,MoveY=1};}
 static void Main()
 {
  foreach(var profile in new[]{new PlayerTuning(),PlayerTuning.SkinRuntime()})
  {
   for(int block=0;block<7;block++)
   {
    var contacts=new Contact();var m=new Locomotion(profile){Interaction=contacts,OwnershipSprint=SprintBinding.LeftGrip};var w=new MotionEnvironment{Grounded=true,IsXr=true};m.Step(F(true),w,.02f);C(m.SprintTier==1);
    contacts.Value=new SprintContacts{LeftSurfaceContact=true,LeftReserved=block==0,LeftGripReserved=block==1,ProviderBlocksSprint=block==2,HasGrab=block==3};
    if(block==4)w.Grounded=false;if(block==5)w.SprintBlocked=true;if(block==6)w.IsXr=false;
    m.Step(F(true),w,.02f);C(m.SprintTier==1);
    contacts.Value=new SprintContacts();w=new MotionEnvironment{Grounded=true,IsXr=true};m.Step(F(true),w,.02f);C(m.SprintTier==1);
    contacts.Value=new SprintContacts{LeftSurfaceContact=true};m.Step(F(true),w,.02f);C(m.SprintTier==0);
    contacts.Value=new SprintContacts();m.Step(F(true),w,.02f);C(m.SprintTier==0);
   }
   foreach(bool mantle in new[]{false,true})
   {
    var contacts=new Contact();var m=new Locomotion(profile){Interaction=contacts,OwnershipSprint=SprintBinding.LeftGrip};var w=new MotionEnvironment{Grounded=true,IsXr=true};m.Step(F(true),w,.02f);
    contacts.Value=new SprintContacts{Climbing=!mantle,Mantling=mantle,LeftReserved=true};m.Step(F(true),w,.02f);C(m.SprintTier==0);
   }
   var input=new PlayerInput();var gate=new Gate();var publication=new PlayerSettingsPublication(input,profile,gate,"s","a",1);var next=profile.MaximumWalkMultiplier==3?PlayerTuning.SkinRuntime():new PlayerTuning();next.WalkMultiplier=profile.MaximumWalkMultiplier;
   var context=new CommandContext("s","a","settings",1,1,true);var settings=new Settings();
   gate.Allow=false;C(!publication.Publish(context,0,next,settings,DominantHand.Left,SprintBinding.LeftGrip).Accepted);C(publication.Revision==0);
   gate.Allow=true;C(publication.Publish(context,0,next,settings,DominantHand.Left,SprintBinding.LeftGrip).Accepted);C(input.Ownership.Dominant==DominantHand.Left&&input.Ownership.Sprint==SprintBinding.LeftGrip&&profile.WalkMultiplier==profile.MaximumWalkMultiplier);
   C(!publication.Publish(context,0,next,settings,DominantHand.Right,SprintBinding.LeftClick).Accepted);
   next.WalkMultiplier=profile.MaximumWalkMultiplier+1;C(!publication.Publish(context,1,next,settings,DominantHand.Right,SprintBinding.LeftClick).Accepted);
   next.WalkMultiplier=1;C(!publication.Publish(new CommandContext("s","other","x",1,1,true),1,next,settings,DominantHand.Right,SprintBinding.LeftClick).Accepted);C(publication.Revision==1);
  }
  var p=new RootMotionProposal{Translation=new Float3(.1f,-.00648f,0),PivotCorrection=new Float3(.02f,0,0)};
  var r=PlayerMotorFeedback.Resolve(p,new Float3(.12f,0,0),.02f,true,false);C(r.Complete);C(Math.Abs(r.Velocity.X-5)<.001&&r.Velocity.Y==0);
  C(!PlayerMotorFeedback.Resolve(p,new Float3(.08f,0,0),.02f,true,false).Complete);
  C(!PlayerMotorFeedback.Resolve(p,new Float3(.12f,0,0),.02f,false,false).Complete);
  Console.WriteLine("PASS "+n+" FAIL 0");
 }
}
