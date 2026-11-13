using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.ClimbingMotor;
public static class CompositionTests
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    sealed class Auth:IAuthorityGate,IClimbFrameAuthorization
    {
        public bool Allow=true;
        public RuleResult Validate(CommandContext c){return Allow?RuleResult.Accept():RuleResult.Reject("denied");}
        public RuleResult Validate(CommandContext c,long seq,long gen){return Validate(c);}
    }
    sealed class Pose:IConvexHeadSource
    {public bool TryRead(out ConvexHeadPose p){p=new ConvexHeadPose{Qw=1};return true;}}
    sealed class Scene:IConvexClimbScene
    {
        public bool Block=false,Penetration=false,Unavailable=false;public long Revision{get{return 1;}}
        public bool Penetrating(ConvexHeadQuery q,out bool p){p=Penetration;return !Unavailable;}
        public bool Cast(ConvexHeadQuery q,out bool h,out ClimbQueryHit hit)
        {h=Block;hit=new ClimbQueryHit{Distance=0,Surface=new SurfaceHit{Point=q.Origin,Normal=new Float3(0,-1,0)}};return !Unavailable;}
        public bool Ray(ClimbQuery q,out bool h,out ClimbQueryHit hit)
        {
            h=true;hit=new ClimbQueryHit{Distance=1,Surface=new SurfaceHit{Point=new Float3(q.Origin.X,0,q.Origin.Z),Normal=new Float3(0,1,0),AuthoredClimbable=true}};
            if(q.Motion.Z!=0)hit=new ClimbQueryHit{Distance=1,Surface=new SurfaceHit{Point=new Float3(0,1,1),Normal=new Float3(0,0,-1),AuthoredClimbable=true}};
            return !Unavailable;
        }
        public bool Grab(ClimbQuery q,out bool h,out ClimbQueryHit hit)
        {h=true;hit=new ClimbQueryHit{Distance=0,Surface=new SurfaceHit{Point=q.Origin,Normal=new Float3(0,1,0),AuthoredClimbable=true}};return !Unavailable;}
    }
    // External physical solver boundary only. Real PlayerMotorFeedback computes feedback;
    // real PlayerFlightMotor, FlightMachine, ClimbController and convex solver all execute.
    sealed class PhysicalRoot:IExclusivePlayerMotor
    {
        public int Calls=0;public bool Partial=false;
        public PlayerMotorResult Apply(RootMotionProposal p,float dt)
        {Calls++;var d=p.Translation;if(Partial)d=new Float3(d.X*.5f,d.Y*.5f,d.Z*.5f);return PlayerMotorFeedback.Resolve(p,d,dt,false,false);}
    }
    static MotorFrame Frame(int tick,bool xr)
    {
        return new MotorFrame{Context=new CommandContext("s","a","step"+tick,tick,2,true),Input=new InputFrame{Tick=tick,Sequence=tick,Tracked=true,HeadPosition=new Float3(0,1.75f,0),LeftHandPosition=new Float3(0,1,0),RightHandPosition=new Float3(0,1,0)},
            Environment=new MotionEnvironment{Grounded=true,IsXr=xr},Flight=new FlightFrame{DeltaSeconds=.02f,Forward=new Float3(0,0,1)},Climb=ClimbOptions.Default,
            Root=new Float3(),Ready=true,LifecycleGeneration=1,DesktopForward=new Float3(0,0,1)};
    }
    public static int Main()
    {
        try
        {
            var auth=new Auth();var scene=new Scene();var collision=new ConvexClimbCollision(scene,new Pose(),new CollisionLayers{StructureMask=1<<8,SupportMask=1<<8,HeadMask=1<<8},.70710678f);
            var climb=new ClimbController(collision);var flight=new PlayerFlightMotor("s","a",2,auth,new PlayerTuning(),1);var root=new PhysicalRoot();
            var motor=new ClimbingRootMotor("s","a",2,climb,flight,root,auth,new SourceSupport(collision,.70710678f));ClimbStep step;PlayerMotorResult applied;
            Check(motor.Step(Frame(1,true),out step,out applied).Accepted,"lifecycle initialization consumes one frame");Check(root.Calls==1,"one root call initialization");
            var f=Frame(2,true);f.Input.GripLeft=true;f.Climb.LeftContact=new Float3(0,1,0);
            Check(motor.Step(f,out step,out applied).Accepted&&step.Climbing&&climb.LeftAnchored,"actual controller grabs");
            f=Frame(3,true);f.Input.GripLeft=true;f.Input.LeftHandPosition=new Float3(0,.8f,0);f.Climb.LeftContact=new Float3(0,1,0);
            Check(motor.Step(f,out step,out applied).Accepted&&applied.AppliedDisplacement.Y>.19f,"actual convex pull applied");
            Check(flight.Velocity.Y>9,"actual Player momentum updated by Flight feedback");Check(root.Calls==3,"one root application per step");
            Check(!motor.Step(f,out step,out applied).Accepted&&root.Calls==3,"duplicate frame has no root mutation");
            auth.Allow=false;Check(!motor.Step(Frame(4,true),out step,out applied).Accepted&&climb.LeftAnchored,"auth reject preserves anchor");auth.Allow=true;
            f=Frame(4,true);f.Input.GripLeft=true;f.Reservations.LeftWeapon=true;
            Check(motor.Step(f,out step,out applied).Accepted&&!climb.LeftAnchored&&step.OwnsMotion,"reservation releases claim with final ownership");
            Check(flight.Velocity.Y==0,"final frame has no second gravity");
            f=Frame(5,true);f.Ready=false;Check(motor.Step(f,out step,out applied).Accepted&&!climb.LeftAnchored,"readiness loss cancels");
            f=Frame(6,false);f.LifecycleGeneration=2;f.DesktopE=true;Check(motor.Step(f,out step,out applied).Accepted&&!climb.LeftAnchored,"new lifecycle disarms held E");
            f=Frame(7,false);f.LifecycleGeneration=2;f.DesktopE=true;Check(motor.Step(f,out step,out applied).Accepted&&climb.LeftAnchored,"desktop E uses actual StepDesktop");
            f=Frame(8,false);f.LifecycleGeneration=2;f.DesktopE=true;Check(motor.Step(f,out step,out applied).Accepted&&applied.AppliedDisplacement.Y>.04f,"desktop anchor ascent 2.2m/s");
            scene.Block=true;f=Frame(9,false);f.LifecycleGeneration=2;f.DesktopE=true;Check(motor.Step(f,out step,out applied).Accepted&&applied.AppliedDisplacement.Y==0,"ceiling blocks actual convex pull");scene.Block=false;
            root.Partial=true;f=Frame(10,false);f.LifecycleGeneration=2;f.DesktopE=true;
            Check(motor.Step(f,out step,out applied).Accepted&&!applied.Complete&&!climb.LeftAnchored,"partial physical result cancels anchor");root.Partial=false;
            f=Frame(11,true);f.Context=new CommandContext("other","a","foreign",11,2,true);Check(!motor.Step(f,out step,out applied).Accepted,"foreign session rejects");
            f=Frame(11,true);f.LifecycleGeneration=1;Check(!motor.Step(f,out step,out applied).Accepted,"old lifecycle rejects");
            Float3 completion;scene.Penetration=true;Check(!collision.HeadMotionClear(new Float3(0,1,0),new Float3(0,.1f,0)),"true penetration refuses");scene.Penetration=false;
            scene.Unavailable=true;Check(!new SourceSupport(collision,.70710678f).TryComplete(new Float3(),new Float3(0,1.75f,0),new Float3(),new Float3(),.02f,out completion),"unavailable support fails closed");
            scene.Unavailable=false;Check(new SourceSupport(collision,.70710678f).TryComplete(new Float3(),new Float3(0,1.75f,0),new Float3(),new Float3(),.02f,out completion),"actual support completion query");
            Check(!new SourceSupport(collision,.70710678f).TryComplete(new Float3(0,-.03f,0),new Float3(0,1.72f,0),new Float3(),new Float3(),.02f,out completion),"final support rise over source .025 refused");
            Check(!new SourceSupport(collision,.70710678f).TryComplete(new Float3(0,.04f,0),new Float3(0,1.79f,0),new Float3(),new Float3(),.02f,out completion),"final support drop over .03 refused");
            Check(SourceHeadConvex.Instance.CopyVertices().Length==168,"source hull remains 168 vertices");
            for(int reservation=0;reservation<8;reservation++)
            {
                var cc=new ClimbController(collision);var physical=new PhysicalRoot();
                var composed=new ClimbingRootMotor("s","a",2,cc,new PlayerFlightMotor("s","a",2,auth,new PlayerTuning(),1),physical,auth,new SourceSupport(collision,.70710678f));
                Check(composed.Step(Frame(20,true),out step,out applied).Accepted,"reservation fixture initialized "+reservation);
                var claim=Frame(21,true);claim.Input.GripLeft=claim.Input.GripRight=true;claim.Climb.LeftContact=claim.Climb.RightContact=new Float3(0,1,0);
                Check(composed.Step(claim,out step,out applied).Accepted&&cc.LeftAnchored&&cc.RightAnchored,"actual pair anchors "+reservation);
                claim=Frame(22,true);claim.Input.GripLeft=claim.Input.GripRight=true;
                var r=new GestureReservations();
                if(reservation==0)r.LeftWeapon=true;if(reservation==1)r.RightWeapon=true;
                if(reservation==2)r.LeftInventory=true;if(reservation==3)r.RightInventory=true;
                if(reservation==4)r.LeftUi=true;if(reservation==5)r.RightUi=true;
                if(reservation==6)r.LeftOther=true;if(reservation==7)r.RightOther=true;
                claim.Reservations=r;
                Check(composed.Step(claim,out step,out applied).Accepted&&cc.LeftAnchored==(reservation%2==1)&&cc.RightAnchored==(reservation%2==0),"each reservation releases only its actual hand "+reservation);
                Check(physical.Calls==3&&step.OwnsMotion,"reservation retains single root owner "+reservation);
            }
            Console.WriteLine(checks+" PASS 0 FAIL");return 0;
        }catch(Exception ex){Console.WriteLine("FAIL "+ex);return 1;}
    }
}
