using Ashfall.Core;
namespace Ashfall.Modules.HealthStates {
 public sealed partial class HealthService {
  // net_session1000._die_r13(...,"fall",false): environmental fall is fatal
  // even during spawn protection and is never a revivable knockdown.
  public RuleResult ApplyMatchFallDeath(CommandContext context,string target,long revision){return AtomicHealth(delegate{lock(sync){var allowed=Admit(context);if(!allowed.Accepted)return allowed;Player p;if(!world.InMatch||!players.TryGetValue(target,out p)||p.Revision!=revision||!Living(p))return RuleResult.Reject("fall-state");long before=p.Revision;p.Health=0;p.Shield=0;p.Deaths++;Cancel(p);p.Life=LifeState.Dead;p.DownedUntil=0;p.RespawnAt=respawnMode?world.Now+respawnDelay:0;p.Revision++;Publish(p,before,"fall");return RuleResult.Accept();}});}
 }
}
