#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        UI.R26DamageDirection incomingDirection;
        public void BindIncomingDirection(UI.R26DamageDirection presentation){incomingDirection=presentation;}
        public void NotifyIncomingDamageSource(Vector3 source){if(incomingDirection!=null)incomingDirection.NotifyDamage(source);}
        public void ApplyPlayerDamage(float amount,Vector3 source)
        {
            if(!new Ashfall.Core.Float3(source.x,source.y,source.z).IsFinite)return;
            float before=Health+Shield;ApplyPlayerDamage(amount);if(Health+Shield<before)NotifyIncomingDamageSource(source);
        }
    }
}
#endif
