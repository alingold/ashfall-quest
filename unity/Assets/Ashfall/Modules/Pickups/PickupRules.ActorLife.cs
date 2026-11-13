using Ashfall.Core;
namespace Ashfall.Modules.Pickups {
 public sealed partial class PickupRules {
  // Trusted presentation acknowledgement: a host receipt has already consumed
  // this local owned item and applied Health. This path applies no effect.
  public RuleResult AcknowledgeConsumed(CommandContext c,string id){lock(sync){if(quarantined||executing||pendingSupply!=null||revision==long.MaxValue||!c.IsAuthority||c.SessionId!=session||c.AuthorityEpoch!=epoch||c.Tick<0||c.Tick>now)return RuleResult.Reject("receipt-context");var allowed=gate.Validate(c);if(!allowed.Accepted)return allowed;Item item;if(id==null||!items.TryGetValue(id,out item))return RuleResult.Reject("receipt-item");if(item.Removed)return RuleResult.Accept();if(item.Owner!=c.ActorId)return RuleResult.Reject("receipt-owner");UseState user;if(users.TryGetValue(c.ActorId,out user)&&user.Held==id)user.Stow();item.Owner=null;item.Reserved=null;item.Removed=true;revision++;return RuleResult.Accept();}}
  // Source net_session1000._die/_spawn clears soda_left and held gestures.
  // This does not restore, move, consume or grant any world/owned item.
  public RuleResult CancelActorLife(CommandContext context){lock(sync){if(quarantined||executing||pendingSupply!=null||revision==long.MaxValue)return RuleResult.Reject("busy");if(!context.IsAuthority||context.SessionId!=session||context.AuthorityEpoch!=epoch||!Id(context.ActorId)||context.Tick<0||context.Tick>now)return RuleResult.Reject("actor-context");var allowed=gate.Validate(context);if(!allowed.Accepted)return allowed;UseState user;if(!users.TryGetValue(context.ActorId,out user))return RuleResult.Accept();var projection=transactions as IPreparedPickupSelection;if(projection!=null&&!projection.PublishSelection(context.ActorId,user.Held,null))return RuleResult.Reject("selection-rejected");user.Stow();user.Soda=0;foreach(var item in items.Values)if(item.Reserved==context.ActorId){item.Reserved=null;item.Until=0;}revision++;return RuleResult.Accept();}}
 }
}
