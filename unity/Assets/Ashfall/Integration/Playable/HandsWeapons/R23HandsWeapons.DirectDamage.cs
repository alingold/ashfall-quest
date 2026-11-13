#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        public bool ApplyDirectPlayerDamage(float amount)
        {
            if(!Ready||round==null)return false;
            float beforeHealth=round.Health,beforeShield=round.Shield;bool applied=round.ApplyDirectPlayerDamage(amount);
            if(applied)PublishPlayerPoolLoss(beforeHealth,beforeShield);
            if(audioCues!=null&&R24PlayerHurtCueRules.ShouldPlay(Ready,applied,hadFocus,isActiveAndEnabled))audioCues.Play(ActiveWeaponId,"hurt",false);
            return applied;
        }
    }
}
#endif
