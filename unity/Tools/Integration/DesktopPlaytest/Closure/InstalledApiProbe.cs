#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.InputSystem;
namespace Ashfall.Tools.DesktopClosure
{
    // Compile-only installed API witness. No component, attribute or activation.
    public static class InstalledApiProbe
    {
        public static Vector2 MouseDelta() { return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue(); }
        public static bool Forward() { return Keyboard.current != null && Keyboard.current.wKey.isPressed; }
        public static CollisionFlags Move(CharacterController controller, Vector3 displacement) { return controller.Move(displacement); }
    }
}
#endif
