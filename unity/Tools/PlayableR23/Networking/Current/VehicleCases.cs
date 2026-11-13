using System;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.Vehicles;
partial class LobbyCases
{
    static void VehicleDynamics()
    {
        foreach(string kind in new[]{"tank","helicopter"})foreach(int side in new[]{-1,1}){
            var marker=R25BikeRider.MountPoint(new Float3(4,2,3),90,kind,side);
            var local=R25BikeRider.MountOffset(kind,side);
            Need(Near(marker.X,4+local.Z,.0001f)&&Near(marker.Z,3-local.X,.0001f)&&Near(marker.Y,2.55f,.0001f),"Yawed boarding point matches hull-side handle");
        }
        var comfortable=new R25BikeDynamics();float previous=0;
        for(int n=0;n<360;n++){comfortable.Step(1f/72,1,0,0,false,false,true,.55f,new Float3(0,1,0));Need(comfortable.Velocity.Z-previous<=11f/72+.0001f,"Bike throttle acceleration bounded at 11 m/s2");previous=comfortable.Velocity.Z;}
        var a=new R25BikeDynamics();var b=new R25BikeDynamics();var up=new Float3(0,1,0);
        for(int i=0;i<300;i++){
            a.Step(.02f,1,0,0,false,false,true,.55f,up);
            b.Step(.02f,1,0,0,false,false,true,.55f,up);
            Need(a.Velocity.Equals(b.Velocity)&&a.Displacement.Equals(b.Displacement),"Deterministic identical stepping");
        }
        Need(Near(a.Velocity.Z,40,.001f)&&a.Velocity.Y==0,"Forward cap and spring equilibrium");
        a.Step(.1f,0,0,0,false,false,true,.55f,up);Need(Near(a.Velocity.Z,39.7f,.001f),"Coasting drag 3");
        a.Step(.1f,-1,0,0,false,false,true,.55f,up);Need(Near(a.Velocity.Z,37.5f,.001f),"Strong brake 22");
        for(int i=0;i<100;i++)a.Step(.02f,-1,1,0,false,false,true,.55f,up);
        Need(a.Velocity.Z>=-6&&a.Velocity.X<=6,"Reverse and strafe caps");
        a.Accept(new Float3(0,0,40));a.Step(.1f,1,0,0,true,false,true,.55f,up);
        for(int i=0;i<18;i++)a.Step(.1f,1,0,0,true,false,true,.55f,up);
        Need(a.Velocity.Z>49.9f&&a.Velocity.Z<=50&&a.BoostRemaining>0,"Boost cap and 2 seconds");
        for(int i=0;i<3;i++)a.Step(.1f,1,0,0,false,false,true,.55f,up);
        Need(a.BoostRemaining==0&&a.Velocity.Z<=40,"Boost expiry returns cap");
        a.Step(.1f,1,0,0,true,false,true,.55f,up);Need(a.BoostRemaining==0,"Boost recharge refuses early click");
        for(int i=0;i<61;i++)a.Step(.1f,0,0,0,false,false,true,.55f,up);
        a.Step(.1f,1,0,0,true,false,true,.55f,up);Need(a.BoostRemaining>1.8f,"Boost rearms after 6 seconds recharge");
        var hop=new R25BikeDynamics();hop.Step(.02f,0,0,0,false,true,true,.55f,up);Need(hop.Velocity.Y==5.5f,"Hop impulse");
        hop.Step(.02f,0,0,0,false,false,false,1,up);hop.Step(.02f,0,0,0,false,true,false,1,up);Need(hop.Velocity.Y<5.5f,"Airborne hop refused");
        hop.Accept(new Float3());hop.Step(.02f,0,0,0,false,false,true,.3f,up);Need(hop.Velocity.Y>0,"Low hover spring lifts");
        hop.Accept(new Float3());hop.Step(.02f,0,0,0,false,false,true,.8f,up);Need(hop.Velocity.Y<0,"High hover spring lowers");
        var slope=new R25BikeDynamics();slope.Accept(new Float3(0,0,4));slope.Step(.1f,1,0,0,false,false,true,.55f,new Float3(0,1,-.5f));Need(slope.Velocity.Y>0,"Follow climbable slope");
        slope.Accept(new Float3());slope.Step(.1f,0,0,0,false,false,true,.55f,new Float3(0,.5f,-1));Need(Near(slope.Velocity.Y,-1.62f,.001f),"Steep slope falls under gravity");
        var old=slope.Velocity;slope.Step(float.NaN,1,1,0,false,false,true,.55f,up);Need(slope.Velocity.Equals(old)&&slope.Displacement.Equals(new Float3()),"Invalid step ignored");
        Need(R24LanLobby.VehicleSpawnCount("range",1)==2&&R24LanLobby.VehicleSpawnCount("valley",6)==3&&R24LanLobby.VehicleSpawnCount("valley",100)==8&&R24LanLobby.VehicleSpawnCount("archive:skb-paintball-gods-skblong",8)==0&&R24LanLobby.VehicleSpawnCount("indoor",8)==0,"Spawn count policy");
        Need(Near(R24LanLobby.VehicleBoxRay(new Float3(0,0,-4),new Float3(0,0,1),new Float3(),0,10),2.9f,.001f),"Analytic chassis box hit");
        Need(Single.IsPositiveInfinity(R24LanLobby.VehicleBoxRay(new Float3(2,0,-4),new Float3(0,0,1),new Float3(),0,10)),"Box miss");
    }
    static void BikeRaw(Pair pair,string hash,float gx,float hx)
    {for(int i=0;i<4;i++)KeepRaw(pair,hash,gx,hx);}
    static void VehicleAuthority()
    {
        using(var pair=new Pair()){
            pair.Host.BindVehicleRuntime();
            var drop=new DropWorld();pair.Host.BindSupplyDropWorld(drop);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f),hash);
            BikeRaw(pair,hash,0,8);
            Need(pair.Host.Vehicles.Length==2&&pair.Client.Vehicles.Length==2,"Two grounded range bikes reliably replicated");
            Need(pair.Client.SelectedMap.Rules.Vehicles,"Default-on rule replicated");
            var first=pair.Host.Vehicles[0];Need(first.Kind=="hoverbike"&&first.Health==300&&first.Rider==""&&first.Home.Equals(pair.Client.Vehicles[0].Home),"Snapshot roundtrip includes kind, home, health and empty seat");
            Need(!pair.Host.RequestVehicleMount(first.Id,2)&&!pair.Host.RequestVehicleMount("missing",0)&&!pair.Host.RequestVehicleMount(first.Id,1),"Invalid hand/id/far hand refused");
            BikeRaw(pair,hash,first.Rest.X-.3f,8);
            Need(pair.Client.RequestVehicleMount(first.Id,1),"Guest mount queues");BikeRaw(pair,hash,first.Rest.X-.3f,8);
            Need(pair.Host.Vehicles[0].Rider==pair.Client.OwnActor&&pair.Client.Vehicles[0].Rider==pair.Client.OwnActor,"Host accepts nearby tracked hand and relays custody");
            Need(!pair.Host.RequestVehicleMount(first.Id,1),"Single seat refuses another actor");
            var second=pair.Host.Vehicles[1];BikeRaw(pair,hash,second.Rest.X-.3f,8);
            pair.Client.RequestVehicleMount(second.Id,1);BikeRaw(pair,hash,second.Rest.X-.3f,8);Need(pair.Host.Vehicles[1].Rider=="","One actor cannot own two seats");
            pair.Client.RequestVehicleDismount(first.Id,new Float3(999,0,0),float.NaN);BikeRaw(pair,hash,second.Rest.X-.3f,8);
            Need(pair.Host.Vehicles[0].Rider==""&&Near(pair.Host.Vehicles[0].Rest.X,second.Rest.X-.3f,.001f),"Far rest and invalid yaw fall back to accepted root/yaw");
            pair.Client.RequestVehicleMount(second.Id,1);BikeRaw(pair,hash,second.Rest.X-.3f,8);Need(pair.Host.Vehicles[1].Rider!="","Freed actor can mount second seat");
            var rest=new Float3(second.Rest.X,0,1);pair.Client.RequestVehicleDismount(second.Id,rest,30);BikeRaw(pair,hash,second.Rest.X-.3f,8);
            Need(pair.Host.Vehicles[1].Rest.Equals(rest)&&pair.Host.Vehicles[1].Yaw==30,"Nearby finite rest pose accepted");
            pair.Client.RequestVehicleMount(second.Id,1);BikeRaw(pair,hash,second.Rest.X-.3f,8);
            Need(pair.Host.DamageVehicle(second.Id,125)&&pair.Host.Vehicles[1].Health==175,"Host damage reduces bike health");
            Need(!pair.Host.DamageVehicle(second.Id,float.NaN)&&!pair.Host.DamageVehicle(second.Id,-1),"Malformed damage refused");
            Need(pair.Host.DamageVehicle(second.Id,200)&&pair.Host.Vehicles[1].Destroyed&&pair.Host.Vehicles[1].Rider=="","Destroy ejects rider");
            double due=pair.Host.Vehicles[1].RespawnAt;pair.Now=due-.2;pair.Tick();Need(pair.Host.Vehicles[1].Destroyed,"No early respawn");
            pair.Now=due+.01;pair.Tick();Need(pair.Host.Vehicles[1].Health==300&&pair.Host.Vehicles[1].Rest.Equals(second.Home)&&pair.Host.Vehicles[1].Rider=="","20-second respawn restores home and health");
            int rejected=pair.Host.RejectedFrames;
            pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(new Packet{Protocol=Protocol.Name,Schema=Protocol.Schema,Kind=PacketKind.Command,FeatureId="playable.vehicle.mount/1",ActorId=pair.Client.OwnActor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=999,BaseSequence=-1,Payload=new byte[]{255}})});pair.Tick();
            Need(pair.Host.RejectedFrames==rejected&&pair.Client.State==R24LanLobbyState.Connected,"Malformed command ignored without peer rejection");
            var pipe=new Pipe();var late=new R24LanLobby(pipe,pipe.Close,delegate{return "unused";},new string('a',64));pipe.Lost=late.TransportDisconnected;
            pair.HostPipe.Links.Add("late",new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=pair.HostPipe,Back="late"});
            late.StartClient("server","Late",pair.Now);pair.Host.TransportConnected("late");
            try{for(int i=0;i<12;i++){pair.Tick();late.Tick(pair.Now);}Need(late.Vehicles.Length==2&&late.Vehicles[1].Health==300,"Late join receives full current snapshot");}finally{late.Dispose();pipe.Dispose();}
            Need(pair.Host.BeginRoundSelection(R24PlayableMatchRules.ForPreset("free_for_all","range").With("vehicles",false),hash,321),"Round reset with rule off");
            for(int i=0;i<6;i++)pair.Tick();Need(pair.Host.Vehicles.Length==0&&pair.Client.Vehicles.Length==0&&!pair.Client.SelectedMap.SourceRules.Vehicles,"Reset clears list and riders; rule-off replicates");
            Need(!pair.Host.RequestVehicleMount(second.Id,1),"Rule off ignores commands");
        }
    }
    static void VehicleSpawnRefusal()
    {
        using(var pair=new Pair()){
            pair.Host.BindSupplyDropWorld(new DropWorld());string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);
            Need(pair.Host.Vehicles.Length==0&&!pair.Host.RequestVehicleMount("bike/0",1),"Absent runtime adapter leaves native scene untouched even with default-on rule");
        }
        using(var pair=new Pair()){
            pair.Host.BindVehicleRuntime();
            var drop=new DropWorld();drop.Refuse=true;pair.Host.BindSupplyDropWorld(drop);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);
            Need(pair.Host.Vehicles.Length==0,"World oracle veto skips unsafe spawn positions");
        }
        using(var pair=new Pair()){
            pair.Host.BindVehicleRuntime();
            pair.Host.BindSupplyDropWorld(new DropWorld());string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("vehicles",false).With("zone_enabled",false),hash);
            Need(pair.Host.Vehicles.Length==0&&pair.Client.Vehicles.Length==0,"Rule off produces no bikes");
        }
    }
    static void VehicleCombatAndLifecycle()
    {
        using(var pair=new Pair()){
            pair.Host.BindVehicleRuntime();
            pair.Host.BindSupplyDropWorld(new DropWorld());pair.Host.BindProjectileWorld(new ProjectileWorld{Contact=true});
            pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){return range;});
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f),hash);
            var bike=pair.Host.Vehicles[0];BikeRaw(pair,hash,0,8);BikeRaw(pair,hash,0,bike.Rest.X-.3f);
            Need(pair.Host.SubmitShot("cx4",0,new Float3(bike.Rest.X,1.2f,0),new[]{new Float3(0,-1,0)}),"Production firearm submitted above bike: "+pair.Host.LastShotRejection);
            BikeRaw(pair,hash,0,bike.Rest.X-.3f);float afterShot=pair.Host.Vehicles[0].Health;
            Need(afterShot<300&&pair.Client.Vehicles[0].Health==afterShot,"Actual bullet nearest-hit path damages analytic chassis and replicates");
            Need(pair.Host.RequestLoot("cx4",1,true,new Float3(bike.Rest.X,1.2f,.2f)),"Free grenade hand");
            BikeRaw(pair,hash,0,bike.Rest.X-.3f);
            Need(pair.Host.RegisterSharedGrenade("vehicle/frag","frag",new Float3(bike.Rest.X,1.2f,.2f))&&pair.Host.RequestGrenade("vehicle/frag",1,0,new Float3()),"Production frag pickup at bike");
            for(int i=0;i<8;i++)KeepRaw(pair,hash,0,bike.Rest.X-.3f);
            Need(pair.Host.RequestGrenade("vehicle/frag",1,1,new Float3()),"Production frag throw");
            for(int i=0;i<70;i++)KeepRaw(pair,hash,0,bike.Rest.X-.3f);
            Need(pair.Host.Vehicles[0].Health<afterShot,"Actual frag fuse and blast damage bike");
        }
        using(var pair=new Pair()){
            pair.Host.BindVehicleRuntime();
            pair.Host.BindSupplyDropWorld(new DropWorld());string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f),hash);
            var bike=pair.Host.Vehicles[0];BikeRaw(pair,hash,bike.Rest.X-.3f,8);
            pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),bike.Rest.X-.3f,0,0,1));pair.Tick();pair.Tick();
            pair.Client.RequestVehicleMount(bike.Id,1);for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.Vehicles[0].Rider=="","Untracked hand cannot mount");
            BikeRaw(pair,hash,bike.Rest.X-.3f,8);pair.Client.RequestVehicleMount(bike.Id,1);BikeRaw(pair,hash,bike.Rest.X-.3f,8);
            Need(pair.Host.Vehicles[0].Rider==pair.Client.OwnActor,"Mount before life transition");
            var h=Health(pair.Host,pair.Client.OwnActor);Need(pair.Host.ApplyHostHealthLoss(pair.Client.OwnActor,h.Revision,h.Health,h.Shield,false).Accepted,"Actual death transition");
            for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.Vehicles[0].Rider==""&&pair.Client.Vehicles[0].Rider=="","Death ejects and replicates empty seat");
            Need(!pair.Host.RequestVehicleMount(bike.Id,1),"Far host remains refused");
            pair.HostPipe.Pressure=12;Need(pair.Host.DamageVehicle(bike.Id,12),"Damage under reliable backpressure");
            for(int i=0;i<20;i++)pair.Tick();Need(pair.Client.Vehicles[0].Health==pair.Host.Vehicles[0].Health,"Dirty snapshot survives send pressure");
        }
        using(var pair=new Pair()){
            pair.Host.BindVehicleRuntime();
            pair.Host.BindSupplyDropWorld(new DropWorld());string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);
            var bike=pair.Host.Vehicles[0];BikeRaw(pair,hash,bike.Rest.X-.3f,8);pair.Client.RequestVehicleMount(bike.Id,1);BikeRaw(pair,hash,bike.Rest.X-.3f,8);
            Need(pair.Host.Vehicles[0].Rider!="","Mount before departure");pair.ClientPipe.Close("server");pair.Tick();Need(pair.Host.Vehicles[0].Rider=="","Departure ejects");
        }
        using(var pair=new Pair()){
            pair.Host.BindVehicleRuntime();pair.Host.BindSupplyDropWorld(new DropWorld());pair.Host.BindPodWorld(new FakePodWorld());
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("team_revive","range").With("zone_enabled",false),hash);
            BikeRaw(pair,hash,0,8);pair.Client.RequestPodLaunch("0:1:0");pair.Tick();pair.Tick();
            R24LanPodState pod;Need(pair.Host.TryReadPod(pair.Client.OwnActor,out pod)&&pod.Phase==R24LanPodPhase.Flying,"Real pod owns guest motion");
            pair.Client.RequestVehicleMount(pair.Host.Vehicles[0].Id,1);pair.Tick();pair.Tick();
            Need(pair.Host.Vehicles[0].Rider=="","Host carrier cannot mount a bike");
        }
    }
    static void VehicleWorldAndFreshness()
    {
        using(var pair=new Pair()){
            pair.Host.BindVehicleRuntime();pair.Host.BindSupplyDropWorld(new DropWorld());
            bool blocked=false;string hash=new string('b',64);
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindSpawnWorld(new HazardWorld());
            pair.Host.BindRawTrackingWorldQuery(delegate(Float3 a,Float3 b){return !blocked;});
            Need(pair.Host.BeginRoundSelection(R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash,321)&&pair.Host.ReportMapLoaded(hash),"World matrix round selection");
            for(int i=0;i<3;i++)pair.Tick();pair.Client.ReportMapLoaded(hash);for(int i=0;i<4;i++)pair.Tick();
            pair.Now=pair.Host.SelectedMap.StartsAt+.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);
            pair.Client.RequestSpawn();for(int i=0;i<4;i++)pair.Tick();pair.Host.RequestSpawn();for(int i=0;i<4;i++)pair.Tick();
            var bike=pair.Host.Vehicles[0];BikeRaw(pair,hash,0,8);BikeRaw(pair,hash,0,bike.Rest.X-.3f);
            blocked=true;Need(!pair.Host.RequestVehicleMount(bike.Id,1),"World line vetoes nearby mount");blocked=false;
            Need(pair.Host.RequestVehicleMount(bike.Id,1),"Identical host state accepts when world clear");
            Need(pair.Host.RequestVehicleDismount(bike.Id,new Float3(float.NaN,0,0),0)&&Near(pair.Host.Vehicles[0].Rest.X,bike.Rest.X-.3f,.001f),"Nonfinite dismount falls back to accepted root");
            pair.Now+=1;pair.Tick();Need(!pair.Host.RequestVehicleMount(bike.Id,1),"Stale accepted raw cannot mount");
            for(int i=0;i<4;i++){pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),bike.Rest.X-.3f,0,0,ry:.6f));pair.Tick();}
            Need(pair.Host.RequestVehicleMount(bike.Id,1),"Fresh nearby hand restores mount admission");
            blocked=true;Need(pair.Host.RequestVehicleDismount(bike.Id,new Float3(bike.Rest.X,0,1),0)&&Near(pair.Host.Vehicles[0].Rest.Z,0,.001f),"World-blocked rest falls back to accepted root");
        }
    }
}
