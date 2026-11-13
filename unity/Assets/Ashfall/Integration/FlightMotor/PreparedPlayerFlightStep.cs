using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.FlightMotor
{
    // This is a mechanics reservation, NOT a root/geometry certificate.
    public sealed class PreparedPlayerFlightStep
    {
        internal readonly PlayerFlightMotor Owner;
        internal readonly PreparedPlayerStep Player;
        internal readonly PreparedFlightStep Flight;
        internal readonly long Tick, Sequence;
        internal bool Finished, Collision;
        internal Float3 FinalVelocity;
        public MotionStep Motion {get; private set;}
        internal PreparedPlayerFlightStep(PlayerFlightMotor owner,PreparedPlayerStep player,PreparedFlightStep flight,InputFrame input,MotionStep motion,Float3 velocity)
        {Owner=owner;Player=player;Flight=flight;Tick=input.Tick;Sequence=input.Sequence;Motion=motion;FinalVelocity=velocity;}
        public RuleResult Validate(){return Owner.ValidatePrepared(this);}
        public RuleResult PrepareCollision(FlightCollisionResult result){return Owner.PrepareCollision(this,result);}
        public void CommitValidated(){Owner.CommitPrepared(this);}
        public void Abort(){if(Finished)return;Finished=true;Player.Abort();Flight.Abort();}
    }
    public sealed partial class PlayerFlightMotor
    {
        public RuleResult PrepareStep(CommandContext c,InputFrame input,MotionEnvironment environment,FlightFrame observation,out PreparedPlayerFlightStep plan)
        {
            plan=null;
            if(c.Tick!=input.Tick||input.Tick<=tick||input.Sequence<=sequence||!Finite(environment.HeadingRadians)||
                !Finite(environment.DesktopYawRadians)||Math.Abs(environment.DesktopYawRadians)>Math.PI||
                !Finite(observation.DeltaSeconds)||observation.DeltaSeconds<=0||observation.DeltaSeconds>.1f)return RuleResult.Reject("motor frame/tick");
            observation.Velocity=player.Velocity;observation.Supported=environment.Grounded;
            observation.Tracked=input.Tracked;observation.Paused=input.Paused;observation.XR=environment.IsXr;observation.CeilingBlocked=environment.CeilingBlocked;
            if(environment.ExternalMotionOwner)observation.InputEnabled=false;
            PreparedFlightStep flightPlan;var r=flight.PrepareStep(c,observation,out flightPlan);if(!r.Accepted)return r;
            bool airborne=flightPlan.Mode!=FlightMode.Ground&&flightPlan.Mode!=FlightMode.Suspended;
            environment.ExternalMotionOwner|=airborne||flightPlan.Mode==FlightMode.Suspended;
            PreparedPlayerStep playerPlan;r=player.PrepareStep(input,environment,observation.DeltaSeconds,out playerPlan);
            if(!r.Accepted){flightPlan.Abort();return r;}
            var motion=playerPlan.Motion;Float3 produced;
            r=flight.ResolvePreparedStep(flightPlan,motion.OwnsYaw?motion.YawRadians:0,playerPlan.Velocity,out produced);
            if(!r.Accepted){playerPlan.Abort();flightPlan.Abort();return r;}
            if(airborne)
            {
                motion.Velocity=produced;motion.Displacement=new Float3(produced.X*observation.DeltaSeconds,produced.Y*observation.DeltaSeconds,produced.Z*observation.DeltaSeconds);motion.OwnsMotion=true;
            }
            plan=new PreparedPlayerFlightStep(this,playerPlan,flightPlan,input,motion,produced);return r;
        }
        internal RuleResult ValidatePrepared(PreparedPlayerFlightStep p)
        {
            if(p==null||p.Owner!=this||p.Finished||p.Tick<=tick||p.Sequence<=sequence)return RuleResult.Reject("stale composed motion");
            var r=p.Player.Validate();return r.Accepted?flight.ValidatePreparedStep(p.Flight):r;
        }
        internal RuleResult PrepareCollision(PreparedPlayerFlightStep p,FlightCollisionResult result)
        {
            var r=ValidatePrepared(p);if(!r.Accepted)return r;
            r=flight.PrepareCollision(p.Flight,result);
            if(r.Accepted){p.Collision=true;p.FinalVelocity=result.Velocity;}return r;
        }
        internal void CommitPrepared(PreparedPlayerFlightStep p)
        {
            if(p==null||p.Owner!=this||p.Finished)throw new InvalidOperationException("motion token");
            // Check BOTH local revisions before the first state write.
            if(!p.Player.Validate().Accepted||!flight.ValidatePreparedStepLocal(p.Flight).Accepted)throw new InvalidOperationException("stale composed publication");
            // Only local invariant checks remain. External gate validation was above.
            p.Player.CommitValidated();flight.CommitPreparedStepValidated(p.Flight);
            player.SetVelocity(p.FinalVelocity);tick=p.Tick;sequence=p.Sequence;p.Finished=true;
        }
    }
}
