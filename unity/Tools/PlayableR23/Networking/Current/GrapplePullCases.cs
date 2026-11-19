using System;
using System.Collections.Generic;
using System.Reflection;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.Traversal;
using Ashfall.Integration.Playable.Exclusion;
partial class LobbyCases
{
    static object PullCall(R24LanLobby lobby,string name,params object[] args)
    {return typeof(R24LanLobby).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(lobby,args);}
    sealed class PullFixture:IDisposable
    {
        internal readonly Pair Pair=new Pair();internal readonly string Hash=new string('b',64);internal string Gun;
        internal bool Wall,Miss,GuestAim=false;internal float VictimX=0,HostX=8;
        internal PullFixture(R24PlayableMatchRules rules=null,bool teammates=false)
        {
            for(int i=0;i<5;i++)Pair.Tick();
            Pair.Host.BindCombatWorld(delegate(Float3 origin,Float3 direction,float range){return Wall?Math.Min(range,2):range;});
            if(teammates)Need(Pair.Host.HostAssignRoomTeam(Pair.Client.OwnActor,Pair.Host.ReadAuthorityMember(Pair.Host.OwnActor).Team),"Explicit grapple teammate assignment");
            StartRound(Pair,new HazardWorld(),(rules??R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0)).With("catalog",true),Hash);
            R24LanSpawn hostSpawn;Need(Pair.Host.TryReadSpawn(Pair.Host.OwnActor,out hostSpawn),"Host fixture placement");HostX=hostSpawn.Feet.X;Sample();while(HostX<8){HostX=Math.Min(8,HostX+.3f);Sample();}
            for(int i=0;i<4;i++)Sample();
            Pair.Host.RequestLoot("cx4",1,true,new Float3(8,1.2f,.2f));for(int i=0;i<4;i++)Sample();
            Need(Pair.Host.RequestCatalog("weapon","grapple",2,1,new Float3(8,1.2f,.2f),out Gun),"Host canonical grapple catalogue");
            for(int i=0;i<3;i++)Sample();Pair.Host.RequestLoot(Gun,1,false,new Float3());for(int i=0;i<4;i++)Sample();
            Need(Pair.Host.RequestEquipment(Gun,1,false),"Host owns and equips grapple");Sample();
        }
        internal void Sample()
        {
            Pair.Host.RequestEquipment(Gun,1,false);
            Pair.Host.PublishRawTracking(Body(Hash,Rev(Pair.Host),HostX,0,0,rx:0,ry:1.25f,rz:0,rqy:Miss?0:-.70710677f,rqw:Miss?1:.70710677f));
            Pair.Client.PublishRawTracking(Body(Hash,Rev(Pair.Client),VictimX,0,0,rqy:GuestAim?.70710677f:0,rqw:GuestAim?.70710677f:1));Pair.Tick();
        }
        internal bool Fire(){Sample();return (bool)PullCall(Pair.Host,"TryStartPull",Pair.Host.OwnActor,1);}
        public void Dispose(){Pair.Dispose();}
    }
    static void GrapplePullCases()
    {
        PullMotorCases();PullAuthorityCases();PullLifecycleCases();PullValidationCases();PullExtraCases();
        Console.WriteLine("P3b host/pull/motor/validator checks PASS; native Physics and feedback unrun");
    }
    static void PullMotorCases()
    {
        foreach(float dt in new[]{1f/72,1f/120,.05f,.1f}){
            var motor=new R25GrapplePull();motor.Begin(new Float3(100,0,0),0);float distance=0,previous=0;
            for(int k=0;k<20&&motor.Active;k++){
                var wanted=motor.Propose(new Float3(distance,0,0),dt);float speed=wanted.X/dt;
                Need(speed<=48.001f&&speed>=previous-.001f,"Ramp monotonic and bounded");
                motor.Accept(wanted,wanted,dt);distance+=wanted.X;previous=speed;
            }
            motor.End();Need(R25TraversalVector.Length(motor.Velocity)<=6.0001f,"Residual velocity cap");
        }
        var m=new R25GrapplePull();m.Begin(new Float3(10,0,0),0);Float3 p=new Float3();
        for(int i=0;i<100&&m.Active;i++){var d=m.Propose(p,.02f);m.Accept(d,d,.02f);p=R25TraversalVector.Add(p,d);}
        Need(Near(p.X,9.1f,.001f)&&!m.Active,"Stops exactly 0.9m away, no overshoot");
        foreach(bool overhang in new[]{false,true}){
            var world=new HfWorld{N=new[]{overhang?new Float3(1,1,0):new Float3(1,0,0)},D=new[]{overhang?3f:2f},GrazingError=true};
            p=new Float3(0,1.7f,0);m.Begin(new Float3(20,1.7f,0),0);int steps=0;
            while(m.Active&&steps++<150){var d=m.Step(p,.02f,new R25HeadClearance(),world.Sweep,world.Clear);p=R25TraversalVector.Add(p,d);Need(world.Clear(p),"Wall/overhang endpoint stays clear");}
            Need(!m.Active&&steps<90,"Blocked pull ends before duration");
        }
        // Body-only low wall: the head is clear above it, but the standing column must stop.
        p=new Float3(0,1.7f,0);m.Begin(new Float3(100,1.7f,0),0);
        Func<Float3,bool> bodyClear=delegate(Float3 eye){return eye.X+.17f<=1.5f+.000001f;};
        Func<Float3,Float3,Float3> bodySweep=delegate(Float3 eye,Float3 desired){float t=desired.X>0?Math.Min(1,Math.Max(0,(1.5f-.17f-eye.X)/desired.X)):1;return R25TraversalVector.Mul(desired,t);};
        for(int i=0;i<80&&m.Active;i++){var d=m.Step(p,.02f,new R25HeadClearance(),bodySweep,bodyClear);p=R25TraversalVector.Add(p,d);Need(bodyClear(p),"Body low wall clearance independent of clear head");}
        Need(!m.Active&&p.X<1.34f,"Body block ends pull after .25 seconds");
        m.Begin(new Float3(100,0,0),0);for(int i=0;i<15;i++){var d=m.Propose(new Float3(),.1f);m.Accept(d,d,.1f);}Need(!m.Active,"Duration bounded to 1.5s");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-1f}){m.Begin(new Float3(),invalid);if(invalid<0)continue;Need(!m.Active,"Malformed elapsed refuses motor");}
        m.Begin(new Float3(float.NaN,0,0),0);Need(!m.Active,"Malformed target refuses motor");
    }
    static void PullAuthorityCases()
    {
        using(var f=new PullFixture()){
            f.Miss=true;Need(!f.Fire(),"Accepted aim miss");f.Miss=false;f.Wall=true;Need(!f.Fire(),"Host world blocks player");f.Wall=false;
            Need(!(bool)PullCall(f.Pair.Host,"TryStartPull",f.Pair.Host.OwnActor,0),"Wrong hand has no custody");
            // Real producer with a spoofed anchor: the host ignores the supplied point for hit selection.
            Need(f.Pair.Host.PublishWeb(new R25WebState(true,new Float3(8,1,40),false,new Float3()),1),"Host rope shot producer");
            Need(f.Pair.Host.GrapplePulls.Length==1&&f.Pair.Host.GrapplePulls[0].Victim==f.Pair.Client.OwnActor,"Host accepted body overrides spoofed point");
            var row=f.Pair.Host.GrapplePulls[0];Need(Near(row.Target.X,8,.001f)&&Near(row.Target.Z,0,.001f)&&Near(row.Target.Y,1.25f,.001f),"Frozen accepted head destination");
            R25WebState web;Need(!f.Pair.Host.TryReadWeb(f.Pair.Host.OwnActor,out web),"Successful shot has no world tether");
            f.Sample();Need(f.Pair.Client.GrapplePulls.Length==1,"Reliable pull reaches victim");Need(!f.Fire(),"Successful shooter cooldown");
            int corrections=f.Pair.Host.CorrectedRawTracking;for(int i=0;i<12;i++)f.Sample();Need(f.Pair.Host.CorrectedRawTracking==corrections,"Stationary victim may ignore pull without punishment");
            PullCall(f.Pair.Host,"EndPull",f.Pair.Client.OwnActor);
            // Isolate victim immunity from the independent longer shooter cooldown.
            ((Dictionary<string,double>)typeof(R24LanLobby).GetField("pullCooldown",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f.Pair.Host)).Clear();
            Need(!f.Fire(),"Victim immune for three seconds after end");
            for(int i=0;i<61;i++)f.Sample();Need(f.Fire(),"Immunity expires after three seconds");
        }
        foreach(bool friends in new[]{false,true})using(var f=new PullFixture(R24PlayableMatchRules.ForPreset("team_respawn").With("grapple_pull_friends",friends),true)){
            Need(f.Pair.Host.ReadAuthorityMember(f.Pair.Host.OwnActor).Team==f.Pair.Host.ReadAuthorityMember(f.Pair.Client.OwnActor).Team,"Fixture actual teammates");Need(f.Fire()==friends,"Friend rule gates actual host decision");
        }
        using(var f=new PullFixture(R24PlayableMatchRules.ForPreset("free_for_all").With("grapple_pull",false)))Need(!f.Fire(),"Disabled pull rule refuses hit");
        using(var f=new PullFixture()){
            f.Pair.Host.RequestEquipment("px4",1,false);Need(!(bool)PullCall(f.Pair.Host,"TryStartPull",f.Pair.Host.OwnActor,1),"Non-grapple custody refuses shot");
            PullCall(f.Pair.Host,"ForgetRawTracking",f.Pair.Client.OwnActor,false);Need(!(bool)PullCall(f.Pair.Host,"TryStartPull",f.Pair.Host.OwnActor,1),"Missing accepted target pose refuses shot");
        }
        using(var f=new PullFixture()){
            // A late actor's actual Session admission and accepted body are nearer than the original victim.
            var pipe=new Pipe();var late=new R24LanLobby(pipe,pipe.Close,delegate{return "unused";},new string('a',64));pipe.Lost=late.TransportDisconnected;
            f.Pair.HostPipe.Links.Add("late",new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=f.Pair.HostPipe,Back="late"});late.StartClient("server","Late",f.Pair.Now);f.Pair.Host.TransportConnected("late");
            try{
                for(int i=0;i<8;i++){f.Sample();late.Tick(f.Pair.Now);}Need(late.ReportMapLoaded(f.Hash),"Third actor loaded");for(int i=0;i<5;i++){f.Sample();late.Tick(f.Pair.Now);}Need(late.RequestSpawn(),"Third actor spawn");for(int i=0;i<5;i++){f.Sample();late.Tick(f.Pair.Now);}
                R24LanSpawn spawn;Need(late.TryReadSpawn(late.OwnActor,out spawn),"Third placement");float x=spawn.Feet.X;
                for(int i=0;i<25;i++){x+=x<4?.2f:-.2f;late.PublishRawTracking(Body(f.Hash,spawn.Revision,x,0,0));f.Sample();late.Tick(f.Pair.Now);}
                Need(f.Fire()&&f.Pair.Host.GrapplePulls[0].Victim==late.OwnActor,"Nearest of two accepted bodies wins");
            }finally{late.Dispose();pipe.Dispose();}
        }
    }
    static void PullLifecycleCases()
    {
        foreach(string cause in new[]{"death-victim","death-shooter","downed-victim","downed-shooter","disconnect-victim","disconnect-shooter","reset","rule-off","expiry"}){
            var rules=R24PlayableMatchRules.ForPreset("team_revive").With("grapple_pull_friends",true).With("zone_enabled",false).With("protection",0);
            using(var f=new PullFixture(rules,true)){
                Need(f.Fire(),"Lifecycle starts pull "+cause);
                if(cause.StartsWith("death")||cause.StartsWith("downed")){
                    string actor=cause.EndsWith("shooter")?f.Pair.Host.OwnActor:f.Pair.Client.OwnActor;var health=Health(f.Pair.Host,actor);
                    Need(f.Pair.Host.ApplyHostHealthLoss(actor,health.Revision,health.Health,health.Shield,cause.StartsWith("downed")).Accepted,"Actual lifecycle loss");if(cause.StartsWith("downed"))Need(Health(f.Pair.Host,actor).Life==Ashfall.Modules.HealthStates.LifeState.Downed,"Actual downed transition, not death");
                }else if(cause.StartsWith("disconnect")){if(cause.EndsWith("victim"))f.Pair.ClientPipe.Close("server");else f.Pair.HostPipe.Close("client");}
                else if(cause=="reset")Need(f.Pair.Host.BeginRoundSelection(rules,f.Hash,555),"Round reset producer");
                else if(cause=="rule-off")typeof(R24LanLobby).GetField("roundRules",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f.Pair.Host,rules.With("grapple_pull",false));
                else for(int i=0;i<31;i++)f.Sample();
                f.Pair.Tick();Need(f.Pair.Host.GrapplePulls.Length==0,"Pull clears on "+cause);
            }
        }
        using(var f=new PullFixture()){
            Need(f.Fire(),"Replay starts pull");var pipe=new Pipe();var late=new R24LanLobby(pipe,pipe.Close,delegate{return "unused";},new string('a',64));pipe.Lost=late.TransportDisconnected;
            f.Pair.HostPipe.Links.Add("late",new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=f.Pair.HostPipe,Back="late"});late.StartClient("server","Late",f.Pair.Now);f.Pair.Host.TransportConnected("late");
            try{for(int i=0;i<8;i++){f.Sample();late.Tick(f.Pair.Now);}Need(late.GrapplePulls.Length==1,"Late join replays active pull");}finally{late.Dispose();pipe.Dispose();}
            int rejected=f.Pair.Client.RejectedFrames;PullCall(f.Pair.Client,"ReadPull",new Packet{Sequence=999,BaseSequence=-1,Payload=new byte[]{255}});Need(f.Pair.Client.RejectedFrames==rejected,"Malformed event ignored without rejection");
        }
    }
    static void PullExtraCases()
    {
        using(var f=new PullFixture()){
            f.GuestAim=true;
            Need(f.Pair.Host.RegisterWorldWeapon("guest/grapple","grapple",2,new Float3(0,1.2f,.2f)),"Guest canonical tool");
            f.Pair.Client.RequestLoot("cx4",1,true,new Float3(0,1.2f,.2f));for(int i=0;i<4;i++)f.Sample();
            f.Pair.Client.RequestLoot("guest/grapple",1,false,new Float3());for(int i=0;i<4;i++)f.Sample();
            f.Pair.Client.RequestEquipment("guest/grapple",1,false);for(int i=0;i<3;i++)f.Sample();
            Need(f.Pair.Client.PublishWeb(new R25WebState(false,new Float3(),false,new Float3()),1),"Open-space guest shot sends hand only");
            for(int i=0;i<3;i++)f.Sample();Need(f.Pair.Host.GrapplePulls.Length==1&&f.Pair.Host.GrapplePulls[0].Victim==f.Pair.Host.OwnActor,"Actual guest rope command resolves on host");
            Need(f.Pair.Client.GrapplePulls.Length==1,"Guest shooter receives confirmation");
        }
        using(var f=new PullFixture()){
            Need(f.Fire(),"Cooldown test starts");double start=f.Pair.Host.GrapplePulls[0].Start;
            for(int i=0;i<4;i++)f.Sample();f.Pair.Client.PublishWeb(new R25WebState(false,new Float3(),false,new Float3()),2);for(int i=0;i<3;i++)f.Sample();
            Need(f.Pair.Host.GrapplePulls.Length==0,"Victim reliable early completion clears host");
            while(f.Pair.Now<start+5.8)f.Sample();Need(!f.Fire(),"Shooter cooldown still applies before six seconds");
            while(f.Pair.Now<start+6.05)f.Sample();Need(f.Fire(),"Shooter cooldown expires at six seconds");
        }
        foreach(string state in new[]{"dead","downed","bike","pod","self"}){
            var rules=R24PlayableMatchRules.ForPreset("team_revive","range").With("grapple_pull_friends",true).With("zone_enabled",false).With("protection",0);
            using(var f=new PullFixture(rules)){
                string actor=f.Pair.Client.OwnActor;
                if(state=="dead"||state=="downed"){
                    var h=Health(f.Pair.Host,actor);Need(f.Pair.Host.ApplyHostHealthLoss(actor,h.Revision,h.Health,h.Shield,state=="downed").Accepted,"Eligibility life transition");f.Pair.Tick();
                }else if(state=="bike"){
                    var bikes=(Dictionary<string,R25VehicleState>)typeof(R24LanLobby).GetField("vehicles",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f.Pair.Host);
                    bikes["test/bike"]=new R25VehicleState("test/bike",new Float3(),0,new Float3(),0,300,actor,0,1);
                }else if(state=="pod"){
                    f.Pair.Host.BindPodWorld(new FakePodWorld());Need(f.Pair.Client.RequestPodLaunch("0:1:0"),"Pod immunity actual request");f.Pair.Tick();f.Pair.Tick();R24LanPodState pod;Need(f.Pair.Host.TryReadPod(actor,out pod)&&pod.Phase==R24LanPodPhase.Flying,"Actual pod custody");
                }else{f.Pair.Host.PublishRawTracking(Body(f.Hash,Rev(f.Pair.Host),8,0,0,rx:-.7f,ry:1.25f,rz:0,rqy:.70710677f,rqw:.70710677f));f.Pair.Tick();}
                Need(!(bool)PullCall(f.Pair.Host,"TryStartPull",f.Pair.Host.OwnActor,1),"Ineligible victim state "+state);
            }
        }
        // Native source assertions verify the pure motor's position-only owner is actually composed.
        string rig=System.IO.File.ReadAllText("unity/Assets/Ashfall/Integration/Playable/R23VRRig.cs");
        string owner=System.IO.File.ReadAllText("unity/Assets/Ashfall/Integration/Playable/Traversal/R23VRRig.GrapplePull.cs");
        Need(rig.IndexOf("TryAdvanceCarrier()")<rig.IndexOf("EvaluateGrapplePull(frame"),"Carrier stays above external pull");
        Need(rig.Contains("bool evaluatedClimb=!pull&&!vehicle")&&rig.Contains("bool traversal=!pull&&!vehicle")&&rig.Contains("if(pull||SourceGhostMotion"),"Pull cancels climb/traversal/build owners");
        Need(owner.Contains("Activities.ResetCarrierMotion()")&&owner.Contains("Climbing.ResetMotion()")&&owner.Contains("CancelTraversal()"),"Pull entry clears glide/pad/build/climb/jet/own rope");
        Need(!owner.Contains("SetSpawn")&&!owner.Contains("Rotate")&&!owner.Contains("SetPositionAndRotation"),"Pull owner never writes yaw or tracked pose");
        Need(owner.Contains("PullBodyMotion")&&owner.Contains("VerifiedHeadMotion")&&owner.Contains("PullBodyClear"),"Body and verified head endpoints both gate displacement");
    }
    static void PullValidationCases()
    {
        string hash=new string('b',64);var history=new R24LanRawTrackingHistory();
        Need(history.Accept(1,1,Body(hash,1,0,0,0),true,1,Open),"Seed actual host history");
        float allowance=R25GrapplePull.Allowance(1,1.1,3);
        Need(history.Accept(2,1.1,Body(hash,1,6,0,0),true,allowance,Open),"Pull window admits bounded extra displacement");
        Need(!history.Accept(3,1.2,Body(hash,1,100,0,0),true,allowance,Open),"Pull window still rejects teleport");
        Need(!history.Accept(4,1.3,Body(hash,1,30,0,0),true,R25GrapplePull.Allowance(1,4,3),Open)&&history.Rejection=="speed","Outside window restores speed refusal");
        Need(R25GrapplePull.Allowance(2,3,3)>2&&R25GrapplePull.Allowance(2,3.0001,3)==2,"Allowance exact boundary plus grace");
        using(var f=new PullFixture()){
            Need(f.Fire(),"Real correction window starts pull");for(int i=0;i<43;i++)f.Sample();
            int before=f.Pair.Host.CorrectedRawTracking;
            for(int i=0;i<12;i++){f.Pair.Client.PublishRawTracking(Body(f.Hash,Rev(f.Pair.Client),100,0,0));f.Pair.Tick();}
            Need(f.Pair.Host.CorrectedRawTracking>before,"After pull and grace, excessive motion still reaches actual host correction");
        }

    }
}
