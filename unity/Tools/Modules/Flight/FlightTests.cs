using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
sealed class FlightTests : IAuthorityGate,IFlightWorld,ICapsuleRouteValidation
{
    bool clear=true,authority=true;int tests;long tick; string actor="a";
    public RuleResult Validate(CommandContext c){return authority?RuleResult.Accept():RuleResult.Reject("host denied");}
    public bool ClearPadContact(string id,Float3 f,Float3 p){return clear;}
    public bool ClearCapsuleExit(string id,Float3 p){return clear;}
    public bool ValidateRoute(string id,CapsulePlan p){return clear;}
    CommandContext C(){tick++;return new CommandContext("s",actor,"c"+tick,tick,3,true);}
    void Check(bool v,string name){if(!v)throw new Exception(name);tests++;Console.WriteLine("PASS "+name);}
    void Near(float a,float b){if(Math.Abs(a-b)>.001)throw new Exception("expected "+b+" got "+a);}
    FlightMachine Machine(){return new FlightMachine("s",actor,3,this,10,1);}
    FlightFrame Frame(){return new FlightFrame{DeltaSeconds=.1f,InputEnabled=true,Tracked=true,Forward=new Float3(0,0,1),Velocity=new Float3(0,-1,0)};}
    CapsulePlan Plan(string id){return new CapsulePlan(id,0,11,new Float3(),new Float3(0,10,0),new Float3(10,10,0),new Float3(10,0,0),new Float3(12,.3f,0),new Float3(0,0,-1),3);}
    CapsuleEligibility Eligibility(){return new CapsuleEligibility{MapId="archive:alpha",Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,Team=0,TeamCount=2};}
    void Run()
    {
        FlightMachine m=Machine();FlightFrame f=Frame();f.DesktopGlide=true;
        Check(m.Step(C(),f).Accepted&&m.Mode==FlightMode.Glide,"air deploy");Near(m.Velocity.Z,18.46154f);Near(m.Velocity.Y,-2.637363f);
        f.Supported=true;Check(m.Step(C(),f).Accepted&&m.Mode==FlightMode.Ground,"landing cancels glide");
        f.Supported=false;f.XR=true;f.HandsReady=true;f.LeftHand=new Float3(-.6f,.4f,0);f.RightHand=new Float3(.6f,0,0);
        Check(m.Step(C(),f).Accepted&&m.Velocity.X>0,"reflected hand bank turns right");
        f.LeftHand=new Float3(-.3f,0,0);f.RightHand=new Float3(.3f,0,0);m.Step(C(),f);Check(m.Mode==FlightMode.Air,"arms below spread threshold retract");
        f=Frame();m=Machine();for(int i=0;i<9;i++){f.Velocity=m.Velocity.Y<0?m.Velocity:f.Velocity;m.Step(C(),f);}Check(m.Mode==FlightMode.Air,"dive unavailable before one second");
        f.DiveForward=true;f.DesktopGlide=true;f.Velocity=m.Velocity;m.Step(C(),f);Check(m.Mode==FlightMode.Dive,"forward fall becomes dive over glide");
        for(int i=0;i<200;i++){f.Velocity=m.Velocity;m.Step(C(),f);}Check(m.Velocity.Y<=-150,"dive maintains accelerating gravity beyond target");
        f.InventoryOpen=true;f.Velocity=m.Velocity;m.Step(C(),f);Check(m.Mode==FlightMode.Air,"inventory prevents dive and glide");
        f.Tracked=false;Check(m.Step(C(),f).Accepted&&m.Mode==FlightMode.Suspended,"tracking loss releases flight");
        f=Frame();f.DeltaSeconds=float.NaN;Check(!m.Step(C(),f).Accepted,"invalid delta rejected");
        m=Machine();Check(m.Launch(C(),new Float3(1,1,0),45,true,false,false).Accepted,"tilted pad launch");Near(m.Velocity.X,31.8198f);Near(m.Velocity.Y,31.8198f);
        f=Frame();f.DesktopGlide=true;f.Velocity=m.Velocity;m.Step(C(),f);Check(m.Mode==FlightMode.Launch,"glide suppressed during face clearance");Near(m.Velocity.X,31.8198f);Near(m.Velocity.Y,31.8198f);
        for(int i=0;i<3;i++){f.Velocity=m.Velocity;m.Step(C(),f);}Check(m.Mode==FlightMode.Glide,"glide interrupts ascent after clearance");
        Float3 before=m.Velocity;authority=false;Check(!m.Launch(C(),new Float3(0,1,0),45,true,false,false).Accepted,"host denial");Near(m.Velocity.Y,before.Y);authority=true;
        CommandContext same=C();Check(m.Launch(same,new Float3(0,1,0),45,true,false,false).Accepted&&!m.Launch(same,new Float3(1,0,0),45,true,false,false).Accepted,"launch replay cannot redirect");
        Check(!m.Launch(new CommandContext("s","intruder","x",tick+1,3,true),new Float3(0,1,0),45,true,false,false).Accepted,"wrong actor rejected");
        Check(!m.Launch(new CommandContext("s",actor,"x",tick+1,2,true),new Float3(0,1,0),45,true,false,false).Accepted,"old epoch rejected");
        Check(!m.Launch(new CommandContext("s",actor,"stale",0,3,true),new Float3(0,1,0),45,true,false,false).Accepted,"stale command cannot rewind flight");
        Check(!m.Launch(C(),new Float3(float.MaxValue,1,0),45,true,false,false).Accepted,"overflow normal rejected");
        var pads=new PadContacts();var p=new LaunchPad("tilt",new Float3(),new Float3(1,0,0),1,45);m=Machine();
        Check(pads.TryLaunch(C(),m,p,this,new Float3(2,0,0),new Float3(-2,0,0),true,0,true,false,false,false).Accepted,"swept sideways pad contact");Near(m.Velocity.X,45);Near(m.Velocity.Y,0);
        Check(!pads.TryLaunch(C(),m,p,this,new Float3(),new Float3(),false,.5,true,false,false,false).Accepted,"pad repeat cooldown");
        Check(pads.TryLaunch(C(),m,p,this,new Float3(),new Float3(),false,1.2,true,false,false,false).Accepted,"pad reusable after cooldown");
        actor="b";var other=Machine();Check(new PadContacts().TryLaunch(C(),other,p,this,new Float3(),new Float3(),false,1.2,true,false,false,false).Accepted,"cooldown owned per actor");actor="a";
        clear=false;Check(!pads.TryLaunch(C(),m,p,this,new Float3(),new Float3(),false,3,true,false,false,false).Accepted,"wall blocks pad");clear=true;
        Check(!pads.TryLaunch(C(),m,p,this,new Float3(100,0,0),new Float3(-100,0,0),true,3,true,false,false,false).Accepted,"teleport does not sweep pads");
        Check(!pads.TryLaunch(C(),m,p,this,new Float3(),new Float3(),false,3,true,false,false,true).Accepted,"ghost cannot launch");
        var fleet=new CapsuleFleet("s",3,this);var e=Eligibility();CapsuleUpdate u;
        e.MapId="archive:battlefield-paintball-gods-pitdevil";Check(!fleet.Begin(C(),e,Plan("p"),0,this,out u).Accepted,"paintball platform prohibited");
        e=Eligibility();e.Indoor=true;Check(!fleet.Begin(C(),e,Plan("p"),0,this,out u).Accepted,"all indoor capsules prohibited");e=Eligibility();
        Check(fleet.Begin(C(),e,Plan("p"),0,this,out u).Accepted&&u.State==CapsuleState.Flying,"twelve-slot team capsule begin");
        actor="b";Check(!fleet.Begin(C(),e,Plan("p"),0,this,out u).Accepted,"pod reserved exclusively");actor="a";
        Check(fleet.Advance(C(),1,true,true,false,this,out u).Accepted,"capsule advances");Near(u.RootPosition.Y,10);Near(u.RootPosition.X,0);
        Check(fleet.Advance(C(),1.5,true,true,false,this,out u).Accepted,"three-leg constant speed path");Near(u.RootPosition.X,5);Near(u.RootPosition.Y,10);
        clear=false;Check(!fleet.Advance(C(),2,true,true,true,this,out u).Accepted,"blocked ejection fails closed");clear=true;
        Check(fleet.Advance(C(),2,true,true,true,this,out u).Accepted&&u.State==CapsuleState.Released,"eject rear outside cabin");Near(u.RootPosition.X,10);Near(u.RootPosition.Y,10);Near(u.RootPosition.Z,-2);
        Check(!fleet.Advance(C(),2.1,true,true,true,this,out u).Accepted,"eject idempotence");
        actor="b";Check(fleet.Begin(C(),e,Plan("p2"),0,this,out u).Accepted,"second actor capsule");
        Check(fleet.Advance(C(),3,true,true,false,this,out u).Accepted&&u.State==CapsuleState.Landed,"arrival uses validated ground exit");Near(u.RootPosition.X,12);Near(u.RootPosition.Y,.3f);
        actor="c";fleet.Begin(C(),e,Plan("p3"),0,this,out u);Check(fleet.Advance(C(),.5,true,false,false,this,out u).Accepted&&u.State==CapsuleState.Released,"death releases passenger");
        bool bad=false;try{new CapsulePlan("bad",0,0,new Float3(),new Float3(1000,0,0),new Float3(),new Float3(),new Float3(),new Float3(0,0,-1),3);}catch(ArgumentException){bad=true;}Check(bad,"overspeed route rejected");
        actor="d";clear=false;Check(!fleet.Begin(C(),e,Plan("p4"),0,this,out u).Accepted,"host route validation required");clear=true;
        Check(fleet.Begin(C(),e,Plan("p4"),0,this,out u).Accepted,"denied route does not reserve capsule");
        Check(fleet.Advance(C(),1,true,true,false,this,out u).Accepted&&!fleet.Advance(C(),.5,true,true,false,this,out u).Accepted,"capsule clock cannot rewind");
        CommandContext replay=C();Check(fleet.Advance(replay,1.1,true,true,false,this,out u).Accepted&&!fleet.Advance(replay,1.2,true,true,false,this,out u).Accepted,"capsule replay rejected");
        Console.WriteLine("TOTAL "+tests+" scenario checks PASS");
    }
    static int Main(){try{new FlightTests().Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}


