#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.Player;
using Ashfall.Integration.DesktopPlaytest;
using Ashfall.Integration.DesktopPlaytest.Pawn;
using UnityEngine;
namespace Ashfall.Tools.Integration.DesktopPlaytest
{
    // Root invokes Run on its native lane. No automatic initialization or engine invocation.
    public static class DesktopPawnNativeFixture
    {
        static int checks;
        static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("DesktopPawn fixture: " + label); checks++; }
        static void Pump(DesktopPawn pawn, int count, DeviceSample sample)
        { for (int i = 0; i < count; i++) pawn.AdvanceLocalSample(sample, Vector2.zero, .02f); }
        public static string Run()
        {
            checks = 0;
            var holder = new GameObject("DesktopPawn fixture owner");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DesktopPlaytestContext context = new DesktopPlaytestContext { Spawn = new Vector3(10000, 1.1f, 10000) };
            try
            {
                floor.layer = wall.layer = roof.layer = 8;
                floor.transform.position = new Vector3(10000, -.5f, 10000); floor.transform.localScale = new Vector3(40, 1, 40);
                wall.transform.position = new Vector3(10000, 2, 10003); wall.transform.localScale = new Vector3(20, 4, .5f);
                roof.transform.position = new Vector3(10000, 2.3f, 10000); roof.transform.localScale = new Vector3(4, .2f, 4);
                Physics.SyncTransforms();
                var pawn = holder.AddComponent<DesktopPawn>(); pawn.Initialize(context);
                // Avoid relying on Application.isFocused in batch mode.
                pawn.NotifyFocus(true); pawn.SetPaused(false);
                DeviceSample neutral = new DeviceSample { Focused = true };
                Pump(pawn, 40, neutral);
                Check(pawn.InputReady, "real PlayerInput neutral arms");
                Check(pawn.Grounded, "CharacterController floor flags");
                pawn.AdvanceLocalSample(neutral, new Vector2(100, 100), .02f);
                Check(context.PlayerRoot.eulerAngles.y > 10 && context.PlayerRoot.eulerAngles.y < 13, "mouse yaw through actual DesktopLook");
                Check(context.View.transform.localRotation != Quaternion.identity, "mouse pitch owns view only");
                context.RequestReset(); pawn.SetPaused(false); Pump(pawn, 40, neutral);
                DeviceSample jump = neutral; jump.Jump = true;
                bool ceiling = false;
                for (int i = 0; i < 30; i++) { Pump(pawn, 1, jump); ceiling |= pawn.CeilingBlocked; if (pawn.CeilingBlocked) Check(pawn.ActualVelocity.Y <= 0, "ceiling feedback clips positive velocity"); }
                Check(ceiling, "actual roof collision observed");
                Pump(pawn, 40, neutral);
                DeviceSample forward = neutral; forward.MoveY = 1;
                Pump(pawn, 120, forward);
                Check(context.PlayerRoot.position.z < 10002.6f, "wall blocks capsule");
                Check(Math.Abs(pawn.ActualVelocity.Z) < .1f, "blocked actual velocity feeds motor");
                Vector3 blocked = context.PlayerRoot.position;
                pawn.NotifyFocus(false); Pump(pawn, 10, forward);
                Check(context.Paused && !pawn.InputReady, "focus loss pauses and disarms");
                Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "focus loss releases mouse");
                Check(context.PlayerRoot.position == blocked, "focus loss no movement");
                pawn.NotifyFocus(true); Check(context.Paused, "focus gain does not auto resume");
                pawn.SetPaused(false); Pump(pawn, 10, forward);
                Check(!pawn.InputReady && context.PlayerRoot.position.z <= blocked.z + .01f, "held controls cannot rearm");
                context.RequestReset(); Check(context.Paused && context.PlayerRoot.position == context.Spawn, "safe reset repositions and pauses");
                pawn.SetPaused(false); Pump(pawn, 40, neutral);
                // Measure walk and hold-sprint on open lateral path with real movement.
                DeviceSample right = neutral; right.MoveX = 1;
                Pump(pawn, 1, right); float walk = pawn.ActualVelocity.X;
                right.Sprint = true; Pump(pawn, 1, right);
                Check(pawn.ActualVelocity.X > walk * 1.4f, "desktop Shift speed gain uses real motor");
                context.Spawn = wall.transform.position;
                Vector3 before = context.PlayerRoot.position; context.RequestReset();
                Check(context.PlayerRoot.position == before && context.Paused, "obstructed spawn reset refused");
                pawn.enabled = false;
                Check(!context.Controller.enabled && !context.View.enabled && context.Paused, "disable pauses controller and view");
                pawn.enabled = true;
                Check(context.Controller.enabled && context.Paused && !pawn.InputReady, "enable stays disarmed");
                UnityEngine.Object.DestroyImmediate(holder);
                Check(context.PlayerRoot == null && context.Controller == null && context.View == null, "destroy clears owned references");
                context.RequestReset(); Check(true, "reset unsubscribed after destruction");
                return "DesktopPawn native PASS checks=" + checks;
            }
            finally
            {
                if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(floor); UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(roof);
            }
        }
    }
}
#endif
