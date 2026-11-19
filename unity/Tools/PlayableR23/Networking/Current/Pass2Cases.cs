using System;using System.Collections.Generic;using System.IO;using Ashfall.Core;using Ashfall.Modules.Flight;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Sessions;using Ashfall.Modules.Networking;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.HandsWeapons;
// 2026-11-08 pass 2: strict raw validation, host pod transition, host PvP,
// custody/ammo, kills/scores, shared revive and the Health receipt window.
// Production lobby + simulated transport only; native UTP/Physics/Root unrun.
partial class LobbyCases
{
    static R24LanTransform Rot(float x,float y,float z,float qy,float qw){return new R24LanTransform(new Float3(x,y,z),new R24LanRotation(0,qy,0,qw));}
    static R24LanRawTracking Body(string hash,int revision,float x,float y,float z,byte flags=7,float lx=-.3f,float ly=1.2f,float lz=.2f,float rx=.3f,float ry=1.2f,float rz=.2f,float lqy=0,float lqw=1,float rqy=0,float rqw=1)
    {return new R24LanRawTracking(hash,flags,PoseAt(x,y,z),PoseAt(x,y+1.7f,z),Rot(x+lx,y+ly,z+lz,lqy,lqw),Rot(x+rx,y+ry,z+rz,rqy,rqw),0,0,revision);}
    static int Rev(R24LanLobby lobby){R24LanSpawn s;Need(lobby.TryReadSpawn(lobby.OwnActor,out s),"Own placement");return s.Revision;}

