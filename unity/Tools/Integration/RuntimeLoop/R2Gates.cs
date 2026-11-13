using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration;
using Ashfall.Integration.InventoryTransfers;
using Ashfall.Integration.RuntimeLoop;
partial class Scenario
{
    void SetupR2(bool missingInventory=false,bool missingHealth=false)
    {
        tick=0;Now=0;rejectSink=false;
        health=new HealthService("s",1,2,false,this,this,this,60,1,0);
        health.Register(C("host"),"a",0,false);if(!missingHealth)health.Register(C("host"),"b",0,false);
        combat=new CombatHealthBridge(health,this,this,this,this,"s",1);items=new TransferBridge("s",1,10,this,this,this,this,this);
        items.AddActor("a");if(!missingInventory)items.AddActor("b");weapons=new SessionWeapons("s",10,this,this);items.BindWeapons(weapons);
        weapons.Grant(C("a"),new ItemInstance("rifle","longwatch",1),WeaponMechanism.PrecisionRifle,3,new Reserve());items.AdmitExistingWeapon(C("a"),new ItemInstance("rifle","longwatch",1),weapons);
        loop=new HostLoop("s",1,"host",this,health,combat,items,this);
        a=new Root{Id="a",Health=health,Position=new Float3(0,.12f,0)};b=new Root{Id="b",Health=health,Position=new Float3(2,.12f,0)};
        Check(loop.AddActor("a",a).Accepted,"R2 bind real actor a");
        Check(loop.AddActor("b",b).Accepted==(!missingInventory&&!missingHealth),"R2 reject absent aggregate before accepting actor b");
    }
    void ArmR2(){Step(A("a"),A("b"));Step(A("a",RuntimeAction.Equip,"rifle"),A("b"));Step(A("a"),A("b"));Step(A("a"),A("b"));Step(A("a"),A("b"));}
    public void RunR2()
    {
        ActorResult[] output;SetupR2();ArmR2();int roots=a.Commits,ammo=weapons.Inspect("rifle").Loaded;
        Check(health.Advance(new CommandContext("s","host","ahead",1000,1,true)).Accepted,"R2 actual independent producer watermark");
        tick++;Now=tick*.1;var c=C("host");var r=loop.Tick(c,.1f,new[]{A("a",RuntimeAction.Fire,"rifle",trigger:true,move:1),A("b")},out output);
        Check(!r.Accepted&&r.Reason=="stale-tick"&&a.Commits==roots&&weapons.Inspect("rifle").Loaded==ammo&&output.Length==0&&loop.LastDisposition==TickDisposition.RejectedBeforeEffects,"H1 stale Health rejects before real root/ammo");
        SetupR2(true);SetupR2(false,true);SetupR2();ArmR2();b.InvalidHeading=true;roots=a.Commits;ammo=weapons.Inspect("rifle").Loaded;
        tick++;Now=tick*.1;c=C("host");r=loop.Tick(c,.1f,new[]{A("a",RuntimeAction.Fire,"rifle",trigger:true),A("b")},out output);
        Check(!r.Accepted&&a.Commits==roots&&weapons.Inspect("rifle").Loaded==ammo,"M1 late invalid environment rejects all actors before effects");
        b.InvalidHeading=false;r=loop.Tick(c,.1f,new[]{A("a"),A("b")},out output);Check(r.Accepted,"M1 rejected tick retry is admitted");
        SetupR2();b.Generation++;tick++;Now=.1;r=loop.Tick(C("host"),.1f,new[]{A("a"),A("b")},out output);Check(!r.Accepted&&a.Commits==0,"root generation drift before actor effect");
        SetupR2();b.BindingEpoch=2;tick++;Now=.1;r=loop.Tick(C("host"),.1f,new[]{A("a"),A("b")},out output);Check(!r.Accepted&&a.Commits==0,"epoch drift before actor effect");
        SetupR2();HealthService.PreparedAdvance p,q;var before=health.Read("a");int pending=health.PendingTransitionCount;c=C("host");
        Check(health.PrepareAdvance(c,out p).Accepted&&health.Read("a").Revision==before.Revision&&health.PendingTransitionCount==pending,"prepare preserves Health and outbox");
        Check(!health.PrepareAdvance(C("host"),out q).Accepted,"global one-outstanding reservation");
        HealthService.PreparedGain gain;Check(!health.PrepareGain(C("a"),"gain","a",before.Revision,before.LifeGeneration,before.ConnectionGeneration,1,0,out gain),"advance reservation blocks actual gain producer");
        p.Abort();p.Abort();Check(health.PrepareAdvance(c,out p).Accepted,"abort idempotently releases reservation");
        Now=.1;Check(!p.Validate(),"frozen clock detects drift");p.Abort();
        c=C("host");Check(health.PrepareAdvance(c,out p).Accepted&&p.Validate(),"fresh advance validates");health.Serialize(delegate{p.CommitValidated();p.CommitValidated();p.Abort();return true;});
        Check(!health.PrepareAdvance(c,out q).Accepted,"committed command replay retained after abort");
        SetupR2();var victim=health.Read("b");Check(health.ApplyLoss(C("host"),"b",victim.Revision,100,100,true).Accepted,"timer fixture actual downed transition");victim=health.Read("b");Now=61;
        Check(health.PrepareAdvance(C("host"),out p).Accepted&&health.Read("b").Life==LifeState.Downed&&health.Read("b").Revision==victim.Revision,"timer projection prepare does not expire life");
        Check(p.Validate(),"timer reservation validates");health.Serialize(delegate{p.CommitValidated();return true;});
        Check(health.Read("b").Life==LifeState.Dead&&health.Read("b").Revision==victim.Revision+1,"preallocated timer commit mutates actual Health");p.Abort();
        SetupR2();victim=health.Read("b");health.ApplyLoss(C("host"),"b",victim.Revision,100,100,true);
        while(health.PendingTransitionCount<1024)health.SetConnected(C("host"),"a",health.Read("a").Revision,true);
        Now=61;Check(!health.PrepareAdvance(C("host"),out p).Accepted&&health.Read("b").Life==LifeState.Downed,"timer outbox pressure rejects without expiry mutation");
        SetupR2();victim=health.Read("b");Check(health.ApplyLoss(C("host"),"b",victim.Revision,100,100,true).Accepted&&loop.Drain().Accepted,"committed fixture clears earlier producer receipts");rejectSink=true;tick=1;Now=61;c=C("host");r=loop.Tick(c,.1f,new[]{A("a"),A("b")},out output);roots=a.Commits;
        Check(!r.Accepted&&loop.LastDisposition==TickDisposition.CommittedPending&&roots==1,"committed pending explicitly classified");
        Check(!loop.Tick(new CommandContext("s","host","next",2,1,true),.1f,new[]{A("a"),A("b")},out output).Accepted&&a.Commits==roots,"pending pressure forbids next actor publication");
        rejectSink=false;Check(loop.Drain().Accepted&&a.Commits==roots&&loop.LastDisposition==TickDisposition.Committed,"drain retries effects without actor replay");
        Check(loop.Drain().Accepted&&a.Commits==roots,"drain idempotent");
        SetupR2();var snap=health.Read("a");HealthService.PreparedGain reserved;
        Check(health.PrepareGain(C("a"),"reserved","a",snap.Revision,snap.LifeGeneration,snap.ConnectionGeneration,1,0,out reserved),"actual gain reserves publication");
        Check(!health.PrepareAdvance(C("host"),out p).Accepted,"gain reservation blocks advance");reserved.Abort();
        var noMembers=new Ashfall.Modules.Sessions.Session("s","content",new Ashfall.Modules.Sessions.SessionRules("map","revive","hash",2,4,true,false,true,true,1),this,this,this,this,this);
        Check(noMembers.BeginLoading(new CommandContext("s","host","loading",0,0,true),noMembers.Rules,true).Accepted&&noMembers.Epoch==1,"Session absence fixture uses matching epoch");
        var bound=new HostLoop("s",1,"host",this,health,combat,items,this,noMembers);
        Check(!bound.AddActor("a",a).Accepted,"missing Session membership rejected at matching epoch");
        PresentationGate();
        Console.WriteLine("R2 GATES "+checks+" PASS 0 FAIL; no independent approval");
    }
    sealed class Playback:Ashfall.Modules.Audio.IAudioPlayback
    {
        public int Starts;
        sealed class Voice:Ashfall.Modules.Audio.IAudioVoice{public void Set(float g,float p,Float3 v){}public void Pause(bool p){}public void Stop(){}}
        public Ashfall.Modules.Audio.IAudioVoice Start(Ashfall.Modules.Audio.AudioAsset a,bool spatial,float gain,float pitch,Float3 p){Starts++;return new Voice();}
        public void SetBus(string b,float g){}public void StopTransients(){}
    }
    sealed class PresentationMap:IHealthPresentationMap
    {
        public RuleResult Map(HealthTransition e,out HealthPresentation p){p=new HealthPresentation{Cue="hit",Damage=1,Hud=new Ashfall.Modules.Presentation.HudInput{Health=e.After.Health,Shield=e.After.Shield,Stamina=6,StaminaMaximum=6}};return RuleResult.Accept();}
    }
    void PresentationGate()
    {
        SetupR2();var playback=new Playback();var hud=new Ashfall.Modules.Presentation.HudModel();
        using(var router=new Ashfall.Modules.Audio.AudioRouter(Ashfall.Modules.Audio.AudioCatalog.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"r2-audio.tsv")),playback)){
            var sink=new HealthPresentationEvents(router,hud,new PresentationMap(),8);var e=health.DrainTransitions()[0];
            Check(sink.Deliver(e).Accepted&&playback.Starts==1&&hud.Incoming==1&&hud.Health==1,"production adapter calls actual AudioRouter and HUD");
            Check(sink.Deliver(e).Accepted&&playback.Starts==1&&hud.Incoming==1,"production fanout retry deduplicates both effects");
            var epochEvent=new HealthTransition(e.SessionId,e.AuthorityEpoch+1,e.Sequence,e.Kind,e.BeforeRevision,e.After);
            Check(sink.Deliver(epochEvent).Accepted&&playback.Starts==2&&hud.Incoming==2,"fanout key includes authority epoch");
            var sessionEvent=new HealthTransition("other",e.AuthorityEpoch,e.Sequence,e.Kind,e.BeforeRevision,e.After);
            Check(sink.Deliver(sessionEvent).Accepted&&playback.Starts==3&&hud.Incoming==3,"fanout key includes complete session tuple");
        }
    }

}
class R2Tests{static int Main(){try{new Scenario().RunR2();return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}}
