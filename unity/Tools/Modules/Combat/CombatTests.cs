using System;
using Ashfall.Core;
using Ashfall.Modules.Combat;
class Tests : IAuthorityGate,ICombatEvidence,IInteractionQuery
{
    readonly System.Collections.Generic.Dictionary<CombatRules,CombatFixture> fixtures=new System.Collections.Generic.Dictionary<CombatRules,CombatFixture>();
    HitEvidence hit; bool clear=true,allowed=true; int count;
    public RuleResult Validate(CommandContext c){return c.ActorId=="host"||allowed?RuleResult.Accept():RuleResult.Reject("owner");}
    public bool TryResolve(CommandContext c,string id,out HitEvidence e){e=hit;return true;}
    public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return clear;}
    CombatRules Rules(int capacity){var r=new CombatRules("session",3,10,capacity,this,this,this);fixtures.Add(r,new CombatFixture());return r;}
    DamageDecision Eval(CombatRules r,CommandContext c,string id,long tick){return fixtures[r].Evaluate(r,c,id,tick,hit);}
    CommandContext C(string id,long tick){return new CommandContext("session","a",id,tick,3,true);}
    void Setup(){hit=new HitEvidence{TargetId="b",WeaponId="katana",Kind=DamageKind.Melee,ActorAlive=true,ActorInputReady=true,TargetAlive=true,MatchActive=true,Health=100,Shield=100,DamageMultiplier=1,HeadRatio=1,HandSpeed=2,Origin=new Float3(0,1,0),BladeBase=new Float3(0,1,.2f),Point=new Float3(0,1,1),TargetHead=new Float3(0,1.5f,1),ContactEntry=1,MaximumRange=100,BaseDamage=40};clear=true;allowed=true;}
    void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
    void Near(float actual,float expected,string name){Check(Math.Abs(actual-expected)<.0001f,name);}
    void Run()
    {
        Setup();var r=Rules(50);var d=Eval(r,C("1",1),"hit",1);Check(d.Accepted,"sword accepted");Near(d.HealthLoss,75,"full speed HP");Near(d.ShieldLoss,0,"melee bypasses shield");
        Check(!Eval(r,C("1",1),"hit",1).Accepted,"command replay");Check(!Eval(r,C("2",1),"hit",1).Accepted,"same contact new command");hit.ContactEntry=2;Check(Eval(r,C("3",1),"hit",1).Accepted,"exit reentry no cooldown");
        Setup();hit.HandSpeed=.2f;Near(Eval(Rules(10),C("s",1),"hit",1).HealthLoss,30,"minimum server hand speed");hit.HandSpeed=.199f;Check(!Eval(Rules(10),C("s",1),"hit",1).Accepted,"slow hand rejected");hit.HandSpeed=12.01f;Check(!Eval(Rules(10),C("s",1),"hit",1).Accepted,"implausible hand rejected");
        Setup();hit.WeaponId="knife";hit.HandSpeed=10;Near(Eval(Rules(10),C("s",1),"hit",1).HealthLoss,35,"knife fixed35");
        Setup();hit.Kind=DamageKind.Bullet;hit.Shield=25;hit.Health=10;d=Eval(Rules(10),C("s",1),"hit",1);Near(d.ShieldLoss,25,"bullet shield first");Near(d.HealthLoss,10,"overkill clamped");
        hit.Headshot=true;hit.HeadRatio=2;hit.Shield=100;Near(Eval(Rules(10),C("s",1),"hit",1).ShieldLoss,80,"bullet head ratio");
        hit.WardenCoreClosed=true;Near(Eval(Rules(10),C("s",1),"hit",1).ShieldLoss,20,"closed warden quarter damage");
        Setup();hit.Allied=true;Check(!Eval(Rules(10),C("s",1),"hit",1).Accepted,"ally protected");hit.FriendlyFire=true;Check(Eval(Rules(10),C("s",1),"hit",1).Accepted,"friendly fire option");
        Setup();hit.Protected=true;Check(!Eval(Rules(10),C("s",1),"hit",1).Accepted,"spawn protection");
        Setup();clear=false;Check(!Eval(Rules(10),C("s",1),"hit",1).Accepted,"wall blocks melee");
        Setup();hit.Origin=new Float3(0,1,-1.5f);Check(!Eval(Rules(10),C("s",1),"hit",1).Accepted,"strict 2.5m boundary");
        Setup();hit.Point=new Float3(0,1,1.81f);Check(!Eval(Rules(10),C("s",1),"hit",1).Accepted,"target body envelope");
        Setup();hit.Health=float.NaN;Check(!Eval(Rules(10),C("s",1),"hit",1).Accepted,"NaN rejected");
        Setup();r=Rules(1);Check(Eval(r,C("s",1),"hit",1).Accepted,"capacity first");Check(!Eval(r,C("t",1),"hit",1).Accepted,"capacity fails closed");Check(!Eval(r,C("s",1),"hit",12).Accepted,"expired replay rejected by age");
        Setup();Check(!Eval(Rules(10),new CommandContext("old","a","s",1,3,true),"hit",1).Accepted,"session mismatch");Check(!Eval(Rules(10),new CommandContext("session","a","s",1,2,true),"hit",1).Accepted,"epoch mismatch");allowed=false;Check(!Eval(Rules(10),C("s",1),"hit",1).Accepted,"owner gate");
        var contacts=new SwordContacts(10);string[] empty=new string[0],body={"b"},twice={"b","b"};
        Check(contacts.Sample(true,.01f,0,0,body,body,body).Length==0,"equip inside body disarmed");
        Check(contacts.Sample(true,.01f,.1f,2,body,body,body).Length==0,"persistent contact disarmed");
        contacts.Sample(true,.01f,.1f,2,empty,empty,empty);
        var entry=contacts.Sample(true,.01f,.1f,2,twice,body,body);Check(entry.Length==1,"child contacts deduplicated");long first=entry[0].Sequence;
        contacts.Sample(true,.01f,.1f,2,empty,empty,body);Check(contacts.Sample(true,.01f,.1f,2,body,body,body).Length==0,"exit hysteresis retains contact");
        contacts.Sample(true,.01f,.1f,2,empty,empty,empty);entry=contacts.Sample(true,.01f,.1f,2,body,empty,empty);Check(entry.Length==1&&entry[0].Sequence>first,"between endpoints crossing damages");
        contacts.Suspend();contacts.Sample(true,.01f,0,0,empty,empty,empty);contacts.Sample(true,.01f,.001f,.1f,body,body,body);Check(contacts.Sample(true,.01f,.1f,2,body,body,body).Length==0,"slow entry cannot later damage while inside");
        Check(contacts.Sample(true,.2f,.1f,2,body,body,body).Length==0,"long frame resets history");Check(contacts.Sample(true,.01f,.1f,2,body,body,body).Length==0,"resume baselines overlap");
                var guard=new BulletGuard(new Float3(0,0,0),new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1),.5f,.6f);float fraction;
        Check(guard.Intersects(new Float3(0,0,1),new Float3(0,0,-1),out fraction),"front guard intersection");Near(fraction,.5f,"guard fraction");
        Check(!guard.Intersects(new Float3(0,0,-1),new Float3(0,0,1),out fraction),"rear guard bypass");
        Check(!guard.Intersects(new Float3(.51f,0,1),new Float3(.51f,0,-1),out fraction),"outside guard rectangle");
        Check(!guard.Intersects(new Float3(0,0,1),new Float3(0,0,.5f),out fraction),"guard beyond bullet endpoint");
        Setup();hit.Guard=guard;hit.Origin=new Float3(0,0,1);hit.Point=new Float3(0,0,-1);hit.Kind=DamageKind.Bullet;
        Check(!Eval(Rules(10),C("guard",1),"hit",1).Accepted,"guard integrated bullet block");hit.Kind=DamageKind.Area;Check(Eval(Rules(10),C("area",1),"hit",1).Accepted,"area bypasses guard");
        Setup();hit.Guard=guard;Check(Eval(Rules(10),C("melee",1),"hit",1).Accepted,"melee bypasses guard");
        Console.WriteLine("TOTAL "+count);
    }
    static int Main(){try{new Tests().Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

