using System;
using Ashfall.Core;
using Ashfall.Modules.Combat;
class Adversarial : IAuthorityGate,ICombatEvidence,IInteractionQuery {
 readonly System.Collections.Generic.Dictionary<CombatRules,CombatFixture> fixtures=new System.Collections.Generic.Dictionary<CombatRules,CombatFixture>(); HitEvidence h; bool clear=true; int pass,fail; string[] empty=new string[0],body={"b"};
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public bool TryResolve(CommandContext c,string id,out HitEvidence e){e=h;return true;}
 public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float r){return clear;}
 CombatRules Rules(int cap){var r=new CombatRules("s",2,10,cap,this,this,this);fixtures.Add(r,new CombatFixture());return r;}
 DamageDecision Eval(CombatRules r,CommandContext c,string id,long tick){return fixtures[r].Evaluate(r,c,id,tick,h);}
 CommandContext C(string id,long tick){return new CommandContext("s","a",id,tick,2,true);}
 void Setup(){clear=true;h=new HitEvidence{TargetId="b",WeaponId="katana",Kind=DamageKind.Bullet,ActorAlive=true,ActorInputReady=true,TargetAlive=true,MatchActive=true,Health=100,Shield=100,BaseDamage=40,DamageMultiplier=1,HeadRatio=2,MaximumRange=100,Origin=new Float3(0,1,0),ViewOrigin=new Float3(0,1,0),BladeBase=new Float3(0,1,.2f),Point=new Float3(0,1,1),TargetHead=new Float3(0,1.5f,1),HandSpeed=2,ContactEntry=1};}
 void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(ok)pass++;else fail++;}
 void Run(){
 Setup();var r=Rules(10);Check(Eval(r,C("one",1),"shot-1",1).Accepted,"first bullet accepted");Check(!Eval(r,C("two",1),"shot-1",1).Accepted,"same hitId with new command must not damage twice");
 Setup();r=Rules(10);Eval(r,C("one",1),"shot-1",1);Check(!Eval(r,C("one",1),"other-shot",1).Accepted,"same command different hit rejected");
 Setup();h.Kind=DamageKind.Zone;h.DamageMultiplier=3;var d=Eval(Rules(10),C("zone",1),"zone-tick",1);Check(d.Accepted&&d.HealthLoss==40&&d.ShieldLoss==0&&d.DirectHealth,"shrinking zone bypasses shields and ignores multiplier");Check(d.Requested==40,"zone ignores multiplier numerically");
 Setup();h.Kind=DamageKind.Area;h.ActorAlive=false;Check(Eval(Rules(10),C("blast",1),"blast-1",1).Accepted,"already launched projectile may hurt after owner death");
 Setup();h.Kind=DamageKind.Zone;h.ActorInputReady=false;Check(Eval(Rules(10),C("zone",1),"zone-tick",1).Accepted,"environmental zone does not require victim gameplay input");
 Setup();h.Kind=DamageKind.Melee;r=Rules(30);Check(Eval(r,C("w1",1),"weapon1-hit",1).Accepted,"first weapon contact accepted");
 var second=new SwordContacts(10);second.Sample(true,.01f,0,0,empty,empty,empty);var ent=second.Sample(true,.01f,.1f,2,body,body,body);h.ContactEntry=ent[0].Sequence;fixtures[r].Instance="sword-B";Check(Eval(r,C("w2",1),"weapon2-hit",1).Accepted,"fresh same archetype weapon first contact accepted");
 Setup();h.Kind=DamageKind.Melee;r=Rules(1);Eval(r,C("one",1),"entry1",1);h.TargetId="c";h.ContactEntry=2;Check(Eval(r,C("two",12),"entry2",12).Reason=="contact-capacity","contact ledger remains bounded and fails closed after command prune");
 Setup();r=Rules(1);Check(Eval(r,C("one",1),"hit1",1).Accepted,"capacity first accepted");Check(Eval(r,C("two",12),"hit2",12).Accepted,"expired command pruned for new command");Check(Eval(r,C("old",1),"hit3",12).Reason=="command-age","pruned old tick still invalid");
 Setup();r=Rules(10);Check(!Eval(r,new CommandContext("old","a","x",1,2,true),"h",1).Accepted,"cross session rejected");Check(!Eval(r,new CommandContext("s","a","x",1,1,true),"h",1).Accepted,"cross epoch rejected");Check(!Eval(r,C("future",2),"h",1).Accepted,"future tick rejected");Check(Eval(r,C("new",2),"h",2).Accepted,"normal tick accepted");Check(Eval(r,C("back",1),"h",1).Reason=="server-clock","server rollback rejected");
 Setup();h.BaseDamage=float.MaxValue;h.Headshot=true;Check(!Eval(Rules(10),C("overflow",1),"h",1).Accepted,"float damage multiplication overflow fails closed");
 Setup();h.Origin=new Float3(float.MaxValue,0,0);h.Point=new Float3(-float.MaxValue,0,0);h.MaximumRange=float.MaxValue;Check(!Eval(Rules(10),C("far",1),"h",1).Accepted,"extreme finite distance computed without float overflow bypass");
 Setup();h.Point=new Float3(float.NaN,0,0);Check(!Eval(Rules(10),C("nan",1),"h",1).Accepted,"NaN geometry rejected");
 Setup();h.Kind=DamageKind.Melee;h.HandSpeed=0;Check(!Eval(Rules(10),C("rest",1),"h",1).Accepted,"resting hand rejected");
 var contacts=new SwordContacts(10);contacts.Sample(true,.01f,0,0,body,body,body);Check(contacts.Sample(true,.01f,.1f,2,body,body,body).Length==0,"resting overlap cannot become strike");contacts.Suspend();contacts.Sample(true,.01f,0,0,empty,empty,empty);ent=contacts.Sample(true,.01f,.1f,2,body,empty,empty);long seq=ent[0].Sequence;contacts.Suspend();contacts.Sample(true,.01f,0,0,empty,empty,empty);ent=contacts.Sample(true,.01f,.1f,2,body,empty,empty);Check(ent[0].Sequence>seq,"same tracker suspend preserves sequence");
 Setup();h.Kind=DamageKind.Area;h.Headshot=true;h.DamageMultiplier=2;d=Eval(Rules(10),C("area",1),"h",1);Check(d.Accepted&&d.ShieldLoss==80&&d.HealthLoss==0,"area shield first multiplied without headshot");
 Setup();h.Allied=true;Check(!Eval(Rules(10),C("ally",1),"h",1).Accepted,"ally protected");h.FriendlyFire=true;Check(Eval(Rules(10),C("ff",1),"h",1).Accepted,"friendly fire enabled");
 var guard=new BulletGuard(new Float3(0,0,0),new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1),.5f,.6f);float t;Check(guard.Intersects(new Float3(.5f,.6f,1),new Float3(.5f,.6f,-1),out t)&&t==.5f,"guard edge inclusive");Check(!guard.Intersects(new Float3(0,0,-1),new Float3(0,0,1),out t),"rear guard rejected");Check(!guard.Intersects(new Float3(0,0,1),new Float3(1,0,1),out t),"parallel guard rejected");
 Console.WriteLine("TOTAL pass="+pass+" fail="+fail);
 }
 static int Main(){var a=new Adversarial();a.Run();return a.fail==0?0:1;}
}
