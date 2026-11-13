using System;
namespace Ashfall.Integration.Playable.Bots
{
 // Frozen main.gd::_damage_player/_process. This is campaign recovery only;
 // match shields, protection, respawn, consumables and Health authority stay
 // with their existing owners. The source subtracts the whole frame first,
 // then heals for that frame when the five-second delay has reached zero.
 public sealed class R24ValleyRecoveryState
 {
  public const float DamageDelay=5,HealthPerSecond=5,HealthCeiling=100;
  public double RemainingSeconds {get;private set;}
  public bool NotifyAcceptedLoss(float healthLoss,float shieldLoss)
  {
   if(!Finite(healthLoss)||!Finite(shieldLoss)||healthLoss<0||shieldLoss<0||healthLoss+shieldLoss<=0)return false;
   RemainingSeconds=DamageDelay;return true;
  }
  public float Step(float dt,bool advancing,float health)
  {
   if(!Finite(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
   if(!advancing)return 0;RemainingSeconds=Math.Max(0,RemainingSeconds-dt);
   if(RemainingSeconds>0||!Finite(health)||health<=0||health>=HealthCeiling)return 0;
   return Math.Min(HealthCeiling-health,dt*HealthPerSecond);
  }
  public void Reset(){RemainingSeconds=0;}
  static bool Finite(float value){return !Single.IsNaN(value)&&!Single.IsInfinity(value);}
 }
}
