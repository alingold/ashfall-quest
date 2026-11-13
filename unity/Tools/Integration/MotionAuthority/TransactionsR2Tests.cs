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
        public int GateCalls;
        public void Revoke(){if(Invalidated!=null)Invalidated();}
        public RuleResult Validate(CommandContext c){GateCalls++;return Allow?RuleResult.Accept():RuleResult.Reject("denied");}
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
        public int Calls;public bool Legacy=false;
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
            var ports=new Ports();var root=new Root();ClimbController climb;PlayerFlightMotor motor;var a=Build(ports,root,out climb,out motor);
            var f=Frame(1);var lease=Prepare(a,f,MotionOwner.Locomotion);ClimbStep cs;PlayerMotorResult result;CapsuleUpdate update;
            Check(!a.StepLocomotion(lease,f,out cs,out result).Accepted&&root.Calls==0&&lease.Validate().Accepted&&!a.Leases.Invalid,"legacy reject before root/lease/input");
            f.Environment.HeadingRadians=Single.NaN;
            Check(!a.StepPad(lease,f,new LaunchPad("pad",new Float3(),new Float3(0,1,0),1,12),ports,new Float3(),false,1,out cs,out result).Accepted&&motor.Velocity.Y==0&&root.Calls==0&&lease.Validate().Accepted,"H3 pad rejection preserves actual Flight and lease");
            var route=new CapsulePlan("pod",0,0,new Float3(),new Float3(0,1,0),new Float3(0,2,0),new Float3(0,3,0),new Float3(0,3,1),new Float3(0,0,-1),2);
            var eligible=new CapsuleEligibility{MapId="test",Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,TeamCount=1};
            var capLease=Prepare(a,f,MotionOwner.Capsule);
            Check(!a.BeginCapsule(capLease,f,eligible,route,0,ports,out update,out result).Accepted&&capLease.Validate().Accepted&&root.Calls==0,"H3 capsule rejects before lease and root");
            Check(!a.AdvanceCapsule(capLease,f,.1,true,true,true,ports,new Float3(1,2,3),out update,out result).Accepted&&root.Calls==0,"capsule advance unavailable before effects");
            Check(!a.ResetLifecycle(Frame(2).Context,2,FlightLifecycle.Respawn,ports).Accepted&&!a.Leases.Invalid,"legacy lifecycle rejection preserves binding");
            ports.Phases=false;f=Frame(2);f.Input.MoveY=1;
            Check(!a.StepLocomotion(Prepare(a,f,MotionOwner.Locomotion),f,out cs,out result).Accepted&&root.Calls==0,"H2 denied receipt cannot follow root write");
            // Verify refusal did not consume the actual Player frame.
            PreparedPlayerFlightStep p,q;var obs=f.Flight;obs.InputEnabled=true;
            Check(motor.PrepareStep(f.Context,f.Input,f.Environment,obs,out p).Accepted,"actual detached prepare after rejected facade");
            Check(motor.Velocity.X==0&&motor.Velocity.Y==0&&motor.PendingMotion==null,"prepare has no published velocity or receipt");
            var m=p.Motion;p.Abort();p.Abort();Check(!p.Validate().Accepted,"idempotent abort");
            Check(motor.PrepareStep(f.Context,f.Input,f.Environment,obs,out p).Accepted&&p.Motion.Displacement.Z==m.Displacement.Z,"same frame retry identical Player algorithm");
            ports.Allow=false;Check(!p.Validate().Accepted&&motor.PendingMotion==null,"external refusal before publication");ports.Allow=true;
            Check(p.Validate().Accepted,"authorization correction retains token");
            Check(p.PrepareCollision(new FlightCollisionResult{Velocity=new Float3(2,0,0),Grounded=true}).Accepted,"fold collision before state publication");
            Check(motor.Velocity.X==0&&motor.PendingMotion==null,"collision preparation remains detached");
            Check(p.Validate().Accepted,"validate all mechanics");int calls=ports.GateCalls;p.CommitValidated();
            Check(ports.GateCalls==calls,"no external authorization during publication");
            Check(motor.Velocity.X==2&&motor.Velocity.Y==0&&motor.Mode==FlightMode.Ground&&motor.PendingMotion==null,"prepared mechanics commit resolved collision once");
            p.Abort();Check(motor.Velocity.X==2,"abort after commit preserves publication");
            Check(!motor.PrepareStep(f.Context,f.Input,f.Environment,obs,out q).Accepted,"committed frame replay refused");
            f=Frame(3);f.Environment.Grounded=false;obs=f.Flight;obs.InputEnabled=true;
            Check(motor.PrepareStep(f.Context,f.Input,f.Environment,obs,out p).Accepted,"air preparation");
            Check(Math.Abs(p.Motion.Velocity.Y+.324f)<.0001f,"single Flight gravity owner");
            Check(!p.PrepareCollision(new FlightCollisionResult{Velocity=new Float3(0,1,0),CeilingBlocked=true}).Accepted,"ceiling momentum rejected before publish");
            Check(motor.Velocity.X==2,"bad feedback preserved live Player");
            Check(motor.PrepareStep(f.Context,f.Input,f.Environment,obs,out q).Accepted&&!p.Validate().Accepted,"new detached Player reservation invalidates stale composition");
            bool threw=false;try{p.CommitValidated();}catch(InvalidOperationException){threw=true;}
            Check(threw&&motor.Velocity.X==2,"stale composed commit before either module writes");q.Abort();
            // Hold exact actual fleet reference to prove the rejected facade did not
            // reserve a pod or consume its command. This direct test is not integration.
            var fleet=new CapsuleFleet("s",2,ports);
            var other=new HostMotionArbiter(Binding(),ports,ports,ports,climb,motor,fleet,root,ports,ports);
            f=Frame(4);f.Environment.HeadingRadians=Single.NaN;
            Check(!other.BeginCapsule(Prepare(other,f,MotionOwner.Capsule),f,eligible,route,0,ports,out update,out result).Accepted,"malformed capsule facade rejected");
            var child=new CommandContext("s","a","f4/capsule-begin",4,2,true);
            Check(fleet.Begin(child,eligible,route,0,ports,out update).Accepted&&update.State==CapsuleState.Flying,"actual fleet command and pod remained unused");
            Check(!fleet.Begin(child,eligible,route,0,ports,out update).Accepted,"direct fleet baseline still enforces replay/reservation");other.Dispose();
            a.Dispose();Check(a.Leases.Invalid&&!climb.LeftAnchored,"explicit lifecycle safety cancellation retained");
            Console.WriteLine("RESULT "+count+" PASS; CPU mechanics only; root/capsule/climb prepared publication BLOCKED");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}

