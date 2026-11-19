#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        // RANGE lane targets reuse the held weapon's practice-dummy "hit" cue (same call as the source dummy path).
        public bool PlayRangeTargetHitCue(){if(!Ready||!HasWeapon||holstered||audioCues==null)return false;audioCues.Play(ActiveWeaponId,"hit",false);return true;}
    }
}
#endif
