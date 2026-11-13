using System;
using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
    public sealed class ScopeRules
    {
        bool gripSession, zoomSession, zoomDown = true;
        public bool Active { get; private set; }
        public float Magnification { get; private set; }
        public ScopeRules() { Magnification = 4; }
        // Call with validated local presentation input. Does not mutate weapon ammo
        // or author a shot ray; rendering must use the current muzzle each shot.
        public void Sample(bool enabled, bool xr, bool primaryTracked, bool supportTracked,
            bool climbing, bool headBlocked, bool eyeToMuzzleClear, bool grip, bool supported,
            bool desktopAim, float stickX, float stickY)
        {
            bool finite = Finite(stickX) && Finite(stickY);
            bool safe = enabled && primaryTracked && !climbing && !headBlocked && eyeToMuzzleClear && finite;
            if (!safe || !supportTracked || !grip) gripSession = false;
            else if (supported) gripSession = true;
            Active = safe && (xr ? supportTracked && grip && gripSession : desktopAim);
            if (!Active) { Magnification = 4; zoomDown = true; zoomSession = false; return; }
            if (!zoomSession) { zoomSession = true; Magnification = 4; zoomDown = stickY > .25f; }
            if (stickY < .25f) zoomDown = false;
            else if (stickY > .7f && Math.Abs(stickX) < .5f && !zoomDown)
            { Magnification = Magnification < 6 ? 8 : 4; zoomDown = true; }
        }
        public static float PhysicalLensFov(float radius, float relief, float magnification)
        {
            if (!Finite(radius) || !Finite(relief) || !Finite(magnification) || radius <= 0 || relief <= 0 || magnification <= 0)
                throw new ArgumentOutOfRangeException("Optical dimensions must be finite and positive");
            return (float)(2 * Math.Atan(Math.Max(radius,.0001f)/(Math.Max(relief,.001f)*Math.Max(magnification,1))) * 180 / Math.PI);
        }
        // Source support aim cone; default influence zero preserves dominant-hand aim.
        public static float SupportAimWeight(Float3 primaryForward, Float3 supportDirection, float weight, float influence)
        {
            if (!primaryForward.IsFinite || !supportDirection.IsFinite || !Finite(weight) || !Finite(influence)) return 0;
            double a = Length(primaryForward), b = Length(supportDirection);
            if (a < .0001 || b <= .15) return 0;
            double dot = (primaryForward.X*supportDirection.X+primaryForward.Y*supportDirection.Y+primaryForward.Z*supportDirection.Z)/(a*b);
            return dot > .80 ? Math.Max(0,Math.Min(1,weight))*Math.Max(0,Math.Min(1,influence)) : 0;
        }
        static double Length(Float3 v) { return Math.Sqrt(v.X*v.X+v.Y*v.Y+v.Z*v.Z); }
        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
