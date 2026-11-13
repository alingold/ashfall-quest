#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
 public sealed partial class R23HandsWeapons
 {
  // Measured AFTER the existing Health owner accepts a mutation. A shield-only
  // hit also restarts source campaign recovery; refused/zero/gain events do not.
  public event Action<float,float> PlayerPoolLost;
  bool PublishPlayerPoolLoss(float beforeHealth,float beforeShield)
  {
   float healthLoss=Mathf.Max(0,beforeHealth-round.Health),shieldLoss=Mathf.Max(0,beforeShield-round.Shield);
   if(healthLoss+shieldLoss<=0)return false;var callback=PlayerPoolLost;if(callback!=null)callback(healthLoss,shieldLoss);return true;
  }
 }
}
#endif
