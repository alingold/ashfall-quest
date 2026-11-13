#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;

namespace Ashfall.Modules.Player
{
    // Assign action assets centrally. XR bindings use the installed OpenXR layout;
    // this component never enables an XR loader, changes packages or owns a camera.
    // Do not combine with another tracked-pose writer on these pose children.
    [RequireComponent(typeof(CharacterController))]
    public sealed class UnityPlayerDriver : MonoBehaviour, IInputSource, IPlayerMotionPort
    {
        [UnityEngine.Serialization.FormerlySerializedAs("UseXr")]
        [SerializeField] private bool useXr;
        public bool UseXr { get {return useXr;} set {if(useXr==value)return;useXr=value;DisarmPublished(true);InvalidatePoseSource();} }
        private object poseSourceIdentity=new object();
        public object PoseSourceIdentity {get{return poseSourceIdentity;}}
        public event System.Action PoseSourceInvalidated;
        private void InvalidatePoseSource()
        {
            poseSourceIdentity=new object();var handlers=PoseSourceInvalidated;
            if(handlers!=null)foreach(System.Action handler in handlers.GetInvocationList())
                try{handler();}catch(System.Exception e){Debug.LogException(e,this);}
        }
        public bool ExternalMotionOwner, SprintBlocked, TurningBlocked;
        public bool InitializationLook, InputRequested;
        private bool pendingInitializationCapture;
        public void BeginDesktopInitializationLook(bool requested)
        {DisarmPublished();InitializationLook=true;InputRequested=requested;pendingInitializationCapture=requested&&!UseXr;}
        // The same input instance used by Sample; trusted Presentation bindings share it.
        public PlayerInput InputOwner {get {return input;} }
        private PlayerSettingsPublication settingsPublication;
        private PlayerTuning runtimeTuning;
        public void BindSettingsAuthority(IAuthorityGate authority)
        {if(runtimeTuning==null)throw new System.InvalidOperationException("Awake must bind the runtime source profile first");settingsPublication=new PlayerSettingsPublication(input,runtimeTuning,authority,sessionId,actorId,authorityEpoch);DisarmPublished();}
        public void BindApprovedGeometry(IUnityApprovedHeadQueries headQueries,int structureMask)
        {
            var preflight=new PlayerGeometryPreflight(new UnityPlayerGeometry(controller,headQueries,structureMask));
            CollisionPreflight=preflight;Motor=new UnityExclusivePlayerMotor(controller,preflight);DisarmPublished();
        }
        public RuleResult PublishAuthorizedSettings(CommandContext context,long generation,long revision,PlayerTuning next,
            IPlayerSettingsPort effective,DominantHand dominant,SprintBinding sprint)
        {
            ObserveLifecycle();
            if(!frames.Valid||generation!=frames.Generation||settingsPublication==null)return RuleResult.Reject("Settings lifecycle unavailable");
            var result=settingsPublication.Publish(context,revision,next,effective,dominant,sprint);
            if(result.Accepted){Settings=effective;DisarmPublished();}
            return result;
        }
        public long SettingsRevision {get {return settingsPublication==null?-1:settingsPublication.Revision;} }
        public long InputGeneration {get {ObserveLifecycle();return frames.Generation;} }
        public IPlayerSettingsPort Settings {get;set;}
        public IPlayerInteractionPort Interaction {get;set;}
        private IXrPlayerLifecycle xrLifecycle;
        public IXrPlayerLifecycle XrLifecycle {get {return xrLifecycle;} set {if(xrLifecycle!=null)xrLifecycle.Invalidated-=OnXrInvalidated;xrLifecycle=value;if(value!=null)value.Invalidated+=OnXrInvalidated;DisarmPublished(true);} }
        private void OnXrInvalidated() {DisarmPublished(true);InvalidatePoseSource();}
        public IExclusivePlayerMotor Motor {get;set;}
        private readonly MovementHeading movementHeading=new MovementHeading();
        private PlayerFrameOwner frames;
        private string sessionId, actorId;private long authorityEpoch=-1;
        public void BindLifecycle(string session,string actor,long epoch) {sessionId=session;actorId=actor;authorityEpoch=epoch;settingsPublication=null;DisarmPublished(true);InvalidatePoseSource();}
        private void ObserveLifecycle()
        {
            if(frames==null)frames=new PlayerFrameOwner(input);
            long beforeGeneration=frames.Generation;
            bool sourceChanged=frames.Observe(new PlayerLifecycleState {Enabled=isActiveAndEnabled,Focused=Application.isFocused&&!applicationSuspended,
                Paused=Paused||Time.timeScale==0,RigValid=RigValid(),Xr=UseXr,Session=sessionId,Actor=actorId,Epoch=authorityEpoch,
                SessionFocused=XrLifecycle!=null&&XrLifecycle.SessionFocused,Present=XrLifecycle!=null&&XrLifecycle.Present});
            if(beforeGeneration!=frames.Generation)
                {if(!frames.Valid)pendingInitializationCapture=false;desktopLook.Release();SetCursor(false);movementHeading.Reset();InteractionsAllowed=false;
                 if(locomotion!=null) {if(sourceChanged||(UseXr&&(XrLifecycle==null||!XrLifecycle.SessionFocused||!XrLifecycle.Present)))locomotion.ResetMotion();else locomotion.CancelMotion();}}
        }
        // Default: central action map owns enable/disable. Opt-in only for an
        // exclusively assigned map: never disable actions enabled by another owner.
        public bool OwnActionLifecycle;
        public bool UsePhysicalXrBindings = true;
        public DominantHand Dominant = DominantHand.Right;
        public SprintBinding SprintControl = SprintBinding.LeftClick;
        public IDesktopViewPort DesktopView { get; set; }
        private readonly DesktopLook desktopLook = new DesktopLook();
        private float desktopYaw;
        private bool paused, xrSessionFocused, applicationSuspended;
        public bool Paused { get { return paused; } set { paused = value; if (value) DisarmPublished(); } }
        public IRootCollisionPreflight CollisionPreflight { get; set; }
        // Explicit ownership assertion from rig builder; runtime scan cannot identify all custom writers.
        public bool ExclusivePoseWriterConfirmed;
        // Central OpenXR session lifecycle must set this from FOCUSED state.
        // Application focus alone is insufficient on a headset. Fail closed.
        public bool XrSessionFocused
        { get { return xrSessionFocused; } set { xrSessionFocused = value; if (!value) DisarmPublished(true); } }
        public Transform TrackingSpace, Head, LeftHand, RightHand;
        public InputActionReference MoveAction, TurnAction, JumpAction, SprintAction,
            GripLeftAction, GripRightAction, TriggerLeftAction, TriggerRightAction,
            ReloadAction, MenuAction, PingAction;
        public PlayerTuning Tuning = new PlayerTuning();
        public InputFrame CurrentFrame { get {ObserveLifecycle();return frames.Current;} private set {if(frames==null)frames=new PlayerFrameOwner(input);frames.Publish(value);} }
        private bool interactionsAllowed;
        public bool InteractionsAllowed {get {ObserveLifecycle();return interactionsAllowed;} private set {interactionsAllowed=value;} }
        public bool LeftInputReady { get { return InteractionsAllowed && (UseXr ? input.LeftReady : input.DesktopReady); } }
        public bool RightInputReady { get { return InteractionsAllowed && (UseXr ? input.RightReady : input.DesktopReady); } }
        private readonly PlayerInput input = new PlayerInput();
        private readonly List<InputAction> enabledHere = new List<InputAction>();
        private Locomotion locomotion;
        private CharacterController controller;
        private bool ceiling;
        private long tick;
        public bool Grounded { get { return controller != null && controller.isGrounded; } }
        public bool CeilingBlocked { get { return ceiling; } }
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            runtimeTuning=Tuning;locomotion = new Locomotion(runtimeTuning);
            input.Ownership.Dominant=Dominant;input.Ownership.Sprint=SprintControl;
            if (!RigValid())
            { Debug.LogError("Player requires distinct pose children, unit upright root hierarchy and exclusive pose ownership", this); enabled = false; }
        }
        private bool RigValid()
        {
            if (controller == null || !controller.enabled || !gameObject.activeInHierarchy ||
                Head == null || TrackingSpace == null || TrackingSpace == transform ||
                !TrackingSpace.IsChildOf(transform) || Head.parent != TrackingSpace ||
                (UseXr && (!ExclusivePoseWriterConfirmed || LeftHand == null || RightHand == null ||
                Head == LeftHand || Head == RightHand || LeftHand == RightHand ||
                LeftHand.parent != TrackingSpace || RightHand.parent != TrackingSpace))) return false;
            for (Transform node = TrackingSpace; node != null; node = node.parent)
                if (!ToFloat(node.localScale).IsFinite || (node.localScale - Vector3.one).sqrMagnitude > .00000001f ||
                    !new Float3(node.position.x, node.position.y, node.position.z).IsFinite) return false;
            if (!ToFloat(Head.localScale).IsFinite || (Head.localScale - Vector3.one).sqrMagnitude > .00000001f ||
                (UseXr && (!ToFloat(LeftHand.localScale).IsFinite || !ToFloat(RightHand.localScale).IsFinite ||
                (LeftHand.localScale - Vector3.one).sqrMagnitude > .00000001f || (RightHand.localScale - Vector3.one).sqrMagnitude > .00000001f))) return false;
            if (!PlayerInput.Finite(controller.radius) || controller.radius <= 0 ||
                !PlayerInput.Finite(controller.height) || controller.height < 2 * controller.radius ||
                !PlayerInput.Finite(controller.skinWidth) || controller.skinWidth < 0 ||
                controller.skinWidth >= controller.radius || !ToFloat(controller.center).IsFinite) return false;
            return PlayerRigSafety.Valid(new RigMeasurements {
                Radius = controller.radius, Height = controller.height, SkinWidth = controller.skinWidth,
                Center = ToFloat(controller.center), RootUp = ToFloat(transform.up), TrackingUp = ToFloat(TrackingSpace.up),
                CurrentHeadPosition = ToFloat(Head.position), LeftHandPosition = UseXr ? ToFloat(LeftHand.position) : new Float3(),
                RightHandPosition = UseXr ? ToFloat(RightHand.position) : new Float3(), HierarchyValid = true,
                UnitScales = true, IsXr = UseXr, ExclusivePoseWriter = ExclusivePoseWriterConfirmed });
        }
        private void DisarmPublished(bool sourceReset = false)
        {
            pendingInitializationCapture=false;
            InteractionsAllowed = false; input.Disarm();
            if(frames!=null)frames.Invalidate();movementHeading.Reset();
            if (locomotion != null) { if (sourceReset) locomotion.ResetMotion(); else locomotion.CancelMotion(); }
            desktopLook.Release(); desktopYaw = 0;
            SetCursor(false);

        }
        private void OnApplicationFocus(bool focused) { if (!focused) { xrSessionFocused = false; DisarmPublished(UseXr); } }
        private void OnApplicationPause(bool suspended)
        { applicationSuspended = suspended; if (suspended) { xrSessionFocused = false; DisarmPublished(UseXr); InvalidatePoseSource(); } }
        private void OnEnable()
        {
            InvalidatePoseSource();
            if (!OwnActionLifecycle) return;
            InputActionReference[] references = { MoveAction, TurnAction, JumpAction, SprintAction,
                GripLeftAction, GripRightAction, TriggerLeftAction, TriggerRightAction, ReloadAction, MenuAction, PingAction };
            foreach (InputActionReference reference in references)
                if (reference != null && reference.action != null && !reference.action.enabled)
                { reference.action.Enable(); enabledHere.Add(reference.action); }
        }
        private void OnDisable()
        {
            foreach (InputAction action in enabledHere) action.Disable();
            enabledHere.Clear(); xrSessionFocused = false; DisarmPublished(UseXr);InvalidatePoseSource();
        }
        private void Update()
        {
            ObserveLifecycle();
            if(!frames.Valid) {DisarmPublished();return;}
            float dt = Time.unscaledDeltaTime;
            if (!PlayerInput.Finite(dt) || dt <= 0 || !RigValid() || applicationSuspended ||
                (CollisionPreflight == null || Motor == null || string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(actorId) || authorityEpoch<0 || (UseXr && (XrLifecycle==null||!XrLifecycle.SessionFocused||!XrLifecycle.Present)))) { DisarmPublished(); return; }
            // Freeze on stalls rather than simulating a long unchecked motion sweep.
            bool stalled = dt > .1f;
            dt = Mathf.Min(dt, .1f);
            // ObserveLifecycle may have released capture for a new generation. Apply only
            // here, after every lifecycle/port check, never from a getter or before Observe.
            if(pendingInitializationCapture&&!stalled&&!UseXr&&InitializationLook&&InputRequested&&DesktopView!=null)
            {pendingInitializationCapture=false;desktopLook.InitializeCapture();}
            try { CurrentFrame = Sample(++tick, dt, stalled); }
            catch (System.ArgumentException) { DisarmPublished(); return; }
            desktopYaw = 0;
            if (!UseXr)
            {
                Mouse mouse = Mouse.current; Keyboard keyboard = Keyboard.current;
                Vector2 delta = mouse == null ? Vector2.zero : mouse.delta.ReadValue();
                DesktopLookStep look = desktopLook.Read(new DesktopLookSample {
                    Focused = Application.isFocused && !applicationSuspended, Paused = Paused || stalled || Time.timeScale == 0,
                    Enabled = DesktopView != null, CapturePressed = mouse != null && mouse.leftButton.wasPressedThisFrame,
                    EscapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame,
                    GameplayReady = input.DesktopReady, InitializationLook=InitializationLook,InputRequested=InputRequested, DeltaX = delta.x, DeltaY = delta.y });
                SetCursor(look.Captured);
                if (look.CaptureChanged || !look.Captured)
                { input.Disarm(); CurrentFrame = new InputFrame { Tick = CurrentFrame.Tick, Sequence = CurrentFrame.Sequence,
                    Tracked = CurrentFrame.Tracked, Paused = true }; }
                CurrentFrame = ClearDesktopTurn(CurrentFrame);
                desktopYaw = look.YawRadians;
                if (DesktopView != null) DesktopView.SetPitchRadians(look.PitchRadians);
            }
            float heading=movementHeading.Read(Settings==null?ForwardReference.Head:Settings.ForwardReference,UseXr,input.LeftReady,
                ToFloat(Head.forward),LeftHand==null?new Float3(0,0,1):ToFloat(LeftHand.forward),
                LeftHand==null?new Float3(0,1,0):ToFloat(LeftHand.up),transform.eulerAngles.y*Mathf.Deg2Rad);
            locomotion.Settings=Settings;locomotion.Interaction=Interaction;locomotion.OwnershipSprint=input.Ownership.Sprint;
            MotionStep step;
            try { step = locomotion.Step(CurrentFrame, new MotionEnvironment {
                Grounded = Grounded, CeilingBlocked = ceiling, IsXr = UseXr,
                ExternalMotionOwner = ExternalMotionOwner, SprintBlocked = SprintBlocked, TurningBlocked = TurningBlocked,
                HeadingRadians = heading, DesktopYawRadians = desktopYaw }, dt); }
            catch (System.ArgumentException) { DisarmPublished(); return; }
            if(!UseXr&&InitializationLook)
                step=new MotionStep {OwnsYaw=InputRequested&&!TurningBlocked,YawRadians=desktopYaw};
            InteractionsAllowed = step.InteractionsAllowed;
            if (!step.OwnsMotion && !step.OwnsYaw) return;
            // Plan BEFORE any root mutation. A denied pivot also denies translation
            // because movement was calculated using the proposed post-turn heading.
            float yaw = step.OwnsYaw ? step.YawRadians : 0;
            Float3 translation = step.OwnsMotion ? step.Displacement : new Float3();
            RootMotionProposal proposal;
            try { proposal = RootMotionSafety.Plan(ToFloat(transform.position), ToFloat(Head.position), translation, yaw, step.OwnsMotion); }
            catch (System.ArgumentException) { DisarmPublished(); return; }

            try {
                if (!RootMotionSafety.Approve(proposal, UseXr, CollisionPreflight).Accepted)
                { DisarmPublished(); return; }
            } catch { DisarmPublished(); throw; }
            PlayerMotorResult result=Motor.Apply(proposal,dt);
            if(!result.Velocity.IsFinite) {DisarmPublished();return;}
            locomotion.SetVelocity(result.Velocity);ceiling=result.CeilingBlocked;
            if(!result.Complete) {InteractionsAllowed=false;input.Disarm();frames.Invalidate();}
        }
        private static Float3 ToFloat(Vector3 value) { return new Float3(value.x, value.y, value.z); }
        public InputFrame ReadFrame(long requestedTick)
        {
            ObserveLifecycle();return frames.Read(requestedTick);
        }
        public void Move(Float3 displacement)
        {
            if (!displacement.IsFinite) throw new System.ArgumentException("displacement");
            throw new System.InvalidOperationException("Use exclusive approved Motor.Apply with collision feedback");
        }
        public void ReceiveVelocity(Float3 velocity) { locomotion.SetVelocity(velocity); }
        private InputFrame Sample(long frameTick, float dt, bool stalled)
        {
            Vector2 move = Vector(MoveAction), turn = Vector(TurnAction);
            DeviceSample raw = new DeviceSample { IsXr = UseXr, Focused = Application.isFocused && !applicationSuspended,
                Paused = Paused || stalled || Time.timeScale == 0 || (!UseXr&&InitializationLook), MoveX = move.x, MoveY = move.y, TurnX = turn.x, TurnY = turn.y,
                Jump = Button(JumpAction), Sprint = Button(SprintAction), Reload = Button(ReloadAction), Ping = Button(PingAction) };
            if (UseXr)
            {
                InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                bool presence;
                raw.HeadTracked = Pose(head, Head);
                if (head.TryGetFeatureValue(CommonUsages.userPresence, out presence) && !presence) raw.HeadTracked = false;
                raw.Focused = raw.Focused && XRSettings.isDeviceActive && XrLifecycle!=null && XrLifecycle.SessionFocused && XrLifecycle.Present;
                raw.Left = Hand(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), LeftHand);
                raw.Right = Hand(InputDevices.GetDeviceAtXRNode(XRNode.RightHand), RightHand);

            }
            else if (Mouse.current != null)
            {
                // Capture clicks remain physical held input during neutral rearming.
                raw.Right.Trigger = Mouse.current.leftButton.isPressed ? 1 : 0;
            }
            raw.HeadPosition = Local(Head);
            raw.Left.Position = Local(LeftHand); raw.Right.Position = Local(RightHand);
            raw.Left.Grip = Mathf.Max(raw.Left.Grip, Scalar(GripLeftAction));
            raw.Right.Grip = Mathf.Max(raw.Right.Grip, Scalar(GripRightAction));
            raw.Left.Trigger = Mathf.Max(raw.Left.Trigger, Scalar(TriggerLeftAction));
            raw.Right.Trigger = Mathf.Max(raw.Right.Trigger, Scalar(TriggerRightAction));
            raw.Left.Menu |= Button(MenuAction);
            if (UseXr && UsePhysicalXrBindings) raw = HandActionAdapter.Bind(raw, input.Ownership);
            return input.Read(frameTick, dt, raw);
        }
        private static HandSample Hand(InputDevice device, Transform target)
        {
            HandSample sample = new HandSample { Tracked = Pose(device, target) };
            Vector2 stick;
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick);
            sample.StickX = stick.x; sample.StickY = stick.y;
            device.TryGetFeatureValue(CommonUsages.grip, out sample.Grip);
            device.TryGetFeatureValue(CommonUsages.trigger, out sample.Trigger);
            device.TryGetFeatureValue(CommonUsages.primaryButton, out sample.Primary);
            device.TryGetFeatureValue(CommonUsages.secondaryButton, out sample.Secondary);
            device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out sample.Click);
            device.TryGetFeatureValue(CommonUsages.menuButton, out sample.Menu);
            return sample;
        }
        private static bool Pose(InputDevice device, Transform target)
        {
            bool tracked; Vector3 position; Quaternion rotation; InputTrackingState state;
            if (target == null || !device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out tracked) || !tracked ||
                !device.TryGetFeatureValue(CommonUsages.trackingState, out state) ||
                (state & (InputTrackingState.Position | InputTrackingState.Rotation)) != (InputTrackingState.Position | InputTrackingState.Rotation) ||
                !device.TryGetFeatureValue(CommonUsages.devicePosition, out position) ||
                !device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation) ||
                !new Float3(position.x, position.y, position.z).IsFinite || !PlayerInput.Finite(rotation.x) ||
                !PlayerInput.Finite(rotation.y) || !PlayerInput.Finite(rotation.z) || !PlayerInput.Finite(rotation.w)) return false;
            // Tracking provider owns the transforms; this adapter only validates device pose.
            return true;
        }
        private Float3 Local(Transform target)
        { if (target == null) return new Float3(); Vector3 p = TrackingSpace.InverseTransformPoint(target.position); return new Float3(p.x, p.y, p.z); }
        private static InputFrame ClearDesktopTurn(InputFrame frame)
        { frame.TurnX = frame.TurnY = 0; return frame; }
        private static void SetCursor(bool captured)
        { Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !captured; }
        private static Vector2 Vector(InputActionReference r) { return r == null || r.action == null ? Vector2.zero : r.action.ReadValue<Vector2>(); }
        private static float Scalar(InputActionReference r) { return r == null || r.action == null ? 0 : r.action.ReadValue<float>(); }
        private static bool Button(InputActionReference r) { return Scalar(r) > .5f; }
    }
}
#endif
