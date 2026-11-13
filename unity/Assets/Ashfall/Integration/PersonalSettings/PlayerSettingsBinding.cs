using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
namespace Ashfall.Integration.PersonalSettings
{
    // Coordinator owns this binding and applies Effective after accepted commits or
    // context refreshes. No tracked pose is swapped to implement dominant hand.
    public sealed class PlayerSettingsBinding : IPlayerSettingsPort
    {
        private readonly PlayerInput input;
        private EffectiveSettings current;
        private bool paused;
        private EffectiveSettings flightBoundSettings;
        private Ashfall.Integration.FlightMotor.PlayerFlightMotor flightBoundMotor;
        private Ashfall.Modules.Flight.FlightSettingsSnapshot flightBoundSnapshot;
        private long flightBoundRevision;
        private Locomotion motor;
        private PlayerTuning liveTuning;
        private TypedPlayerSettingsPort port;
        private readonly MovementHeading heading=new MovementHeading();
        public ForwardReference ForwardReference { get { return current!=null&&current.Text("forward_reference")=="hand"?ForwardReference.Hand:ForwardReference.Head; } }
        public float GroundSpeedGain(float sourceGain)
        {
            if(current==null)throw new InvalidOperationException("Settings required");
            return port.GroundSpeedGain(sourceGain);
        }
        // One persistent real motor: reapply tuning without replacing motion/stamina.
        public Locomotion CreateLocomotion()
        {
            if(motor!=null)return motor;
            liveTuning=CreateTuning();motor=new Locomotion(liveTuning);
            motor.Settings=port;motor.OwnershipSprint=input.Ownership.Sprint;return motor;
        }
        public float ReadHeading(bool xr,Float3 headForward,Float3 leftForward,Float3 leftUp,float rootYaw)
        { return heading.Read(ForwardReference,xr,input.LeftReady,headForward,leftForward,leftUp,rootYaw); }
        public string[] UnsupportedTuningFields { get { return flightBoundSettings==current&&current!=null&&flightBoundMotor!=null&&flightBoundMotor.FlightSettingsRevision==flightBoundRevision&&Object.ReferenceEquals(flightBoundMotor.FlightSettings,flightBoundSnapshot)?new[]{"sprint_double_press_window"}:new[]{"sprint_double_press_window","glide_steering_gain"}; } }
        public PlayerSettingsBinding(PlayerInput input)
        { if(input==null)throw new ArgumentNullException("input");this.input=input; }
        public EffectiveSettings Current { get { return current; } }
        public PlayerTuning CreateTuning()
        { if(current==null)throw new InvalidOperationException("Settings required");return current.CreatePlayerTuning(); }
        public void Apply(EffectiveSettings next)
        {
            if(next==null)throw new ArgumentNullException("next");
            var nextPort=new TypedPlayerSettingsPort(next);
            var dominant=next.LeftHanded?DominantHand.Left:DominantHand.Right;
            var sprint=next.Text("sprint_binding")=="left_grip"?SprintBinding.LeftGrip:SprintBinding.LeftClick;
            if(current==null||input.Ownership.Dominant!=dominant||input.Ownership.Sprint!=sprint)input.Disarm();
            input.Ownership.Dominant=dominant;input.Ownership.Sprint=sprint;
            if(current==null||current.Text("forward_reference")!=next.Text("forward_reference"))heading.Reset();
            current=next;port=nextPort;
            if(motor!=null) {
                var t=next.CreatePlayerTuning();
                liveTuning.WalkMultiplier=t.WalkMultiplier;liveTuning.Turning=t.Turning;
                liveTuning.SnapRadians=t.SnapRadians;liveTuning.SmoothRadiansPerSecond=t.SmoothRadiansPerSecond;
                liveTuning.SmoothAnalog=t.SmoothAnalog;liveTuning.SmoothEaseIn=t.SmoothEaseIn;
                motor.OwnershipSprint=sprint;
                motor.Settings=nextPort;
            }
        }
        // Call after Apply and before preparing the next Flight frame. Successful
        // consumption is explicit; merely creating a snapshot does not mark it bound.
        public RuleResult PublishFlightSettings(Ashfall.Integration.FlightMotor.PlayerFlightMotor flight,CommandContext context,long generation,long revision)
        {
            if(flight==null||current==null)return RuleResult.Reject("Flight/settings required");
            var source=current;
            var result=flight.PublishFlightSettings(context,generation,revision,FlightSettingsPort.Capture(source));
            if(result.Accepted){flightBoundSettings=source;flightBoundMotor=flight;flightBoundRevision=flight.FlightSettingsRevision;flightBoundSnapshot=flight.FlightSettings;}
            return result;
        }
        public void SetPresentationPaused(bool value)
        { if(value!=paused)input.Disarm();paused=value; }
        public InputFrame Read(long tick,float dt,DeviceSample sample)
        {
            // A missing binding fails closed while keeping poses available.
            sample.Paused|=paused||current==null;
            return input.Read(tick,dt,HandActionAdapter.Bind(sample,input.Ownership));
        }
    }
}
