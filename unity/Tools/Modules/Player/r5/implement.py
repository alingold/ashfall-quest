from pathlib import Path
r=Path(r'C:\Users\aling\Documents\ashfall');p=r/'unity/Assets/Ashfall/Modules/Player'
f=p/'Locomotion.cs';s=f.read_text();s=s.replace('private readonly PlayerTuning tuning;','private readonly PlayerTuning tuning;\n        public IPlayerSettingsPort Settings {get;set;}\n        public IPlayerInteractionPort Interaction {get;set;}')
s=s.replace('bool eligible = world.IsXr && world.Grounded && !world.SprintBlocked;','''SprintContacts contacts=Interaction==null?new SprintContacts():Interaction.ReadContacts();
            bool gripBlocked=OwnershipSprint==SprintBinding.LeftGrip&&(contacts.LeftReserved||contacts.LeftGripReserved||contacts.ProviderBlocksSprint);
            bool contact=OwnershipSprint==SprintBinding.LeftGrip&&contacts.LeftSurfaceContact&&input.Sprint;
            bool eligible = world.IsXr && world.Grounded && !world.SprintBlocked && !contacts.Climbing && !contacts.Mantling && !contacts.HasGrab && !gripBlocked;
            if(contacts.Climbing||contacts.Mantling||contact)sprintTier=0;''').replace('if (eligible && input.Sprint && !sprintHeld)','if (eligible && !contact && input.Sprint && !sprintHeld)')
s=s.replace('float speed = PlayerTuning.WalkSpeed','if(Settings!=null)gain=Settings.GroundSpeedGain(gain);\n            if(!PlayerInput.Finite(gain)||gain<0||gain>100)throw new ArgumentException("ground gain");\n            float speed = PlayerTuning.WalkSpeed')
s=s.replace('public Float3 Velocity','public SprintBinding OwnershipSprint {get;set;}\n        public Float3 Velocity');f.write_text(s)
f=p/'PlayerR3Ports.cs';s=f.read_text().replace('public bool Focused, Paused, Enabled, CapturePressed, EscapePressed, GameplayReady;','public bool Focused, Paused, Enabled, CapturePressed, EscapePressed, GameplayReady;\n        public bool InitializationLook, InputRequested;');f.write_text(s)
f=p/'PlayerR3Input.cs';s=f.read_text().replace('public void Release()','public void InitializeCapture() { captured=true; }\n        public void Release()').replace('else if (s.GameplayReady)','else if (s.GameplayReady || (s.InitializationLook && s.InputRequested))');f.write_text(s)
f=p/'Engine/UnityPlayerDriver.cs';s=f.read_text();s=s.replace('public bool UseXr, ExternalMotionOwner, SprintBlocked, TurningBlocked;','''[UnityEngine.Serialization.FormerlySerializedAs("UseXr")]
        [SerializeField] private bool useXr;
        public bool UseXr { get {return useXr;} set {if(useXr==value)return;useXr=value;DisarmPublished(true);} }
        public bool ExternalMotionOwner, SprintBlocked, TurningBlocked;
        public bool InitializationLook, InputRequested;
        public IPlayerSettingsPort Settings {get;set;}
        public IPlayerInteractionPort Interaction {get;set;}
        public IXrPlayerLifecycle XrLifecycle {get;set;}
        public IExclusivePlayerMotor Motor {get;set;}
        private readonly MovementHeading movementHeading=new MovementHeading();
        private PlayerFrameOwner frames;
        private string sessionId, actorId;private long authorityEpoch=-1;
        public void BindLifecycle(string session,string actor,long epoch) {sessionId=session;actorId=actor;authorityEpoch=epoch;DisarmPublished(true);}
        private void ObserveLifecycle()
        {
            if(frames==null)frames=new PlayerFrameOwner(input);
            if(frames.Observe(new PlayerLifecycleState {Enabled=isActiveAndEnabled,Focused=Application.isFocused&&!applicationSuspended,
                Paused=Paused||Time.timeScale==0,RigValid=RigValid(),Xr=UseXr,Session=sessionId,Actor=actorId,Epoch=authorityEpoch,
                SessionFocused=XrLifecycle!=null&&XrLifecycle.SessionFocused,Present=XrLifecycle!=null&&XrLifecycle.Present}))
                {desktopLook.Release();SetCursor(false);movementHeading.Reset();}
        }''')
