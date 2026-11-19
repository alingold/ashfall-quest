namespace Ashfall.Integration.Playable.Networking
{
    // Warmup is an actual room stage, never the selected next-match preset or an epoch parity.
    public static class R26WarmupFeedback
    {
        // HUD19 WARMUP-HUD: the HUD shows only HostLine (no "WARMUP" / "damage off" text). Caption is kept unchanged only because the maintained Tools hit-reg suite still asserts it; nothing in the game displays it.
        public const string HostLine="HOST STARTS THE MATCH";
        public const string Caption="WARMUP - damage off - host: START MATCH";
        public static bool DamageOff(R26RoomStage stage){return stage==R26RoomStage.FreeRoam;}
    }
}
