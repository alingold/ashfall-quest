using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Climbing;
sealed class PreparedOwnersTests:IAuthorityGate,IFlightWorld,ICapsuleRouteValidation,IFlightLifecycleAuthorization,IClimbCollision
{
    int count;bool allow=true,clear=true,route=true;
    public RuleResult Validate(CommandContext c){return allow?RuleResult.Accept():RuleResult.Reject("denied");}
    public RuleResult ValidateTransition(CommandContext c,long g,FlightLifecycle reason){return Validate(c);}
    public bool ValidateRoute(string actor,CapsulePlan plan){return route;}
    public bool ClearCapsuleExit(string actor,Float3 at){return clear;}
    public bool ClearPadContact(string actor,Float3 a,Float3 b){return clear;}
    public bool TryGrab(Float3 p,float radius,out SurfaceHit hit){hit=new SurfaceHit{Point=p,Normal=new Float3(0,0,-1),AuthoredClimbable=true};return true;}
    public bool Raycast(Float3 a,Float3 b,QueryKind kind,out SurfaceHit hit){hit=new SurfaceHit();return false;}
    public bool HeadMotionClear(Float3 a,Float3 b){return true;}
    public MotionResult SweepHead(Float3 h,Float3 d,float dt,bool slide){return new MotionResult(d,new Float3(d.X/dt,d.Y/dt,d.Z/dt));}
    static CommandContext C(string id,long tick){return new CommandContext("s","a",id,tick,3,true);}
    static CapsuleEligibility E(){return new CapsuleEligibility{MapId="alpha",Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,TeamCount=1};}
    static CapsulePlan P(){return new CapsulePlan("pod",0,0,new Float3(),new Float3(0,2,0),new Float3(1,2,0),new Float3(1,0,0),new Float3(2,0,0),new Float3(0,0,-1),3);}
    void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static InputFrame I(long seq){return new InputFrame{Sequence=seq,Tick=seq,Tracked=true,GripLeft=true,HeadPosition=new Float3(0,1.75f,0),LeftHandPosition=new Float3(0,1,0)};}
    void Run()
    {
        var f=new CapsuleFleet("s",3,this);PreparedCapsuleUpdate p,q;CapsuleUpdate u;
        route=false;Check(!f.PrepareBegin(C("begin",1),E(),P(),0,this,out p).Accepted&&f.CheckpointReady().Accepted,"route failure has no reservation");route=true;
        Check(f.PrepareBegin(C("begin",1),E(),P(),0,this,out p).Accepted,"prepare begin retry");
        Check(!f.CheckpointReady().Accepted&&!f.PrepareBegin(C("other",2),E(),P(),0,this,out q).Accepted,"pending capture and competing prepare blocked");
        Check(!f.Advance(C("advance",2),1,true,true,false,this,out u).Accepted,"prepare publishes no ride");p.Abort();p.Abort();
        Check(f.CheckpointReady().Accepted&&!p.Validate().Accepted,"abort idempotent releases owner");
        Check(f.PrepareBegin(C("begin",1),E(),P(),0,this,out p).Accepted&&p.Validate().Accepted,"same command pod reusable after abort");allow=false;Check(!p.Validate().Accepted,"retired gate refuses prepared capsule");allow=true;Check(p.Validate().Accepted,"authorization denial preserves capsule retry");p.CommitValidated();p.Abort();
        Check(f.CheckpointReady().Accepted&&!f.Begin(C("begin",1),E(),P(),0,this,out u).Accepted,"committed replay survives abort");
        clear=false;Check(!f.PrepareAdvance(C("exit",2),3,true,true,true,this,out p).Accepted,"blocked exit fails without consuming");clear=true;
        Check(f.PrepareAdvance(C("exit",2),1,true,true,false,this,out p).Accepted,"earlier time retry proves detached clock");p.Abort();
        Check(f.Advance(C("exit",2),.5,true,true,false,this,out u).Accepted,"abort preserves actual replay and last time");
        Check(f.PrepareAdvance(C("advance",3),1,true,true,false,this,out p).Accepted,"prepare actual existing ride");
        Check(f.Advance(C("legacy",4),2,true,true,false,this,out u).Accepted&&!p.Validate().Accepted,"legacy update invalidates reservation");p.Abort();
        allow=false;Check(!f.PrepareResetActor(C("reset",5),1,FlightLifecycle.Respawn,this,out p).Accepted,"reset denial preserved");allow=true;
        Check(f.PrepareResetActor(C("reset",5),1,FlightLifecycle.Respawn,this,out p).Accepted,"prepare reset");p.Abort();
        Check(!f.Begin(C("new",6),E(),P(),0,this,out u).Accepted,"aborted reset retains occupied pod");
        Check(f.PrepareResetActor(C("reset",5),1,FlightLifecycle.Respawn,this,out p).Accepted,"reset retry");p.CommitValidated();
        Check(f.Begin(C("new",6),E(),P(),0,this,out u).Accepted,"committed reset releases actual pod");
        Check(!f.PrepareResetActor(C("reset",7),2,FlightLifecycle.Death,this,out p).Accepted,"lifecycle replay retained across copy");
        var climb=new ClimbController(this);PreparedClimbMotion c,d;var options=ClimbOptions.Default;options.LeftContact=new Float3(0,1,0);
        Check(climb.PrepareStep(I(1),new Float3(),.02f,options,false,false,new Float3(),false,out c).Accepted&&c.Step.Climbing,"actual climb prepare");
        Check(!climb.LeftAnchored&&!climb.CheckpointReady().Accepted,"anchors remain detached; capture blocked");
        Check(!climb.PrepareStep(I(2),new Float3(),.02f,options,false,false,new Float3(),false,out d).Accepted,"second reservation refused");c.Abort();c.Abort();
        Check(climb.CheckpointReady().Accepted&&climb.PrepareStep(I(1),new Float3(),.02f,options,false,false,new Float3(),false,out c).Accepted,"climb abort same sequence retry");c.CommitValidated();c.Abort();
        Check(climb.LeftAnchored&&!climb.PrepareStep(I(1),new Float3(),.02f,options,false,false,new Float3(),false,out c).Accepted,"actual anchor committed with replay");
        Check(climb.PrepareStep(I(2),new Float3(),.02f,options,false,false,new Float3(),true,out c).Accepted,"prepared reset and reacquire");c.Abort();Check(climb.LeftAnchored,"reset abort preserves original arrays");
        Check(climb.PrepareStep(I(2),new Float3(),.02f,options,false,false,new Float3(),false,out c).Accepted,"prepare before legacy reset");climb.Reset();Check(!c.Validate().Accepted,"legacy reset invalidates plan");c.Abort();
        Check(!climb.PrepareStep(I(3),new Float3(),float.NaN,options,false,false,new Float3(),false,out c).Accepted&&climb.CheckpointReady().Accepted,"invalid frame has no publication or reservation");
        // White-box fixture seeds an in-progress actual mantle (not a replacement
        // motion algorithm) to exercise the mutable array's hardest alias boundary.
        climb=new ClimbController(this);var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var path=new[]{new Float3(0,.5f,0)};
        typeof(ClimbController).GetField("route",flags).SetValue(climb,path);
        typeof(ClimbController).GetField("length",flags).SetValue(climb,.5f);
        Check(climb.PrepareStep(I(1),new Float3(),.02f,options,false,false,new Float3(),false,out c).Accepted,"actual in-progress mantle prepared");
        var first=c.Step.RootDisplacement;
        Check(path[0].Y==.5f&&first.Y>0,"mantle route element remains deep isolated");c.Abort();
        Check(climb.PrepareStep(I(1),new Float3(),.02f,options,false,false,new Float3(),false,out c).Accepted&&c.Step.RootDisplacement.Y==first.Y,"mantle abort preserves elapsed segment and route");c.CommitValidated();
        Check(((Float3[])typeof(ClimbController).GetField("route",flags).GetValue(climb))[0].Y<.5f,"committed actual route advances");
        Check(climb.CheckpointReady().Accepted,"committed mantle local capture readiness");
        Console.WriteLine("TOTAL "+count);
    }
    static int Main(){try{new PreparedOwnersTests().Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}

