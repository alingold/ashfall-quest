using System;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons {
 // Source net_session1000 reload policy, over the SAME actual finite family.
 // Unlimited reload takes/refunds do not change the stored finite reserve.
 public sealed class R24MatchAmmoReserve:IRevisionedAmmoReserve {
  readonly IRevisionedAmmoReserve backing;long revision;bool infinite;public R24MatchAmmoReserve(IRevisionedAmmoReserve actualFinite){if(actualFinite==null)throw new ArgumentNullException();backing=actualFinite;}
  public bool Infinite{get{return infinite;}}public int StoredAvailable{get{return backing.Available;}}public int Available{get{return infinite?-1:backing.Available;}}public long Revision{get{return revision;}}
  internal bool ReconcileNetworkReserve(int amount){if(amount<0)return false;int current=backing.Available;if(current==amount)return true;int changed=amount>current?backing.Refund(amount-current):backing.Take(current-amount);if(changed!=Math.Abs((long)amount-current))return false;revision++;return true;}
  public void SetInfinite(bool enabled){if(infinite==enabled)return;infinite=enabled;revision++;}
  public int Take(int requested){if(requested<=0||revision==long.MaxValue)return 0;int n=infinite?requested:backing.Take(requested);if(n>0)revision++;return n;}
  public int Refund(int requested){if(requested<=0||revision==long.MaxValue)return 0;int n=infinite?requested:backing.Refund(requested);if(n>0)revision++;return n;}
  public int GrantActualSupply(int requested){if(requested<=0||revision==long.MaxValue)return 0;int n=backing.Refund(requested);if(n>0)revision++;return n;}public int WithdrawActualSupply(int requested){if(requested<=0||revision==long.MaxValue)return 0;int n=backing.Take(requested);if(n>0)revision++;return n;}
 }
}
