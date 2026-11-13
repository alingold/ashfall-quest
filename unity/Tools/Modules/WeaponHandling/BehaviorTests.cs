using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
class BehaviorTests
{
    sealed class Gate : IAuthorityGate { public RuleResult Validate(CommandContext c) { return c.ActorId == "owner" ? RuleResult.Accept() : RuleResult.Reject("owner"); } }
    sealed class Ammo : IAmmoReserve
    {
        public int Count = 100, Limit = 1000;
        public int Available { get { return Count; } }
        public int Take(int n) { int a=Math.Min(n,Count); Count-=a; return a; }
        public int Refund(int n) { int a=Math.Min(n,Limit-Count); Count+=a; return a; }
    }
    sealed class Rig
    {
        public Ammo Ammo = new Ammo(); public WeaponHandling Gun;
        public HandlingInput Input; public long N;
        public Rig(bool precision)
        {
            Gun = TestSpawn.New("session","owner",1,precision ? WeaponMechanism.PrecisionRifle : WeaponMechanism.ServiceRifle,Ammo,new Gate());
            Input = new HandlingInput { Enabled=true, PrimaryTracked=true, SupportTracked=true, ForegripDistance=1, Desktop=true };
        }
        public RuleResult Do(HandlingAction action) { N++; Input.Sequence=N; return Gun.Apply(Context(N), action, Input); }
        public CommandContext Context(long n) { return new CommandContext("session","owner","cmd"+n,n,1,true); }
        public HandlingSnapshot S { get { return Gun.Snapshot; } }
        public void At(float x,float y,float z,bool grip) { Input.SupportLocal=new Float3(x,y,z); Input.Grip=grip; Do(HandlingAction.Sample); }
        public void Advance(float time) { Input.DeltaSeconds=.2f; for(float t=0;t<time;t+=.2f) Do(HandlingAction.Sample); Input.DeltaSeconds=0; }
    }
    static int checks;
    static void Check(bool condition,string name) { if(!condition) throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
    static void Main()
    {
        var r = new Rig(false);
        r.Input.Trigger=true; Check(r.Do(HandlingAction.Fire).Accepted && r.S.Loaded==23,"rifle live shot consumes one");
        Check(!r.Do(HandlingAction.Fire).Accepted && r.S.Shots==1,"cooldown rejects extra shot");
        Check(r.Do(HandlingAction.ReloadStep).Accepted && r.S.Phase==ReloadPhase.NeedsMagazine && r.Ammo.Count==123,"tactical eject conserves loaded rounds");
        r.Do(HandlingAction.ReloadStep);
        Check(r.S.Phase==ReloadPhase.NeedsRack && r.S.Seated==24 && !r.S.Chambered && r.Ammo.Count==99,"insert consumes reserve without premature chamber");
        Check(!r.Do(HandlingAction.Fire).Accepted,"seated magazine cannot fire");
        r.Do(HandlingAction.ReloadStep); r.Advance(.2f);
        Check(!r.Do(HandlingAction.Fire).Accepted,"held trigger cannot fire after reload");
        r.Input.Trigger=false;r.Do(HandlingAction.Sample);r.Input.Trigger=true;
        Check(r.Do(HandlingAction.Fire).Accepted && r.S.Loaded==23,"release re-arms trigger");
        var limited = new Rig(false); limited.Ammo.Limit=105;
        Check(!limited.Do(HandlingAction.Eject).Accepted && limited.S.Loaded==19 && limited.Ammo.Count==105,"partial refund preserves remainder");
        Check(!limited.Do(HandlingAction.Eject).Accepted && limited.S.Loaded==19,"refused retry cannot destroy ammunition");
        var physical = new Rig(false);physical.Input.Desktop=false;physical.Do(HandlingAction.Eject);
        Check(!physical.Do(HandlingAction.ReloadStep).Accepted,"XR cannot button bypass physical reload");
        physical.At(0,-.278f,.15f,true); Check(physical.S.Phase==ReloadPhase.HeldMagazine,"physical magazine grab");
        physical.At(0,-.25f,.15f,true); Check(physical.S.Phase==ReloadPhase.HeldMagazine,"short insert stroke rejected");
        physical.At(0,-.18f,.15f,true); Check(physical.S.Phase==ReloadPhase.NeedsRack,"upward insert stroke seats magazine");
        physical.At(-.045f,.064f,.10f,false);physical.At(-.045f,.064f,.10f,true);
        physical.At(-.045f,.064f,.04f,true);Check(!physical.S.Chambered,"short rack does not chamber");
        physical.Input.SupportTracked=false;physical.Do(HandlingAction.Sample);
        Check(physical.S.BoltTravel==0 && !physical.S.ClaimsSupport,"tracking loss cancels bolt contact");
        physical.Input.SupportTracked=true;physical.At(-.045f,.064f,-.02f,true);
        Check(!physical.S.Chambered,"tracking resume held grip cannot finish old stroke");
        physical.At(-.045f,.064f,.10f,false);physical.At(-.045f,.064f,.10f,true);physical.At(-.045f,.064f,.02f,true);
        Check(physical.S.Chambered && physical.S.Loaded==24,"fresh reflected rearward rack chambers");
        var p=new Rig(true);p.Input.Trigger=true;p.Do(HandlingAction.Fire);
        Check(p.S.Loaded==2 && p.S.SpentChamber && p.S.Phase==ReloadPhase.NeedsBolt,"precision shot retains spent chamber");
        p.Input.Desktop=false;p.At(.072f,.045f,.07f,false);p.At(.072f,.045f,.07f,true);p.At(.072f,.045f,-.03f,true);
        Check(!p.S.Chambered,"precision 10cm pull insufficient");
        p.At(.072f,.045f,-.05f,true);
        Check(p.S.Chambered && !p.S.SpentChamber && p.S.Loaded==2,"precision full back cycle preserves next round");
        p.Input.Desktop=true;p.Input.Grip=false;p.Do(HandlingAction.Sample);p.Do(HandlingAction.ReloadStep);
        Check(p.S.Phase==ReloadPhase.NeedsMagazine && p.Ammo.Count==102,"precision tactical reload refunds after bolt");
        p.Ammo.Count=1;p.Do(HandlingAction.ReloadStep);p.Do(HandlingAction.ReloadStep);
        Check(p.S.Loaded==1 && p.S.Chambered && p.Ammo.Count==0,"partial precision magazine chambers one round");
        p.Advance(1);p.Input.Trigger=false;p.Do(HandlingAction.Sample);p.Input.Trigger=true;p.Do(HandlingAction.Fire);
        Check(p.S.Phase==ReloadPhase.NeedsBolt,"last precision shot keeps magazine until bolt");
        p.Do(HandlingAction.ReloadStep);Check(p.S.Phase==ReloadPhase.NeedsMagazine,"last precision bolt ejects empty magazine");
        p.Do(HandlingAction.ReloadStep);Check(p.S.Phase==ReloadPhase.NeedsMagazine,"empty reserve cannot create magazine");
        var auth = new Rig(false);auth.Input.Sequence=1;auth.Input.Trigger=true;
        Check(!auth.Gun.Apply(new CommandContext("other","owner","bad",1,1,true),HandlingAction.Fire,auth.Input).Accepted,"foreign session rejected");
        Check(!auth.Gun.Apply(new CommandContext("session","owner","bad",1,2,true),HandlingAction.Fire,auth.Input).Accepted,"foreign epoch rejected");
        Check(!auth.Gun.Apply(new CommandContext("session","attacker","bad",1,1,true),HandlingAction.Fire,auth.Input).Accepted,"foreign actor rejected");
        Check(!auth.Gun.Apply(new CommandContext("session","owner","bad",1,1,false),HandlingAction.Fire,auth.Input).Accepted,"client authority rejected");
        auth.Do(HandlingAction.Fire);auth.Input.Sequence=2;
        Check(!auth.Gun.Apply(auth.Context(1),HandlingAction.Fire,auth.Input).Accepted && auth.S.Shots==1,"command replay cannot consume ammunition");
        auth.Input.DeltaSeconds=float.NaN;Check(!auth.Do(HandlingAction.Sample).Accepted,"NaN time rejected");
        var support=new Rig(false);support.Input.Grip=true;support.Input.ForegripDistance=.24f;support.Do(HandlingAction.Sample);
        Check(support.S.Supported,"two hand acquire radius");support.Input.ForegripDistance=.4f;support.Do(HandlingAction.Sample);Check(support.S.Supported,"two hand release hysteresis");
        support.Input.SupportReserved=true;support.Do(HandlingAction.Sample);Check(!support.S.Supported,"climb reservation wins support");
        support.Input.SupportReserved=false;support.Do(HandlingAction.Sample);Check(!support.S.Supported,"reservation recovery requires release");
        var one=new Rig(false);var two=new Rig(false);one.Input.Trigger=two.Input.Trigger=true;two.Input.Grip=true;two.Input.ForegripDistance=.1f;
        one.Do(HandlingAction.Fire);two.Do(HandlingAction.Fire);Check(two.S.RecoilPitch<one.S.RecoilPitch && two.S.RecoilPitch>0,"supported recoil reduction");
        one.Advance(1);Check(one.S.RecoilPitch==0 && one.S.RecoilDistance==0,"idle resets recoil");
        Check(ScopeRules.SupportAimWeight(new Float3(0,0,1),new Float3(0,0,1),1,0)==0,"zero offhand influence preserves dominant aim");
        Check(ScopeRules.SupportAimWeight(new Float3(0,0,1),new Float3(1,0,0),1,1)==0,"support aim cone rejects sideways pull");
        var scope=new ScopeRules();
        scope.Sample(true,true,true,true,false,false,true,true,true,false,0,0);
        Check(scope.Active && scope.Magnification==4,"precision grip scope activates");
        scope.Sample(true,true,true,true,false,false,true,true,false,false,0,1);
        Check(scope.Active && scope.Magnification==8,"scope latch survives reload with grip and toggles zoom");
        scope.Sample(true,true,true,true,false,false,true,true,false,false,0,1);Check(scope.Magnification==8,"held zoom stick does not repeat");
        scope.Sample(true,true,true,true,false,false,false,true,true,false,0,0);Check(!scope.Active && scope.Magnification==4,"cover blocks scope and resets zoom");
        Check(Math.Abs(ScopeRules.PhysicalLensFov(.15f,.45f,4)-9.52728f)<.001f,"physical 4x FOV matches optical geometry");
        Check(!WeaponHandling.Reach(new Float3(0,.16f,0),new Float3(0,0,0),.10f),"reload reach does not add vertical tolerance");
        Check(WeaponHandling.Reach(new Float3(.18f,0,0),new Float3(0,0,0),.10f),"reload reach includes source lateral extension");
        var empty=new Rig(false);empty.Input.Trigger=true;
        for(int n=0;n<24;n++) { empty.Do(HandlingAction.Fire); if(n<23) empty.Advance(.2f); }
        Check(empty.S.Shots==24 && empty.S.Phase==ReloadPhase.NeedsMagazine && empty.S.Loaded==0,"rifle final shot automatically ejects");
        var pause=new Rig(false);pause.Input.Trigger=true;pause.Input.Paused=true;
        Check(!pause.Do(HandlingAction.Fire).Accepted && pause.S.Loaded==24,"pause disarms weapon");
        pause.Input.Paused=false;Check(!pause.Do(HandlingAction.Fire).Accepted,"pause resume requires trigger release");
        var toggle=new Rig(false);toggle.Gun.GripToggle=true;toggle.Input.ForegripDistance=.1f;toggle.Input.Grip=true;toggle.Do(HandlingAction.Sample);
        toggle.Input.Grip=false;toggle.Do(HandlingAction.Sample);Check(toggle.S.Supported,"toggle grip retains support on release");
        toggle.Input.Grip=true;toggle.Do(HandlingAction.Sample);Check(!toggle.S.Supported,"second fresh toggle grip releases support");
        var time=new Rig(false);time.Input.Trigger=true;time.Do(HandlingAction.Fire);
        time.Input.Sequence=2;time.Input.DeltaSeconds=.2f;
        Check(!time.Gun.Apply(new CommandContext("session","owner","duplicate-time",1,1,true),HandlingAction.Fire,time.Input).Accepted && time.S.Shots==1,"same tick cannot accelerate cooldown");
        var replay=new Rig(false);replay.Do(HandlingAction.Sample);
        for(int n=0;n<300;n++) replay.Do(HandlingAction.Sample);
        replay.Input.Sequence=1;
        Check(!replay.Gun.Apply(replay.Context(1),HandlingAction.Eject,replay.Input).Accepted && replay.S.Loaded==24,"old sequence rejected beyond ID replay window");
        var drop=new Rig(false);drop.Do(HandlingAction.Eject);drop.At(0,-.278f,.15f,true);drop.Input.SupportTracked=false;drop.Do(HandlingAction.Sample);
        Check(drop.S.Phase==ReloadPhase.NeedsMagazine && drop.Ammo.Count==124,"tracking loss drops unconsumed offered magazine");
        Console.WriteLine("PASS total="+checks);
    }
}
