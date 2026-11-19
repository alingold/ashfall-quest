using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
sealed class R2Tests : IAuthorityGate,IFlightWorld,ICapsuleRouteValidation
{
    bool deny,route=true,clear=true;int pass,fail,gates,queries;
    public RuleResult Validate(CommandContext c){gates++;return deny?RuleResult.Reject("denied"):RuleResult.Accept();}
    public bool ValidateRoute(string a,CapsulePlan p){return route;}
    public bool ClearCapsuleExit(string a,Float3 p){queries++;if(!p.IsFinite)throw new Exception("nonfinite query");return clear;}
    public bool ClearPadContact(string a,Float3 f,Float3 p){queries++;if(!f.IsFinite||!p.IsFinite)throw new Exception("nonfinite query");return clear;}
    static CommandContext C(string a,string id,long tick){return new CommandContext("s",a,id,tick,3,true);}
    static FlightFrame F(){return new FlightFrame{DeltaSeconds=.1f,InputEnabled=true,Tracked=true,Forward=new Float3(0,0,1),Velocity=new Float3(0,-1,0)};}
    FlightMachine M(){return new FlightMachine("s","a",3,this,10,1);}
    static CapsuleEligibility E(){return new CapsuleEligibility{MapId="alpha",Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,Team=0,TeamCount=2};}
    static CapsulePlan P(){return new CapsulePlan("pod",0,0,new Float3(),new Float3(0,10,0),new Float3(10,10,0),new Float3(10,0,0),new Float3(12,0,0),new Float3(0,0,-1),3);}
    void Check(bool b,string name){Console.WriteLine((b?"PASS ":"FAIL ")+name);if(b)pass++;else fail++;}
    void Run()
    {
        CapsuleUpdate u;
        for(int kind=0;kind<5;kind++)
        {
            var fleet=new CapsuleFleet("s",3,this);bool rejected=true;int before=gates;
            deny=kind==0;route=kind!=2;
            for(int i=0;i<600;i++)
            {
                var e=E();if(kind==3)e.Indoor=true;
                rejected &= !fleet.Begin(C("bad"+i,kind==4?"":"x",kind==1?-1:1000),e,P(),0,this,out u).Accepted;
            }
            Check(rejected,"600 rejected admissions variant "+kind);
            if(kind==0||kind==2)Check(gates-before==600,"one gate call per prospective actor variant "+kind);
            else Check(gates==before,"invalid admission never calls authority gate variant "+kind);
            deny=false;route=true;
            Check(fleet.Begin(C("good","ok",1),E(),P(),0,this,out u).Accepted,"rejections leave fleet capacity variant "+kind);
        }
        var retryFleet=new CapsuleFleet("s",3,this);var ctx=C("a","same",1);route=false;
        Check(!retryFleet.Begin(ctx,E(),P(),0,this,out u).Accepted,"route rejects before reservation");route=true;
        Check(retryFleet.Begin(ctx,E(),P(),0,this,out u).Accepted,"route rejection leaves command reusable");
        ctx=C("a","exit",2);clear=false;
        Check(!retryFleet.Advance(ctx,2,true,true,true,this,out u).Accepted&&u.State==CapsuleState.None,"blocked exit no relocation");clear=true;
        Check(retryFleet.Advance(ctx,1,true,true,false,this,out u).Accepted,"blocked exit preserves tick command time and ride");
        Check(!retryFleet.Advance(ctx,2,true,true,false,this,out u).Accepted,"successful retry remains replay protected");
        var full=new CapsuleFleet("s",3,this);bool admitted=true;
        for(int i=0;i<256;i++)
        {
            var p=new CapsulePlan("p"+i,0,0,new Float3(),new Float3(),new Float3(),new Float3(),new Float3(),new Float3(0,0,-1),3);
            admitted &= full.Begin(C("owner"+i,"x",1),E(),p,0,this,out u).Accepted;
        }
        Check(admitted&&!full.Begin(C("overflow","x",1),E(),P(),0,this,out u).Accepted,"256 accepted owners retain bounded capacity");
        var m=M();m.Launch(C("a","seed",0),new Float3(1,1,0),45,true,false,false);Float3 old=m.Velocity;
        var f=F();f.Velocity=new Float3(float.MaxValue,-1,0);f.WalkTarget=new Float3(-float.MaxValue,0,0);int calls=gates;
        ctx=C("a","overflow",1000);
        Check(!m.Step(ctx,f).Accepted&&m.Velocity.X==old.X&&m.Mode==FlightMode.Launch&&gates==calls,"review overflow rejects without state or gate mutation");
        f=F();f.Velocity=old;Check(m.Step(C("a","overflow",1),f).Accepted&&m.Mode==FlightMode.Launch&&m.Velocity.X==old.X,"overflow rejection preserves fence and launch clearance");
        m=M();f=F();f.Velocity=new Float3(0,-10000,0);ctx=C("a","derived",99);calls=gates;
        // FLIGHT-DIVE r12: quadratic drag can no longer push a bound-edge descent past the derived bound.
        Check(m.Step(ctx,f).Accepted&&m.Velocity.Y> -10000&&m.Velocity.Y< -R26FreefallTuning.BellyTerminalSpeed&&gates==calls+1,"bound-edge descent decelerates inside the derived bound");
        f=F();Check(!m.Step(C("a","derived",100),f).Accepted&&m.Step(C("a","derived-next",100),f).Accepted,"accepted bound-edge step commits its command id");
        for(int i=0;i<6;i++)
        {
            m=M();f=F();switch(i){case 0:f.Velocity=new Float3(10001,0,0);break;case 1:f.WalkTarget=new Float3(1001,0,0);break;case 2:f.Forward=new Float3(3,0,0);break;case 3:f.MoveDirection=new Float3(0,float.MaxValue,0);break;case 4:f.LeftHand=new Float3(11,0,0);break;default:f.RightHand=new Float3(float.NaN,0,0);break;}
            Check(!m.Step(C("a","bad",1),f).Accepted&&m.Velocity.IsFinite,"frame component envelope "+i);
        }
        m=M();f=F();f.Supported=true;f.Velocity=new Float3(10000,-10000,10000);f.WalkTarget=new Float3(-1000,1000,1000);f.Forward=new Float3(2,2,2);f.MoveDirection=new Float3(-2,2,2);f.LeftHand=new Float3(10,10,10);f.RightHand=new Float3(-10,-10,-10);
        Check(m.Step(C("a","edge",1),f).Accepted&&m.Velocity.IsFinite,"inclusive frame boundary accepted with no gravity on supported frame");
        m=M();f=F();f.Velocity=new Float3(10000,-1,10000);f.WalkTarget=new Float3(-1000,0,-1000);
        Check(m.Step(C("a","toward",1),f).Accepted&&m.Velocity.X<10000&&m.Velocity.X>9999,"opposite bounded endpoints steer finitely");
        var pads=new PadContacts();var pad=new LaunchPad("tilted",new Float3(),new Float3(1,1,0),1,45);m=M();clear=false;ctx=C("a","pad",1);
        Check(!pads.TryLaunch(ctx,m,pad,this,new Float3(),new Float3(),false,100,true,false,false,false).Accepted,"occluded tilted pad rejected");clear=true;
        Check(pads.TryLaunch(ctx,m,pad,this,new Float3(),new Float3(),false,0,true,false,false,false).Accepted&&Math.Abs(m.Velocity.X-31.8198f)<.001,"rejected pad preserves clock cooldown command and tilted direction");
        Check(pads.TryLaunch(C("a","pad2",2),m,pad,this,new Float3(),new Float3(),false,1.2,true,false,false,false).Accepted,"tilted pad repeat at exact cooldown");
        calls=queries;Check(!pads.TryLaunch(C("a","bad",3),m,pad,this,new Float3(float.MaxValue,0,0),new Float3(),false,3,true,false,false,false).Accepted&&queries==calls,"huge feet rejected before world query");
        bool bad=false;try{new LaunchPad("bad",new Float3(float.MaxValue,0,0),new Float3(0,1,0),1,45);}catch(ArgumentException){bad=true;}Check(bad,"huge pad center rejected");
        bad=false;try{new CapsulePlan("bad",0,0,new Float3(float.MaxValue,0,0),new Float3(float.MaxValue,0,0),new Float3(float.MaxValue,0,0),new Float3(float.MaxValue,0,0),new Float3(),new Float3(0,0,-1),3);}catch(ArgumentException){bad=true;}Check(bad,"huge constant capsule route rejected");
        var indoor=E();indoor.Indoor=true;Check(!CapsuleFleet.AllowsCapsules(indoor),"explicit indoor gate");
        indoor=E();indoor.MapId="archive:battlefield-paintball-gods-pitdevil";Check(!CapsuleFleet.AllowsCapsules(indoor),"first Paintball gate");
        indoor.MapId="archive:skb-paintball-gods-skblong";Check(!CapsuleFleet.AllowsCapsules(indoor),"second Paintball gate");
        Console.WriteLine("TOTAL "+pass+" PASS / "+fail+" FAIL");
    }
    static int Main(){var t=new R2Tests();try{t.Run();return t.fail==0?0:1;}catch(Exception e){Console.WriteLine(e);return 2;}}
}
