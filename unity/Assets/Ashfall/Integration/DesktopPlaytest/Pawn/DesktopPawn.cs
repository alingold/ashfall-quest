#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = Ashfall.Modules.Player.PlayerInput;
namespace Ashfall.Integration.DesktopPlaytest.Pawn
{
    // Synthetic local desktop actor. No session, authority, tracking or recovery claims.
    [DefaultExecutionOrder(-100)]
    public sealed class DesktopPawn : MonoBehaviour, IDesktopPlaytestModule
    {
        DesktopPlaytestContext context;
        GameObject body;
        Transform ownedRoot;
        CharacterController controller;
        Camera view;
        PlayerInput input;
        Locomotion motor;
        DesktopLook look;
        readonly DesktopSpeed walk = new DesktopSpeed(1), sprint = new DesktopSpeed(9.5f / 6.5f);
        long tick;
        bool focused = true, suspended, ceiling, destroyed, placed;
        public bool Grounded { get; private set; }
        public bool CeilingBlocked { get { return ceiling; } }
        public Float3 ActualVelocity { get; private set; }
        public bool InputReady { get { return input != null && input.DesktopReady; } }
        sealed class DesktopSpeed : IPlayerSettingsPort
        {
            readonly float gain;
            public DesktopSpeed(float gain) { this.gain = gain; }
            public ForwardReference ForwardReference { get { return ForwardReference.Head; } }
            public float GroundSpeedGain(float sourceGain) { return sourceGain * gain; }
        }
        public void Initialize(DesktopPlaytestContext value)
        {
            if (destroyed || context != null) throw new InvalidOperationException("DesktopPawn already initialized/disposed");
            if (value == null) throw new ArgumentNullException("value");
            if (!isActiveAndEnabled || value.PlayerRoot != null || value.Controller != null || value.View != null)
                throw new InvalidOperationException("Active module and exclusive empty pawn context required");
            context = value;
            input = new PlayerInput(); look = new DesktopLook();
            motor = new Locomotion(PlayerTuning.SkinRuntime()); motor.Settings = walk;
            body = new GameObject("Local desktop sandbox pawn"); body.layer = 10;
            ownedRoot = body.transform;
            controller = body.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .3f; controller.center = Vector3.zero;
            controller.skinWidth = .03f; controller.stepOffset = .3f; controller.minMoveDistance = 0;
            var cameraObject = new GameObject("Desktop view");
            cameraObject.transform.SetParent(body.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, .72f, 0);
            view = cameraObject.AddComponent<Camera>(); view.nearClipPlane = .05f; view.farClipPlane = 500;
            context.PlayerRoot = body.transform; context.Controller = controller; context.View = view;
            context.ResetRequested += ResetLocal;
            focused = Application.isFocused;
            ResetLocal();
            if (placed && focused && !suspended) SetPaused(false);
        }
        void Update()
        {
            if (context == null) return;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (!focused || suspended || keyboard == null || mouse == null) { SetPaused(true); return; }
            if (keyboard.escapeKey.wasPressedThisFrame) { SetPaused(!context.Paused); return; }
            if (context.Paused) return;
            if (Cursor.lockState != CursorLockMode.Locked) { SetPaused(true); return; }
            DeviceSample sample = new DeviceSample { Focused = true,
                MoveX = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                MoveY = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0),
                Jump = keyboard.spaceKey.isPressed,
                Sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed };
            // Held combat controls must also become neutral after a focus/pause transition.
            sample.Right.Trigger = mouse.leftButton.isPressed ? 1 : 0;
            sample.Reload = keyboard.rKey.isPressed;
            AdvanceLocalSample(sample, mouse.delta.ReadValue(), Mathf.Min(Time.unscaledDeltaTime, .1f));
        }
        // Callable native fixture seam; identical physical path to Update, local sandbox only.
        public void AdvanceLocalSample(DeviceSample sample, Vector2 mouseDelta, float seconds)
        {
            if (context == null || !isActiveAndEnabled || !focused || suspended || context.Paused) return;
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            seconds = Mathf.Min(seconds, .1f);
            if (!sample.Focused || sample.Paused || sample.IsXr) { SetPaused(true); return; }
            var frame = input.Read(++tick, seconds, sample);
            var rotation = look.Read(new DesktopLookSample { Focused = true, Enabled = true,
                GameplayReady = input.DesktopReady, DeltaX = mouseDelta.x, DeltaY = mouseDelta.y });
            if (!rotation.Captured) { SetPaused(true); return; }
            motor.Settings = frame.Sprint ? sprint : walk;
            MotionStep step = motor.Step(frame, new MotionEnvironment { Grounded = Grounded,
                CeilingBlocked = ceiling, HeadingRadians = body.transform.eulerAngles.y * Mathf.Deg2Rad,
                DesktopYawRadians = rotation.YawRadians }, seconds);
            body.transform.Rotate(0, step.YawRadians * Mathf.Rad2Deg, 0, Space.World);
            view.transform.localRotation = Quaternion.Euler(rotation.PitchRadians * Mathf.Rad2Deg, 0, 0);
            Vector3 before = body.transform.position;
            CollisionFlags flags = controller.Move(ToVector(step.Displacement));
            Grounded = (flags & CollisionFlags.Below) != 0 || controller.isGrounded;
            ceiling = (flags & CollisionFlags.Above) != 0;
            var result = PlayerMotorFeedback.Resolve(new RootMotionProposal { Translation = step.Displacement,
                OwnsTranslation = true }, ToFloat(body.transform.position - before), seconds, Grounded, ceiling);
            ActualVelocity = result.Velocity; motor.SetVelocity(result.Velocity);
            context.MovementStatus = String.Format("{0} | {1} | velocity {2:F1}, {3:F1}, {4:F1} | ceiling {5}",
                input.DesktopReady ? "WASD / mouse / Space / hold Shift / Esc" : "Release controls to arm",
                Grounded ? "grounded" : "airborne", ActualVelocity.X, ActualVelocity.Y, ActualVelocity.Z, ceiling);
        }
        public void SetPaused(bool paused)
        {
            if (context == null) return;
            if (!focused || suspended || !isActiveAndEnabled || !placed) paused = true;
            if (context.Paused != paused || paused) { input.Disarm(); motor.CancelMotion(); ActualVelocity = new Float3(); }
            context.Paused = paused;
            if (paused) { look.Release(); ReleaseCursor(); context.MovementStatus = "Paused - Esc to resume; release controls to arm"; }
            else { look.InitializeCapture(); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
        public void NotifyFocus(bool hasFocus)
        { focused = hasFocus; if (!hasFocus) SetPaused(true); }
        void OnApplicationFocus(bool hasFocus) { NotifyFocus(hasFocus); }
        void OnApplicationPause(bool paused) { suspended = paused; if (paused) SetPaused(true); }
        void OnEnable()
        { if (context != null) { if (controller != null) controller.enabled = true; if (view != null) view.enabled = true; SetPaused(true); } }
        void OnDisable()
        { if (context != null) { SetPaused(true); if (controller != null) controller.enabled = false; if (view != null) view.enabled = false; } }
        void ResetLocal()
        {
            SetPaused(true);
            placed = false;
            Vector3 spawn = context.Spawn;
            if (!ToFloat(spawn).IsFinite) { context.MovementStatus = "Reset refused: nonfinite spawn"; return; }
            // Spawn is Arena-owned. Never silently substitute or teleport into structure.
            Physics.SyncTransforms();
            foreach (Collider hit in Physics.OverlapCapsule(spawn + Vector3.up * .6f,
                spawn - Vector3.up * .6f, .29f, ~0, QueryTriggerInteraction.Ignore))
                if (hit != controller) { context.MovementStatus = "Reset refused: spawn capsule obstructed"; return; }
            controller.enabled = false; body.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            controller.enabled = isActiveAndEnabled;
            view.transform.localRotation = Quaternion.identity;
            input.Disarm(); motor.ResetMotion(); look = new DesktopLook();
            ActualVelocity = new Float3(); Grounded = ceiling = false;
            placed = true;
            // Reset owns local state only; no reconnect, cold restore or shared context counters.
            context.MovementStatus = "Local reset complete - Esc to resume";
        }
        void OnDestroy()
        {
            destroyed = true;
            if (context == null) return;
            context.ResetRequested -= ResetLocal;
            SetPaused(true);
            if (context.PlayerRoot == ownedRoot) context.PlayerRoot = null;
            if (context.Controller == controller) context.Controller = null;
            if (context.View == view) context.View = null;
            if (body != null) Destroy(body); context = null;
        }
        static void ReleaseCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        static Vector3 ToVector(Float3 v) { return new Vector3(v.X, v.Y, v.Z); }
        static Float3 ToFloat(Vector3 v) { return new Float3(v.x, v.y, v.z); }
    }
}
#endif
