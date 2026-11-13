using System;
using System.Collections.Generic;
using System.IO;
using Ashfall.Core;
using Ashfall.Integration;
using Ashfall.Integration.CombatPublication;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Persistence;
class PublicationTests : IHealthWorld,IAuthorityGate,IInteractionQuery,ICombatEvidence
{
    public double Now {get;set;} bool inMatch=true; public bool InMatch {get{return inMatch;}} public bool TeamChangesAllowed {get{return true;}}
    public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}
    public RuleResult Validate(CommandContext c){return c.IsAuthority&&(c.ActorId=="host"||c.ActorId=="a"||c.ActorId=="b")?RuleResult.Accept():RuleResult.Reject("auth");}
    public bool HasLineOfSight(string a,string b,Float3 o,Float3 p,float r){return los;}
    bool los;HealthService health;CombatHealthBridge bridge;WeaponLease weapon;long serial;int cmd;
    readonly Dictionary<string,HitEvidence> facts=new Dictionary<string,HitEvidence>();
    public bool TryResolve(CommandContext c,string id,out HitEvidence e){return facts.TryGetValue(id,out e);}
    CommandContext C(string actor="host",long tick=1){return new CommandContext("s",actor,"cmd"+(++cmd),tick,2,true);}
    void Setup(bool respawn=false){Now=0;inMatch=true;los=false;facts.Clear();serial=0;cmd=0;health=new HealthService("s",2,2,respawn,this,this,this,60,5,0);health.Register(C(),"a",0,false);health.Register(C(),"b",1,false);bridge=new CombatHealthBridge(health,this,this,this,this,"s",2);weapon=bridge.Equip(C(),"w","a","katana",1,1);}
    SourceRadialProvenance Provenance(){return new SourceRadialProvenance("bdba6e30012e1733efd5021fd8ded593f82f26e6","frag",new string('a',64));}
    string Hit(bool radial,float distance=5,float power=80,SourceRadialFalloff falloff=SourceRadialFalloff.Linear,DamageKind kind=DamageKind.Area,long tick=1,string sub="sub",HostEvent existing=null)
    {
        long n=++serial;string id="hit"+n;HostEvent ev=existing??(radial?bridge.PublishSourceExplosion(C("host",tick),n,tick+100,weapon,Provenance(),new Float3(0,1,0),10,power,falloff,tick):bridge.PublishEvent(C("host",tick),n,tick+100,DamageSource.HostProjectile,kind,weapon,tick));
        HostHit hit=bridge.PublishHit(C("host",tick),id,ev,sub,"b",kind==DamageKind.Melee?1:0,tick);
        facts[id]=new HitEvidence{Identity=hit,ActorInputReady=true,BaseDamage=power,DamageMultiplier=1,HeadRatio=1,Origin=new Float3(0,1,0),ViewOrigin=new Float3(0,1,0),BladeBase=new Float3(0,1,0),Point=new Float3(0,1,distance),TargetHead=new Float3(0,1.5f,distance),MaximumRange=100,HandSpeed=1,ContactEntry=1};return id;
    }
    static int checks;static void Check(bool b,string n){if(!b)throw new Exception(n);checks++;Console.WriteLine("PASS "+n);}
    void Reject(Action a,string n){bool rejected=false;try{a();}catch(ArgumentException){rejected=true;}Check(rejected,n);}
    void Radial()
    {
        Setup();string id=Hit(false);Check(bridge.Apply(C(),id,1,true).Result.Reason=="occluded"&&health.Read("b").Shield==100,"generic Area blocked");
        id=Hit(false,1,25,SourceRadialFalloff.Constant,DamageKind.Bullet);Check(bridge.Apply(C(),id,1,true).Result.Reason=="occluded","bullet blocked");
        // Melee publication cannot use HostProjectile, and current intent remains occluded.
        HostEvent melee=bridge.PublishEvent(C(),++serial,101,DamageSource.CurrentActorIntent,DamageKind.Melee,weapon,1);
        id=Hit(false,1,25,SourceRadialFalloff.Constant,DamageKind.Melee,1,"sword",melee);Check(bridge.Apply(C("a"),id,1,true).Result.Reason=="occluded","melee blocked");
        id=Hit(true);HostEvent ev=facts[id].Identity.Event;Check(ev.SourceExplosion.Provenance.ContentSha256==new string('a',64)&&ev.SourceExplosion.Provenance.CatalogId=="frag","immutable policy provenance");
        Check(!bridge.Apply(C("a"),id,1,true).Result.Accepted,"client principal cannot dispatch radial");
        Check(bridge.Apply(C(),id,1,true).Result.Accepted&&health.Read("b").Shield==60,"actual radial linear falloff through obstruction");
        long rev=health.Read("b").Revision;Check(bridge.Apply(C(),id,1,true).Result.Accepted&&health.Read("b").Revision==rev,"physical retry no double effect");
        Reject(()=>bridge.PublishSourceExplosion(C("a"),++serial,101,weapon,Provenance(),new Float3(),10,80,SourceRadialFalloff.Linear,1),"client cannot mint policy");
        Reject(()=>bridge.PublishSourceExplosion(new CommandContext("s","host","fake",1,2,false),++serial,101,weapon,Provenance(),new Float3(),10,80,SourceRadialFalloff.Linear,1),"client authority bit fails");
        Reject(()=>bridge.PublishSourceExplosion(C(),++serial,101,null,Provenance(),new Float3(),10,80,SourceRadialFalloff.Linear,1),"environment cannot become weapon radial");
        foreach(float distance in new[]{10f,11f}){Setup();id=Hit(true,distance);Check(!bridge.Apply(C(),id,1,true).Result.Accepted&&health.Read("b").Shield==100,"edge zero or outside radius "+distance);}
        Setup();id=Hit(true,5,80,SourceRadialFalloff.Constant);facts[id].WardenCoreClosed=true;Check(bridge.Apply(C(),id,1,true).Result.Accepted&&health.Read("b").Shield==80,"constant blast armor quarter");
        Setup();id=Hit(true);inMatch=false;Check(health.SetTeam(C(),"b",1,0).Accepted,"actual team change");inMatch=true;Check(bridge.Apply(C(),id,1,true).Result.Reason=="friendly-fire"&&health.Read("b").Shield==100,"actual team blocks");
        Setup();id=Hit(true);health.ApplyLoss(C(),"a",1,100,100,false);bridge.Retire(C(),weapon,1);Check(bridge.Apply(C(),id,1,true).Result.Accepted&&health.Read("b").Shield==60,"launch survives owner death retirement");
        Setup();id=Hit(true);health.SetConnected(C(),"a",1,false);Check(bridge.Apply(C(),id,1,true).Result.Accepted,"launch survives owner disconnect");
        Setup();id=Hit(true,0,1000);Check(bridge.Apply(C(),id,1,true).Result.Accepted&&health.Read("b").Health==0&&health.Read("b").Shield==0,"damage caps actual pools");
        Setup(true);id=Hit(true);ev=facts[id].Identity.Event;health.ApplyLoss(C("host",2),"b",1,100,100,false);Now=6;health.Spawn(C("host",7),"b",health.Read("b").Revision,100,100);
        string alias="alias";bridge.PublishHit(C("host",7),alias,ev,"sub","b",0,7);facts[alias]=facts[id];Check(bridge.Apply(C("host",7),alias,7,true).Result.Reason=="collision-lifecycle"&&health.Read("b").Shield==100,"radial alias cannot renew lifecycle");
        string fresh=Hit(true,5,80,SourceRadialFalloff.Linear,DamageKind.Area,7,"fresh",ev);Check(bridge.Apply(C("host",7),fresh,7,true).Result.Accepted&&health.Read("b").Shield==60,"fresh physical collision lingering event");
        Setup();id=Hit(true);Check(!bridge.Apply(C("host",102),id,102,true).Result.Accepted&&health.Read("b").Shield==100,"expiry rejects");
        Setup();id=Hit(true);ev=facts[id].Identity.Event;
        HostHit other=bridge.PublishHit(C(),"other",ev,"other-body","a",0,1);
        facts["other"]=new HitEvidence{Identity=other,ActorInputReady=true,BaseDamage=999,DamageMultiplier=1,HeadRatio=1,TargetHead=new Float3(0,1.5f,2),MaximumRange=1};
        Check(bridge.Apply(C(),id,1,true).Result.Accepted&&bridge.Apply(C(),"other",1,true).Result.Accepted&&health.Read("a").Shield==36&&health.Read("b").Shield==60,"one radial event multiple physical targets frozen power range");
        Setup();health=new HealthService("s",2,2,false,this,this,this,60,5,3);health.Register(C(),"a",0,false);health.Register(C(),"b",1,false);bridge=new CombatHealthBridge(health,this,this,this,this,"s",2);weapon=bridge.Equip(C(),"w","a","katana",1,1);
        inMatch=false;Check(health.Spawn(C(),"b",1,100,100).Accepted,"actual protected spawn");inMatch=true;id=Hit(true);Check(!bridge.Apply(C(),id,1,true).Result.Accepted&&health.Read("b").Shield==100,"radial respects actual spawn protection");
        Reject(()=>bridge.PublishSourceExplosion(C(),++serial,101,weapon,Provenance(),new Float3(),float.NaN,10,SourceRadialFalloff.Linear,1),"nonfinite radius rejected");
        Reject(()=>bridge.PublishSourceExplosion(new CommandContext("s","host","epoch",1,3,true),++serial,101,weapon,Provenance(),new Float3(),10,10,SourceRadialFalloff.Linear,1),"wrong epoch publication rejected");

    }
    void Journal(string dir)
    {
        Setup();string path=Path.Combine(dir,"journal.json");var lane=new HealthPublicationLane(health,bridge,new AtomicSettingsStore(path),"s",2,"host",128);
        Check(lane.Advance(C()).Accepted,"lane actual expiry producer");var first=lane.Offer(C());Check(first.Length==2,"prior registrations durably captured");
        Check(!lane.Acknowledge(C("a"),first[1].Sequence).Accepted&&!lane.Acknowledge(C(),999).Accepted,"ack principal and offered bound");
        Check(lane.Acknowledge(C(),first[1].Sequence).Accepted,"durable prefix ack");
        string id=Hit(true);Check(lane.Combat(C(),C(),id,1,true).Accepted&&health.Read("b").Shield==60,"lane actual Combat Health commit");
        var sent=lane.Offer(C());Check(sent.Length==1,"damage retained after offer lost ack");
        Check(lane.Offer(C())[0].Payload==sent[0].Payload,"retry stable exact payload");
        var reopened=new HealthPublicationLane(health,bridge,new AtomicSettingsStore(path),"s",2,"host",128);
        bool quarantined=false;try{reopened.Offer(C());}catch(InvalidOperationException){quarantined=true;}
        Check(reopened.RecoveryRequired&&quarantined,"restart publication quarantined until verified cold restore");
        long rev=health.Read("b").Revision;Check(!reopened.Combat(C(),C(),id,1,true).Accepted&&health.Read("b").Revision==rev,"restart no double effect fails closed without checkpoint");
        Check(!reopened.Acknowledge(C(),sent[0].Sequence).Accepted,"restart acknowledgment quarantined until verified cold restore");
        Setup();path=Path.Combine(dir,"pressure.json");lane=new HealthPublicationLane(health,bridge,new AtomicSettingsStore(path),"s",2,"host",65);
        // A full reservation requires 65 slots for a worst-case deadline batch.
        rev=health.Read("b").Revision;Check(!lane.Damage(C(),"pressure","b",rev,0,1,true).Accepted&&health.Read("b").Revision==rev,"pressure before commit preserves Health");
        var offered=lane.Offer(C());lane.Acknowledge(C(),offered[offered.Length-1].Sequence);
        Check(lane.Damage(C(),"gain-loss","b",rev,0,10,true).Accepted,"actual direct producer through lane");
        offered=lane.Offer(C());lane.Acknowledge(C(),offered[offered.Length-1].Sequence);
        var target=health.Read("b");Check(lane.Gain(C(),C("b"),"gain","b",target.Revision,target.LifeGeneration,target.ConnectionGeneration,0,5).Accepted&&health.Read("b").Shield==95,"actual prepared Health gain publication");
        Check(lane.Offer(C()).Length==1,"gain event retained");
        offered=lane.Offer(C());lane.Acknowledge(C(),offered[offered.Length-1].Sequence);
        var gainRetry=health.Read("b");Check(lane.Gain(C(),C("b"),"gain","b",gainRetry.Revision,gainRetry.LifeGeneration,gainRetry.ConnectionGeneration,0,5).Accepted&&health.Read("b").Shield==95&&lane.Offer(C()).Length==0,"gain retry no duplicate publication or effect");
        Check(lane.SetConnected(C(),"b",health.Read("b").Revision,false).Accepted,"connection producer serviced");offered=lane.Offer(C());Check(offered.Length==1,"connection durable event");lane.Acknowledge(C(),offered[0].Sequence);
        Check(!lane.Spawn(C(),"b",health.Read("b").Revision,100,100).Accepted&&lane.Offer(C()).Length==0,"rejected producer serviced without fabricated success");
        using(var held=new PhysicalSettingsFiles().AcquireFamily(path)) {
            rev=health.Read("a").Revision;Check(!lane.Damage(C(),"io-block","a",rev,0,1,true).Accepted&&health.Read("a").Revision==rev&&lane.RecoveryRequired,"real filesystem contention rejects before mutation freezes lane");
        }

    }
    static void Main(string[] args){var t=new PublicationTests();t.Radial();t.Journal(args[0]);Console.WriteLine("TOTAL "+checks+" candidate gates");}
}


