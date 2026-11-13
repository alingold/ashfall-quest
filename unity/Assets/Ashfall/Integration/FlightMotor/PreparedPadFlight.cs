using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.FlightMotor
{
    // Additive owner composition; no root/collider publication or geometry certificate.
    public sealed class PreparedPadFlight
    {
        internal readonly PlayerFlightMotor Owner;
        internal readonly PadContacts Contacts;
        internal readonly PreparedPadLaunch Pad;
        internal readonly PreparedPlayerStep Player;
        internal readonly long Tick,Sequence;
        internal bool Finished,Validated;
        public MotionStep Motion {get; private set;}
        internal PreparedPadFlight(PlayerFlightMotor owner,PadContacts contacts,PreparedPadLaunch pad,PreparedPlayerStep player,InputFrame input,MotionStep motion)
        {Owner=owner;Contacts=contacts;Pad=pad;Player=player;Tick=input.Tick;Sequence=input.Sequence;Motion=motion;}
        public RuleResult Validate(double now){Validated=false;var r=Owner.ValidatePadFlight(this,now);Validated=r.Accepted;return r;}
        public void CommitValidated(){Owner.CommitPadFlight(this);}
        public RuleResult PrepareCollision(FlightCollisionResult result){Validated=false;return Pad.PrepareCollision(result);}
        public void Abort(){if(Finished)return;Finished=true;Player.Abort();Pad.Abort();}
    }
    public sealed partial class PlayerFlightMotor
    {
        public RuleResult PrepareStepAfterPad(CommandContext launch,CommandContext step,InputFrame input,MotionEnvironment environment,FlightFrame observation,
            LaunchPad pad,IFlightWorld world,Float3 feet,Float3 previous,bool hasPrevious,double now,bool ready,bool climbing,bool mantling,bool ghost,out PreparedPadFlight plan)
        {
            plan=null;
            if(step.Tick!=input.Tick||launch.Tick!=step.Tick||input.Tick<=tick||input.Sequence<=sequence||!Finite(environment.HeadingRadians)||
                !Finite(environment.DesktopYawRadians)||Math.Abs(environment.DesktopYawRadians)>Math.PI||!Finite(observation.DeltaSeconds)||observation.DeltaSeconds<=0||observation.DeltaSeconds>.1f)
                return RuleResult.Reject("pad motor frame/tick");
            PreparedPadLaunch padPlan;var r=contacts.PrepareLaunch(launch,flight,pad,world,feet,previous,hasPrevious,now,ready,climbing,mantling,ghost,out padPlan);
            if(!r.Accepted)return r;
            observation.Supported=environment.Grounded;observation.Tracked=input.Tracked;observation.Paused=input.Paused;observation.XR=environment.IsXr;observation.CeilingBlocked=environment.CeilingBlocked;
            if(environment.ExternalMotionOwner)observation.InputEnabled=false;
            environment.ExternalMotionOwner=true;
            PreparedPlayerStep playerPlan;r=player.PrepareStep(input,environment,observation.DeltaSeconds,out playerPlan);
            if(!r.Accepted){padPlan.Abort();return r;}
            var motion=playerPlan.Motion;FlightMode flightMode;
            r=flight.PrepareStepAfterPad(padPlan,step,observation,motion.OwnsYaw?motion.YawRadians:0,(!input.Tracked||input.Paused)?playerPlan.Velocity:padPlan.Velocity,out flightMode);
            if(!r.Accepted){playerPlan.Abort();padPlan.Abort();return r;}
            // The pad owns this sample's velocity; Player calculated only input/yaw/stamina.
            if(flightMode!=FlightMode.Suspended){motion.Velocity=padPlan.PreparedVelocity;motion.Displacement=new Float3(motion.Velocity.X*observation.DeltaSeconds,motion.Velocity.Y*observation.DeltaSeconds,motion.Velocity.Z*observation.DeltaSeconds);motion.OwnsMotion=true;}
            plan=new PreparedPadFlight(this,contacts,padPlan,playerPlan,input,motion);return r;
        }
        internal RuleResult ValidatePadFlight(PreparedPadFlight p,double now)
        {
            var r=PadFlightLocal(p);if(!r.Accepted)return r;
            r=p.Pad.Validate(now);return r.Accepted?PadFlightLocal(p):r;
        }
        RuleResult PadFlightLocal(PreparedPadFlight p)
        {return p==null||p.Owner!=this||p.Finished||contacts!=p.Contacts||p.Tick<=tick||p.Sequence<=sequence?RuleResult.Reject("pad motor stale"):p.Player.Validate();}
        internal void CommitPadFlight(PreparedPadFlight p)
        {
            if(!PadFlightLocal(p).Accepted||!p.Pad.ValidateLocal().Accepted||!p.Validated)throw new InvalidOperationException("stale/unvalidated composed pad");
            p.Player.CommitValidated();p.Pad.CommitValidated();player.SetVelocity(p.Pad.PreparedVelocity);tick=p.Tick;sequence=p.Sequence;p.Finished=true;
        }
    }
}
