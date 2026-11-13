using System;
using Ashfall.Core;
using Ashfall.Modules.Combat;
class R2Tests : ICombatEvidence,IInteractionQuery,IAuthorityGate,ICombatApplication
{
    CombatRules r; HitEvidence h; long tick,serial; int count,applied; long revision;
    bool los=true,fail, reenter; string lastTarget; float hp,sp;
    WeaponLease a;
    public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
    public bool TryResolve(CommandContext c,string id,out HitEvidence e){e=id=="known"?h:null;return e!=null;}
    public bool HasLineOfSight(string a0,string b,Float3 o,Float3 p,float range){return los;}
    public RuleResult TryApply(DamageDecision d)
    {
        if(reenter) Check(!r.Commit(d,this,tick).Accepted,"reentrant commit rejected");
        if(fail||revision!=d.ExpectedRevision)return RuleResult.Reject("revision");
        applied++;revision++;hp+=d.HealthLoss;sp+=d.ShieldLoss;lastTarget=d.TargetId;return RuleResult.Accept();
    }
    CommandContext C(string actor,string id){return new CommandContext("s",actor,id,tick,2,true);}
    CommandContext Host(){return C("host","publish");}
    void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
    void Throws(Action action,string name){bool threw=false;try{action();}catch(ArgumentException){threw=true;}Check(threw,name);}
    void Setup(int cap=100)
    {
        tick=1;serial=0;revision=0;applied=0;hp=sp=0;lastTarget=null;los=true;fail=reenter=false;
        r=new CombatRules("s",2,10,cap,this,this,this);
        a=r.Equip(Host(),"A","a","katana",1,tick);
        h=new HitEvidence{TargetId="b",WeaponId="katana",Kind=DamageKind.Bullet,ActorAlive=true,ActorInputReady=true,TargetAlive=true,MatchActive=true,
            Health=100,Shield=100,BaseDamage=40,DamageMultiplier=3,HeadRatio=2,MaximumRange=100,HandSpeed=2,ContactEntry=1,
            Origin=new Float3(0,1,0),ViewOrigin=new Float3(0,1,0),BladeBase=new Float3(0,1,.2f),Point=new Float3(0,1,1),TargetHead=new Float3(0,1.5f,1)};
    }
    HostEvent Event(DamageSource source,DamageKind kind,long life=10,WeaponLease weapon=null)
    {return r.PublishEvent(Host(),++serial,tick+life,source,kind,source==DamageSource.HostEnvironment?null:weapon??a,tick);}
    HostHit Hit(HostEvent ev,string sub="0",string target="b",long entry=1)
    {return r.PublishHit(Host(),ev,sub,target,entry,tick);}
    DamageDecision Eval(HostHit hit,string cmd="cmd",string actor=null)
    {
        h.Identity=hit;h.HealthRevision=revision;
        if(hit!=null){h.Kind=hit.Event.Kind;h.TargetId=hit.TargetId;h.ContactEntry=hit.ContactEntry;h.WeaponId=hit.Event.Weapon==null?null:hit.Event.Weapon.Archetype;}
        return r.Evaluate(C(actor??(hit!=null&&hit.Event.Source!=DamageSource.CurrentActorIntent?"host":"a"),cmd),"known",tick);
    }
    void Apply(HostHit hit,string cmd="cmd"){var d=Eval(hit,cmd);Check(d.Accepted&&r.Commit(d,this,tick).Accepted,"valid commit "+cmd);}
    void Run()
    {
        Setup();var hit=Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Bullet));Apply(hit);
        Check(!Eval(hit,"fresh").Accepted&&applied==1,"fresh command authentic bullet cannot double damage");
        tick=12;Check(!Eval(hit,"after-eviction").Accepted,"old hit after command and event ledger eviction rejected");
        Throws(()=>r.PublishEvent(Host(),1,20,DamageSource.CurrentActorIntent,DamageKind.Bullet,a,tick),"expired event serial cannot be republished");
        Setup();var ev=Event(DamageSource.HostProjectile,DamageKind.Area,30);hit=Hit(ev);
        h.ActorAlive=false;h.ActorInputReady=false;r.RetireWeapon(Host(),a,tick);
        Apply(hit,"dead-disconnected-owner");Check(sp==100&&hp==20,"projectile keeps shield first and multiplier after death retirement");
        Setup();los=false;ev=Event(DamageSource.HostEnvironment,DamageKind.Zone);hit=Hit(ev);
        h.ActorAlive=false;h.ActorInputReady=false;h.WardenCoreClosed=true;
        var d=Eval(hit);Check(d.Accepted&&d.DirectHealth&&d.HealthLoss==40&&d.ShieldLoss==0,"environment direct unmultiplied no armor input or weapon ray");
        Check(!Eval(hit,"client-environment","a").Accepted,"client cannot dispatch environmental source");
        Check(!r.Evaluate(new CommandContext("s","host","fake",1,2,false),"known",1).Accepted,"client authority flag rejected");
        Throws(()=>r.PublishEvent(C("a","fake-publish"),2,11,DamageSource.HostEnvironment,DamageKind.Zone,null,tick),"client actor cannot publish bypass");
        Throws(()=>r.PublishEvent(Host(),2,11,DamageSource.HostProjectile,DamageKind.Zone,a,tick),"weapon zone cannot select environmental kind");
        Throws(()=>r.PublishEvent(Host(),2,11,DamageSource.HostEnvironment,DamageKind.Area,null,tick),"environment cannot publish weapon area");
        h.Protected=true;Check(!Eval(hit,"protected").Accepted,"environment preserves spawn protection");h.Protected=false;h.MatchActive=false;
        Check(!Eval(hit,"outside-match").Accepted,"environment preserves match gate");h.MatchActive=true;h.TargetAlive=false;
        Check(!Eval(hit,"dead-target").Accepted,"environment preserves target life gate");
        Setup();hit=Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Melee));
        h.ActorAlive=false;Check(!Eval(hit,"dead").Accepted,"current intent requires live actor");
        h.ActorAlive=true;h.ActorInputReady=false;Check(!Eval(hit,"paused").Accepted,"current intent requires input ready");
        Setup();ev=Event(DamageSource.HostProjectile,DamageKind.Area);hit=Hit(ev);los=false;
        Check(!Eval(hit).Accepted,"weapon zone grenade area still requires occlusion policy");los=true;
        d=Eval(hit,"clear");Check(d.Accepted&&!d.DirectHealth&&d.Requested==120&&d.ShieldLoss==100,"weapon zone grenade area multiplied shield first");
        Setup();ev=Event(DamageSource.CurrentActorIntent,DamageKind.Bullet);hit=Hit(ev,"pellet1");
        Apply(hit,"pellet1");var pellet=Hit(ev,"pellet2");Apply(pellet,"pellet2");
        var other=Hit(ev,"pellet1","c");Apply(other,"other-target");Check(applied==3&&lastTarget=="c","distinct pellets and area targets legitimate");
        Check(Object.ReferenceEquals(hit,Hit(ev,"pellet1")),"same identity publication returns same capability");
        Throws(()=>Hit(ev,"pellet1","b",2),"host hit identity immutable");
        Setup();ev=Event(DamageSource.CurrentActorIntent,DamageKind.Melee);hit=Hit(ev);
        Apply(hit,"A1");
        var b=r.Equip(Host(),"B","a","katana",1,tick);
        Apply(Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Melee,10,b)),"B1");
        Check(applied==2,"concurrent same archetype instances independent");
        var duplicate=Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Melee),"another-subhit");
        Check(!Eval(duplicate,"same-contact").Accepted,"same instance contact cannot alias fresh event");
        r.RetireWeapon(Host(),a,tick);
        Check(!Eval(hit,"retired").Accepted,"retired instance cannot replay");
        Throws(()=>r.Equip(Host(),"A","a","katana",1,tick),"same equip generation cannot resurrect");
        var anew=r.Equip(Host(),"A","a","katana",2,tick);
        Apply(Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Melee,10,anew)),"A-generation2");
        Check(applied==3,"reequip first entry independent");
        Check(!Eval(duplicate,"old-generation").Accepted,"old generation events rejected");
        Setup();hit=Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Bullet));
        d=Eval(hit);revision=1;
        Check(!r.Commit(d,this,tick).Accepted&&applied==0,"stale health revision mutates nothing");
        var retry=Eval(hit,"retry");
        Check(retry.Accepted&&retry.ExpectedRevision==1&&retry.SessionId=="s"&&retry.AuthorityEpoch==2&&retry.LogicalEvent==1&&retry.SubhitId=="0"&&retry.TargetId=="b","retry decision binds fresh snapshot and logical identity");
        Check(!r.Commit(d,this,tick).Accepted,"superseded stale decision invalid");
        reenter=true;Check(r.Commit(retry,this,tick).Accepted&&applied==1,"fresh revision succeeds after failure");
        Check(!r.Commit(retry,this,tick).Accepted&&!Eval(hit,"again").Accepted,"successful retry consumes exactly once");
        Setup();hit=Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Melee));
        d=Eval(hit);fail=true;Check(!r.Commit(d,this,tick).Accepted,"failed melee commit not consumed");
        fail=false;Apply(hit,"retry-melee");
        Setup();hit=Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Bullet,0));d=Eval(hit);tick++;
        Check(!r.Commit(d,this,tick).Accepted&&applied==0,"decision cannot outlive host event");
        Setup();hit=Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Bullet));h.Identity=hit;h.HealthRevision=0;h.TargetId="c";
        Check(!r.Evaluate(C("a","target-spoof"),"known",tick).Accepted,"snapshot target cannot replace immutable hit target");
        var foreign=hit;Setup();Check(!Eval(foreign).Accepted,"foreign rules capability rejected even matching session epoch serial");
        Setup(1);ev=Event(DamageSource.HostProjectile,DamageKind.Area,0);hit=Hit(ev);Apply(hit);tick=12;
        var ev2=Event(DamageSource.HostProjectile,DamageKind.Area,0);Apply(Hit(ev2),"reuse-capacity");
        Check(applied==2&&!Eval(hit,"expired").Accepted,"bounded event storage reuses expired slots without resurrecting old hits");

        Setup();hit=Hit(Event(DamageSource.HostProjectile,DamageKind.Area));d=Eval(hit);
        Check(d.OwnerId=="a"&&d.WeaponInstanceId=="A"&&d.WeaponArchetype=="katana"&&d.EquipGeneration==1&&d.Source==DamageSource.HostProjectile&&d.Kind==DamageKind.Area,"decision retains immutable source and attribution for application");
        Setup();var tracker=new SwordContacts(a,10);string[] empty=new string[0],body={"b"};
        Throws(()=>new SwordContacts(a,10),"second tracker cannot reset same equip contact sequence");
        tracker.Sample(true,.01f,0,0,empty,empty,empty);
        var entry=tracker.Sample(true,.01f,.1f,2,body,empty,empty)[0];
        ev=Event(DamageSource.CurrentActorIntent,DamageKind.Melee);
        hit=r.PublishSwordHit(Host(),ev,"sweep",entry,tick);Apply(hit,"bound-first");
        tracker.Suspend();tracker.Sample(true,.01f,0,0,empty,empty,empty);
        var next=tracker.Sample(true,.01f,.1f,2,body,empty,empty)[0];
        Check(next.Sequence>entry.Sequence&&Object.ReferenceEquals(next.Weapon,a),"bound tracker suspend preserves equip sequence");
        ev=Event(DamageSource.CurrentActorIntent,DamageKind.Melee);
        Apply(r.PublishSwordHit(Host(),ev,"sweep",next,tick),"bound-resume");
        var blease=r.Equip(Host(),"B","a","katana",1,tick);
        var btracker=new SwordContacts(blease,10);
        btracker.Sample(true,.01f,0,0,empty,empty,empty);
        var bentry=btracker.Sample(true,.01f,.1f,2,body,empty,empty)[0];
        Throws(()=>r.PublishSwordHit(Host(),ev,"wrong-weapon",bentry,tick),"sword entry cannot bind another instance");
        r.RetireWeapon(Host(),a,tick);
        Check(tracker.Sample(true,.01f,.1f,2,body,empty,empty).Length==0,"retired bound tracker disarmed");
        var reequipped=r.Equip(Host(),"A","a","katana",2,tick);
        var fresh=new SwordContacts(reequipped,10);fresh.Sample(true,.01f,0,0,body,body,body);
        Check(fresh.Sample(true,.01f,.1f,2,body,body,body).Length==0,"respawn reequip baselines overlap");
        fresh.Sample(true,.01f,.1f,2,empty,empty,empty);
        var restart=fresh.Sample(true,.01f,.1f,2,body,empty,empty)[0];
        Check(restart.Sequence==1&&restart.Weapon.Generation==2,"new generation tracker safely restarts sequence");
        Setup();hit=Hit(Event(DamageSource.CurrentActorIntent,DamageKind.Bullet));d=Eval(hit);
        r.RetireWeapon(Host(),a,tick);Check(!r.Commit(d,this,tick).Accepted,"pending current bullet cannot commit after retirement");
        Console.WriteLine("TOTAL "+count);
    }
    static int Main(){try{new R2Tests().Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
