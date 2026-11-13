using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;

sealed class HealthTests : IHealthWorld, IAuthorityGate, IInteractionQuery
{
    public double Time; public bool Match=true, Lobby=false, Clear=true, Allow=true, BlockRight=false;
    public readonly Dictionary<string,RescuePose> Poses=new Dictionary<string,RescuePose>();
    public double Now { get{return Time;} } public bool InMatch { get{return Match;} }
    public bool TeamChangesAllowed { get{return Lobby;} }
    public bool TryGetPose(string id,out RescuePose pose){return Poses.TryGetValue(id,out pose);}
    public RuleResult Validate(CommandContext c){return Allow?RuleResult.Accept():RuleResult.Reject("gate");}
    public bool HasLineOfSight(string actor,string target,Float3 a,Float3 b,float range){return Clear&&!(BlockRight&&a.X>0);}
    HealthService service;long sequence;static int checks;
    CommandContext C(string actor="a"){sequence++;return new CommandContext("s",actor,"c"+sequence,sequence,7,true);}
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
    void Yes(RuleResult r){if(!r.Accepted)throw new Exception("unexpected rejection: "+r.Reason);}
    void Init(bool respawn=false,int teams=2)
    {
        Time=0;Match=true;Lobby=false;Clear=true;Allow=true;BlockRight=false;sequence=0;
        service=new HealthService("s",7,teams,respawn,this,this,this);Poses.Clear();
        foreach(string id in new[]{"a","b","c","enemy"})
        {
            Yes(service.Register(C(),id,teams==0?-1:(id=="enemy"?1:0),false));
            Poses[id]=new RescuePose{Tracked=true,PaddlesActive=true,Desktop=true,Facing=true,
                Left=new Float3(-.13f,1,0),Right=new Float3(.13f,1,0),Chest=new Float3(0,1,0)};
        }
    }
    void Down(string id,bool revivable=true){Yes(service.ApplyLoss(C("enemy"),id,service.Read(id).Revision,100,100,revivable));}
    void Charge(string id="a")
    {
        Yes(service.SamplePaddles(C(id)));
        for(int i=0;i<56;i++){Time+=.1;Yes(service.SamplePaddles(C(id)));}
        Check(service.Read(id).ReadyUntil>Time,"desktop charge reaches ready");
    }
    void Run()
    {
        Init();Down("b");HealthSnapshot b=service.Read("b");
        Check(b.Life==LifeState.Downed&&b.Health==0&&b.Shield==0&&b.Deaths==1&&b.DownedUntil==60,"revive mode lethal loss downs for sixty seconds");
        Check(!service.ApplyLoss(C(),"b",b.Revision,1,0,true).Accepted&&service.Read("b").Deaths==1,"downed cannot die twice from damage");
        Check(service.TeamHasLivingMember(0)&&service.TeamHasLivingMember(1),"living team query");
        Charge();long old=b.Revision;double until=service.Read("a").ReadyUntil;
        Clear=false;Check(!service.Revive(C(),"b",old).Accepted&&service.Read("b").Life==LifeState.Downed,"wall occlusion blocks revive");Clear=true;
        BlockRight=true;Check(!service.Revive(C(),"b",old).Accepted,"second hand line of sight independently required");BlockRight=false;
        RescuePose pose=Poses["a"];pose.Right=new Float3(3,1,0);Poses["a"]=pose;
        Check(!service.Revive(C(),"b",old).Accepted,"one valid hand cannot revive");pose.Right=new Float3(.13f,1,0);Poses["a"]=pose;
        var cmd=C();Yes(service.Revive(cmd,"b",old));b=service.Read("b");
        Check(b.Life==LifeState.Alive&&b.Health==50&&b.Shield==0&&b.ProtectionUntil==Time+3&&b.DownedUntil==0,"revive restores fifty and protection");
        Check(!service.Revive(cmd,"b",old).Accepted&&!service.Revive(C(),"b",old).Accepted,"duplicate command and competing revive cannot repeat");
        Check(!service.ApplyLoss(C("enemy"),"b",b.Revision,50,0,true).Accepted,"revive protection blocks damage mutation");
        Down("c");Yes(service.Revive(C(),"c",service.Read("c").Revision));
        Check(service.Read("a").ReadyUntil==until,"multiple allies share original ready window without extension");
        Time=until;Yes(service.Advance(C()));Check(service.Read("a").ReadyUntil==0,"ready expires at exact boundary");
        Init();Down("b");Charge("enemy");Check(!service.Revive(C("enemy"),"b",service.Read("b").Revision).Accepted,"enemy cannot revive");
        Init();Down("b");Charge();Time=60;
        Check(!service.Revive(C(),"b",service.Read("b").Revision).Accepted&&service.Read("b").Life==LifeState.Dead,"bleedout wins at exact deadline without frame-order race");
        Init();Down("b",false);Check(service.Read("b").Life==LifeState.Dead,"non-revivable fall bypasses downed");
        Init(true);Down("b");Check(service.Read("b").Life==LifeState.Dead&&service.Read("b").RespawnAt==5,"respawn mode death deadline");
        long revision=service.Read("b").Revision;Check(!service.Spawn(C(),"b",revision,100,100).Accepted,"early respawn denied");
        Time=5;Yes(service.Spawn(C(),"b",revision,80,20));Check(service.Read("b").Health==80&&service.Read("b").Shield==20&&service.Read("b").RespawnAt==0,"due respawn uses configured start stats");
        Check(!service.ApplyLoss(C("enemy"),"b",revision,80,20,true).Accepted,"old-life combat result rejected after respawn");
        Time=8;Yes(service.ApplyLoss(C("enemy"),"b",service.Read("b").Revision,80,20,true));Check(service.Read("b").Life==LifeState.Dead,"protection ends at exact boundary");
        Init(false,0);Down("b");Check(service.Read("b").Life==LifeState.Dead,"FFA has no downed state");
        Init();Charge();pose=Poses["a"];pose.Tracked=false;Poses["a"]=pose;
        Check(!service.SamplePaddles(C()).Accepted&&service.Read("a").ReadyUntil==0,"tracking loss cancels ready");
        Init();Charge();pose=Poses["a"];pose.Paused=true;Poses["a"]=pose;Down("b");
        Check(!service.Revive(C(),"b",service.Read("b").Revision).Accepted&&service.Read("a").ReadyUntil==0,"pause rechecked on discharge");
        Init();Charge();Yes(service.SetConnected(C(),"a",service.Read("a").Revision,false));
        Check(service.Read("a").ReadyUntil==0&&!service.Read("a").Connected,"disconnect cancels ready");
        Init();Charge();Check(!service.SetTeam(C(),"a",service.Read("a").Revision,1).Accepted,"team change forbidden during match");
        Match=false;Lobby=true;Yes(service.SetTeam(C(),"a",service.Read("a").Revision,1));Check(service.Read("a").Team==1&&service.Read("a").ReadyUntil==0,"lobby team change cancels ready");
        Init();pose=Poses["a"];pose.Desktop=false;Poses["a"]=pose;
        Yes(service.SamplePaddles(C()));for(int i=0;i<20;i++){Time+=.1;Yes(service.SamplePaddles(C()));}
        Check(Math.Abs(service.Read("a").Charge-.2)<.0001,"source stationary facing XR charge takes ten seconds");
        pose.Facing=false;Poses["a"]=pose;for(int i=0;i<5;i++){Time+=.1;Yes(service.SamplePaddles(C()));}
        Check(service.Read("a").Charge<.0001,"orientation apart drains at authored rate");
        pose.Facing=true;pose.LocalLeft=new Float3(100,0,0);Poses["a"]=pose;Time+=.1;Yes(service.SamplePaddles(C()));
        Check(service.Read("a").Charge<.0001,"tracking jump above eight meters per second adds no charge");
        Init();HealthSnapshot initial=service.Read("b");
        Check(!service.ApplyLoss(new CommandContext("s","a","bad",1,7,false),"b",initial.Revision,100,0,true).Accepted,"client authority denied");
        Check(!service.ApplyLoss(new CommandContext("other","a","bad",1,7,true),"b",initial.Revision,100,0,true).Accepted,"session mismatch denied");
        Check(!service.ApplyLoss(new CommandContext("s","a","bad",1,8,true),"b",initial.Revision,100,0,true).Accepted,"epoch mismatch denied");
        Allow=false;Check(!service.ApplyLoss(C(),"b",initial.Revision,100,0,true).Accepted,"Core authority gate enforced");Allow=true;
        Check(!service.ApplyLoss(C(),"b",initial.Revision,Single.NaN,0,true).Accepted&&service.Read("b").Health==100,"nonfinite loss has no mutation");
        Yes(service.ApplyLoss(C(),"b",initial.Revision,25,50,true));Check(service.Read("b").Health==75&&service.Read("b").Shield==50,"combat supplied split applied without reevaluating damage");
        Check(!service.ApplyLoss(C(),"b",initial.Revision,25,50,true).Accepted,"concurrent stale combat decision rejected");
        var high=new CommandContext("s","a","high",1000,7,true);Yes(service.Advance(high));
        Check(!service.Advance(new CommandContext("s","a","late",1,7,true)).Accepted,"evicted command tick cannot reenter replay window");
        Time=-1;Check(!service.Advance(C()).Accepted,"clock regression denied");
        Init();Down("b");Charge();old=service.Read("b").Revision;
        int successes=0;CommandContext one=C(),two=C("c");
        // Charge c before racing; both requests carry the same target incarnation.
        // Existing a window would expire, so charge together in a fresh scenario below.
        Init();Down("b");Yes(service.SamplePaddles(C()));Yes(service.SamplePaddles(C("c")));
        for(int i=0;i<56;i++){Time+=.1;Yes(service.SamplePaddles(C()));Yes(service.SamplePaddles(C("c")));}
        old=service.Read("b").Revision;one=C();two=C("c");
        var start=new ManualResetEvent(false);
        Thread t1=new Thread(delegate(){start.WaitOne();if(service.Revive(one,"b",old).Accepted)Interlocked.Increment(ref successes);});
        Thread t2=new Thread(delegate(){start.WaitOne();if(service.Revive(two,"b",old).Accepted)Interlocked.Increment(ref successes);});
        t1.Start();t2.Start();start.Set();t1.Join();t2.Join();start.Dispose();
        Check(successes==1&&service.Read("b").Revision==old+1,"simultaneous revivers commit exactly one state transition");
        Init();
        for(int i=0;i<4092;i++)Yes(service.Advance(new CommandContext("s","a","bounded"+i,4,7,true)));
        RuleResult full=service.Advance(new CommandContext("s","a","overflow",4,7,true));
        Check(!full.Accepted&&full.Reason=="command-window-full","replay cache saturation fails closed");
        Yes(service.Advance(new CommandContext("s","a","later",1000,7,true)));
        Check(!service.Advance(new CommandContext("s","a","bounded0",4,7,true)).Accepted,"expired command remains rejected after cache pruning");
        Init();Yes(service.Register(C(),"observer",0,true));
        Check(service.Read("observer").Life==LifeState.Spectator&&!service.ApplyLoss(C(),"observer",1,100,0,true).Accepted,"spectator cannot receive combat loss");
        Console.WriteLine("HEALTH_STATES checks="+checks+" failures=0");
    }
    static int Main(){try{new HealthTests().Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