s=s.replace('public InputFrame CurrentFrame { get; private set; }','''public InputFrame CurrentFrame { get {ObserveLifecycle();return frames.Current;} private set {if(frames==null)frames=new PlayerFrameOwner(input);frames.Publish(value);} }''')
s=s.replace('InteractionsAllowed = false; input.Disarm();','InteractionsAllowed = false; input.Disarm();\n            if(frames!=null)frames.Invalidate();movementHeading.Reset();')
s=s.replace('if (!UseXr) SetCursor(false);','SetCursor(false);').replace('            CurrentFrame = new InputFrame { Tick = CurrentFrame.Tick, Sequence = CurrentFrame.Sequence, Paused = true };','')
s=s.replace('float dt = Time.unscaledDeltaTime;','ObserveLifecycle();\n            float dt = Time.unscaledDeltaTime;')
s=s.replace('(UseXr && CollisionPreflight == null)','(CollisionPreflight == null || Motor == null || string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(actorId) || authorityEpoch<0 || (UseXr && (XrLifecycle==null||!XrLifecycle.SessionFocused||!XrLifecycle.Present)))')
s=s.replace('GameplayReady = input.DesktopReady,','GameplayReady = input.DesktopReady, InitializationLook=InitializationLook,InputRequested=InputRequested,')
a=s.index('            Vector3 forward = Vector3.ProjectOnPlane');b=s.index('            MotionStep step;',a)
s=s[:a]+'''            float heading=movementHeading.Read(Settings==null?ForwardReference.Head:Settings.ForwardReference,UseXr,input.LeftReady,
                ToFloat(Head.forward),LeftHand==null?new Float3(0,0,1):ToFloat(LeftHand.forward),
                LeftHand==null?new Float3(0,1,0):ToFloat(LeftHand.up),transform.eulerAngles.y*Mathf.Deg2Rad);
            locomotion.Settings=Settings;locomotion.Interaction=Interaction;locomotion.OwnershipSprint=SprintControl;
'''+s[b:]
s=s.replace('HeadingRadians = Mathf.Atan2(forward.x, forward.z)','HeadingRadians = heading')
s=s.replace('            Float3 correction = proposal.PivotCorrection;','')
s=s.replace('            transform.Rotate(0, yaw * Mathf.Rad2Deg, 0, Space.World);\n            Move(new Float3(translation.X + correction.X, translation.Y, translation.Z + correction.Z));','''            PlayerMotorResult result=Motor.Apply(proposal,dt);
            if(!result.Velocity.IsFinite) {DisarmPublished();return;}
            locomotion.SetVelocity(result.Velocity);ceiling=result.CeilingBlocked;
            if(!result.Complete) {InteractionsAllowed=false;input.Disarm();}''')
s=s.replace('            if (requestedTick != CurrentFrame.Tick) throw new System.ArgumentException("Frame unavailable");\n            return CurrentFrame;','            ObserveLifecycle();return frames.Read(requestedTick);')
s=s.replace('                raw.Focused = raw.Focused && XRSettings.isDeviceActive && XrSessionFocused;','                raw.Focused = raw.Focused && XRSettings.isDeviceActive && XrLifecycle!=null && XrLifecycle.SessionFocused && XrLifecycle.Present;')
s=s.replace('            target.localPosition = position; target.localRotation = rotation;','            // Tracking provider owns the transforms; this adapter only validates device pose.').replace('            CollisionFlags flags = controller.Move(new Vector3(displacement.X, displacement.Y, displacement.Z));\n            ceiling = (flags & CollisionFlags.Above) != 0;','            throw new System.InvalidOperationException("Use exclusive approved Motor.Apply with collision feedback");')
f.write_text(s)
