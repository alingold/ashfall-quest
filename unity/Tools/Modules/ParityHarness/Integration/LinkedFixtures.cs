using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Modules.WeaponHandling;
using Gun = Ashfall.Modules.WeaponHandling.WeaponHandling;
namespace Ashfall.Modules.ParityHarness
{
    // Tooling only: real neutral module sources are frozen by Run-R2.py before compile.
    public sealed class LinkedSubjectProvider : ISubjectProvider
    {
        public Subjects Create() { return new Subjects { Conversion=new HistoricalConversionAdapter(), Gameplay=new LinkedFixtures() }; }
    }
    public sealed class LinkedFixtures : IGameplayFixtures
    {
        public IPadFixture CreatePad(PadInitialState state) { return new FlightFixture(state); }
        public IReloadFixture CreateReload(int reserve) { return new WeaponFixture(reserve); }
    }
    internal sealed class FixtureGate : IAuthorityGate
    {
        public RuleResult Validate(CommandContext c)
        { return c.IsAuthority && c.SessionId=="parity" && c.ActorId=="owner" && c.AuthorityEpoch==1 && c.Tick>=0 ? RuleResult.Accept() : RuleResult.Reject("fixture identity"); }
    }
    public sealed class FlightFixture : IPadFixture
    {
        public readonly FlightMachine Machine;
        readonly PadInitialState initial;
        long tick;
        public CommandContext Next() { long n=++tick; return new CommandContext("parity","owner","flight-"+n,n,1,true); }
        public FlightFixture(PadInitialState state)
        {
            if (state.Gliding && state.Diving) throw new UnknownEvidenceException("FlightMode cannot construct simultaneous Glide and Dive");
            initial=state; Machine=new FlightMachine("parity","owner",1,new FixtureGate(),6.5f,1);
            FlightFrame frame=new FlightFrame { DeltaSeconds=1, Tracked=true, InputEnabled=true, Supported=!state.Gliding&&!state.Diving, Forward=new Float3(0,0,1), Velocity=state.Velocity, DesktopGlide=state.Gliding, DiveForward=state.Diving };
            if(state.Diving) frame.Velocity=new Float3(0,-10,0);
            Verify.That(Machine.Step(Next(),frame).Accepted,"Production flight setup rejected");
            Verify.That(Gliding==state.Gliding && Diving==state.Diving,"Production flight mode setup differs");
            if(!state.Gliding&&!state.Diving) Same(Machine.Velocity,state.Velocity);
        }
        static void Same(Float3 a,Float3 b) { Verify.Near(a.X,b.X,"setup vx"); Verify.Near(a.Y,b.Y,"setup vy"); Verify.Near(a.Z,b.Z,"setup vz"); }
        public bool Launch(float speed, Float3 direction) { return Machine.Launch(Next(),direction,speed,initial.InputEnabled,initial.Climbing,initial.Mantling).Accepted; }
        public Float3 Velocity { get { return Machine.Velocity; } }
        public bool Gliding { get { return Machine.Mode==FlightMode.Glide; } }
        public bool Diving { get { return Machine.Mode==FlightMode.Dive; } }
        public void Dispose() { }
    }
    internal sealed class FiniteReserve : IAmmoReserve
    {
        int count;
        public FiniteReserve(int count) { if(count<0)throw new ArgumentOutOfRangeException("count"); this.count=count; }
        public int Available { get { return count; } }
        public int Take(int requested) { if(requested<0)throw new ArgumentOutOfRangeException("requested"); int n=Math.Min(requested,count); count-=n; return n; }
        public int Refund(int rounds) { if(rounds<0)throw new ArgumentOutOfRangeException("rounds"); count=checked(count+rounds); return rounds; }
    }
    public sealed class WeaponFixture : IReloadFixture
    {
        public readonly Gun Machine;
        readonly FiniteReserve reserve;
        public HandlingInput Input;
        long tick;
        public CommandContext Next() { long n=++tick; Input.Sequence=n; return new CommandContext("parity","owner","weapon-"+n,n,1,true); }
        public RuleResult Send(HandlingAction action) { CommandContext c=Next(); return Machine.Apply(c,action,Input); }
        public WeaponFixture(int rounds)
        {
            reserve=new FiniteReserve(rounds); Machine=new Gun("parity","owner",1,WeaponMechanism.ServiceRifle,reserve,new FixtureGate());
            Input=new HandlingInput { Enabled=true,PrimaryTracked=true,SupportTracked=true,ForegripDistance=1,DeltaSeconds=.2f };
            // Constructor policy changed concurrently: r1 starts loaded, r2/2 starts empty.
            // Observe the real public snapshot; drain only actual initial rounds. Never seed internal state.
            int initialRounds=Machine.Snapshot.Loaded;
            for(int n=0;n<initialRounds;n++) {
                Input.Trigger=false; Verify.That(Send(HandlingAction.Sample).Accepted,"Trigger release setup");
                Input.Trigger=true; Verify.That(Send(HandlingAction.Fire).Accepted,"Real drain shot rejected");
            }
            Input.Trigger=false; Verify.That(Send(HandlingAction.Sample).Accepted,"Post drain release");
            Verify.That(Machine.Snapshot.Shots==initialRounds && Stage=="needs_mag" && Ammo==0 && Reserve==rounds && !Chambered,"Real empty rifle setup");
        }
        public void At(float x,float y,float z,bool grip) { Input.SupportLocal=new Float3(x,y,z); Input.Grip=grip; Verify.That(Send(HandlingAction.Sample).Accepted,"Physical sample rejected"); }
        public void GrabMagazine() { At(0,-.278f,.15f,false); At(0,-.278f,.15f,true); }
        public void InsertMagazine() { At(0,-.18f,.15f,true); }
        public void RackBolt() { At(-.045f,.064f,.10f,false); At(-.045f,.064f,.10f,true); At(-.045f,.064f,.02f,true); }
        public string Stage { get { switch(Machine.Snapshot.Phase) { case ReloadPhase.Ready:return "ready"; case ReloadPhase.NeedsMagazine:return "needs_mag"; case ReloadPhase.HeldMagazine:return "held_mag"; case ReloadPhase.NeedsRack:return "needs_rack"; case ReloadPhase.NeedsBolt:return "needs_bolt"; default:throw new InvalidOperationException("Unknown phase"); } } }
        public int Ammo { get { return Machine.Snapshot.Loaded; } }
        public int Seated { get { return Machine.Snapshot.Seated; } }
        public bool Chambered { get { return Machine.Snapshot.Chambered; } }
        public int Reserve { get { return reserve.Available; } }
        public void Dispose() { }
    }
}
