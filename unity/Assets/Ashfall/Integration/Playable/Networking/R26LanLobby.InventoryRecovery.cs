using System;using System.Collections.Generic;
namespace Ashfall.Integration.Playable.Networking {
 public static class R26DropReceipt {
  public static bool Fresh(bool host,bool hostOwnsCommand,bool knownAlias,bool pending,long beforeRevision,long revision){return host?!hostOwnsCommand:knownAlias||pending&&revision>beforeRevision;}
 }
 public sealed class R26OwnedWeaponReceipt {
  public readonly string Id,Profile;public readonly int Stars,Loaded,Left,Right,Reserve;
  internal R26OwnedWeaponReceipt(string id,string profile,int stars,int loaded,int left,int right,int reserve){Id=id;Profile=profile;Stars=stars;Loaded=loaded;Left=left;Right=right;Reserve=reserve;}
 }
 public sealed partial class R24LanLobby {
  // Composition-only projection of existing authoritative custody/ammo receipts. No new protocol.
  internal void CopyOwnWeaponReceipts(List<R26OwnedWeaponReceipt> into){Check();into.Clear();
   if(State==R24LanLobbyState.Hosting){CombatActor actor;if(!combat.TryGetValue(OwnActor,out actor))return;foreach(var pair in actor.Weapons){var w=pair.Value;if(w.Profile=="smaw"&&w.Ammo==0&&w.RocketReloads==0)continue;int reserve=w.Profile=="smaw"?w.RocketReloads:0;if(w.Family!=null)actor.Reserves.TryGetValue(w.Family,out reserve);into.Add(new R26OwnedWeaponReceipt(pair.Key,w.Profile,w.Stars,w.Ammo,w.Hands==null?-1:w.Hands[0],w.Hands==null?-1:w.Hands[1],reserve));}return;}
   if(State!=R24LanLobbyState.Connected)return;
   foreach(var pair in ammoStates){var a=pair.Value;if(!OwnsAmmo(a)||a.Profile=="smaw"&&a.Loaded==0&&a.Reserve==0)continue;R24LanLootState world;int stars=1;
    if(lootStates.TryGetValue(a.Instance,out world)){if(world.Owner!=OwnActor)continue;stars=world.Stars;}
    // A starter ID ceases to be custody when its first actor-scoped world row exists.
    // Recollecting that row owns its global ID, never the old starter ID again.
    else if(lootStates.ContainsKey(OwnActor+"/"+a.Instance))continue;
    into.Add(new R26OwnedWeaponReceipt(a.Instance,a.Profile,stars,a.Loaded,a.Left,a.Right,a.Reserve));
   }
  }
 }
}
