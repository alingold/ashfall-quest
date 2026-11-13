using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.ClimbingMotor;
using Ashfall.Integration.MotionAuthority;
public static class MotionAcceptance
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    sealed class Ports:IAuthorityGate,IMotionLeaseAuthorization,IMotionCollisionReceipts,IMotionInvalidation,IClimbFrameAuthorization,IClimbSupport,IFlightWorld,ICapsuleRouteValidation,IFlightLifecycleAuthorization
    {
        public event Action Invalidated;
        public bool Allow=true,Phases=true;
        public void Revoke(){if(Invalidated!=null)Invalidated();}
        public RuleResult Validate(CommandContext c){return Allow?RuleResult.Accept():RuleResult.Reject("denied");}
        public RuleResult Validate(CommandContext c,long sequence,long generation){return Validate(c);}
        public RuleResult Validate(MotionBinding b,CommandContext c,long sequence,MotionOwner owner){return Validate(c);}
        public RuleResult ValidateBinding(MotionBinding b,long f,long s){return Allow?RuleResult.Accept():RuleResult.Reject("binding");}
        public RuleResult ValidateResult(MotionBinding b,long f,long s,RootMotionProposal p,PlayerMotorResult r){return Phases?RuleResult.Accept():RuleResult.Reject("phase");}
        public RuleResult ValidateTransition(CommandContext c,long g,FlightLifecycle reason){return Validate(c);}
        public bool TryComplete(Float3 r,Float3 h,Float3 p,Float3 v,float dt,out Float3 complete){complete=new Float3();return true;}
        public bool ClearPadContact(string id,Float3 a,Float3 b){return true;}
        public bool ClearCapsuleExit(string id,Float3 a){return true;}
        public bool ValidateRoute(string actor,CapsulePlan plan){return true;}
    }
    sealed class Collision:IClimbCollision
    {
        public bool TryGrab(Float3 p,float r,out SurfaceHit hit){hit=new SurfaceHit{Point=p,Normal=new Float3(0,1,0),AuthoredClimbable=true};return true;}
        public bool Raycast(Float3 a,Float3 b,QueryKind k,out SurfaceHit hit){hit=new SurfaceHit{Point=new Float3(a.X,.5f,a.Z),Normal=new Float3(0,1,0)};return k==QueryKind.Support;}
        public bool HeadMotionClear(Float3 h,Float3 m){return true;}
        public MotionResult SweepHead(Float3 h,Float3 m,float dt,bool slide){return new MotionResult(m,new Float3(m.X/dt,m.Y/dt,m.Z/dt));}
    }
    sealed class Root:IExclusivePlayerMotor
    {
        public int Calls;public bool Legacy;
        public PlayerMotorResult Apply(RootMotionProposal p,float dt)
        {Calls++;return Legacy?PlayerMotorFeedback.Resolve(p,p.Translation,dt,false,false):PlayerMotorFeedback.ResolvePhases(p,p.Translation,p.PivotCorrection,dt,false,false);}
    }
    static MotionBinding Binding(){return new MotionBinding{Session="s",Actor="a",Life=1,Connection=1,Epoch=2,Generation=1};}
    static MotorFrame Frame(int tick)
    {return new MotorFrame{Context=new CommandContext("s","a","f"+tick,tick,2,true),Input=new InputFrame{Tick=tick,Sequence=tick,Tracked=true,HeadPosition=new Float3(0,1.75f,0),LeftHandPosition=new Float3(0,1,0)},Root=new Float3(),Ready=true,LifecycleGeneration=1,Environment=new MotionEnvironment{Grounded=true,IsXr=true},Flight=new FlightFrame{DeltaSeconds=.02f,Forward=new Float3(0,0,1)},Climb=ClimbOptions.Default};}
    static PreparedMotionLease Prepare(HostMotionArbiter a,MotorFrame f,MotionOwner owner)
    {PreparedMotionLease p;Check(a.Leases.Prepare(a.Leases.Binding,f.Context,f.Input.Sequence,owner,out p).Accepted,"prepare "+f.Context.Tick+" "+owner);return p;}
    static HostMotionArbiter Build(Ports p,Root r,out ClimbController climb,out PlayerFlightMotor flight)
    {climb=new ClimbController(new Collision());flight=new PlayerFlightMotor("s","a",2,p,new PlayerTuning(),1);return new HostMotionArbiter(Binding(),p,p,p,climb,flight,new CapsuleFleet("s",2,p),r,p,p);}
    public static int Main()
    {
        try {
            var ports=new Ports();var root=new Root();ClimbController climb;PlayerFlightMotor flight;var a=Build(ports,root,out climb,out flight);
            var f=Frame(1);var lease=Prepare(a,f,MotionOwner.Locomotion);ClimbStep step;PlayerMotorResult applied;
            f.Environment.ExternalMotionOwner=true;Check(!a.StepLocomotion(lease,f,out step,out applied).Accepted&&root.Calls==0&&lease.Validate().Accepted,"external reject preserves lease/input same frame");
            f.Environment.ExternalMotionOwner=false;Check(a.StepLocomotion(lease,f,out step,out applied).Accepted&&root.Calls==1,"corrected same-frame retry actual motor");
            PreparedMotionLease duplicate;Check(!a.Leases.Prepare(Binding(),f.Context,1,MotionOwner.Capsule,out duplicate).Accepted,"duplicate cannot acquire another root owner");
            f=Frame(2);f.Input.GripLeft=true;f.Climb.LeftContact=new Float3(0,1,0);Check(a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied).Accepted&&climb.LeftAnchored,"actual climb anchor");
            f=Frame(3);Check(a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied).Accepted&&climb.Mantling,"actual release mantle planned");
            f=Frame(4);var capsule=Prepare(a,f,MotionOwner.Capsule);var stale=Prepare(a,f,MotionOwner.Locomotion);
            var plan=new CapsulePlan("pod",0,0,new Float3(),new Float3(0,1,0),new Float3(0,2,0),new Float3(0,3,0),new Float3(0,3,1),new Float3(0,0,-1),2);
            CapsuleUpdate update;var eligibility=new CapsuleEligibility{MapId="test",Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,TeamCount=1};
            Check(a.BeginCapsule(capsule,f,eligibility,plan,0,ports,out update,out applied).Accepted&&!climb.LeftAnchored&&!climb.Mantling&&root.Calls==4,"capsule acquisition clears stale anchor/mantle one root");
            Check(!stale.Validate().Accepted,"competing prepared lease invalidated");
            f=Frame(5);Check(!a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied).Accepted&&root.Calls==4,"flying capsule cannot be stolen");
            Check(a.AdvanceCapsule(Prepare(a,f,MotionOwner.Capsule),f,.1,true,true,true,ports,new Float3(2,3,0),out update,out applied).Accepted&&flight.Velocity.Y==3&&root.Calls==5,"actual fleet ejection transfers momentum once");
            Check(!a.AdvanceCapsule(capsule,Frame(4),.1,true,true,true,ports,new Float3(2,3,0),out update,out applied).Accepted&&root.Calls==5,"old capsule receipt cannot repeat momentum");
            f=Frame(6);f.Environment.Grounded=false;Check(a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied).Accepted&&root.Calls==6,"release returns to actual locomotion");
            var bad=Binding();bad.Connection=2;Check(!a.Leases.Prepare(bad,Frame(8).Context,8,MotionOwner.Locomotion,out duplicate).Accepted,"connection replacement rejected");
            bad=Binding();bad.Life=2;Check(!a.Leases.Prepare(bad,Frame(8).Context,8,MotionOwner.Locomotion,out duplicate).Accepted,"wrong life rejected");
            bad=Binding();bad.Generation=2;Check(!a.Leases.Prepare(bad,Frame(8).Context,8,MotionOwner.Locomotion,out duplicate).Accepted,"wrong generation rejected");
            bad=Binding();bad.Epoch=3;Check(!a.Leases.Prepare(bad,Frame(8).Context,8,MotionOwner.Locomotion,out duplicate).Accepted,"wrong epoch rejected");
            f=Frame(7);lease=Prepare(a,f,MotionOwner.Locomotion);ports.Allow=false;Check(!a.StepLocomotion(lease,f,out step,out applied).Accepted&&root.Calls==6,"revoked authority before input/root");ports.Allow=true;
            Check(a.StepLocomotion(lease,f,out step,out applied).Accepted&&root.Calls==7,"authority correction same-frame retry");
            ports.Revoke();Check(a.Leases.Invalid&&!climb.LeftAnchored,"explicit invalidation cancels anchors");
            Check(!a.Leases.Prepare(Binding(),Frame(7).Context,7,MotionOwner.Locomotion,out duplicate).Accepted,"invalidation terminal");a.Dispose();
            ports=new Ports();root=new Root{Legacy=true};a=Build(ports,root,out climb,out flight);f=Frame(1);
            Check(!a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied).Accepted&&a.Leases.Invalid,"missing physical/pivot receipt quarantines");a.Dispose();
            ports=new Ports();root=new Root();a=Build(ports,root,out climb,out flight);f=Frame(1);a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied);
            f=Frame(2);Check(a.StepPad(Prepare(a,f,MotionOwner.Locomotion),f,new LaunchPad("p",new Float3(),new Float3(0,1,0),1,12),ports,new Float3(),false,1,out step,out applied).Accepted&&flight.Velocity.Y>0,"actual pad launch through actual motors");
            f=Frame(3);f.Environment.Grounded=false;f.Flight.DesktopGlide=true;f.Environment.IsXr=false;
            a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied);
            f=Frame(4);f.Environment.Grounded=false;f.Flight.DesktopGlide=true;f.Environment.IsXr=false;
            Check(a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied).Accepted&&flight.Mode==FlightMode.Glide,"actual glide after desktop transition");
            Check(a.ResetLifecycle(Frame(5).Context,2,FlightLifecycle.Respawn,ports).Accepted&&a.Leases.Invalid&&flight.Velocity.Y==0,"actual motor/fleet respawn clears momentum and retires lease");a.Dispose();
            ports=new Ports();root=new Root();a=Build(ports,root,out climb,out flight);f=Frame(1);a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied);
            f=Frame(2);f.Input.MoveY=1;f.Input.TurnX=1;f.Input.HeadPosition=new Float3(.2f,1.75f,0);
            Check(a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied).Accepted&&applied.AppliedPhysicalTranslation.Z>0&&applied.HasPhaseFeedback&&Math.Abs(applied.AppliedPivotCorrection.Z)>0,"actual walk and pivot phases remain separate");
            ports.Phases=false;f=Frame(3);Check(!a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out step,out applied).Accepted&&a.Leases.Invalid,"explicit collision receipt denial quarantines");a.Dispose();
            Console.WriteLine("RESULT "+count+" assertions PASS; CPU port fixtures only; no native solver approval");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
