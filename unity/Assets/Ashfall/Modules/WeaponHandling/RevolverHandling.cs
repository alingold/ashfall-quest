using System;
using Ashfall.Core;

namespace Ashfall.Modules.WeaponHandling
{
    public sealed partial class WeaponHandling
    {
        // Catalog one_star_050 index 7, applied AFTER revolver.setup by weapon_rack.
        // This is the frozen default profile; user timing overrides require a future port.
        public const string MagnumSourceId = "magnum";
        public static int MagazineCapacity(WeaponMechanism value)
        {
            switch (value)
            {
                case WeaponMechanism.ServiceRifle: return 24;
                case WeaponMechanism.PrecisionRifle: return 3;
                case WeaponMechanism.MagnumRevolver: return 7;
                default: return 0;
            }
        }
        RuleResult OpenCylinder()
        {
            if (phase != ReloadPhase.Ready) return RuleResult.Reject("cylinder already open");
            // Live rounds stay in their cylinder: opening is never a refund/refill.
            chambered = false; contact = false; boltTravel = 0;
            SetStage(ReloadPhase.CylinderOpen); releaseTrigger = true; releaseGrip = true;
            return RuleResult.Accept();
        }
        RuleResult LoadCylinder()
        {
            if (phase != ReloadPhase.CylinderOpen) return RuleResult.Reject("cylinder not open");
            int requested = capacity - loaded;
            int taken = reserve.Take(requested);
            if (taken < 0 || taken > requested) throw new InvalidOperationException("Reserve violated take contract");
            loaded += taken;
            if (loaded == 0) return RuleResult.Reject("no rounds");
            SetStage(ReloadPhase.CylinderLoaded); contact = false;
            return RuleResult.Accept();
        }
        RuleResult CloseCylinder()
        {
            if (phase != ReloadPhase.CylinderLoaded) return RuleResult.Reject("cylinder not loaded");
            chambered = loaded > 0; contact = false; boltTravel = 0;
            SetStage(ReloadPhase.Ready); releaseTrigger = true;
            return RuleResult.Accept();
        }
        RuleResult RevolverStep()
        {
            if (phase == ReloadPhase.Ready) return OpenCylinder();
            if (phase == ReloadPhase.CylinderOpen) return LoadCylinder();
            return CloseCylinder();
        }
        static Float3 RevolverPoint(bool left, bool offer)
        {
            return offer ? new Float3(left ? .18f : -.18f, .095f, -.13f)
                         : new Float3(left ? .10f : -.10f, .035f, .10f);
        }
        void RevolverGesture(HandlingInput i)
        {
            if (releaseGrip) return;
            bool fresh = i.Grip && !previousGrip;
            if (!i.Grip) contact = false;
            else if (fresh && !contact && Reach(i.SupportLocal,
                RevolverPoint(i.LeftHanded, phase == ReloadPhase.CylinderOpen), .18f))
            {
                if (phase == ReloadPhase.CylinderOpen || phase == ReloadPhase.CylinderLoaded)
                { contact = true; grab = i.SupportLocal; }
            }
            if (!contact || !i.Grip) return;
            // Z reflection converts source forward load into increasing Unity Z.
            if (phase == ReloadPhase.CylinderOpen && i.SupportLocal.Z - grab.Z >= .06f &&
                Reach(i.SupportLocal, RevolverPoint(i.LeftHanded, false), .18f))
            { LoadCylinder(); contact = false; }
            else if (phase == ReloadPhase.CylinderLoaded &&
                (i.SupportLocal.X - grab.X >= .05f || i.SupportLocal.Y - grab.Y >= .05f))
                CloseCylinder();
            // A new grip is required after loading. Suspension and transfer clear contact.
        }
    }
}
