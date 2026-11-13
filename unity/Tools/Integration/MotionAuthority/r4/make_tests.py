from pathlib import Path
R=Path.cwd();p=R/'unity/Tools/Integration/MotionAuthority/TransactionsR2Tests.cs';t=p.read_text()
t=t.replace('IFlightLifecycleAuthorization\n','IFlightLifecycleAuthorization,IRootCollisionPreflight\n').replace('public bool Allow=true,Phases=true;','public bool Allow=true,Phases=true; public bool Geometry=true,ClimbAllowed=true;')
t=t.replace('public RuleResult Validate(CommandContext c,long sequence,long generation){return Validate(c);}','public RuleResult Validate(CommandContext c,long sequence,long generation){return ClimbAllowed?Validate(c):RuleResult.Reject("climb denied");}\n public RuleResult Validate(RootMotionProposal p){return Geometry?RuleResult.Accept():RuleResult.Reject("geometry denied");}')
extra=r'''
            ports=new Ports();root=new Root();climb=new ClimbController(new Collision());motor=new PlayerFlightMotor("s","a",2,ports,new PlayerTuning(),1);
            var host=new HostMotionLease(Binding(),ports);var participant=new MotionPublicationPreparation(host,climb,motor,ports,ports,ports,ports);
            f=Frame(10);f.Input.MoveY=1;PreparedMotionLease lp;PreparedMotionPublication prepared,second;
            Check(host.Prepare(Binding(),f.Context,f.Input.Sequence,MotionOwner.Locomotion,out lp).Accepted,"R4 lease");
            ports.ClimbAllowed=false;
            Check(!participant.Prepare(lp,f,out prepared).Accepted&&prepared==null&&lp.Validate().Accepted,"R4 independent climb denial preserves retry lease");
            ports.ClimbAllowed=true;f.Environment.HeadingRadians=Single.NaN;
            Check(!participant.Prepare(lp,f,out prepared).Accepted&&climb.CheckpointReady().Accepted,"R4 downstream malformed frame drains actual climb reservation");
            f.Environment.HeadingRadians=0;ports.Geometry=false;
            Check(!participant.Prepare(lp,f,out prepared).Accepted&&prepared==null&&climb.CheckpointReady().Accepted,"R4 root preflight denial aborts actual owners");
            ports.Geometry=true;
            Check(participant.Prepare(lp,f,out prepared).Accepted,"R4 actual movement preparation after same frame correction");
            Check(prepared.Proposal.Translation.Z>0,"R4 actual walking proposal");
            Check(!climb.CheckpointReady().Accepted,"R4 real climb reservation retained");
            Check(motor.Velocity.Z==0&&motor.PendingMotion==null&&!climb.LeftAnchored,"R4 detached actual states preserved");
            Check(!participant.Prepare(lp,f,out second).Accepted,"R4 exclusive pending preparation");
            ports.Geometry=false;Check(!prepared.Validate().Accepted,"R4 changed geometry rechecked");ports.Geometry=true;
            ports.ClimbAllowed=false;Check(!prepared.Validate().Accepted,"R4 changed upstream admission rechecked");ports.ClimbAllowed=true;
            ports.Allow=false;Check(!prepared.Validate().Accepted,"R4 revoked authority rechecked");ports.Allow=true;
            Check(prepared.Validate().Accepted,"R4 corrected admission retains real prepared token");
            var saved=prepared.Proposal;var copy=prepared.Proposal;copy.Translation=new Float3(999,999,999);
            Check(prepared.Proposal.Translation.Z==saved.Translation.Z,"R4 proposal value isolation");
            Check(!prepared.Publish().Accepted&&root.Calls==0&&motor.Velocity.Z==0&&lp.Validate().Accepted,"R4 unavailable certificate refuses before physical or owner publication");
            prepared.Abort();prepared.Abort();Check(!prepared.Validate().Accepted&&climb.CheckpointReady().Accepted,"R4 abort idempotent and actual reservation drained");
            Check(participant.Prepare(lp,f,out prepared).Accepted&&prepared.Proposal.Translation.Z==saved.Translation.Z,"R4 actual movement same frame deterministic retry");prepared.Abort();
            f.Environment.Grounded=false;
            Check(participant.Prepare(lp,f,out prepared).Accepted,"R4 actual airborne preparation");
            Check(Math.Abs(prepared.Motion.Velocity.Y+.324f)<.0001f,"R4 actual Flight single gravity proposal");
            Check(motor.Velocity.Y==0,"R4 gravity remains detached");prepared.Abort();
            Check(participant.Prepare(lp,f,out prepared).Accepted&&Math.Abs(prepared.Motion.Velocity.Y+.324f)<.0001f,"R4 retry does not integrate gravity twice");prepared.Abort();
            f.Environment.Grounded=true;f.Input.GripLeft=true;f.Climb.LeftTracked=true;f.Climb.LeftContact=f.Input.LeftHandPosition;
            Check(participant.Prepare(lp,f,out prepared).Accepted&&prepared.Climbing.Climbing,"R4 actual grip controller prepares anchor");
            Check(!climb.LeftAnchored,"R4 staged anchor never leaks");prepared.Abort();
            f.Reservations.LeftWeapon=true;
            Check(participant.Prepare(lp,f,out prepared).Accepted&&!prepared.Climbing.Climbing,"R4 actual reservation blocks grip");prepared.Abort();
            f.Context=new CommandContext("s","a","forged",10,2,true);
            Check(!participant.Prepare(lp,f,out prepared).Accepted,"R4 exact command binding");
            f=Frame(10);f.LifecycleGeneration=2;
            Check(!participant.Prepare(lp,f,out prepared).Accepted,"R4 lifecycle mismatch refusal");
            f=Frame(10);f.Environment.ExternalMotionOwner=true;
            Check(!participant.Prepare(lp,f,out prepared).Accepted,"R4 foreign motion owner refusal");
            f=Frame(10);Check(participant.Prepare(lp,f,out prepared).Accepted,"R4 final preparation");host.Invalidate();
            Check(!prepared.Validate().Accepted&&motor.PendingMotion==null,"R4 invalidated lease cannot publish");prepared.Dispose();
            Check(climb.CheckpointReady().Accepted,"R4 invalidated preparation still drains");
'''
t=t.replace('Console.WriteLine("RESULT "+count',extra+'\nConsole.WriteLine("RESULT "+count')
(R/'unity/Tools/Integration/MotionAuthority/r4/PublicationTests.cs').write_text(t)

