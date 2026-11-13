using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ashfall.Core;
using Ashfall.Integration;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;

class CombatHealthBridgeTests : IHealthWorld,ICombatEvidence,IAuthorityGate,IInteractionQuery
{
    public double Now {get;set;}
    public bool InMatch {get{return true;}}
    public bool TeamChangesAllowed {get{return false;}}
    bool paddles;
    public bool TryGetPose(string id,out RescuePose pose) {pose=new RescuePose{Tracked=true,PaddlesActive=true,Desktop=true,Facing=true,Left=new Float3(0,1,0),Right=new Float3(0,1,0),Chest=new Float3(0,1,0)};return paddles;}
    public RuleResult Validate(CommandContext c)
    {return c.IsAuthority&&(c.ActorId=="host"||c.ActorId=="a"||c.ActorId=="b"||c.ActorId=="c")?RuleResult.Accept():RuleResult.Reject("actor");}
    public bool HasLineOfSight(string a,string b,Float3 o,Float3 p,float r){return true;}
    readonly Dictionary<string,HitEvidence> facts=new Dictionary<string,HitEvidence>();
    HealthService health;CombatHealthBridge bridge;WeaponLease weapon;long serial;static int checks;
    CommandContext C(string actor,string command,long tick=1){return new CommandContext("s",actor,command,tick,2,true);}
    public bool TryResolve(CommandContext c,string id,out HitEvidence e){return facts.TryGetValue(id,out e);}
    void Setup(bool respawn=false)
    {
        Now=0;facts.Clear();serial=0;paddles=false;
        health=new HealthService("s",2,2,respawn,this,this,this,60,5,0);
        health.Register(C("host","register-a"),"a",0,false);
        health.Register(C("host","register-b"),"b",1,false);
        health.Register(C("host","register-c"),"c",1,false);
        bridge=new CombatHealthBridge(health,this,this,this,this,"s",2);
        weapon=bridge.Equip(C("host","equip"),"rifle-a","a","kestrel",1,1);
    }
    string Hit(string target="b",DamageSource source=DamageSource.CurrentActorIntent,DamageKind kind=DamageKind.Bullet,float amount=25,long tick=1)
    {
        serial++;string id="hit-"+serial;
        HostEvent ev=bridge.PublishEvent(C("host","event-"+serial,tick),serial,tick+100,source,kind,source==DamageSource.HostEnvironment?null:weapon,tick);
        HostHit hit=bridge.PublishHit(C("host","publish-"+serial,tick),id,ev,"sub",target,0,tick);
        facts.Add(id,new HitEvidence{Identity=hit,ActorInputReady=true,BaseDamage=amount,DamageMultiplier=1,HeadRatio=1,
            Origin=new Float3(0,1,0),ViewOrigin=new Float3(0,1,0),BladeBase=new Float3(0,1,0),Point=new Float3(0,1,1),TargetHead=new Float3(0,1.5f,1),MaximumRange=100});
        return id;
    }
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    void CheckOldAlias(string id,long tick)
    {
        HostHit old=facts[id].Identity;string alias="alias-"+id;
        HostHit result=bridge.PublishHit(C("host","alias-publish",tick),alias,old.Event,old.SubhitId,old.TargetId,old.ContactEntry,tick);
        facts.Add(alias,facts[id]);long before=health.Read(old.TargetId).Revision;
        Check(Object.ReferenceEquals(result,old),"alias refers to original physical collision capability");
        string actor=old.Event.Source==DamageSource.CurrentActorIntent?old.Event.OwnerId:"host";
        Check(!bridge.Apply(C(actor,"alias-delivery",tick),alias,tick,true).Result.Accepted&&health.Read(old.TargetId).Revision==before,"new alias cannot renew old lifecycle binding");
        bridge.PublishHit(C("host","alias-publish-retry",tick),alias,old.Event,old.SubhitId,old.TargetId,old.ContactEntry,tick);
        Check(!bridge.Apply(C(actor,"alias-delivery-retry",tick),alias,tick,true).Result.Accepted&&health.Read(old.TargetId).Revision==before,"same alias remains bound after rejected retry");
    }
    void Run()
    {
        Setup();string first=Hit();var receipt=bridge.Apply(C("a","first"),first,1,true);
        Check(receipt.Result.Accepted&&health.Read("b").Shield==75,"current Combat Commit applies actual Health loss");
        Check(receipt.Events.Count(e=>e.Kind=="damage")==1,"actual health damage transition exported");
        var retry=bridge.Apply(C("a","retry"),first,1,true);
        Check(retry.Result.Accepted&&health.Read("b").Shield==75,"fresh delivery command returns same damage receipt without mutation");
        Check(!bridge.Apply(C("b","steal-receipt"),first,1,true).Result.Accepted,"cached receipt remains bound to event principal");
        string second=Hit();
        Check(!bridge.Apply(C("a","retry"),second,1,true).Result.Accepted&&health.Read("b").Shield==75,"cached redelivery command cannot bind a second hit");
        Check(bridge.Apply(C("a","second"),second,1,true).Result.Accepted&&health.Read("b").Shield==50,"distinct hit same simulation tick commits");
        Check(health.ApplyLoss(C("host","independent"),"a",health.Read("a").Revision,0,1,true).Accepted,"independent current health mutation");
        var withExtra=bridge.Apply(C("a","retry-two"),first,1,true);
        var repeated=bridge.Apply(C("a","retry-three"),first,1,true);
        Check(withExtra.Events.Select(e=>e.Sequence).SequenceEqual(repeated.Events.Select(e=>e.Sequence)),"newly drained transitions persist across repeated cached replies");
        long last=repeated.Events.Max(e=>e.Sequence);
        Check(!bridge.Acknowledge(C("a","bad-ack"),last).Accepted,"only host publisher can acknowledge outbox");
        Check(bridge.Acknowledge(C("host","ack"),last).Accepted,"host publisher acknowledges owned transitions");
        Check(bridge.Apply(C("a","after-ack"),first,1,true).Events.Length==0,"acknowledged events no longer redelivered");

        Setup();first=Hit();var attempts=new CombatHealthBridge.Receipt[16];
        Parallel.For(0,16,i=>{attempts[i]=bridge.Apply(C("a","duplicate-"+i),first,1,true);});
        Check(attempts.All(r=>r.Result.Accepted)&&health.Read("b").Shield==75,"sixteen concurrent same-hit deliveries commit once");
        Check(attempts.All(r=>r.Events.Count(e=>e.Kind=="damage")==1),"duplicate replies preserve one stable damage event");

        Setup();first=Hit();second=Hit();
        Parallel.Invoke(()=>bridge.Apply(C("a","concurrent-one"),first,1,true),()=>bridge.Apply(C("a","concurrent-two"),second,1,true));
        Check(health.Read("b").Shield==50&&health.Read("b").Revision==3,"two concurrent distinct hits observe serialized fresh revisions");

        Setup();first=Hit();health.ApplyLoss(C("host","interleave"),"b",1,0,1,true);
        Check(bridge.Apply(C("a","after-interleave"),first,1,true).Result.Accepted&&health.Read("b").Shield==74,"current bridge resolves pools after prior shield mutation");

        Setup();first=Hit("b",DamageSource.HostProjectile,DamageKind.Area);
        health.SetConnected(C("host","disconnect-owner"),"a",health.Read("a").Revision,false);
        bridge.Retire(C("host","retire-owner-weapon"),weapon,1);
        Check(bridge.Apply(C("host","lingering-blast"),first,1,true).Result.Accepted&&health.Read("b").Shield==75,"published projectile survives owner disconnect and weapon retirement");

        Setup();first=Hit();health.SetConnected(C("host","disconnect-intent"),"a",health.Read("a").Revision,false);
        Check(!bridge.Apply(C("a","disconnected-intent"),first,1,true).Result.Accepted&&health.Read("b").Shield==100,"current actor intent fails after disconnect");

        Setup();first=Hit("b",DamageSource.HostEnvironment,DamageKind.Zone,40);
        Check(!bridge.Apply(C("a","fake-environment"),first,1,true).Result.Accepted,"environment dispatch requires host principal");
        Check(bridge.Apply(C("host","environment"),first,1,true).Result.Accepted&&health.Read("b").Health==60&&health.Read("b").Shield==100,"real environment direct-health routing reaches Health");

        Setup();first=Hit("b",DamageSource.CurrentActorIntent,DamageKind.Bullet,250);
        Check(bridge.Apply(C("a","lethal"),first,1,true).Result.Accepted&&health.Read("b").Life==LifeState.Downed,"lethal current hit produces actual downed lifecycle");
        Check(!health.Revive(C("c","unready-revive"),"b",health.Read("b").Revision).Accepted,"unready revive cannot race lethal damage into alive state");
        Now=60;
        var atDeadline=bridge.Apply(C("a","cached-at-deadline",60),first,60,true);
        Check(atDeadline.Events.Count(e=>e.Kind=="bleedout")==1&&health.Read("b").Life==LifeState.Dead,"cached reply also publishes exact deadline bleedout");
        Check(bridge.Apply(C("a","again-at-deadline",60),first,60,true).Events.Count(e=>e.Kind=="bleedout")==1,"bleedout outbox identity stable across retries");

        foreach(string method in new[]{"synchronize","prepare","stale-damage","missing-target"}) {
            Setup();Check(health.Advance(C("host","old",3)).Accepted,"baseline replay command "+method);
            if(method=="synchronize")health.Synchronize(C("host","future",1000));
            else if(method=="prepare")health.PrepareDamage(C("host","future",1000));
            else health.ApplyDamage(C("host","future",1000),"future-event",method=="missing-target"?"missing":"b",999,0,1,true);
            Check(!health.Advance(C("host","old",3)).Accepted,"nonrecording preflight retains replay high-water "+method);
        }
        Setup();health.DrainTransitions();health.ApplyLoss(C("host","one"),"a",1,0,1,true);
        bool full=false;try{health.DrainTransitions(0);}catch(InvalidOperationException){full=true;}
        Check(full&&health.DrainTransitions().Length==1,"outbox capacity failure never destructively drains health events");
        foreach(string method in new[]{"advance","spawn","connect","revive"}) {
            Setup();Check(health.Advance(C("host","old",100)).Accepted,"recording replay baseline "+method);
            Check(health.Advance(C("host","recent",300)).Accepted,"recording recent baseline "+method);
            CommandContext denied=C("host","recent",500);RuleResult r;
            if(method=="advance")r=health.Advance(denied);
            else if(method=="spawn")r=health.Spawn(denied,"b",1,100,100);
            else if(method=="connect")r=health.SetConnected(denied,"b",1,false);
            else r=health.Revive(denied,"b",1);
            Check(!r.Accepted&&!health.Advance(C("host","old",100)).Accepted,"rejected recording preserves watermark "+method);
        }
        Setup(true);first=Hit();long life=health.Read("b").LifeGeneration;
        Check(health.ApplyLoss(C("host","kill-before-respawn",2),"b",1,100,100,false).Accepted,"actual pre-respawn death");
        Now=6;Check(health.Spawn(C("host","respawn",7),"b",health.Read("b").Revision,100,100).Accepted,"actual due respawn");
        Check(health.Read("b").LifeGeneration==life+1&&!bridge.Apply(C("a","old-life",7),first,7,true).Result.Accepted&&health.Read("b").Shield==100,"old collision cannot damage respawned life");
        CheckOldAlias(first,7);
        second=Hit("b",DamageSource.HostProjectile,DamageKind.Area,25,7);
        Check(bridge.Apply(C("host","new-life-projectile",7),second,7,true).Result.Accepted&&health.Read("b").Shield==75,"fresh lingering-projectile collision can damage new life");
        Setup();first=Hit();long connection=health.Read("b").ConnectionGeneration;
        health.SetConnected(C("host","leave",2),"b",health.Read("b").Revision,false);
        health.SetConnected(C("host","return",3),"b",health.Read("b").Revision,true);
        Check(health.Read("b").ConnectionGeneration==connection+2&&!bridge.Apply(C("a","old-connection",3),first,3,true).Result.Accepted,"old collision cannot cross reconnect");
        CheckOldAlias(first,3);
        second=Hit("b",DamageSource.CurrentActorIntent,DamageKind.Bullet,25,3);
        Check(bridge.Apply(C("a","new-connection",3),second,3,true).Result.Accepted,"new collision after reconnect succeeds");
        Setup();first=Hit();life=health.Read("b").LifeGeneration;
        health.ApplyLoss(C("host","down-before-revive",2),"b",1,100,100,true);paddles=true;
        for(int i=0;i<63;i++){Now=i*.1;Check(health.SamplePaddles(C("c","sample-"+i,i+3)).Accepted,"genuine paddle sample "+i);}
        Check(health.Revive(C("c","revive",66),"b",health.Read("b").Revision).Accepted,"genuine charged revive");
        Check(health.Read("b").LifeGeneration==life+1&&!bridge.Apply(C("a","old-revive",66),first,66,true).Result.Accepted&&health.Read("b").Health==50,"old collision cannot damage revived life");
        CheckOldAlias(first,66);
        Setup();health.DrainTransitions();
        for(int i=0;i<16385;i++)if(!health.SetConnected(C("host","backlog-"+i,i+2),"c",health.Read("c").Revision,i%2==0).Accepted)throw new Exception("backlog producer");
        first=Hit("b",DamageSource.CurrentActorIntent,DamageKind.Bullet,25,16387);
        var blocked=bridge.Apply(C("a","pressure",16387),first,16387,true);
        Check(!blocked.Result.Accepted&&health.Read("b").Shield==100&&blocked.Events.Length==16384&&blocked.PendingHealthEvents==1,"backlog rejects before damage and transfers bounded prefix");
        Check(!bridge.Acknowledge(C("host","future-ack",16387),Int64.MaxValue).Accepted,"future unoffered acknowledgment rejected");
        Check(!bridge.Pump(C("a","foreign-pump",16387)).Result.Accepted,"publication pump host only");
        Check(bridge.Acknowledge(C("host","ack-full",16387),blocked.Events.Last().Sequence).Accepted,"full prefix acknowledgment");
        var tail=bridge.Pump(C("host","pump-tail",16387));
        Check(tail.Events.Length==1&&tail.PendingHealthEvents==0,"publication pump recovers retained tail");
        Check(bridge.Apply(C("a","retry-pressure",16387),first,16387,true).Result.Accepted&&health.Read("b").Shield==75,"pressure retry applies once after bounded recovery");
        Check(bridge.Apply(C("a","repeat-pressure",16387),first,16387,true).Events.Count(e=>e.Kind=="damage")==1&&health.Read("b").Shield==75,"pressure recovery stable damage receipt");
        Console.WriteLine("TOTAL "+checks+" current-module integration checks passed");
    }
    static void Main(){new CombatHealthBridgeTests().Run();}
}
