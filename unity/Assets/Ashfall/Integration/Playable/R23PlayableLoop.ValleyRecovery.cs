#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Bots;
namespace Ashfall.Integration.Playable
{
 public sealed partial class R23PlayableLoop
 {
  readonly R24ValleyRecoveryState valleyRecovery=new R24ValleyRecoveryState();
  public double ValleyRecoveryRemaining {get{return valleyRecovery.RemainingSeconds;}}
  void ValleyPlayerPoolLost(float healthLoss,float shieldLoss)
  {
   if(!ValleyCampaignContext()||!roundStarted||switchingMap||Lobby==null||Lobby.Visible||Gameplay.LocalMatchHealthConfigured)return;
   valleyRecovery.NotifyAcceptedLoss(healthLoss,shieldLoss);
  }
  void StepValleyRecovery(float dt,bool eligible,bool advancing)
  {
   if(!eligible||Gameplay.LocalMatchHealthConfigured){valleyRecovery.Reset();return;}
   float gain=valleyRecovery.Step(dt,advancing,Gameplay.Health);
   if(gain>0)Gameplay.ApplyPlayerGain(gain,0);
  }
 }
}
#endif
