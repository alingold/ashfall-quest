using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.ClimbingMotor;
using Ashfall.Integration.MotionAuthority;
public static class AccountingTests
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    public sealed class Ports:IAuthorityGate,IMotionLeaseAuthorization,IMotionCollisionReceipts,IMotionInvalidation,IClimbFrameAuthorization,IClimbSupport,IFlightWorld,ICapsuleRouteValidation,IFlightLifecycleAuthorization,IRootCollisionPreflight
    {
        public event Action Invalidated;
        public bool Allow=true,Phases=true; public bool Geometry=true,ClimbAllowed=true;
        public int GateCalls;
        public void Revoke(){if(Invalidated!=null)Invalidated();}
        public RuleResult Validate(CommandContext c){GateCalls++;return Allow?RuleResult.Accept():RuleResult.Reject("denied");}
        public RuleResult Validate(CommandContext c,long sequence,long generation){return ClimbAllowed?Validate(c):RuleResult.Reject("climb denied");}
 public RuleResult Validate(RootMotionProposal p){return Geometry?RuleResult.Accept():RuleResult.Reject("geometry denied");}
        public RuleResult Validate(MotionBinding b,CommandContext c,long sequence,MotionOwner owner){return Validate(c);}
        public RuleResult ValidateBinding(MotionBinding b,long f,long s){return Allow?RuleResult.Accept():RuleResult.Reject("binding");}
        public RuleResult ValidateResult(MotionBinding b,long f,long s,RootMotionProposal p,PlayerMotorResult r){return Phases?RuleResult.Accept():RuleResult.Reject("phase");}
        public RuleResult ValidateTransition(CommandContext c,long g,FlightLifecycle reason){return Validate(c);}
        public bool TryComplete(Float3 r,Float3 h,Float3 p,Float3 v,float dt,out Float3 complete){complete=new Float3();return true;}
        public bool ClearPadContact(string id,Float3 a,Float3 b){return true;}
        public bool ClearCapsuleExit(string id,Float3 a){return true;}
        public bool ValidateRoute(string actor,CapsulePlan plan){return true;}
    }
    public sealed class Collision:IClimbCollision
    {
        public bool TryGrab(Float3 p,float r,out SurfaceHit hit){hit=new SurfaceHit{Point=p,Normal=new Float3(0,1,0),AuthoredClimbable=true};return true;}
        public bool Raycast(Float3 a,Float3 b,QueryKind k,out SurfaceHit hit){hit=new SurfaceHit{Point=new Float3(a.X,.5f,a.Z),Normal=new Float3(0,1,0)};return k==QueryKind.Support;}
        public bool HeadMotionClear(Float3 h,Float3 m){return true;}
        public MotionResult SweepHead(Float3 h,Float3 m,float dt,bool slide){return new MotionResult(m,new Float3(m.X/dt,m.Y/dt,m.Z/dt));}
    }
    sealed class Root:IExclusivePlayerMotor
    {
        public int Calls;public bool Legacy=false;
        public PlayerMotorResult Apply(RootMotionProposal p,float dt)
        {Calls++;return Legacy?PlayerMotorFeedback.Resolve(p,p.Translation,dt,false,false):PlayerMotorFeedback.ResolvePhases(p,p.Translation,p.PivotCorrection,dt,false,false);}
    }
    public static MotionBinding Binding(){return new MotionBinding{Session="s",Actor="a",Life=1,Connection=1,Epoch=2,Generation=1};}
    public static MotorFrame Frame(int tick)
    {return new MotorFrame{Context=new CommandContext("s","a","f"+tick,tick,2,true),Input=new InputFrame{Tick=tick,Sequence=tick,Tracked=true,HeadPosition=new Float3(0,1.75f,0),LeftHandPosition=new Float3(0,1,0)},Root=new Float3(),Ready=true,LifecycleGeneration=1,Environment=new MotionEnvironment{Grounded=true,IsXr=true},Flight=new FlightFrame{DeltaSeconds=.02f,Forward=new Float3(0,0,1)},Climb=ClimbOptions.Default};}
    public static int Main()
    {
        try {
            for(int mode=0;mode<4;mode++) {
                var ports=new Ports();var motor=new PlayerFlightMotor("s","a",2,ports,new PlayerTuning(),1);
                var f=Frame(1);f.Environment.Grounded=mode==0;f.Input.MoveY=1;f.Flight.InputEnabled=true;
                PreparedPlayerFlightStep step;Check(motor.PrepareStep(f.Context,f.Input,f.Environment,f.Flight,out step).Accepted,"prepare actual "+mode);
                var a=new NativeReceiptAccounting(step);
                bool threw=false;try{a.CommitValidated();}catch(InvalidOperationException){threw=true;}
                Check(threw&&!a.MechanicsCommitted,"no feedback no success");
                var proposal=RootMotionSafety.Plan(f.Root,f.Input.HeadPosition,step.Motion.Displacement,.2f,true);
                var physical=step.Motion.Displacement;
                var pivot=new Float3(1,0,0);
                var applied=PlayerMotorFeedback.ResolvePhases(proposal,physical,pivot,.02f,false,false);
                var feedback=ReleaseFeedback.Resolve(false,new ClimbStep(),applied,.02f);
                Check(Math.Abs(feedback.Velocity.X-physical.X/.02f)<.00001f,"pivot excluded from momentum");
                if(mode>0)Check(Math.Abs(step.Motion.Velocity.Y+.324f)<.00001f,"once gravity proposal");
                Check(!a.PrepareCollision(new FlightCollisionResult{Velocity=new Float3(float.NaN,0,0)}).Accepted,"bad numeric feedback refused");
                Check(a.PrepareCollision(feedback).Accepted,"fold actual collision");
                Check(!a.PrepareCollision(feedback).Accepted,"duplicate fold refused");
                Check(motor.Velocity.Z==0&&!a.MechanicsCommitted,"detached fold");
                ports.Allow=false;Check(!a.Validate().Accepted,"authority revoked");ports.Allow=true;
                Check(a.Validate().Accepted,"retry exact fold");
                a.CommitValidated();var velocity=motor.Velocity;a.CommitValidated();
                Check(a.MechanicsCommitted&&motor.Velocity.Z==velocity.Z&&motor.PendingMotion==null,"idempotent mechanics commit");
                Check(Math.Abs(motor.Velocity.Y-feedback.Velocity.Y)<.00001f,"no second gravity");
                step.Abort();Check(motor.Velocity.Y==velocity.Y,"postcommit abort preserves");
                PreparedPlayerFlightStep replay;Check(!motor.PrepareStep(f.Context,f.Input,f.Environment,f.Flight,out replay).Accepted,"frame replay rejected");
            }
            {
                var ports=new Ports();var motor=new PlayerFlightMotor("s","a",2,ports,new PlayerTuning(),1);var f=Frame(2);
                PreparedPlayerFlightStep first,second;Check(motor.PrepareStep(f.Context,f.Input,f.Environment,f.Flight,out first).Accepted,"stale prepare");
                var a=new NativeReceiptAccounting(first);Check(a.PrepareCollision(new FlightCollisionResult()).Accepted,"stale fold");
                Check(motor.PrepareStep(f.Context,f.Input,f.Environment,f.Flight,out second).Accepted,"intervening real reservation");
                bool threw=false;try{a.CommitValidated();}catch(InvalidOperationException){threw=true;}
                Check(threw&&!a.MechanicsCommitted&&motor.Velocity.Y==0,"stale commit no success");second.Abort();
                Check(!new NativeReceiptAccounting(second).Validate().Accepted,"aborted actual token");
            }
            var launch=new ClimbStep{Launch=true,Velocity=new Float3(2,6,3)};
            var ap=new PlayerMotorResult{HasPhaseFeedback=true,Complete=true,Grounded=true,AppliedPhysicalTranslation=new Float3()};
            var fb=ReleaseFeedback.Resolve(true,launch,ap,.02f);
            Check(fb.Velocity.Y==6&&!fb.Grounded&&fb.ExternalMotionOwner,"actual climb launch feedback");
            ap.CeilingBlocked=true;fb=ReleaseFeedback.Resolve(true,launch,ap,.02f);
            Check(fb.Velocity.Y==0&&fb.CeilingBlocked,"release ceiling clipping");
            Console.WriteLine("ACCOUNTING "+count+" PASS");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
