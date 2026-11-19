using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Modules.Flight;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable.Activities;
class Mechanics
{
    static void Check(bool okay,string label){if(!okay)throw new Exception(label);Console.WriteLine("PASS "+label);}
    sealed class World:IConstructionWorld
    {
        public bool Ground=true;
        public bool HasStaticContact(Panel p,float tolerance){return Ground&&p.Center.Y-CandidateGeometry.HalfSize(p).Y<=.05f;}
        public bool IsBuried(Panel p){return false;}
        public string Clearance(Panel p,string actor){return "";}
        public bool HasLineOfSight(string actor,Float3 from,Float3 to){return true;}
    }
    sealed class Gate:IAuthorityGate,IFlightCollisionAuthorization
    {
        public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
        public RuleResult ValidateCollision(CommandContext c,FlightMotionReceipt receipt,FlightCollisionResult result){return RuleResult.Accept();}
    }
    static CommandContext C(long tick,string id){return new CommandContext("r23-playable","player",id,tick,0,true);}
    static void Main()
    {
        CheckTravelHeading();
        var gesture=new R23BuildGesture();
        gesture.Step(false,true,true,true,false,false,true);Check(!gesture.Active,"held startup trigger cannot place");
        gesture.Step(false,false,true,true,false,false,true);
        gesture.Step(false,true,true,true,false,false,true);Check(gesture.Active&&gesture.Hand==2,"right trigger begins preview");
        gesture.Step(true,true,true,true,false,false,true);Check(gesture.Hand==2,"preview remains pinned to hand");
        gesture.Step(true,false,true,true,false,false,true);Check(gesture.Commit&&!gesture.Active,"release commits once");
        gesture.Step(false,false,true,true,false,false,true);Check(!gesture.Commit,"release cannot repeat commit");
        gesture.Step(true,false,true,false,false,false,true);Check(gesture.Hand==1,"free left hand builds with weapon hand unavailable");
        gesture.Step(false,false,false,true,false,false,true);Check(!gesture.Commit&&!gesture.Active,"lost hand reservation cancels instead of placing");
        gesture.Step(false,false,true,true,false,false,true);
        gesture.Step(false,true,true,true,false,false,true);
        gesture.Step(false,false,true,true,false,false,false);Check(!gesture.Commit,"tracking loss cancels release");
        var construction=new R23ConstructionSession(new World());
        var pose=new BuilderState(new Float3(1,1.3f,.5f),new Float3(0,0,1),new Float3(1,1.6f,.5f),true,true);
        PlacedPanel[] plan;int cost;
        var result=construction.Evaluate(pose,1,false,out plan,out cost);
        Check(result.Accepted&&cost>0&&construction.Balance==999&&construction.Snapshot().Length==0,"source grid preview validates without spending");
        result=construction.Evaluate(pose,2,true,out plan,out cost);
        Check(result.Accepted&&construction.Balance==999-cost&&construction.Snapshot().Length==plan.Length,"release placement debits exact source panel cost");
        var practiceConstruction=new R23ConstructionSession(new World(),true);
        result=practiceConstruction.Evaluate(pose,1,true,out plan,out cost);
        Check(result.Accepted&&cost==0&&practiceConstruction.Balance==999&&practiceConstruction.Snapshot().Length==plan.Length&&plan.Length>0,"source infinite builds still place through real service without debiting finite supply");
        CheckConstructionDamage();
        var gate=new Gate();var flight=new FlightMachine("r23-playable","player",0,gate,6.5f,1);
        var f=new FlightFrame{DeltaSeconds=.02f,InputEnabled=true,Tracked=true,XR=true,HandsReady=true,Forward=new Float3(0,0,1),LeftHand=new Float3(-.6f,1,0),RightHand=new Float3(.6f,1,0)};
        Check(flight.Step(C(1,"glide"),f).Accepted&&flight.Mode==FlightMode.Glide&&Math.Abs(flight.Velocity.Z-12)<.001f&&flight.Velocity.Y<0,"source spread starts 12m/s descending glide");
        f.LeftHand=new Float3(-.6f,1.4f,0);flight.Step(C(2,"bank"),f);Check(flight.Velocity.X>0,"source height difference banks right");
        var receipt=flight.PendingMotion;Check(flight.ApplyCollisionResult(C(2,"collision"),receipt,new FlightCollisionResult{Grounded=true},gate).Accepted&&flight.Mode==FlightMode.Ground,"accepted collision stops flight at floor");
        f.HandsReady=false;f.DeltaSeconds=.16f;f.Velocity=new Float3(2,-1,0);flight.Step(C(3,"fall"),f);Check(flight.Mode==FlightMode.Air&&flight.Velocity.Y< -1,"unready hands cannot glide and use source gravity");
        f.DeltaSeconds=.02f;f.Climbing=true;flight.Step(C(4,"climb"),f);Check(flight.Mode==FlightMode.Suspended,"climbing keeps sole root motion authority");
        f.Climbing=false;f.DiveForward=true;f.Velocity=new Float3(0,-2,0);for(int i=5;i<=60;i++)flight.Step(C(i,"dive"+i),f);
        Check(flight.Mode==FlightMode.Dive,"one second fall plus forward enters source dive");
        flight=new FlightMachine("r23-playable","player",0,gate,6.5f,1);
        Check(flight.Launch(C(1,"horizontal-pad"),new Float3(0,0,1),45,true,false,false).Accepted,"authored horizontal pad publishes45m/s without invented upward bias");
        f=new FlightFrame{DeltaSeconds=.05f,InputEnabled=true,Tracked=true,XR=true,Forward=new Float3(0,0,1)};
        for(int i=2;i<=5;i++)
        {
            f.Velocity=flight.Velocity;Check(flight.Step(C(i,"clearance"+i),f).Accepted&&flight.Mode==FlightMode.Launch&&Math.Abs(flight.Velocity.Z-45)<.001f&&flight.Velocity.Y==0,"source pad keeps straight clearance at"+((i-1)*.05f)+" seconds");
            var contact=flight.PendingMotion;Check(flight.ApplyCollisionResult(C(i,"ack"+i),contact,new FlightCollisionResult{Velocity=flight.Velocity},gate).Accepted,"horizontal physical feedback preserves source launch at"+i);
        }
        for(int i=6;i<12;i++){f.Velocity=flight.Velocity;flight.Step(C(i,"after-clearance"+i),f);flight.ApplyCollisionResult(C(i,"after-ack"+i),flight.PendingMotion,new FlightCollisionResult{Velocity=flight.Velocity},gate);}
        Check(flight.Mode==FlightMode.Air&&flight.Velocity.Y<0&&flight.Velocity.Z<45,"horizontal launch resumes actual gravity and air drag after source300ms");
        Console.WriteLine("Managed source mechanics checked; actual Unity engine/device execution remains separate.");
    }
    static BuilderState WallAt(int level)
    {float y=level*CandidateGeometry.Side;return new BuilderState(new Float3(1,y+1.3f,.5f),new Float3(0,0,1),new Float3(1,y+1.6f,.5f),true,true);}
    static string Place(R23ConstructionSession session,int level,long tick)
    {
        PlacedPanel[] next;int cost;var before=session.Snapshot();var result=session.Evaluate(WallAt(level),tick,true,out next,out cost);
        Check(result.Accepted&&cost==1,"stack level"+level+" placed through actual CandidateService");
        foreach(var p in session.Snapshot()){bool old=false;foreach(var b in before)if(b.Id==p.Id)old=true;if(!old)return p.Id;}throw new Exception("No new real panel");
    }
    static void CheckConstructionDamage()
    {
        var world=new World();var session=new R23ConstructionSession(world);string lower=Place(session,0,1),upper=Place(session,1,2),top=Place(session,2,3);int balance=session.Balance;
        Check(session.Snapshot().Length==3&&session.Snapshot()[0].Health==180,"actual wall max180 from Candidate snapshot");
        Check(session.Damage(lower,25,3).Accepted&&session.Snapshot()[0].Health==155&&session.Balance==balance,"real25 damage updates owner snapshot only; no charge change");
        Check(!session.Damage(lower,float.NaN,3).Accepted&&!session.Damage(lower,-1,3).Accepted&&!session.Damage("foreign",10,3).Accepted&&session.Snapshot()[0].Health==155,"invalid and foreign damage inert");
        Check(session.Damage(lower,155,4).Accepted&&session.Snapshot().Length==2&&session.Balance==balance,"lethal anchor damage withdraws only actual owner panel; no refund");
        Check(!session.Damage(lower,10,4).Accepted,"withdrawn panel id cannot take damage twice");
        Check(session.Advance(.29f,false).Accepted&&session.Snapshot().Length==2,"dependent walls remain before source300ms");
        Check(session.Advance(100,true).Accepted&&session.Snapshot().Length==2,"paused100 seconds cannot advance collapse clock");
        Check(session.Advance(.01f,false).Accepted&&session.Snapshot().Length==1&&session.Snapshot()[0].Id==top,"first dependent collapses at source300ms; upper branch retains600ms deadline");
        Check(session.Advance(.30f,false).Accepted&&session.Snapshot().Length==0&&session.Balance==balance,"second support depth collapses at600ms without refund");
        string replacement=Place(session,0,5);Check(replacement!=lower&&session.Balance==balance-1&&session.Snapshot()[0].Health==180,"same slot rebuild consumes new charge and genuine fresh180hp id");
        upper=Place(session,1,6);Check(session.Damage(replacement,180,7).Accepted,"second anchor sever accepted");
        Place(session,0,8);session.Advance(10,false);Check(session.Snapshot().Length==2,"reattached genuine anchor rescues pending branch before collapse");
        world.Ground=false;session.Advance(0,false);session.Advance(.29f,false);Check(session.Snapshot().Length==2,"lost native world support schedules source delay");
        world.Ground=true;session.Advance(.02f,false);Check(session.Snapshot().Length==2,"restored world contact rescues due structures");
        PlacedPanel[] floor;int floorCost;var floors=new R23ConstructionSession(new World());var down=new BuilderState(new Float3(1,1.3f,.5f),new Float3(0,-1,0),new Float3(1,1.6f,.5f),true,true);
        Check(floors.Evaluate(down,1,true,out floor,out floorCost).Accepted&&floor.Length==1&&floor[0].Shape.Kind=="floor"&&floor[0].Health==140,"actual floor max140 from CandidateService");
        int floorBalance=floors.Balance;Check(floors.Damage(floor[0].Id,140,2).Accepted&&floors.Snapshot().Length==0&&floors.Balance==floorBalance,"real140 floor lethal damage and no refund");
        Check(!floors.Advance(float.PositiveInfinity,false).Accepted,"invalid advance cannot mutate construction");
    }
    static void CheckTravelHeading()
    {
        var heading=new MovementHeading();bool continuous=true;
        foreach(double degrees in new[]{80.0,89.0,90.0,91.0,100.0,91.0,90.0,89.0})
        {
            double angle=degrees*Math.PI/180;
            float value=heading.Read(ForwardReference.Hand,true,true,new Float3(1,0,0),new Float3(0,(float)-Math.Sin(angle),(float)Math.Cos(angle)),new Float3(0,(float)Math.Cos(angle),(float)Math.Sin(angle)),0);
            continuous&=Math.Abs(value)<.00001f;
        }
        Check(continuous,"source controller heading retains azimuth across the vertical pole in both directions");
        float yaw=(float)(Math.PI*.5);
        Check(Math.Abs(heading.Read(ForwardReference.Hand,true,true,new Float3(0,0,1),new Float3(1,0,0),new Float3(0,1,0),yaw)-yaw)<.00001f,"controller heading follows artificial body yaw without stale-world reversal");
        Check(Math.Abs(Math.Abs(heading.Read(ForwardReference.Hand,true,false,new Float3(0,0,-1),new Float3(1,0,0),new Float3(0,1,0),yaw))-(float)Math.PI)<.00001f,"unavailable left controller falls back to actual head reference");
        var travel=R23TravelHeading.Resolve(yaw,yaw,.2f,.5f);
        Check(Math.Abs(travel.Forward.Z+1)<.00001f&&Math.Abs(travel.Direction.X+.2f)<.00001f&&Math.Abs(travel.Direction.Z+.5f)<.00001f,"same-tick turn is composed once and preserves analog stick magnitude");
        var frame=new FlightFrame{DeltaSeconds=.02f,InputEnabled=true,Tracked=true,XR=true,HandsReady=true,LeftHand=new Float3(-.7f,1,0),RightHand=new Float3(.7f,1,0)};
        var gate=new Gate();var flight=new FlightMachine("r23-playable","player",0,gate,6.5f,1);
        travel=R23TravelHeading.Resolve(0,0,.2f,0);frame.Forward=travel.Forward;frame.MoveDirection=travel.Direction;
        var result=flight.Step(C(1,"analog-glide"),frame);double expected=12*.07/Math.Sqrt(1+.07*.07);
        Check(result.Accepted&&flight.Mode==FlightMode.Glide&&Math.Abs(flight.Velocity.X-expected)<.00001,"actual Flight owner preserves source partial-stick .35 steering influence");
        flight=new FlightMachine("r23-playable","player",0,gate,6.5f,1);travel=R23TravelHeading.Resolve(0,yaw,0,1);frame.Forward=travel.Forward;frame.MoveDirection=travel.Direction;
        Check(flight.Step(C(1,"turned-glide"),frame).Accepted&&Math.Abs(flight.Velocity.X-12)<.0001&&Math.Abs(flight.Velocity.Z)<.0001,"actual Flight owner receives current-frame travel heading instead of stale head direction");
    }
}
