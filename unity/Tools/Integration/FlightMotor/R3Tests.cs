using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
sealed class R3Tests : IAuthorityGate,IFlightWorld,ICapsuleRouteValidation,IFlightLifecycleAuthorization
{
    bool allow=true,life=true,clear=true;int pass;long n;
    public RuleResult Validate(CommandContext c){return allow?RuleResult.Accept():RuleResult.Reject("denied");}
    public RuleResult ValidateTransition(CommandContext c,long g,FlightLifecycle r){return life?RuleResult.Accept():RuleResult.Reject("unbound event");}
    public bool ClearPadContact(string p,Float3 a,Float3 b){return clear;}
    public bool ClearCapsuleExit(string a,Float3 p){return clear;}
    public bool ValidateRoute(string a,CapsulePlan p){return true;}
    CommandContext C(string a){return new CommandContext("s",a,"cmd"+(++n),n,3,true);}
    static CommandContext X(string a,string id,long t,long epoch,bool host){return new CommandContext("s",a,id,t,epoch,host);}
    void Check(bool b,string name){if(!b)throw new Exception(name);Console.WriteLine("PASS "+name);pass++;}
    void Near(float a,float b,string s){Check(Math.Abs(a-b)<.001f,s);}
    static CapsuleEligibility E(){return new CapsuleEligibility{MapId="alpha",Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,Team=0,TeamCount=2};}
    static CapsulePlan P(string id){return new CapsulePlan(id,0,0,new Float3(),new Float3(0,10,0),new Float3(10,10,0),new Float3(10,0,0),new Float3(12,0,0),new Float3(0,0,-1),3);}
    void Run()
    {
        foreach(FlightLifecycle reason in Enum.GetValues(typeof(FlightLifecycle)))
        {
            var f=new CapsuleFleet("s",3,this);CapsuleUpdate u;
            var original=C("a");Check(f.Begin(original,E(),P("pa"),0,this,out u).Accepted,"begin a "+reason);
            Check(f.Begin(C("b"),E(),P("pb"),0,this,out u).Accepted,"simultaneous b "+reason);
            var reset=C("a");life=false;
            Check(!f.ResetActor(reset,1,reason,this).Accepted,"unbound lifecycle denied "+reason);life=true;
            Check(!f.ResetActor(X("a","bad",reset.Tick,3,false),1,reason,this).Accepted,"client reset denied "+reason);
            Check(!f.ResetActor(X("a","bad",reset.Tick,2,true),1,reason,this).Accepted,"old epoch reset denied "+reason);
            Check(f.ResetActor(reset,1,reason,this).Accepted,"authenticated cleanup "+reason);
            Check(!f.ResetActor(reset,2,reason,this).Accepted,"reset command replay "+reason);
            Check(!f.Begin(X("a",reset.CommandId,reset.Tick+1,3,true),E(),P("pa"),0,this,out u).Accepted,"lifecycle ID cannot rebind to Begin "+reason);
            Check(!f.ResetActor(X("a",original.CommandId,reset.Tick+1,3,true),2,reason,this).Accepted,"Begin ID cannot rebind to reset "+reason);
            Check(!f.ResetActor(C("a"),1,reason,this).Accepted,"generation replay "+reason);
            Check(f.Advance(C("b"),1,true,true,false,this,out u).Accepted&&u.State==CapsuleState.Flying,"other ride preserved "+reason);
            Check(!f.Begin(C("c"),E(),P("pb"),0,this,out u).Accepted,"other reservation preserved "+reason);
            Check(!f.Begin(original,E(),P("pa"),0,this,out u).Accepted,"prelife command rejected "+reason);
            if(reason!=FlightLifecycle.Respawn)
            {
                Check(!f.Begin(C("a"),E(),P("pa"),0,this,out u).Accepted,"inactive cannot redeploy "+reason);
                Check(f.ResetActor(C("a"),2,FlightLifecycle.Respawn,this).Accepted,"respawn reactivates "+reason);
            }
            Check(f.Begin(C("a"),E(),P("pa"),0,this,out u).Accepted,"same pod redeployment "+reason);
            clear=false;var exit=C("a");Check(!f.Advance(exit,1,true,false,false,this,out u).Accepted,"blocked exit retained "+reason);clear=true;
            Check(f.Advance(exit,1,true,false,false,this,out u).Accepted&&u.State==CapsuleState.Released,"death exit retry "+reason);
            Check(f.ResetActor(C("a"),3,FlightLifecycle.Respawn,this).Accepted,"cleanup terminal ride "+reason);
            Check(f.Begin(C("a"),E(),P("new"),0,this,out u).Accepted,"different pod redeploy "+reason);
        }
        var saturated=new CapsuleFleet("s",3,this);CapsuleUpdate update;
        Check(saturated.Begin(X("a","begin",100,3,true),E(),P("sat"),0,this,out update).Accepted,"saturation begin");
        for(int i=0;i<4095;i++)if(!saturated.Advance(X("a","fill"+i,100,3,true),0,true,true,false,this,out update).Accepted)throw new Exception("saturation setup");
        Check(!saturated.Advance(X("a","full",100,3,true),0,true,true,false,this,out update).Accepted,"ordinary window saturated");
        Check(saturated.ResetActor(X("a","cleanup",101,3,true),1,FlightLifecycle.Respawn,this).Accepted,"lifecycle independent of saturated commands");
        Check(!saturated.ResetActor(X("a","old-life",99,3,true),2,FlightLifecycle.Respawn,this).Accepted,"cleanup tick cannot rewind");
        Check(!saturated.Begin(X("a","begin",102,3,true),E(),P("sat"),0,this,out update).Accepted,"cleanup retains ordinary replay");
        Check(saturated.Begin(X("a","newlife",400,3,true),E(),P("sat"),0,this,out update).Accepted,"later bounded window recovery");
        var nextEpoch=new CapsuleFleet("s",4,this);
        Check(!nextEpoch.Begin(X("a","epoch",1,3,true),E(),P("sat"),0,this,out update).Accepted,"new fleet rejects prior epoch");
        Check(nextEpoch.Begin(X("a","epoch",1,4,true),E(),P("sat"),0,this,out update).Accepted,"new epoch accepts current scope");
        var motor=new PlayerFlightMotor("s","m",3,this,new PlayerTuning(),1);
        var env=new MotionEnvironment();var obs=new FlightFrame{DeltaSeconds=.1f,InputEnabled=true,Forward=new Float3(0,0,1)};MotionStep motion;
        var c=C("m");var input=new InputFrame{Tick=c.Tick,Sequence=1,Tracked=true};
        Check(motor.Step(c,input,env,obs,out motion).Accepted&&motion.OwnsMotion,"air owns one returned movement");
        Near(motion.Velocity.Y,R26Freefall.Fall(0,0,.1f),"single gravity in air");Near(motion.Displacement.Y,R26Freefall.Fall(0,0,.1f)*.1f,"single displacement");
        Check(!motor.Step(c,input,env,obs,out motion).Accepted,"composed replay denied");
        var pad=new LaunchPad("p",new Float3(),new Float3(1,1,0),1,45);
        Check(motor.LaunchPad(C("m"),pad,this,new Float3(),new Float3(),false,0,true,false,false,false).Accepted,"composed tilted launch");
        float launchY=motor.Velocity.Y;
        for(int i=0;i<2;i++)
        {
            c=C("m");input.Tick=c.Tick;input.Sequence++;obs.DesktopGlide=true;env.Grounded=true;
            Check(motor.Step(c,input,env,obs,out motion).Accepted&&motor.Mode==FlightMode.Launch,"pad overrides lingering grounded contact");
            Near(motor.Velocity.Y,launchY,"pad clearance no extra gravity");
        }
        for(int i=0;i<2;i++){c=C("m");input.Tick=c.Tick;input.Sequence++;env.Grounded=false;Check(motor.Step(c,input,env,obs,out motion).Accepted,"clearance progression");}
        Check(motor.Mode==FlightMode.Glide,"composed glide after clearance");
        Near(motor.Velocity.Y,-PlayerTuning.WalkSpeed*2.5f*(12f/6.5f)/7,"calibration applied once");
        obs.DesktopGlide=false;obs.DiveForward=true;
        for(int i=0;i<12;i++){c=C("m");input.Tick=c.Tick;input.Sequence++;Check(motor.Step(c,input,env,obs,out motion).Accepted,"dive progression "+i);}
        Check(motor.Mode==FlightMode.Dive,"composed dive");
        env.Grounded=true;c=C("m");input.Tick=c.Tick;input.Sequence++;
        Check(motor.Step(c,input,env,obs,out motion).Accepted&&motor.Mode==FlightMode.Ground,"landing hands back Player ownership");
        Near(motor.Velocity.Y,-1.62f,"landing Player gravity once");
        c=C("m");input.Tick=c.Tick;input.Sequence++;input.Tracked=false;
        Check(motor.Step(c,input,env,obs,out motion).Accepted&&!motion.OwnsMotion,"tracking loss no translation");
        Near(motor.Velocity.Y,0,"tracking loss clears player velocity");
        Check(motor.ResetActor(C("m"),1,FlightLifecycle.Disconnect,this).Accepted,"motor disconnect reset");
        Check(!motor.LaunchPad(C("m"),pad,this,new Float3(),new Float3(),false,3,true,false,false,false).Accepted,"disconnected pad denied");
        Check(motor.ResetActor(C("m"),2,FlightLifecycle.Respawn,this).Accepted,"motor next life");
        Check(motor.LaunchPad(C("m"),pad,this,new Float3(),new Float3(),false,3,true,false,false,false).Accepted,"motor pad next life");
        var other=new PlayerFlightMotor("s","other",3,this,new PlayerTuning(),1);
        Check(other.LaunchPad(C("other"),pad,this,new Float3(),new Float3(),false,0,true,false,false,false).Accepted,"second motor independent");
        Near(other.Velocity.Y,launchY,"other motor launch unaffected");
        Console.WriteLine("TOTAL "+pass+" PASS");
    }
    static int Main(){try{new R3Tests().Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}

