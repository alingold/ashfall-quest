from pathlib import Path
p=Path('C:/Users/aling/Documents/ashfall/unity/Assets/Ashfall/Modules/Player')
f=p/'Locomotion.cs';s=f.read_text().replace('if (world.TurningBlocked) { turnNeedsNeutral', 'if (world.TurningBlocked) { yaw = 0; turnNeedsNeutral');f.write_text(s)
f=p/'RootMotionSafety.cs';s=f.read_text().replace('''        // Called before rotating''','''        public static RootMotionProposal Plan(Float3 root, Float3 head, Float3 translation, float yaw, bool ownsTranslation)
        {
            if (!root.IsFinite || !head.IsFinite || !translation.IsFinite || !PlayerInput.Finite(yaw) || System.Math.Abs(yaw) > System.Math.PI)
                throw new System.ArgumentException("root proposal");
            double x = (double)head.X - root.X, z = (double)head.Z - root.Z;
            double c = System.Math.Cos(yaw), s = System.Math.Sin(yaw);
            Float3 correction = new Float3((float)(x - (c * x + s * z)), 0, (float)(z - (-s * x + c * z)));
            if (!correction.IsFinite) throw new System.ArgumentException("pivot correction");
            return new RootMotionProposal { RootPosition = root, HeadPosition = head, Translation = translation,
                PivotCorrection = correction, YawRadians = yaw, OwnsTranslation = ownsTranslation };
        }
        // Called before rotating''');f.write_text(s)
f=p/'Engine/UnityPlayerDriver.cs';s=f.read_text()
s=s.replace('public bool UseXr, ExternalMotionOwner, SprintBlocked, TurningBlocked;', '''public bool UseXr, ExternalMotionOwner, SprintBlocked, TurningBlocked;
        // Default: central action map owns enable/disable. Opt-in only for an
        // exclusively assigned map: never disable actions enabled by another owner.
        public bool OwnActionLifecycle;
        public bool UsePhysicalXrBindings = true;
        public DominantHand Dominant = DominantHand.Right;
        public SprintBinding SprintControl = SprintBinding.LeftClick;
        public IDesktopViewPort DesktopView { get; set; }
        private readonly DesktopLook desktopLook = new DesktopLook();
        private float desktopYaw;''')
s=s.replace('return Vector3.Dot(transform.up, Vector3.up) > .99999f &&\n                Vector3.Dot(TrackingSpace.up, Vector3.up) > .99999f;', '''return PlayerRigSafety.Valid(new RigMeasurements {
                Radius = controller.radius, Height = controller.height, SkinWidth = controller.skinWidth,
                Center = ToFloat(controller.center), RootUp = ToFloat(transform.up), TrackingUp = ToFloat(TrackingSpace.up),
                CurrentHeadPosition = ToFloat(Head.position), LeftHandPosition = UseXr ? ToFloat(LeftHand.position) : new Float3(),
                RightHandPosition = UseXr ? ToFloat(RightHand.position) : new Float3(), HierarchyValid = true,
                UnitScales = true, IsXr = UseXr, ExclusivePoseWriter = ExclusivePoseWriterConfirmed });''')
s=s.replace('private void DisarmPublished()', 'private void DisarmPublished(bool sourceReset = false)')
s=s.replace('if (locomotion != null) locomotion.ResetMotion();','if (locomotion != null) { if (sourceReset) locomotion.ResetMotion(); else locomotion.CancelMotion(); }\n            desktopLook.Release(); desktopYaw = 0;\n            if (!UseXr) SetCursor(false);')
s=s.replace('xrSessionFocused = value; if (!value) DisarmPublished();','xrSessionFocused = value; if (!value) DisarmPublished(true);')
s=s.replace('''            InputActionReference[] references''','''            if (!OwnActionLifecycle) return;
            InputActionReference[] references''')
s=s.replace('''            CurrentFrame = Sample(++tick, dt, stalled);''','''            CurrentFrame = Sample(++tick, dt, stalled);
            desktopYaw = 0;
            if (!UseXr)
            {
                Mouse mouse = Mouse.current; Keyboard keyboard = Keyboard.current;
                Vector2 delta = mouse == null ? Vector2.zero : mouse.delta.ReadValue();
                DesktopLookStep look = desktopLook.Read(new DesktopLookSample {
                    Focused = Application.isFocused && !applicationSuspended, Paused = Paused || stalled || Time.timeScale == 0,
                    Enabled = DesktopView != null, CapturePressed = mouse != null && mouse.leftButton.wasPressedThisFrame,
                    EscapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame,
                    GameplayReady = input.DesktopReady, DeltaX = delta.x, DeltaY = delta.y });
                SetCursor(look.Captured);
                if (look.CaptureChanged || !look.Captured)
                { input.Disarm(); CurrentFrame = new InputFrame { Tick = CurrentFrame.Tick, Sequence = CurrentFrame.Sequence,
                    Tracked = CurrentFrame.Tracked, Paused = true }; }
                desktopYaw = look.YawRadians;
                if (DesktopView != null) DesktopView.SetPitchRadians(look.PitchRadians);
            }''')
s=s.replace('HeadingRadians = Mathf.Atan2(forward.x, forward.z)', 'HeadingRadians = Mathf.Atan2(forward.x, forward.z), DesktopYawRadians = desktopYaw')
a=s.index('            Vector3 offset = Head.position - transform.position;'); b=s.index('            try {',a)
s=s[:a]+'''            Float3 translation = step.OwnsMotion ? step.Displacement : new Float3();
            RootMotionProposal proposal = RootMotionSafety.Plan(ToFloat(transform.position), ToFloat(Head.position), translation, yaw, step.OwnsMotion);
            Float3 correction = proposal.PivotCorrection;
'''+s[b:]
s=s.replace('correction.x','correction.X').replace('correction.z','correction.Z')
s=s.replace('''                // Action asset decides handedness/bindings; gate against both the
                // physical controls and bound actions during neutral rearming.
                raw.Left.Primary |= raw.Jump; raw.Left.Click |= raw.Sprint;
                raw.Right.Secondary |= raw.Reload || raw.Ping;''', '')
s=s.replace('''            return input.Read(frameTick, dt, raw);''','''            input.Ownership.Dominant = Dominant; input.Ownership.Sprint = SprintControl;
            if (UseXr && UsePhysicalXrBindings) raw = HandActionAdapter.Bind(raw, input.Ownership);
            return input.Read(frameTick, dt, raw);''')
s=s.replace('''        private static Vector2 Vector''','''        private static void SetCursor(bool captured)
        { Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !captured; }
        private static Vector2 Vector''')
f.write_text(s)
