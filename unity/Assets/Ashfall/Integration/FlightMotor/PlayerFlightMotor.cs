using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.FlightMotor
{
    // Neutral composition of real public Player and Flight APIs, no engine/root writes.
    // Serialized host loop only. The supplied tuning is copied to prevent later mutation.
    public sealed partial class PlayerFlightMotor : IFlightMotor
    {
        readonly Locomotion player;
        readonly FlightMachine flight;
        PadContacts contacts=new PadContacts();
        long tick=-1,sequence;
        public Float3 Velocity { get { return player.Velocity; } }
        public FlightMotionReceipt PendingMotion { get { return flight.PendingMotion; } }
        public FlightMode Mode { get { return flight.Mode; } }
        public PlayerFlightMotor(string session,string actor,long epoch,IAuthorityGate gate,PlayerTuning source,float flightMultiplier)
        {
            if(source==null||!Finite(source.WalkMultiplier)||!Finite(source.SnapRadians)||source.SnapRadians<=0||source.SnapRadians>Math.PI||
                !Finite(source.SmoothRadiansPerSecond)||source.SmoothRadiansPerSecond<0||source.SmoothRadiansPerSecond>100||!Enum.IsDefined(typeof(TurnStyle),source.Turning))throw new ArgumentException("tuning");
            PlayerTuning tuning=source.MaximumWalkMultiplier==3?PlayerTuning.SkinRuntime():new PlayerTuning();
            tuning.WalkMultiplier=source.WalkMultiplier;tuning.Turning=source.Turning;tuning.SnapRadians=source.SnapRadians;
            tuning.SmoothRadiansPerSecond=source.SmoothRadiansPerSecond;tuning.SmoothAnalog=source.SmoothAnalog;tuning.SmoothEaseIn=source.SmoothEaseIn;
            player=new Locomotion(tuning);
            flight=new FlightMachine(session,actor,epoch,gate,PlayerTuning.WalkSpeed*Math.Max(.25f,Math.Min(tuning.MaximumWalkMultiplier,tuning.WalkMultiplier)),flightMultiplier);
        }
        static bool Finite(float x){return !Single.IsNaN(x)&&!Single.IsInfinity(x);}
        public RuleResult Step(CommandContext c,InputFrame input,MotionEnvironment environment,FlightFrame observation,out MotionStep motion)
        {
            motion=new MotionStep();PreparedPlayerFlightStep plan;
            var r=PrepareStep(c,input,environment,observation,out plan);if(!r.Accepted)return r;
            r=plan.Validate();if(!r.Accepted){plan.Abort();return r;}
            motion=plan.Motion;plan.CommitValidated();return r;
        }
        public RuleResult ApplyCollisionResult(CommandContext c,FlightMotionReceipt receipt,FlightCollisionResult result,IFlightCollisionAuthorization authorization)
        {
            RuleResult r=flight.ApplyCollisionResult(c,receipt,result,authorization);
            if(r.Accepted)player.SetVelocity(flight.Velocity);
            return r;
        }
        public RuleResult LaunchPad(CommandContext c,LaunchPad pad,IFlightWorld world,Float3 feet,Float3 previous,bool hasPrevious,double now,bool ready,bool climbing,bool mantling,bool ghost)
        {
            RuleResult r=contacts.TryLaunch(c,flight,pad,world,feet,previous,hasPrevious,now,ready,climbing,mantling,ghost);
            if(r.Accepted)player.SetVelocity(flight.Velocity);
            return r;
        }
        public RuleResult ResetActor(CommandContext c,long generation,FlightLifecycle reason,IFlightLifecycleAuthorization authorization)
        {
            RuleResult r=flight.ResetActor(c,generation,reason,authorization);
            if(r.Accepted){player.ResetMotion();contacts=new PadContacts();}
            return r;
        }
    }
}
