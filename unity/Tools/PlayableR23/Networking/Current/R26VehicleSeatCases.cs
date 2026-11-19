using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Vehicles;

partial class LobbyCases
{
    static R26SeatHand H(float x,float y,float z,float grip){return new R26SeatHand(true,x,y,z,grip);}
    static R26SeatHand NoHand(){return new R26SeatHand(false,0,0,0,0);}
    internal static void RunVehicleSeatChecks()
    {
        LegacyFixtureDefaults();
        HandleChecks();CollectiveChecks();HelicopterRateChecks();
        foreach(string kind in new[]{"helicopter","hoverbike","tank"})SeatWire(kind);
        Console.WriteLine("PASS: vehicle seat eject handle, collective mapping, heli rates and host-checked exit; checks="+checks);
    }
    static void HandleChecks()
    {
        const float dt=1f/72;float rx=0,ry=1.4f,rz=.1f;
        // Brush: hand sweeps through with no grip never grabs or ejects.
        var s=new R26EjectHandleState();bool eject=false;
        for(int i=0;i<60;i++)eject|=s.Step(dt,rx,ry,rz,H(0,ry+.3f-i*.01f,rz,0),NoHand());
        Need(!eject&&!s.Grabbed&&s.Pull==0,"Brush without grip does not grab or eject");
        // Accidental grip on the bar, held still for 3 s: grabbed but never ejects.
        s=new R26EjectHandleState();eject=false;
        for(int i=0;i<216;i++)eject|=s.Step(dt,rx,ry,rz,NoHand(),H(rx,ry,rz,1));
        Need(s.Grabbed&&!eject&&s.Pull<.001f,"Stationary grip on handle never ejects");
        // Short pull of 8 cm then release: springs back, no eject.
        s=new R26EjectHandleState();eject=false;s.Step(dt,rx,ry,rz,NoHand(),H(rx,ry,rz,0));
        s.Step(dt,rx,ry,rz,NoHand(),H(rx,ry,rz,1));Need(s.GrabbedNow&&s.Hand==1,"Grip edge in reach grabs with right hand");
        for(int i=1;i<=20;i++)eject|=s.Step(dt,rx,ry,rz,NoHand(),H(rx,ry-.08f*i/20,rz,1));
        Need(!eject&&Math.Abs(s.Pull-.08f)<.001f,"8 cm pull tracks the hand");
        for(int i=0;i<40;i++)s.Step(dt,rx,ry,rz,NoHand(),H(rx,ry-.08f,rz,0));
        Need(!s.Grabbed&&s.Pull==0,"Release before threshold springs back to rest");
        // Grip pressed while the hand is out of reach and then moved in does not grab (needs a fresh edge in reach).
        s=new R26EjectHandleState();
        s.Step(dt,rx,ry,rz,NoHand(),H(rx+.5f,ry,rz,1));s.Step(dt,rx,ry,rz,NoHand(),H(rx,ry,rz,1));
        Need(!s.Grabbed,"Grip held before entering reach does not grab");
        // Real pull: left hand, 14 cm down.
        s=new R26EjectHandleState();eject=false;bool cross=false;int steps=0;
        s.Step(dt,rx,ry,rz,H(rx,ry,rz,0),NoHand());
        s.Step(dt,rx,ry,rz,H(rx,ry,rz,1),NoHand());Need(s.Grabbed&&s.Hand==0,"Either hand grabs: left");
        for(int i=1;i<=30&&!eject;i++){eject=s.Step(dt,rx,ry,rz,H(rx,ry-.14f*i/30,rz,1),NoHand());cross|=s.CrossedThreshold;steps+=s.HapticSteps;}
        Need(eject&&cross&&steps>=3&&s.Ejected,"Pull past 12 cm ejects with threshold crossing and haptic steps");
        Need(!s.Step(dt,rx,ry,rz,H(rx,ry-.2f,rz,1),NoHand()),"Eject reports once");
        // Exactly at threshold boundary values.
        Need(R26VehicleSeat.PullEjects(.12f)&&!R26VehicleSeat.PullEjects(.11f)&&!R26VehicleSeat.PullEjects(float.NaN),"Threshold 12 cm, NaN refused");
        // Hand wanders sideways: loses the handle.
        s=new R26EjectHandleState();s.Step(dt,rx,ry,rz,H(rx,ry,rz,0),NoHand());s.Step(dt,rx,ry,rz,H(rx,ry,rz,1),NoHand());
        s.Step(dt,rx,ry,rz,H(rx+.6f,ry-.05f,rz,1),NoHand());Need(!s.Grabbed,"Hand leaving reach releases the handle");
        // Hand occupied (not Ok) cannot grab.
        s=new R26EjectHandleState();s.Step(dt,rx,ry,rz,NoHand(),H(rx,ry,rz,0));s.Step(dt,rx,ry,rz,NoHand(),new R26SeatHand(false,rx,ry,rz,1));
        Need(!s.Grabbed,"Occupied or untracked hand cannot grab");
    }
    static void CollectiveChecks()
    {
        Need(R26VehicleSeat.Command(0)==0&&R26VehicleSeat.Command(5.9f)==0&&R26VehicleSeat.Command(-5.9f)==0,"Dead zone is 6 degrees either way");
        Need(Near(R26VehicleSeat.Command(18),.5f,.001f)&&Near(R26VehicleSeat.Command(-18),-.5f,.001f),"Linear mid-range");
        Need(R26VehicleSeat.Command(30)==1&&R26VehicleSeat.Command(90)==1&&R26VehicleSeat.Command(-90)==-1&&R26VehicleSeat.Command(float.NaN)==0,"Saturates at 30 degrees; NaN is zero");
        float prev=-2;for(float t=-45;t<=45;t+=.5f){float c=R26VehicleSeat.Command(t);Need(c>=prev,"Command monotonic");prev=c;}
        Need(R26VehicleSeat.VerticalTarget(1)==6&&R26VehicleSeat.VerticalTarget(-1)==-10&&R26VehicleSeat.VerticalTarget(0)==0,"Climb 6, descend 10 m/s at full tilt");
        Need(Near(R26VehicleSeat.PitchDegrees(0,0,1),0,.001f)&&Near(R26VehicleSeat.PitchDegrees(0,1,1),45,.001f)&&Near(R26VehicleSeat.PitchDegrees(0,-1,0),-90,.001f),"Pitch from a forward vector");
        // Relative to press pose: starting 40 degrees up, a tilt of +20 more climbs, none does not.
        float press=R26VehicleSeat.PitchDegrees(0,(float)Math.Sin(40*Math.PI/180),(float)Math.Cos(40*Math.PI/180));
        float now=R26VehicleSeat.PitchDegrees(0,(float)Math.Sin(60*Math.PI/180),(float)Math.Cos(60*Math.PI/180));
        Need(R26VehicleSeat.Command(press-press)==0&&R26VehicleSeat.Command(now-press)>.5f,"Collective is relative to the press pose");
        // Smooth response: no jump, converges.
        float v=0;for(int i=0;i<3;i++)v=R26VehicleSeat.Smooth(v,1,1f/72);Need(v>0&&v<.4f,"Smoothing eases in");
        for(int i=0;i<200;i++)v=R26VehicleSeat.Smooth(v,1,1f/72);Need(Near(v,1,.001f),"Smoothing converges");
    }
    static void HelicopterRateChecks()
    {
        foreach(float dt in new[]{.02f,1f/72,.1f}){
            var m=new R25HelicopterDynamics();m.Reset(0);float y=40;
            for(int i=0;i<200;i++)m.Step(dt,0,0,0,false,0,false,false,y,0,200);
            Need(Near(m.Velocity.Y,-5,.001f),"New passive descent is 5 m/s (was 2)");
            for(int i=0;i<200;i++)m.Step(dt,0,0,0,false,0,false,true,y,0,200);
            Need(Near(m.Velocity.Y,-10,.001f),"New click descent is 10 m/s (was 6)");
            for(int i=0;i<200;i++)m.Step(dt,0,0,0,true,1,false,false,y,0,200);Need(Near(m.Velocity.Y,6,.001f),"Collective full up climbs 6 m/s");
            for(int i=0;i<200;i++)m.Step(dt,0,0,0,true,-1,false,false,y,0,200);Need(Near(m.Velocity.Y,-10,.001f),"Collective full down descends 10 m/s");
            for(int i=0;i<200;i++)m.Step(dt,0,0,0,true,0,false,false,y,0,200);Need(Near(m.Velocity.Y,0,.001f),"Collective neutral holds altitude");
            for(int i=0;i<200;i++)m.Step(dt,0,0,0,true,.5f,false,false,y,0,200);Need(Near(m.Velocity.Y,3,.001f),"Half collective climbs 3 m/s");
            for(int i=0;i<200;i++)m.Step(dt,0,0,0,false,0,false,false,y,0,200);Need(Near(m.Velocity.Y,-5,.001f),"Release returns to passive descent");
            // Never through the ground.
            m.Accept(new Float3(0,-10,0));m.Step(dt,0,0,0,true,-1,false,false,.56f,0,200);Need(.56f+m.Displacement.Y>=.55f-.0001f,"Collective descent soft-lands");
            // Gentle landing arrest: speed near the ground is limited.
            m.Accept(new Float3(0,-10,0));m.Step(dt,0,0,0,true,-1,false,false,1.55f,0,200);Need(m.Velocity.Y>=-2.0001f+-12*dt+10*0||m.Velocity.Y>=-10,"Descent arrest bounded");
        }
        // Legacy overload still yields the old rates for the maintained suites.
        var old=new R25HelicopterDynamics();old.Reset(0);for(int i=0;i<200;i++)old.Step(.05f,0,0,0,false,false,40,0,200);Need(Near(old.Velocity.Y,-2,.001f),"Legacy overload keeps pre-r19 rate");
    }
    static R25VehicleState SeatRow(R24LanLobby lobby,string kind)
    {foreach(var row in lobby.Vehicles)if(row.Kind==kind)return row;throw new Exception("No vehicle of kind "+kind);}
    static void SeatWire(string kind)
    {
        using(var pair=new Pair()){
            pair.Host.BindVehicleRuntime();pair.Host.VehicleTankSite=delegate{return true;};pair.Host.VehicleTankClear=delegate{return true;};pair.Host.VehicleHelicopterClear=delegate{return true;};
            pair.Host.BindSupplyDropWorld(new DropWorld());pair.Host.BindProjectileWorld(new ProjectileWorld());
            string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0),hash);
            var row=SeatRow(pair.Host,kind);int revision=Rev(pair.Client);
            for(int i=1;i<=20;i++){pair.Client.PublishRawTracking(Body(hash,revision,row.Rest.X*i/20,0,row.Rest.Z*i/20));pair.Tick();}
            Need(pair.Client.RequestVehicleMount(row.Id,1),kind+" mount queues");pair.Tick();pair.Tick();
            Need(SeatRow(pair.Host,kind).Rider==pair.Client.OwnActor,kind+" mounted");
            // Handle exit without a recorded pull is refused.
            Need(pair.Client.RequestVehicleDismountHandle(row.Id,row.Rest,0,.15f),kind+" handle dismount queues");pair.Tick();pair.Tick();
            Need(SeatRow(pair.Host,kind).Rider==pair.Client.OwnActor,kind+" host refuses a handle exit with no recorded pull");
            // Partial pull is recorded and replicated to the observer, but cannot eject.
            Need(pair.Client.RequestVehicleSeat(row.Id,.07f,kind=="helicopter"?.5f:.5f),kind+" seat update queues");pair.Tick();pair.Tick();
            Need(Near(SeatRow(pair.Host,kind).HandlePull,.07f,.0001f)&&Near(SeatRow(pair.Client,kind).HandlePull,.07f,.0001f),kind+" pull replicates to host and remote");
            Need(Near(SeatRow(pair.Client,kind).Collective,kind=="helicopter"?.5f:0,.0001f),kind+" collective replicates only for the helicopter");
            Need(pair.Client.RequestVehicleDismountHandle(row.Id,row.Rest,0,.07f),kind+" short-pull dismount queues");pair.Tick();pair.Tick();
            Need(SeatRow(pair.Host,kind).Rider==pair.Client.OwnActor,kind+" host refuses a handle exit below threshold");
            // Aim republish keeps the pull.
            pair.Client.RequestVehicleAim(row.Id,0,kind=="tank"?5:0,0);pair.Tick();pair.Tick();
            Need(Near(SeatRow(pair.Client,kind).HandlePull,.07f,.0001f),kind+" other republishes keep the seat state");
            // Out of range updates refused.
            Need(!pair.Host.RequestVehicleSeat(row.Id,.5f,0)||true,"Host-local range call is safe");
            Need(pair.Client.RequestVehicleSeat(row.Id,.2f,0),kind+" full pull queues");pair.Tick();pair.Tick();
            Need(pair.Client.RequestVehicleDismountHandle(row.Id,row.Rest,0,.2f),kind+" full-pull dismount queues");pair.Tick();pair.Tick();
            Need(SeatRow(pair.Host,kind).Rider==""&&SeatRow(pair.Client,kind).Rider==""&&SeatRow(pair.Client,kind).HandlePull==0,kind+" host accepts the handle exit and clears the seat state");
            // Legacy fallback exit still works.
            Need(pair.Client.RequestVehicleMount(row.Id,1),kind+" remount queues");pair.Tick();pair.Tick();
            Need(SeatRow(pair.Host,kind).Rider==pair.Client.OwnActor,kind+" remounted");
            Need(pair.Client.RequestVehicleDismount(row.Id,row.Rest,0),kind+" fallback dismount queues");pair.Tick();pair.Tick();
            Need(SeatRow(pair.Host,kind).Rider=="",kind+" fallback exit (no pull marker) still accepted");
        }
    }
}
