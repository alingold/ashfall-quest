using System;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Integration.DesktopPlaytest.Combat;
class Checks
{
    sealed class Query:IInteractionQuery { public bool Clear=true;public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float e){return Clear;} }
    sealed class ForeignEvidence:ICombatEvidence { public HitEvidence Value;public bool TryResolve(CommandContext c,string id,out HitEvidence e){e=Value;return true;} }
    static int count;
    static void Need(bool value,string name){count++;if(!value)throw new Exception(name);}
    static void Shot(LocalRifleSandbox s,string target){s.Step(.25f,false,false,false);Need(s.Step(.25f,true,false,false),"real mechanical shot");Need(s.Hit(target,new Float3(),new Float3(0,0,2)).Accepted,"actual combat health commit");}
    static void Main()
    {
        var q=new Query();var s=new LocalRifleSandbox(new[]{"a","b","c","d","e","f"},q);
        var reader=new LocalCombatFeedback(s);LocalRifleSandbox.FeedbackReceipt r;
        Need(!reader.TryTake(false,out r),"empty");
        Need(!s.Hit("a",new Float3(),new Float3()).Accepted&&s.FeedbackSequence==0,"no pseudo injection");
        Need(!((ICombatApplication)s).TryApply(DamageDecision.Reject("foreign")).Accepted,"foreign application");
        var fe=new ForeignEvidence();var fc=new CommandContext("desktop-synthetic-rifle-v1","local-player","foreign/1",0,0,true);
        var foreign=new CombatRules("desktop-synthetic-rifle-v1",0,256,4096,s,fe,q,"local-player");
        var fl=foreign.Equip(fc,"local-rifle","local-player","service_rifle",1,0);
        var ev=foreign.PublishEvent(fc,1,1,DamageSource.CurrentActorIntent,DamageKind.Bullet,fl,0);
        var fh=foreign.PublishHit(fc,ev,"ray","a",0,0);
        fe.Value=new HitEvidence{Identity=fh,HealthRevision=s.Read("a").Revision,TargetId="a",WeaponId="service_rifle",Kind=DamageKind.Bullet,ActorAlive=true,ActorInputReady=true,TargetAlive=true,MatchActive=true,Health=s.Read("a").Health,Shield=s.Read("a").Shield,BaseDamage=25,DamageMultiplier=1,HeadRatio=1,Origin=new Float3(),ViewOrigin=new Float3(),Point=new Float3(0,0,2),TargetHead=new Float3(0,0,2),MaximumRange=100};
        var fd=foreign.Evaluate(fc,"foreign-ray",0);Need(fd.Accepted,"real foreign Combat accepted decision");
        Need(!foreign.Commit(fd,s,0).Accepted&&s.FeedbackSequence==0&&s.Read("a").Revision==1,"foreign capability cannot damage or mint");
        Shot(s,"a");Need(reader.TryTake(false,out r),"receipt");
        Need(Object.ReferenceEquals(r.Sandbox,s)&&r.Sequence==1&&r.Shot==1&&r.LogicalEvent==1,"exact producer shot");
        Need(r.TargetId=="a"&&r.ActorId=="local-player"&&r.WeaponInstanceId=="local-rifle"&&r.WeaponArchetype=="service_rifle","source ids");
        Need(r.After.Revision==r.Before.Revision+1&&r.After.LifeGeneration==r.Before.LifeGeneration&&r.After.ConnectionGeneration==r.Before.ConnectionGeneration,"revision life");
        Need(r.HealthLoss+r.ShieldLoss==25&&r.After.Shield==s.Read("a").Shield&&!r.Killed,"actual loss");
        var retained=r;Need(!reader.TryTake(false,out r),"no replay");
        Shot(s,"a");Need(retained.Sequence==1&&retained.After.Revision==2,"immutable retained bytes");
        Need(!reader.TryTake(true,out r)&&!reader.TryTake(false,out r),"muted permanently disposed");
        Shot(s,"a");reader.DiscardPending();Need(!reader.TryTake(false,out r),"focus reset no replay");
        var late=new LocalCombatFeedback(s);Need(!late.TryTake(false,out r),"late subscription no history");
        q.Clear=false;s.Step(.25f,false,false,false);s.Step(.25f,true,false,false);
        long before=s.FeedbackSequence;Need(!s.Hit("a",new Float3(),new Float3(0,0,2)).Accepted&&s.FeedbackSequence==before,"occluded no receipt");q.Clear=true;
        s.Step(.25f,false,false,false);s.Step(.25f,true,false,false);s.SuspendControls();Need(!s.Hit("a",new Float3(),new Float3()).Accepted&&s.FeedbackSequence==before,"suspended no receipt");s.Step(.25f,false,false,false);
        for(int i=0;i<5;i++)Shot(s,"a");
        int kills=0;while(reader.TryTake(false,out r)){if(r.Killed)kills++;}Need(kills==1,"once actual death");
        s.Step(.25f,false,false,false);s.Step(.25f,true,false,false);before=s.FeedbackSequence;Need(!s.Hit("a",new Float3(),new Float3()).Accepted&&s.FeedbackSequence==before,"dead no receipt");
        var gap=new LocalCombatFeedback(s);
        foreach(string target in new[]{"b","c","d","e","f"})for(int i=0;i<8;i++) {
            if(s.Snapshot.Loaded==0){for(int k=0;k<50;k++)s.Step(.25f,false,k==0,false);}
            Shot(s,target);
        }
        Need(!gap.TryTake(false,out r)&&gap.DisposedThrough==s.FeedbackSequence,"gap discarded once");Need(!gap.TryTake(false,out r),"gap cannot replay");
        var other=new LocalRifleSandbox(new[]{"a"},q);Shot(other,"a");Need(!gap.TryTake(false,out r),"replacement cannot impersonate");
        LocalRifleSandbox.FeedbackReceipt first;Need(s.ReadFeedback(-1,out first)==LocalRifleSandbox.FeedbackRead.Gap,"invalid cursor");
        reader.Dispose();Need(reader.Retired&&!reader.TryTake(false,out r),"terminal");
        Need(typeof(LocalRifleSandbox.FeedbackReceipt).GetConstructors().Length==0,"no public mint API");
        Console.WriteLine("PASS committed receipts "+count+" observations");
    }
}
