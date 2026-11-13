using System;
using Ashfall.Core;

namespace Ashfall.Modules.Player
{
    public sealed partial class Locomotion
    {
        private readonly PlayerTuning tuning;
        private IPlayerSettingsPort settings;
        public IPlayerSettingsPort Settings {get{return settings;}set{settings=value;revision++;}}
        private IPlayerInteractionPort interaction;
        public IPlayerInteractionPort Interaction {get{return interaction;}set{interaction=value;revision++;}}
        private Float3 velocity;
        private long lastTick = -1, lastSequence;
        private bool jumpHeld, sprintHeld, turnNeedsNeutral, snapReady = true;
        private int sprintTier;
        private float stamina = -1, snapTimer, turnRate;
        private SprintBinding ownershipSprint;
        public SprintBinding OwnershipSprint {get{return ownershipSprint;}set{ownershipSprint=value;revision++;}}
        public Float3 Velocity { get { return velocity; } }
        public float SprintStamina { get { return stamina < 0 ? StaminaMaximum : Math.Min(stamina,StaminaMaximum); } }
        public float StaminaMaximum { get { var p=settings as IPlayerSprintSettingsPort;return p==null?6:p.StaminaSeconds; } }
        private float RecoverySeconds { get { var p=settings as IPlayerSprintSettingsPort;return p==null?4:p.RecoverySeconds; } }
        public int SprintTier { get { return sprintTier; } }
        public Locomotion(PlayerTuning tuning)
        {
            if (tuning == null) throw new ArgumentNullException("tuning");
            this.tuning = tuning;
        }
        // Called by the central exclusive owner on a flight/climb -> walk handoff.
        public void SetVelocity(Float3 value)
        { if (!value.IsFinite || Math.Abs(value.X) > 10000 || Math.Abs(value.Y) > 10000 || Math.Abs(value.Z) > 10000) throw new ArgumentException("velocity"); velocity = value; revision++; }
        // Privileged host effect port; caller owns authenticated exactly-once item transaction.
        // Does not alter motion, input edges, or refill on cancellation.
        public float RestoreStamina(float amount)
        {if(!PlayerInput.Finite(amount)||amount<0||amount>6)throw new ArgumentOutOfRangeException("amount");ValidateSprintSettings();revision++;float before=SprintStamina;stamina=Math.Min(StaminaMaximum,before+amount);return stamina-before;}
        public void ResetMotion()
        {
            ValidateSprintSettings();stamina = StaminaMaximum;
            CancelMotion();
        }
        // Pause/collision cancellation must not refill stamina.
        public void CancelMotion()
        {
            revision++; velocity = new Float3(); jumpHeld = sprintHeld = false; sprintTier = 0;
            snapReady = true; turnNeedsNeutral = false; snapTimer = turnRate = 0;
        }
        public MotionStep Step(InputFrame input, MotionEnvironment world, float dt)
        {
            // Calculate on a detached state so validation failures preserve retryable input.
            Locomotion next=(Locomotion)MemberwiseClone();
            MotionStep motion=next.CalculateStep(input,world,dt);
            var plan=new PreparedPlayerStep(this,next,revision,++reservation,motion,tuning);
            CommitPrepared(plan);
            return motion;
        }
        private MotionStep CalculateStep(InputFrame input, MotionEnvironment world, float dt)
        {
            // DesktopLook and published tuning carry float radians; accept their
            // rounded inclusive pi endpoint, while rejecting the next float outside.
            if (!PlayerInput.Finite(dt) || dt <= 0 || dt > .1f || !PlayerInput.Finite(world.HeadingRadians) ||
                !PlayerInput.Finite(world.DesktopYawRadians) || Math.Abs(world.DesktopYawRadians) > (float)Math.PI)
                throw new ArgumentOutOfRangeException("dt/heading");
            if (input.Tick <= lastTick || input.Sequence <= lastSequence) throw new ArgumentException("Stale input");
            ValidateTuning();
            ValidateSprintSettings();
            float capacity=StaminaMaximum,recovery=RecoverySeconds;
            stamina=stamina<0?capacity:Math.Min(stamina,capacity);
            lastTick = input.Tick; lastSequence = input.Sequence;
            if (!input.Tracked || input.Paused)
            {
                if (!input.Tracked) ResetMotion(); else CancelMotion();
                return new MotionStep();
            }
            float yaw = world.IsXr ? 0 : world.DesktopYawRadians;
            if (world.TurningBlocked) { yaw = 0; turnNeedsNeutral = true; snapReady = false; snapTimer = turnRate = 0; }
            else if (!turnNeedsNeutral || Math.Abs(Axis(input.TurnX)) < .3f)
            { turnNeedsNeutral = false; yaw += Turn(input.TurnX, input.TurnY, dt); }
            if (world.ExternalMotionOwner)
            {
                jumpHeld = input.Jump; sprintHeld = input.Sprint; sprintTier = 0;
                return new MotionStep { InteractionsAllowed = true, YawRadians = yaw, OwnsYaw = !world.TurningBlocked };
            }
            float x = Axis(input.MoveX), y = Axis(input.MoveY);
            float length = (float)Math.Sqrt(x * x + y * y);
            if (length > 1) { x /= length; y /= length; }
            if (length < .15f) x = y = 0;
            bool moving = length > .3f;
            SprintContacts contacts=Interaction==null?new SprintContacts():Interaction.ReadContacts();
            bool gripBlocked=OwnershipSprint==SprintBinding.LeftGrip&&(contacts.LeftReserved||contacts.LeftGripReserved||contacts.ProviderBlocksSprint);
            bool contact=OwnershipSprint==SprintBinding.LeftGrip&&contacts.LeftSurfaceContact&&input.Sprint;
            bool eligible = world.IsXr && world.Grounded && !world.SprintBlocked && !contacts.Climbing && !contacts.Mantling && !contacts.HasGrab && !gripBlocked;
            if(contacts.Climbing||contacts.Mantling||(eligible&&contact))sprintTier=0;
            if (eligible && !contact && input.Sprint && !sprintHeld)
                sprintTier = sprintTier == 1 ? 2 : (sprintTier == 2 ? 0 : (stamina >= capacity*.25f ? 1 : 0));
            sprintHeld = input.Sprint;
            if (!moving) sprintTier = 0;
            bool sprint = eligible && moving && sprintTier != 0;
            stamina = sprint ? Math.Max(0, stamina - dt) : Math.Min(capacity, stamina + dt * capacity/recovery);
            if (stamina == 0) { sprintTier = 0; sprint = false; }
            float gain = sprint ? (sprintTier == 2 ? 9.5f / 6.5f : 7.5f / 6.5f) : 1;
            if(Settings!=null)gain=Settings.GroundSpeedGain(gain);
            if(!PlayerInput.Finite(gain)||gain<0||gain>100)throw new ArgumentException("ground gain");
            float speed = PlayerTuning.WalkSpeed * Math.Max(.25f, Math.Min(tuning.MaximumWalkMultiplier, tuning.WalkMultiplier)) * gain;
            // Reduce BEFORE Framework trig: very large finite arguments are unsafe on this runtime.
            double heading = ((double)world.HeadingRadians % (2 * Math.PI) + yaw) % (2 * Math.PI);
            float sin = (float)Math.Sin(heading), cos = (float)Math.Cos(heading);
            float tx = (cos * x + sin * y) * speed, tz = (-sin * x + cos * y) * speed;
            float vx = tx, vz = tz;
            if (!world.Grounded)
            {
                float dx = tx - velocity.X, dz = tz - velocity.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                float amount = (x == 0 && y == 0 ? .8f : 6f) * dt;
                float fraction = distance == 0 ? 1 : Math.Min(1, amount / distance);
                vx = velocity.X + dx * fraction; vz = velocity.Z + dz * fraction;
            }
            float vy = velocity.Y - 16.2f * dt;
            if (world.Grounded && vy < 0) vy = -16.2f * dt;
            bool jump = input.Jump && !jumpHeld && world.Grounded && !world.CeilingBlocked;
            jumpHeld = input.Jump;
            if (jump) vy = 6;
            if (world.CeilingBlocked && vy > 0) vy = 0;
            Float3 next = new Float3(vx, vy, vz);
            Float3 displacement = new Float3(vx * dt, vy * dt, vz * dt);
            if (!next.IsFinite || !displacement.IsFinite)
            { CancelMotion(); return new MotionStep(); }
            velocity = next;
            return new MotionStep { Velocity = velocity, Displacement = displacement,
                YawRadians = yaw, OwnsYaw = !world.TurningBlocked, Jumped = jump, InteractionsAllowed = true, OwnsMotion = true };
        }
        private float Turn(float horizontal, float vertical, float dt)
        {
            float axis = Axis(horizontal);
            if (vertical < -.7f && Math.Abs(axis) < .5f) axis = 0; // rifle manual-eject reservation
            snapTimer = Math.Max(0, snapTimer - dt);
            if (snapTimer < .000001f) snapTimer = 0;
            if (Math.Abs(axis) < .3f) { snapReady = true; snapTimer = turnRate = 0; return 0; }
            if (tuning.Turning == TurnStyle.Smooth)
            {
                float amount = tuning.SmoothAnalog ? (Math.Min(1, Math.Abs(axis)) - .3f) / .7f : 1;
                float target = Math.Sign(axis) * amount * tuning.SmoothRadiansPerSecond;
                turnRate = tuning.SmoothEaseIn ? Approach(turnRate, target, tuning.SmoothRadiansPerSecond * dt / .12f) : target;
                return turnRate * dt; // Z reflection reverses Godot yaw sign
            }
            turnRate = 0;
            if (Math.Abs(axis) > .7f && (snapReady || (tuning.Turning == TurnStyle.AutoSnap && snapTimer <= 0)))
            { snapReady = false; snapTimer = .3f; return Math.Sign(axis) * tuning.SnapRadians; }
            return 0;
        }
        private void ValidateTuning()
        {
            if (!PlayerInput.Finite(tuning.WalkMultiplier) || (tuning.MaximumWalkMultiplier != 3f && tuning.MaximumWalkMultiplier != 5f) ||
                !PlayerInput.Finite(tuning.SnapRadians) || tuning.SnapRadians <= 0 || tuning.SnapRadians > (float)Math.PI ||
                !PlayerInput.Finite(tuning.SmoothRadiansPerSecond) || tuning.SmoothRadiansPerSecond < 0 || tuning.SmoothRadiansPerSecond > 100 ||
                !Enum.IsDefined(typeof(TurnStyle), tuning.Turning))
                throw new ArgumentException("Invalid player tuning");
        }
        private void ValidateSprintSettings()
        {
            if(!PlayerInput.Finite(StaminaMaximum)||StaminaMaximum<2||StaminaMaximum>20||
                !PlayerInput.Finite(RecoverySeconds)||RecoverySeconds<1||RecoverySeconds>20)
                throw new ArgumentException("Invalid sprint duration/recovery");
        }
        private static float Axis(float x) { return PlayerInput.Finite(x) ? Math.Max(-1, Math.Min(1, x)) : 0; }
        private static float Approach(float a, float b, float amount) { return a + Math.Sign(b - a) * Math.Min(Math.Abs(b - a), amount); }
    }
}