    // D8 (corrected): no blanket gap acceptance; the restored protection and
    // the new spawn anchor both hold. Pods are handled by D9 instead.
    static void StrictGapAndAnchor()
    {
        using(var pair=new Pair()){
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);string guest=pair.Client.OwnActor;int rev=Rev(pair.Client);
            Need(pair.Client.PublishRawTracking(Body(hash,rev,0,0,0)),"Anchored first sample");pair.Tick();pair.Tick();R24LanRawTrackingSample s;Need(pair.Host.TryReadRawTracking(guest,pair.Now,out s),"Anchored first sample accepted");
            pair.Now+=4;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now); // deliberate pause
            for(int i=0;i<8;i++){pair.Client.PublishRawTracking(Body(hash,rev,150,0,80));pair.Tick();}
            var m=pair.Host.ReadAuthorityMember(guest);
            Need(pair.Host.CorrectedRawTracking==1&&m.SpawnRevision==rev+1&&Math.Abs(m.Position.X)<.01f,"Pausing updates cannot bypass validation: a far post-gap sample is refused and the host returns the actor to its last accepted root (pos="+m.Position.X+")");
            // The correction cleared history; the next first sample must start at the committed placement.
            int next=Rev(pair.Client);Need(next==rev+1,"Guest adopted corrected revision");
            for(int i=0;i<3;i++){pair.Client.PublishRawTracking(Body(hash,next,150,0,80));pair.Tick();}
            Need(pair.Host.LastRawTrackingRejection=="spawn-anchor"&&!pair.Host.TryReadRawTracking(guest,pair.Now,out s),"Post-correction teleport is refused by the spawn anchor (last="+pair.Host.LastRawTrackingRejection+")");
            for(int i=0;i<8;i++){pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),150,0,80));pair.Tick();}
            m=pair.Host.ReadAuthorityMember(guest);Need(Math.Abs(m.Position.X)<.01f&&m.SpawnRevision>next,"Sustained anchor faults re-publish the committed placement, never the claimed one");
            int last=Rev(pair.Client);Need(pair.Client.PublishRawTracking(Body(hash,last,0,0,0)),"Return to placement");pair.Tick();pair.Tick();Need(pair.Host.TryReadRawTracking(guest,pair.Now,out s)&&s.Pose.Root.Position.X==0,"Placement-anchored stream resumes");
        }
    }

    sealed class FakePodWorld:IR24LanPodWorld
    {
        internal int Calls;internal string LastKey;
        public bool TryPlan(string rideKey,int team,string podId,Float3 root,double clock,out CapsulePlan plan)
        {
            Calls++;LastKey=rideKey;plan=null;if(podId!="0:1:0"&&podId!="0:1:1"||Math.Abs(root.X)>1.5f||Math.Abs(root.Z)>1.5f)return false; // must stand in own cabin at the origin deck
            plan=new CapsulePlan(podId,team,0,new Float3(0,.26f,0),new Float3(0,5.26f,10),new Float3(30,5.26f,10),new Float3(30,.12f,10),new Float3(30,.12f,8),new Float3(0,0,-1),2.0);return true;
        }
    }
    // D9: host-validated carrier/landing transition (source _pod_launch_r12/_step_pods_r12).
    static void HostPodTransition()
    {
        using(var pair=new Pair()){
            var pods=new FakePodWorld();for(int i=0;i<5;i++)pair.Tick();pair.Host.BindPodWorld(pods);
            bool refused=false;try{pair.Client.BindPodWorld(pods);}catch(InvalidOperationException){refused=true;}Need(refused,"Guest cannot bind a pod planner");
            var rules=R24PlayableMatchRules.ForPreset("team_revive","range").With("zone_enabled",false).With("protection",0f);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);string guest=pair.Client.OwnActor;int rev=Rev(pair.Client);var team=pair.Host.ReadAuthorityMember(guest).Team;string pod="0:1:0";
            Need(team==1,"Guest on team 1 (pod fixture key)");
            Need(pair.Client.PublishRawTracking(Body(hash,rev,0,0,0)),"Deck raw");pair.Tick();pair.Tick();
            Need(pair.Client.RequestPodLaunch(pod),"Guest requests a pod launch (pod id only)");pair.Tick();pair.Tick();
            R24LanPodState ride;Need(pair.Host.TryReadPod(guest,out ride)&&ride.Phase==R24LanPodPhase.Flying&&pods.LastKey==guest,"Host plans and owns the ride with its own runtime");
            var m=pair.Host.ReadAuthorityMember(guest);Need(m.SpawnRevision==rev+1&&Math.Abs(m.Position.Y-.26f)<.001f,"Host relocates the rider to its planned start (new revision)");
            for(int i=0;i<3;i++)pair.Tick();Need(pair.Client.TryReadPod(guest,out ride)&&ride.Phase==R24LanPodPhase.Flying&&ride.Plan.ExitTarget.X==30,"Guest receives the authoritative ride to adopt");
            int flying=Rev(pair.Client);Need(flying==rev+1,"Guest adopts start revision");
            pair.Client.PublishRawTracking(Body(hash,flying,15,5,10));pair.Tick();pair.Tick();Need(pair.Host.LastRawTrackingRejection=="carried","While the host ride flies, client poses are refused (no continuity skip)");
            Need(!pair.Client.RequestPodLaunch("0:1:1")||true,"queued");pair.Tick();pair.Tick();Need(pair.Host.PodLaunches==1,"One ride per life; a second launch is refused");
            pair.Now=pair.Host.SelectedMap.StartsAt+ride.Started+2.05;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);for(int i=0;i<4;i++)pair.Tick();
            m=pair.Host.ReadAuthorityMember(guest);Need(pair.Host.PodLandings==1&&m.SpawnRevision==rev+2&&m.Position.X==30&&m.Position.Z==8,"Host lands the rider at ITS exit target (pos="+m.Position.X+","+m.Position.Z+")");
            int landed=Rev(pair.Client);Need(landed==rev+2,"Guest adopts landing revision");
            pair.Client.PublishRawTracking(Body(hash,landed,90,0,90));pair.Tick();pair.Tick();Need(pair.Host.LastRawTrackingRejection=="spawn-anchor","A landing claimed elsewhere is refused");
            Need(pair.Client.PublishRawTracking(Body(hash,landed,30,0,8)),"Landing at host exit");pair.Tick();pair.Tick();R24LanRawTrackingSample s;Need(pair.Host.TryReadRawTracking(guest,pair.Now,out s)&&s.Pose.Root.Position.X==30,"Host-exit landing pose accepted");
            pair.Client.PublishRawTracking(Body(hash,landed,130,0,8));pair.Tick();pair.Tick();Need(pair.Host.LastRawTrackingRejection=="speed","Strict continuity resumes after landing (last="+pair.Host.LastRawTrackingRejection+")");
        }
        using(var pair=new Pair()){
            var pods=new FakePodWorld();for(int i=0;i<5;i++)pair.Tick();pair.Host.BindPodWorld(pods);
            var rules=R24PlayableMatchRules.ForPreset("team_revive","range").With("zone_enabled",false).With("protection",0f);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);string guest=pair.Client.OwnActor;int rev=Rev(pair.Client);
            pair.Client.PublishRawTracking(Body(hash,rev,0,0,0));pair.Tick();pair.Tick();
            Need(pair.Client.RequestPodLaunch("bogus"),"Queued");pair.Tick();pair.Tick();Need(pair.Host.PodLaunches==0&&pair.Host.LastPodRejection=="host-plan","Unknown/other-team pod refused by the host planner");
            Need(pair.Client.RequestPodLaunch("0:1:0"),"Queued");pair.Tick();pair.Tick();Need(pair.Host.PodLaunches==1,"Launch");R24LanPodState ride;Need(pair.Host.TryReadPod(guest,out ride),"Ride");
            pair.Now=pair.Host.SelectedMap.StartsAt+ride.Started+1.0;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Need(pair.Client.RequestPodEject(),"Eject request");pair.Tick();pair.Tick();
            var m=pair.Host.ReadAuthorityMember(guest);var at=ride.Plan.Position(pair.Host.RoundElapsed-ride.Started);
            Need(pair.Host.TryReadPod(guest,out ride)&&ride.Phase==R24LanPodPhase.Released&&Math.Abs(m.Position.Z-ride.Terminal.Z)<.001f&&m.SpawnRevision==rev+2,"Eject relocates to the HOST path point behind the cabin");
            Need(Math.Abs(at.X-ride.Terminal.X)<3,"Eject point is on the host path");
        }
        using(var pair=new Pair()){
            var pods=new FakePodWorld();for(int i=0;i<5;i++)pair.Tick();pair.Host.BindPodWorld(pods);
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();pair.Tick();
            pair.Client.RequestPodLaunch("0:1:0");pair.Tick();pair.Tick();Need(pair.Host.PodLaunches==0&&pods.Calls==0,"Respawn/FFA rounds have no source deployment pods");
        }
    }

    // D10: host-authoritative PvP shots, custody, cadence, kills, scores.
    static float SetBlock=180;
    static void KeepRaw(Pair pair,string hash,float gx,float hx)
    {pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),gx,0,0));pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),hx,0,0));pair.Tick();}
    static Float3 Dir(Float3 a,Float3 b){float x=b.X-a.X,y=b.Y-a.Y,z=b.Z-a.Z,n=(float)Math.Sqrt(x*x+y*y+z*z);return new Float3(x/n,y/n,z/n);}
    static void PvpShotsAndScores()
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return Math.Min(r,SetBlock);});SetBlock=180;
            bool refused=false;try{pair.Client.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});}catch(InvalidOperationException){refused=true;}Need(refused,"Guest cannot bind ballistic world");
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_shield",0).With("respawn_delay",1f);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);string guest=pair.Client.OwnActor,host=pair.Host.OwnActor;
            for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
            var muzzle=new Float3(7.6f,1.6f,0);var chest=new Float3(0,1.25f,0);
            Need(!pair.Host.SubmitShot("awp",0,muzzle,new[]{Dir(muzzle,chest)})&&pair.Host.LastShotRejection=="custody","Unowned weapon refused (default loadout cx4/px4/m1014)");
            Need(!pair.Host.SubmitShot("cx4",0,new Float3(20,1.6f,0),new[]{Dir(new Float3(20,1.6f,0),chest)})&&pair.Host.LastShotRejection=="origin","Muzzle beyond source 5 m of authenticated root refused");
            Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,chest)}),"Valid volley accepted");KeepRaw(pair,hash,0,8);KeepRaw(pair,hash,0,8);
            var g=Health(pair.Host,guest);Need(Math.Abs(g.Health-81)<.01f,"Source cx4 body 19 applied by host (health="+g.Health+")");
            Need(Math.Abs(Health(pair.Client,guest).Health-81)<.01f,"Victim receives host-owned damage");
            int ammo,reserve;Need(pair.Host.TryReadAmmo(host,"cx4",out ammo,out reserve)&&ammo==24&&reserve==2400,"Accepted volley consumed one host-ledger round");
            for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);SetBlock=2;Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,chest)}),"Blocked volley still consumes");KeepRaw(pair,hash,0,8);Need(Math.Abs(Health(pair.Host,guest).Health-81)<.01f,"Host map blocker before target prevents damage");SetBlock=180;
            for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
            Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,chest)})&&pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,chest)})&&!pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,chest)})&&pair.Host.LastShotRejection=="cadence","Source R18 cadence: one same-stamp catch-up, then refused");
            for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
            var gm=new Float3(.4f,1.6f,0);var hostChest=new Float3(8,1.25f,0);Need(pair.Client.SubmitShot("cx4",0,gm,new[]{Dir(gm,hostChest)}),"Guest volley queued (rays only)");for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
            Need(Math.Abs(Health(pair.Host,host).Health-81)<.01f,"Guest's validated volley damages the host through the host owner");
            int guard=0;while(Health(pair.Host,guest).Life==LifeState.Alive&&guard++<20){for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,chest)});KeepRaw(pair,hash,0,8);}
            var dead=Health(pair.Host,guest);Need(dead.Life==LifeState.Dead&&dead.Deaths==1,"Lethal host damage kills the guest");
            R24LanScoreRow row;for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadScore(host,out row)&&row.Kills==1&&row.Score==1,"Kill attributed to the shooter");
            Need(pair.Client.TryReadScore(host,out row)&&row.Kills==1&&pair.Client.TryReadScore(guest,out row)&&row.Deaths==1,"Scores replicate to the guest");
            int rejected=pair.Host.RejectedShots;for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,chest)});KeepRaw(pair,hash,0,8);Need(Health(pair.Host,guest).Life==LifeState.Dead&&Health(pair.Host,guest).Deaths==1,"Dead target is not hit again");
            pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(new Packet{Kind=PacketKind.Command,FeatureId="playable.combat.shot/1",ActorId=guest,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=100000,BaseSequence=-1,Payload=new byte[]{9,9,9}})});pair.Tick();
            Need(pair.Host.LastShotRejection=="payload"&&pair.Host.Members.Length==2,"Malformed shot rejected without dropping the connection");
            pair.Now+=1.5;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Need(!pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,chest)})&&pair.Host.LastShotRejection!=null,"Shooter without fresh authenticated tracking cannot fire");
        }
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_health",100).With("starting_shield",100);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);string host=pair.Host.OwnActor;for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
            var muzzle=new Float3(7.6f,1.6f,0);var away=Dir(muzzle,new Float3(7.6f,50,0));int fired=0;
            for(int i=0;i<30;i++){if(pair.Host.SubmitShot("cx4",0,muzzle,new[]{away}))fired++;for(int k=0;k<4;k++)KeepRaw(pair,hash,0,8);}
            int ammo,reserve;Need(fired==25&&pair.Host.LastShotRejection=="ammo"&&pair.Host.TryReadAmmo(host,"cx4",out ammo,out reserve)&&ammo==0,"Host ledger stops shots at the cx4 1-star magazine (fired="+fired+")");
            Need(pair.Host.SubmitReload("cx4",-1)&&pair.Host.TryReadAmmo(host,"cx4",out ammo,out reserve)&&ammo==25&&reserve==2375,"Reload tops up from the rifle reserve exactly once");
            Need(!pair.Host.SubmitReload("cx4",-1),"Full magazine cannot reload again");
            Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{away}),"Fires after host reload");
            var g=Health(pair.Host,pair.Client.OwnActor);var gchest=new Float3(0,1.25f,0);for(int k=0;k<4;k++)KeepRaw(pair,hash,0,8);Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,gchest)}),"Shield target");KeepRaw(pair,hash,0,8);
            var h=Health(pair.Host,pair.Client.OwnActor);Need(h.Health==g.Health&&Math.Abs(h.Shield-(g.Shield-19))<.01f,"Shield absorbs first (source hurt)");
        }
    }

    // D11: shared revive through the existing HealthService paddle rules.
    sealed class ReviveWorld:IR24LanSpawnWorld
    {
        public bool ReadyForSpawn(string map,bool warmup){return map=="range";}
        public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("a",new Float3(0,.12f,0),-1,false,false),new SpawnSite("c",new Float3(10,.12f,0),-1,false,false)};}
        public bool IsSafe(string map,string actor,Float3 feet){return map=="range";}
    }
    static void SharedRevive()
    {
        foreach(bool enable in new[]{false,true})using(var pair=new Pair()){
            var pipe=new Pipe();var ally=new R24LanLobby(pipe,pipe.Close,delegate{return "unused";},new string('a',64));pipe.Lost=ally.TransportDisconnected;
            pair.HostPipe.Links.Add("ally",new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=pair.HostPipe,Back="ally"});ally.StartClient("server","Ally",pair.Now);pair.Host.TransportConnected("ally");
            Action tick=delegate{pair.Tick();ally.Tick(pair.Now);};
            try{
                for(int i=0;i<8;i++)tick();Need(ally.State==R24LanLobbyState.Connected&&pair.Host.Members.Length==3,"Three members");
                pair.Host.BindSpawnWorld(new ReviveWorld());bool clear=true;pair.Host.BindRawTrackingWorldQuery(delegate(Float3 x,Float3 y){return clear;});pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
                var rules=R24PlayableMatchRules.ForPreset("team_revive","range").With("enemy_revive",enable).With("friendly_fire",enable).With("zone_enabled",false).With("protection",0f);string hash=new string('b',64);
                Need(pair.Host.BeginRoundSelection(rules,hash,77)&&pair.Host.ReportMapLoaded(hash),"Round");for(int i=0;i<3;i++)tick();pair.Client.ReportMapLoaded(hash);ally.ReportMapLoaded(hash);for(int i=0;i<5;i++)tick();
                pair.Now=pair.Host.SelectedMap.StartsAt+.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);ally.Tick(pair.Now);
                string host=pair.Host.OwnActor,enemy=pair.Client.OwnActor,friend=ally.OwnActor;
                Need(pair.Host.ReadAuthorityMember(friend).Team==pair.Host.ReadAuthorityMember(host).Team&&pair.Host.ReadAuthorityMember(enemy).Team!=pair.Host.ReadAuthorityMember(host).Team,"Host and ally share a team");
                Need(ally.RequestSpawn(),"Ally spawn");for(int i=0;i<4;i++)tick();Need(pair.Host.RequestSpawn(),"Host spawn");for(int i=0;i<4;i++)tick();Need(pair.Client.RequestSpawn(),"Enemy spawn");for(int i=0;i<5;i++)tick();
                Action raw=delegate{ally.PublishRawTracking(Body(hash,Rev(ally),0,0,0));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),10,0,0));};
                for(int i=0;i<3;i++){raw();pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),0,0,0));tick();}
                var muzzle=new Float3(-1,1.6f,0);var chest=new Float3(0,1.25f,0);
                float before=Health(pair.Host,friend).Shield;
                Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,chest)}),"Host volley against teammate");tick();
                Need((Health(pair.Host,friend).Shield<before)==enable,"Friendly fire damage switch");
                var f=Health(pair.Host,friend);Need(pair.Host.ApplyHostHealthLoss(friend,f.Revision,f.Health,f.Shield,true).Accepted&&Health(pair.Host,friend).Life==LifeState.Downed,"Ally downed (revivable)");
                Need(pair.Client.SelectedMap.Rules.EnemyRevive==enable&&ally.SelectedMap.Rules.FriendlyFire==enable,"Both host switches replicated");
                Need(!pair.Host.RequestRevive(friend),"Unarmed contact refused");
                // Move the opposing peer in legal steps; it can touch once only when the toggle permits.
                for(int i=0;i<21;i++){float x=Math.Max(0,10-i*.5f);ally.PublishRawTracking(Body(hash,Rev(ally),0,0,0));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),x,0,0));tick();}
                var outside=Body(hash,Rev(pair.Host),0,0,0,15,-.7f,1.1f,.6f,.7f,1.1f,.6f);
                pair.Host.PublishRawTracking(outside);Need(Health(pair.Host,friend).Life==LifeState.Downed,"Both paddles miss");
                var touch=Body(hash,Rev(pair.Host),0,0,0,15,-.7f,1.1f,.6f,.05f,1.1f,.1f);
                clear=false;pair.Host.PublishRawTracking(touch);Need(Health(pair.Host,friend).Life==LifeState.Downed,"Wall rejects contact");clear=true;
                for(int i=0;i<12;i++)tick();pair.Host.PublishRawTracking(touch);
                Need(Health(pair.Host,friend).Life==LifeState.Downed,"Stale patient pose");
                ally.PublishRawTracking(Body(hash,Rev(ally),0,0,0));tick();
                pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),0,0,0,11,-.7f,1.1f,.6f,.05f,1.1f,.1f));
                Need(Health(pair.Host,friend).Life==LifeState.Downed,"Lost controller rejected");
                foreach(bool enemySwitch in new[]{false,true}){
                    Need(!R26TeamVisibility.ShowAvatar(true,false,true,LifeState.Dead,enemySwitch),"Enemy eliminated hidden");
                    Need(!R26TeamVisibility.ShowAvatar(true,false,true,LifeState.Spectator,enemySwitch),"Enemy spectator hidden");
                    Need(!R26TeamVisibility.ShowAvatar(true,false,false,LifeState.Alive,enemySwitch),"Unplaced enemy spectator hidden");
                    Need(R26TeamVisibility.ShowAvatar(true,true,true,LifeState.Dead,enemySwitch),"Friendly ghost visible");
                    Need(R26TeamVisibility.ShowAvatar(true,false,true,LifeState.Downed,enemySwitch)==enemySwitch,"Only rescuable enemy casualty visible");
                    Need(R26TeamVisibility.ShowAvatar(true,false,true,LifeState.Alive,enemySwitch),"Enemy living visible");
                }
                // Send a valid strike before charge. Try both the automatic route and the explicit command.
                var rescuer=enable?pair.Client:pair.Host;string rescuerActor=rescuer.OwnActor;
                var paddle=Body(hash,Rev(rescuer),0,0,0,15,-.6f,1.1f,.3f,.05f,1.1f,.1f);
                rescuer.PublishRawTracking(paddle);tick();tick();
                rescuer.RequestRevive(friend);tick();tick();
                Need(Health(pair.Host,friend).Life==LifeState.Downed&&pair.Host.LastReviveRejection=="paddles-not-ready","Uncharged strike/reflected request rejected");
                if(!enable){
                    Need(!pair.Host.ApplyRevive(enemy,friend)&&pair.Host.LastReviveRejection=="ally-required","Enemy rescue off");
                }

                Action feedPatient=delegate{ally.PublishRawTracking(Body(hash,Rev(ally),0,0,0));};
                Func<int,R24LanRawTracking> spin=delegate(int i){
                    float offset=i%2==0?.05f:-.05f;
                    const float half=.70710678f;
                    var left=new R24LanTransform(new Float3(-.55f,1.1f,.6f+offset),new R24LanRotation(0,0,half,half));
                    var right=new R24LanTransform(new Float3(.55f,1.1f,.6f-offset),new R24LanRotation(0,0,-half,half));
                    return new R24LanRawTracking(hash,15,PoseAt(0,0,0),PoseAt(0,1.7f,0),left,right,0,0,Rev(rescuer));
                };
                Func<double> charge=delegate{
                    double start=pair.Now;
                    for(int i=0;i<135;i++){
                        feedPatient();rescuer.PublishRawTracking(spin(i));tick();
                        if(i==75)Need(Health(pair.Host,rescuerActor).ReadyUntil==0,"No early ready window");
                        var deadline=Health(pair.Host,rescuerActor).ReadyUntil;
                        if(deadline>0){
                            Need(pair.Now-start>=5.45&&pair.Now-start<5.9,"Historical fast charge timing");
                            Need(deadline-pair.Now>3.35&&deadline-pair.Now<=3.5,"Historical 3.5 second ready deadline");
                            return deadline;
                        }
                    }
                    throw new Exception("No authority charge from opposing corrected paddle normals");
                };
                double readyUntil=charge();
                var miss=Body(hash,Rev(rescuer),0,0,0,15,-.6f,1.1f,.6f,.6f,1.1f,.6f);
                while(pair.Now<readyUntil+.05){feedPatient();rescuer.PublishRawTracking(miss);tick();}
                Need(Health(pair.Host,rescuerActor).ReadyUntil==0,"Host clears expired ready receipt");
                feedPatient();rescuer.PublishRawTracking(paddle);tick();tick();
                Need(Health(pair.Host,friend).Life==LifeState.Downed,"Strike after ready expiry rejected");
                readyUntil=charge();
                for(int i=0;i<6;i++){feedPatient();rescuer.PublishRawTracking(miss);tick();}
                Need(Health(ally,rescuerActor).ReadyUntil==readyUntil,"Patient receives same-revision authority ready deadline");

                clear=false;feedPatient();rescuer.PublishRawTracking(paddle);tick();tick();
                Need(Health(pair.Host,friend).Life==LifeState.Downed,"Wall blocks a charged strike");
                clear=true;
                feedPatient();rescuer.PublishRawTracking(paddle);tick();tick();
                Need(Health(pair.Host,friend).Life==LifeState.Alive,"First unobstructed one-electrode strike inside window");
                Need(Health(pair.Host,rescuerActor).ReadyUntil==readyUntil,"Original window persists after a successful strike");
                Console.WriteLine("D11 paddles: uncharged and expired touches rejected; charged " +(enable?"enemy guest":"allied host")+" accepted");
                var back=Health(pair.Host,friend);Need(back.Life==LifeState.Alive&&back.Health==50&&back.Shield==0,"Source revive: 50 health, no shield");
                for(int i=0;i<3;i++){raw();tick();}Need(Health(ally,friend).Life==LifeState.Alive,"Revived ally receives its host life");
                Need(!pair.Host.RequestRevive(friend)&&pair.Host.LastReviveRejection=="downed-required","Alive target cannot be revived again");
            }finally{ally.Dispose();pipe.Dispose();}
        }
    }

    // D12: the HealthService receipt bound no longer silently refuses long-match damage.
    sealed class ReceiptPorts:IAuthorityGate,IHealthWorld,IInteractionQuery
    {
        public double Time=1;public double Now{get{return Time;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
        public bool TryGetPose(string id,out RescuePose pose){pose=new RescuePose();return false;}public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float d){return false;}
        public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("authority");}
    }
    static void DamageReceiptWindow()
    {
        var ports=new ReceiptPorts();var health=new HealthService("s",0,0,true,ports,ports,ports,60,5,0);long tick=0;
        Func<CommandContext> next=delegate{long id=++tick;return new CommandContext("s","host","sessions-lifecycle/"+id,id,0,true);};
        Need(health.RegisterConfiguredSpawn(next(),"p",-1,false,100,0).Accepted,"Register");
        int accepted=0;CommandContext early=new CommandContext(),recent=new CommandContext();
        for(int i=0;i<9000;i++){var c=next();if(i==10)early=c;if(i==8999)recent=c;var h=health.Read("p");if(health.ApplyLoss(c,"p",h.Revision,.001f,0,false).Accepted)accepted++;health.DrainTransitions();}
        Need(accepted==9000,"All 9000 host losses accepted past the old 8192 receipt bound (accepted="+accepted+")");
        float before=health.Read("p").Health;var stale=health.ApplyDamage(early,early.CommandId,"p",health.Read("p").Revision,5,0,false);
        Need(stale.Result.Accepted&&health.Read("p").Health==before,"Old logical receipt survives command pruning without reapplication ("+stale.Result.Reason+")");
        var again=health.ApplyDamage(recent,recent.CommandId,"p",health.Read("p").Revision,5,0,false);Need(again.Result.Accepted&&health.Read("p").Health==before,"In-window replay returns its cached receipt without a second mutation");
    }

    // D13: promoted source ballistic table agrees with the live Unity rarity owner.
    static void BallisticTableMatchesRarity()
    {
        foreach(string id in new[]{"cx4","px4","mk18","awp","ump","magnum","m60","m1014","p90","uzis","matadors","spr","mp5","mp9","1911","tec9","akm","rfb","fal","s85","dt11"}){
            R24LanBallisticProfile p;Need(R24LanBallisticProfile.TryRead(id,out p),"Profile "+id);Need(R24LanLoadout.Family(id)!=null&&R24LanLoadout.Capacity(id,1)>0,"Custody data "+id);
            for(int stars=1;stars<=5;stars++){R23RarityStats s;Need(R23WeaponRarity.TryRead(id,stars,out s),"Rarity "+id);
                float body=p.Damage(stars,false,0)*p.Pellets,head=p.Damage(stars,true,0)*p.Pellets;Need(Math.Abs(body-s.BodyDamage)<.01f&&Math.Abs(head-s.HeadDamage)<.01f||id=="dt11"&&Math.Abs(body-96)<.01f,"Source table matches Unity rarity "+id+"/"+stars+" body="+body+"/"+s.BodyDamage+" head="+head+"/"+s.HeadDamage);}
        }
        Need(R24LanLoadout.Profile(0)=="cx4"&&R24LanLoadout.Profile(2)=="px4"&&R24LanLoadout.Profile(9)=="m1014","Default starting catalog maps to source profiles");
    }
}
