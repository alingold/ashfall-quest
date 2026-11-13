namespace Ashfall.Integration.Playable.Exclusion
{
    // Crouch constants (metres). The crouch itself lives in R23VRRig.RampCrouch.cs:
    // origin Y = calibrated standing origin Y - crouch, crouch from free space.
    public static class R25RampCrouch
    {
        // R8 (playtest 7): 0.60 m put the view almost on the ground ("pretty low"),
        // 1.0 m was "slightly too high"; owner asked for 0.8 m (R10).
        public const float MinimumEyeHeight = .8f;
        public const float Reserve = .008f;
    }
}
