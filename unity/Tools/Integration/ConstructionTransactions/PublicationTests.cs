using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.Combat;
using Ashfall.Integration.ConstructionRuntime;
using Ashfall.Integration.ConstructionTransactions;
public static class PublicationTests
{
    sealed class Clock:IConstructionHostState
    {public long Now;public long Tick{get{return Now;}}public bool TryRead(string a,out HostBuilderStamp s){s=null;return false;}}
    sealed class Structure:IPreparedPlacement
    {
        public bool Valid=true,Committed,Aborted;public int Commits;public int Cost{get{return 1;}}
        public ConstructionPublication Publication{get;set;}
        public bool Validate(){return Valid&&!Aborted&&!Committed;}
        public void CommitValidated(){Committed=true;Commits++;}
        public void Abort(){if(!Committed)Aborted=true;}
    }
    sealed class Charge:IPreparedBuildCharge
    {public bool Valid=true,Spent,Aborted;public bool Validate(){return Valid&&!Spent&&!Aborted;}public void CommitValidated(){Spent=true;}public void Abort(){if(!Spent)Aborted=true;}}
    sealed class Backend:IStructurePreparation
    {
        public Structure Next;public int Calls;
        public RuleResult PreparePlace(ConstructionCommand c,ConstructionLease l,long e,out IPreparedStructure p){Calls++;p=Next;return RuleResult.Accept();}
        public RuleResult PrepareDamage(CommandContext c,string b,float d,long v,long e,out IPreparedStructure p){Calls++;p=Next;return RuleResult.Accept();}
        public RuleResult PrepareAdvance(float s,bool p,long e,out IPreparedStructure r){Calls++;r=Next;return RuleResult.Accept();}
    }
    sealed class Charges:IBuildChargePreparation
    {public Charge Next;public RuleResult Prepare(CommandContext c,int n,long e,out IPreparedBuildCharge p){p=Next;return RuleResult.Accept();}}
    sealed class Evidence:IAuthorityGate,ICombatEvidence,IInteractionQuery
    {
        public HostHit Hit;
        public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
        public bool HasLineOfSight(string a,string b,Float3 f,Float3 t,float r){return true;}
        public bool TryResolve(CommandContext c,string id,out HitEvidence e)
        {e=null;if(id!="registered")return false;e=new HitEvidence{Identity=Hit,TargetId=Hit.TargetId,Kind=DamageKind.Bullet,WeaponId="pistol",ActorAlive=true,ActorInputReady=true,TargetAlive=true,MatchActive=true,Health=140,HealthRevision=1,BaseDamage=30,DamageMultiplier=1,HeadRatio=1,MaximumRange=20};return true;}
    }
    static int count;
    static void Check(bool b,string s){if(!b)throw new Exception(s);count++;Console.WriteLine("PASS "+s);}
    static CommandContext C(string id){return new CommandContext("s","a",id,0,0,true);}
    static Structure S(long n,string session="s")
    {return new Structure{Publication=new ConstructionPublication(n,"place","a","c",new string('a',64),998,new PlacedPanel[0],session,0,"h")};}
    public static int Main()
    {
        var clock=new Clock();var pub=new PreparedPublisher(clock,1);var structure=S(1);var charge=new Charge{Valid=false};
        Check(!pub.Publish(structure,charge,10).Accepted&&!structure.Committed&&!charge.Spent&&pub.Pending==null,"second-stage rejection preserves all effects");
        Check(structure.Aborted&&charge.Aborted,"both uncommitted participants aborted");
        structure=S(1);charge=new Charge();Check(pub.Publish(structure,charge,10).Accepted&&structure.Committed&&charge.Spent,"validate all publishes once");
        var pending=pub.Pending;Check(Object.ReferenceEquals(pending,pub.Pending),"failed delivery retry retains exact object");
        Check(!pub.Acknowledge(S(1).Publication),"forged equal serial ack rejected");
        var full=S(2);var unspent=new Charge();Check(!pub.Publish(full,unspent,10).Accepted&&!full.Committed&&!unspent.Spent,"full outbox rejects before spend");
        Check(pub.Acknowledge(pending)&&pub.Pending==null,"exact head acknowledgement");
        Check(!pub.Publish(structure,charge,10).Accepted&&structure.Commits==1,"committed reservation retry cannot republish");
        var wrong=S(2,"other");Check(!pub.Publish(wrong,new Charge(),10).Accepted&&!wrong.Committed,"cross-session same-host publication rejects");
        clock.Now=11;structure=S(2);charge=new Charge();Check(!pub.Publish(structure,charge,10).Accepted&&!charge.Spent,"expired reservation no spend");
        clock.Now=5;Check(!pub.Publish(S(2),new Charge(),20).Accepted,"clock regression fails permanently closed");
        clock=new Clock();pub=new PreparedPublisher(clock,1);
        Check(!pub.Place(null,null,new ConstructionCommand(C("place"),1),null,20).Accepted,"missing real prepared APIs fail closed");
        var backend=new Backend{Next=S(1)};var bank=new Charges{Next=new Charge{Valid=false}};
        Check(!pub.Place(backend,bank,new ConstructionCommand(C("place"),1),null,20).Accepted&&!backend.Next.Committed&&!bank.Next.Spent,"placement second participant rejection");
        backend.Next=S(1);bank.Next=new Charge();Check(pub.Place(backend,bank,new ConstructionCommand(C("place"),1),null,20).Accepted,"prepared placement adapter composition");
        // Actual Combat capability registration/Evaluate/Commit. Structure backend remains
        // an explicit fixture: no claim of actual Construction prepared implementation.
        var evidence=new Evidence();var rules=new CombatRules("s",0,100,100,evidence,evidence,evidence);
        var host=new CommandContext("s","host","equip",0,0,true);
        var weapon=rules.Equip(host,"weapon","a","pistol",1,0);
        var ev=rules.PublishEvent(host,1,20,DamageSource.CurrentActorIntent,DamageKind.Bullet,weapon,0);
        string body=BodyIdentity.Constructed("scope/1");evidence.Hit=rules.PublishHit(host,ev,"pellet",body,0,0);
        clock=new Clock();pub=new PreparedPublisher(clock,1);backend=new Backend{Next=S(1)};
        var bridge=new CombatStructures(rules,backend,pub,clock);
        Check(!bridge.Apply(C("forged"),"forged",body,20).Accepted&&backend.Calls==0,"forged Combat ID never reaches structure");
        Check(!bridge.Apply(C("alias"),"registered","semantic-owner",20).Accepted&&backend.Calls==0,"semantic owner cannot impersonate body");
        backend.Next.Valid=false;Check(!bridge.Apply(C("rejected"),"registered",body,20).Accepted&&!backend.Next.Committed,"actual Combat second-stage rejection no structure mutation");
        backend.Next=S(1);Check(bridge.Apply(C("retry-fresh-command"),"registered",body,20).Accepted&&backend.Next.Committed,"actual Combat hit commits prepared application");
        Check(!bridge.Apply(C("physical-retry"),"registered",body,20).Accepted&&backend.Calls==2,"actual Combat logical replay blocks duplicate damage");
        pub.Acknowledge(pub.Pending);backend.Next=S(2);backend.Next.Valid=false;
        Check(!pub.Advance(backend,.5f,false,20).Accepted&&!backend.Next.Committed,"collapse second-stage rejection preserves structure");
        backend.Next=S(2);Check(pub.Advance(backend,.5f,false,20).Accepted,"prepared collapse publication adapter");
        Check(Object.ReferenceEquals(pub.Pending,backend.Next.Publication),"collapse retry retains exact publication");
        Check(!pub.Advance(backend,float.NaN,false,20).Accepted,"nonfinite advance rejected before backend");
        Console.WriteLine("RESULT "+count+" PASS; external structure/charge fixtures, not actual prepared module evidence");return 0;
    }
}
