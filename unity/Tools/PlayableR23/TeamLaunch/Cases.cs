using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.TeamLaunch;
class Cases
{
    static int checks;static void Need(bool okay,string why){checks++;if(!okay)throw new Exception(why);}
    static void Near(Float3 a,Float3 b,string why){Need(R24LaunchMath.Distance(a,b)<.001f,why);}
    sealed class World:IR24LaunchWorld
    {
        public int Rays;public bool Empty,Safe=true,UseSafe;public bool HasSafeSpawn{get{return UseSafe;}}public bool SafeSpawn(Float3 point){return Safe;}
        public bool RaycastDown(Float3 at,float end,out R24LaunchFloor hit){Rays++;hit=new R24LaunchFloor{Point=new Float3(at.X,0,at.Z),NormalY=1};return !Empty&&at.Y>=0&&end<0;}
    }
    static R24LaunchMap Map(string id="archive:alpha",float span=200){return new R24LaunchMap{Id=id,Minimum=new Float3(-span*.5f,0,-span*.5f),Maximum=new Float3(span*.5f,0,span*.5f),Top=20,OriginY=0};}
    static R24LaunchRules Rules(string stage="match"){return new R24LaunchRules{Live=true,Mode="revive",Stage=stage,Teams=2,Epoch=7};}
    static R24LaunchPlayer Player(R24LaunchLayout layout,string id="actual-player",int team=0,int slot=0){return new R24LaunchPlayer{Id=id,Team=team,Alive=true,OnDeck=true,Position=layout.Pod(team,slot).Origin};}
    static R24LaunchInput Input(R24LaunchFrame cabin,bool pressed=true)
    {return new R24LaunchInput{Enabled=true,XR=true,Right=new R24LaunchHand{Tracked=true,Position=cabin.Point(R24LaunchMath.Add(R24TeamLaunchRuntime.ControlLocalPoint,new Float3(0,0,-.2f))),Forward=cabin.Forward,Trigger=pressed?1:0}};}
    static void Main()
    {
        var rules=Rules();var layout=new R24LaunchLayout(Map(),rules);
        Near(layout.Station(0).Origin,new Float3(-74.5f,85,90),"source corner0 reflected once with fitted49m deck");
        Near(layout.Station(1).Origin,new Float3(74.5f,85,-90),"source corner1 reflected once");
        var small=new R24LaunchLayout(Map(span:40),rules);Need(small.Station(1).Origin.Y==103&&small.Ceiling==141,"source stacked18m small-map deck and ceiling");
        Need(!R24LaunchLayout.Eligible(Map("archive:battlefield-paintball-gods-pitdevil"),rules)&&!R24LaunchLayout.Eligible(Map("archive:skb-paintball-gods-skblong"),rules),"both source Paintball maps excluded");
        var roster=new R24LaunchAssignment[12];for(int i=0;i<12;i++){roster[i]=layout.Assign("player"+i,0,roster);Need(roster[i].Slot==i&&roster[i].PodId=="7:0:"+i,"source first free own-team slot "+i);}
        Near(roster[0].Spawn,layout.Station(0).Point(new Float3(-19.8f,.12f,-3)),"actual source deck spawn behind pod row");
        Need(layout.Staging(roster[0].Spawn)&&!layout.Staging(new Float3(0,-200,0)),"source staging AABB");
        var runtime=new R24TeamLaunchRuntime(42);Need(runtime.Configure(Map(),Rules("lobby")),"source lobby hardware eligible");var player=Player(runtime.Layout);var world=new World();R24LaunchRide ride;
        Need(!runtime.TryLaunch(player,"7:0:0",1,new[]{new Float3(0,.12f,0)},null,new Float3(),world,out ride),"lobby console cannot launch before match");
        var before=runtime.Layout;runtime.Configure(Map(),Rules());Need(Object.ReferenceEquals(before,runtime.Layout),"stage transition preserves same decks/assignments");
        player=Player(runtime.Layout);var cabin=runtime.Layout.Pod(0,0);
        Need(runtime.SampleInput(player,1,Input(cabin)).Kind==R24LaunchIntentKind.None,"entering pod with held trigger requires neutral");
        Need(runtime.SampleInput(player,1,Input(cabin,false)).Kind==R24LaunchIntentKind.None,"actual neutral arms console");
        var intent=runtime.SampleInput(player,1,Input(cabin));Need(intent.Kind==R24LaunchIntentKind.Launch&&intent.PodId=="7:0:0","fresh aimed/near trigger requests actual cabin key");
        Need(runtime.SampleInput(player,1,Input(cabin)).Kind==R24LaunchIntentKind.None,"held trigger does not repeat request");
        var menu=Input(cabin,false);runtime.SampleInput(player,1,menu);menu=Input(cabin);menu.MenuOpen=true;Need(runtime.SampleInput(player,1,menu).Kind==R24LaunchIntentKind.None,"field menu consumes source fresh edge without launch");
        Need(runtime.TryLaunch(player,intent.PodId,10,new[]{new Float3(0,.12f,0)},null,new Float3(),world,out ride),"genuine source floor route creates real CapsulePlan");
        Need(ride.Plan.Duration>=1.5&&ride.Plan.Duration<=120&&ride.Plan.Team==0&&ride.Plan.Slot==0,"source plan identity/duration");
        Near(ride.Plan.Start,R24LaunchMath.Add(cabin.Origin,new Float3(0,.12f,0)),"source cabin root offset .12");
        Near(ride.Plan.Clear,R24LaunchMath.Add(ride.Plan.Start,new Float3(0,5,-10)),"source open front clearance, no root rotation");
        Near(ride.Position(0),ride.Plan.Start,"pre-start path clamp");Near(ride.Position(10+ride.Plan.Duration),ride.Plan.End,"actual source constant-speed path ends on floor");
        double duration=ride.Plan.Duration;for(int i=1;i<=20;i++){var a=ride.Position(10+duration*(i-1)/20);var b=ride.Position(10+duration*i/20);Need(R24LaunchMath.Distance(a,b)/(duration/20)<=35.01,"source path speed bound "+i);}
        Need(runtime.Used("7:0:0")&&!runtime.TryLaunch(Player(runtime.Layout,"other"),"7:0:0",10,new[]{new Float3(0,.12f,0)},null,new Float3(),world,out ride),"same physical pod cannot be double claimed");ride=runtime.ReadRide(player.Id);
        R24LaunchCarrier carrier;Need(runtime.TryCarrier(player,10.5,false,out carrier)&&carrier.Carried,"actual flight requests translation only");Near(carrier.Displacement,R24LaunchMath.Sub(carrier.Target,player.Position),"requested root displacement from actual supplied position");
        Need(!ride.HasAccepted,"request does not fabricate accepted Root movement");runtime.AcceptCarrierMotion(player.Id,player.Position);Need(ride.HasAccepted&&ride.LastAccepted.Equals(player.Position),"Root accepted pose is an observation, path clock unchanged");
        var flyingFrame=ride.Frame;flyingFrame.Origin=R24LaunchMath.Add(ride.Position(10.5),new Float3(0,-.12f,0));runtime.SampleInput(player,10.5,Input(flyingFrame,false));Need(runtime.SampleInput(player,10.5,Input(flyingFrame)).Kind==R24LaunchIntentKind.Eject,"fresh in-flight source console requests early exit");
        Need(runtime.TryCarrier(player,10.5,true,out carrier)&&carrier.State==R24LaunchRideState.Released&&!carrier.Carried,"source early exit releases carry");Near(carrier.Target,R24LaunchMath.Add(ride.Position(10.5),new Float3(0,0,2)),"source ejection behind visible pod2m");
        Need(runtime.Used("7:0:0")&&runtime.TryCarrier(player,10.6,false,out carrier),"released pod remains consumed, requested terminal move remains until Root accepts");runtime.AcceptCarrierMotion(player.Id,carrier.Target);Need(!runtime.TryCarrier(player,10.6,false,out carrier),"accepted terminal displacement not repeatedly emitted");
        runtime.RemovePlayer(player.Id);Need(!runtime.Used("7:0:0"),"source remove/deployment reset withdraws that actor ride");
        player=Player(runtime.Layout);Need(runtime.TryLaunch(player,"7:0:0",20,new[]{new Float3(0,.12f,0)},null,new Float3(),world,out ride),"new actual source actor deployment can reuse cleared pod");
        Need(runtime.TryCarrier(player,20+ride.Plan.Duration,false,out carrier)&&carrier.State==R24LaunchRideState.Landed,"clock-driven source landing");Near(carrier.Target,ride.Plan.ExitTarget,"landing requests source floor-validated rear exit");Need(runtime.Used("7:0:0"),"landed hardware remains consumed");
        runtime.Clear();Need(runtime.Configure(Map("builtin"),Rules()),"built-in source arena eligible");player=Player(runtime.Layout);world=new World();Need(runtime.TryLaunch(player,"7:0:0",1,new Float3[0],null,new Float3(0,.12f,0),world,out ride)&&world.Rays==83,"built-in source9x9 actual floor probes plus landing/exit rays");
        runtime.Clear();runtime.Configure(Map(),Rules());player=Player(runtime.Layout);world=new World{UseSafe=true,Safe=false};Need(!runtime.TryLaunch(player,"7:0:0",1,new[]{new Float3(0,.12f,0)},null,new Float3(),world,out ride),"actual source safe_spawn rejects candidate");
        world=new World{Empty=true};Need(!runtime.TryLaunch(player,"7:0:0",1,new[]{new Float3(0,.12f,0)},null,new Float3(),world,out ride),"no actual landing floor cannot invent platform");
        Console.WriteLine("PASS "+checks+" source layout/slots/console/clock/CapsulePlan/root-motion/exit/finite-pod cases; pure ports, no native floor/physics claim");
    }
}
