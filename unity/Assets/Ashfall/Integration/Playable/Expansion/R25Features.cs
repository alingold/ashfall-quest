namespace Ashfall.Integration.Playable.Expansion
{
    // Local kill switches for 2026-11-09 expansion features that only change this
    // player's own presentation or movement (nothing the host has to agree on).
    // Every flag defaults ON. Add new flags here; never remove another feature's flag.
    public static class R25Features
    {
        // exclusion: features/exclusion.md
        // A. Push the head hull out of geometry (or back to a recent clear head
        //    position) after spawn, pod exit, calibration, ghost moves or when stuck.
        public static bool HeadOverlapRecovery = true;
        public static bool HeadClearance = true;
        public static bool RampCrouch = true;
        public static bool MapStyles = true;
        public static bool DetailLayer = true;
        public static bool TitleMoment = true;
        public static bool LobbyNight = true;
        public static bool MapSolids = true;
        // B. Fade the view to black while the eye itself is pressed into geometry.
        // R4 (playtest 3, owner): "we should never see black screens in this
        // game". Off: walking into a wall simply stops you; a deliberate
        // physical lean may show the wall surface, never a black screen.
        public static bool ObstructedViewFade = false;
        // C. Chest/leg column that blocks thumbstick walking into tall obstacles and
        //    limits automatic step-up while walking to a stair-sized step.
        public static bool WalkBodyColumn = true;
        // D. Pull held weapons back from walls (or hide them) and hide gloves that
        //    are behind a wall. Presentation only; firing rules are unchanged.
        public static bool ObstructedWeaponPresentation = true;

        // flashsmoke: features/flashsmoke.md (gameplay switches are the replicated
        // match rules "flashbangs" and "smoke_grenades"). Comfort option: a
        // flashbang darkens the view instead of whiting it out (same timing).
        public static bool FlashDarkMode = false;
    }
}
