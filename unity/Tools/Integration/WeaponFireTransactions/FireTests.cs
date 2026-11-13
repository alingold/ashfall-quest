using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.WeaponFireTransactions;
static class FireTests
{
    sealed class Gate : IAuthorityGate, IWeaponLifetimeAuthority, IWeaponFireBinding
    {
        public bool Allowed=true;
        public bool Binding=true;
        public RuleResult Validate(CommandContext c){return Allowed?RuleResult.Accept():RuleResult.Reject("revoked");}
        public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Validate(c);}
        public RuleResult AuthorizeRestore(CommandContext c,string id,string a,long e){return Validate(c);}
        public RuleResult Validate(CommandContext c,string id,long e,long l,long n,long f)
        {return Binding&&e==3&&l==4&&n==5&&f>=0?Validate(c):RuleResult.Reject("binding");}
    }
    sealed class Reserve : IRevisionedAmmoReserve
    {
        public readonly FiniteAmmoReserve Real=new FiniteAmmoReserve(100,50);
        public int Calls;
        public int Available {get{return Real.Available;}}
        public long Revision {get{return Real.Revision;}}
        public int Take(int n){Calls++;return Real.Take(n);}
        public int Refund(int n){Calls++;return Real.Refund(n);}
    }
    sealed class Fixture
    {
        public Gate Gate=new Gate(); public Reserve Reserve=new Reserve();
        public WeaponLifetimes Ledger;public WeaponHandling Weapon;
        public Fixture(WeaponMechanism m,int rounds)
        {Ledger=new WeaponLifetimes("s",Gate);Need(Ledger.CreateNew(C("create",0),"w",m,rounds,Reserve,Gate,out Weapon).Accepted,"create");}
        public IPreparedWeaponFire Prepare(string id,long tick,long seq)
        {IPreparedWeaponFire p;Need(Ledger.PrepareFire(C(id,tick),"w",I(seq),3,4,5,tick+10,Gate,out p).Accepted,"prepare");return p;}
    }
    static int count;
    static void Need(bool b,string n){if(!b)throw new Exception(n);count++;}
    static CommandContext C(string id,long t){return new CommandContext("s","a",id,t,0,true);}
    static HandlingInput I(long s){return new HandlingInput{Sequence=s,Trigger=true,Enabled=true,PrimaryTracked=true,SupportTracked=true,ForegripDistance=1};}
    static void Same(HandlingSnapshot a,HandlingSnapshot b)
    {
        foreach(var field in typeof(HandlingSnapshot).GetFields())
            Need(Object.Equals(field.GetValue(a),field.GetValue(b)),"production parity "+field.Name);
    }
    static void Main()
    {
        foreach(WeaponMechanism m in Enum.GetValues(typeof(WeaponMechanism)))
        foreach(int rounds in new int[]{1,2})
        {
            var f=new Fixture(m,rounds);var direct=new Fixture(m,rounds);
            var input=I(1);input.DeltaSeconds=.2f;input.Grip=true;input.ForegripDistance=.1f;
            IPreparedWeaponFire p;Need(f.Ledger.PrepareFire(C("parity",1),"w",input,3,4,5,10,f.Gate,out p).Accepted,"parity prepare");
            Need(direct.Weapon.Apply(C("parity",1),HandlingAction.Fire,input).Accepted,"production fire");
            Need(p.Validate(1).Accepted,"parity validate");p.CommitValidated();Same(f.Weapon.Snapshot,direct.Weapon.Snapshot);
        }
        foreach(WeaponMechanism m in Enum.GetValues(typeof(WeaponMechanism)))
        {
            var f=new Fixture(m,2);var p=f.Prepare("shot",1,1);
            Need(f.Weapon.Snapshot.Loaded==2&&f.Weapon.Snapshot.Shots==0&&f.Reserve.Calls==0&&p.Result==null,"prepare isolated");
            p.Abort();p.Abort();p.CommitValidated();Need(f.Weapon.Snapshot.Loaded==2,"abort idempotent");
            p=f.Prepare("shot",1,1);Need(p.Validate(1).Accepted,"validate");p.CommitValidated();p.CommitValidated();p.Abort();
            Need(f.Weapon.Snapshot.Loaded==1&&f.Weapon.Snapshot.Shots==1&&p.Result!=null,"once committed");
            var receipt=p.Result;var copy=receipt.After;copy.Loaded=100;Need(receipt.After.Loaded==1,"immutable value snapshot");
            Need(receipt.Mechanism==m&&receipt.FrameSequence==1&&receipt.LifeGeneration==4,"exact receipt");
            IPreparedWeaponFire replay;Need(!f.Ledger.PrepareFire(C("shot",2),"w",I(2),3,4,5,10,f.Gate,out replay).Accepted,"replay");
            WeaponTransfer transfer;Need(f.Ledger.Capture(C("drop",2),"w",out transfer).Accepted,"capture");
            Need(receipt.After.Shots==1&&receipt.ExpiresTick==11,"receipt survives retirement unchanged");
            Need(!f.Weapon.Apply(C("retired",3),HandlingAction.Fire,I(3)).Accepted,"retired cannot extra fire");
        }
        {
            var f=new Fixture(WeaponMechanism.ServiceRifle,1);var p=f.Prepare("last",1,1);
            Need(f.Reserve.Calls==0&&f.Reserve.Available==50,"last round no shallow reserve call");
            Need(p.Validate(1).Accepted,"last valid");p.CommitValidated();Need(f.Weapon.Snapshot.Loaded==0&&f.Weapon.Snapshot.Phase==ReloadPhase.NeedsMagazine,"real last eject");
            var reload=I(2);reload.Trigger=false;reload.Desktop=true;reload.DeltaSeconds=.2f;
            Need(f.Weapon.Apply(C("insert",2),HandlingAction.ReloadStep,reload).Accepted,"actual reserve insert");
            Need(f.Reserve.Available==26&&f.Weapon.Snapshot.Seated==24,"refill debits actual reserve");
            reload.Sequence=3;Need(f.Weapon.Apply(C("rack",3),HandlingAction.ReloadStep,reload).Accepted,"actual rack");
            reload.Sequence=4;Need(f.Weapon.Apply(C("release",4),HandlingAction.Sample,reload).Accepted,"source requires trigger release after rack");
            var shot=I(5);shot.DeltaSeconds=.2f;IPreparedWeaponFire refilled;
            Need(f.Ledger.PrepareFire(C("refilled",5),"w",shot,3,4,5,10,f.Gate,out refilled).Accepted,"prepare after refill");
            refilled.Abort();Need(f.Weapon.Snapshot.Loaded==24&&f.Reserve.Available==26,"refill abort conservation");
        }
        {
            var f=new Fixture(WeaponMechanism.ServiceRifle,3);var p=f.Prepare("cadence",1,1);p.Validate(1);p.CommitValidated();
            IPreparedWeaponFire blocked;Need(!f.Ledger.PrepareFire(C("cadence-retry",2),"w",I(2),3,4,5,10,f.Gate,out blocked).Accepted,"cooldown refuses without consumption");
            var input=I(2);input.DeltaSeconds=.12f;Need(f.Ledger.PrepareFire(C("cadence-retry",2),"w",input,3,4,5,10,f.Gate,out blocked).Accepted,"same failed command recover cadence");
            Need(blocked.Validate(2).Accepted,"cadence validate");blocked.CommitValidated();Need(f.Weapon.Snapshot.Loaded==1,"cadence true ammo");
        }
        {
            var f=new Fixture(WeaponMechanism.PrecisionRifle,2);var p=f.Prepare("precision",1,1);p.Validate(1);p.CommitValidated();
            Need(!f.Weapon.Snapshot.Chambered&&f.Weapon.Snapshot.SpentChamber&&f.Weapon.Snapshot.Phase==ReloadPhase.NeedsBolt,"precision bolt preserved");
        }
        {
            var f=new Fixture(WeaponMechanism.MagnumRevolver,1);var p=f.Prepare("magnum",1,1);p.Validate(1);p.CommitValidated();
            Need(f.Weapon.Snapshot.Phase==ReloadPhase.CylinderOpen&&f.Weapon.Snapshot.TriggerReleaseRequired,"magnum cylinder preserved");
        }
        {
            var f=new Fixture(WeaponMechanism.ServiceRifle,3);var p=f.Prepare("aba",1,1);
            f.Reserve.Take(1);f.Reserve.Refund(1);Need(!p.Validate(1).Accepted&&f.Weapon.Snapshot.Loaded==3,"reserve ABA stale");p.Abort();
            p=f.Prepare("retry",1,1);Need(!p.Validate(12).Accepted,"expiry");p.Abort();
            p=f.Prepare("retry",1,1);Need(p.Validate(5).Accepted&&!p.Validate(4).Accepted,"clock reversal");p.Abort();
            p=f.Prepare("retry",1,1);f.Gate.Binding=false;Need(!p.Validate(1).Accepted,"canonical binding revoked");p.Abort();f.Gate.Binding=true;
            p=f.Prepare("retry",1,1);f.Gate.Allowed=false;Need(!p.Validate(1).Accepted,"authority revoked");p.Abort();f.Gate.Allowed=true;
            p=f.Prepare("retry",1,1);Need(f.Weapon.Apply(C("sample",1),HandlingAction.Sample,new HandlingInput{Sequence=1,Enabled=true,PrimaryTracked=true}).Accepted,"actual mutation");
            Need(!p.Validate(1).Accepted,"mechanics stale");p.Abort();
            p=f.Prepare("retry",2,2);IPreparedWeaponTransfer tr;Need(!f.Ledger.PrepareTransfer(C("transfer",2),"w","b",1,out tr).Accepted,"fire reservation excludes transfer");p.Abort();Need(f.Ledger.PrepareTransfer(C("transfer",2),"w","b",1,out tr).Accepted,"transfer prepare after fire abort");
            Need(tr.Validate().Accepted,"transfer validate");tr.CommitValidated();Need(!p.Validate(2).Accepted,"transfer stale");p.Abort();
        }
        {
            var f=new Fixture(WeaponMechanism.ServiceRifle,3);var first=f.Prepare("first",1,1);
            IPreparedWeaponFire second;
            Need(!f.Ledger.PrepareFire(C("second",1),"w",I(2),3,4,5,10,f.Gate,out second).Accepted,"one outstanding fire");
            first.Abort();second=f.Prepare("second",1,2);Need(second.Validate(1).Accepted,"abort releases capacity");second.Abort();
            first=f.Prepare("expired",2,3);second=f.Prepare("replacement",13,4);
            first.Abort();Need(!first.Validate(13).Accepted&&second.Validate(13).Accepted,"expiry replacement old abort safe");second.Abort();
        }
        {
            var f=new Fixture(WeaponMechanism.ServiceRifle,3);
            var r=AtomicLaunchPreparation.Prepare(f.Ledger,C("launch",1),"w",I(1),3,4,5,10,f.Gate);
            Need(!r.Accepted&&r.Reason=="combat-prepared-launch-api-missing"&&f.Weapon.Snapshot.Loaded==3,"aggregate missing fail closed");
            var p=f.Prepare("launch",1,1);Need(p.Validate(1).Accepted,"same command retry after abort");p.CommitValidated();
            Need(f.Weapon.Snapshot.Loaded==2,"retry actual ammo");
        }
        Console.WriteLine("PASS "+count+" assertions; neutral production API only");
    }
}
