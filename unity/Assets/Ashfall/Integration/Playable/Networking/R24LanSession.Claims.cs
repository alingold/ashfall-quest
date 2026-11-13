#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  sealed class PendingClaim {internal string Kind,Local,Canonical,Value;internal int Hand,Spawn;internal long Epoch;internal double Until,Next;}
  readonly List<string> completedClaims=new List<string>();
  readonly Dictionary<string,PendingClaim> pendingClaims=new Dictionary<string,PendingClaim>();
  void TrackClaim(string kind,string local,string canonical,string value,int hand){R24LanSpawn spawn;if(root.LanCatalogPickup||!peer.Lobby.TryReadSpawn(peer.Lobby.OwnActor,out spawn))return;pendingClaims[kind+"/"+canonical]=new PendingClaim{Kind=kind,Local=local,Canonical=canonical,Value=value,Hand=hand,Spawn=spawn.Revision,Epoch=peer.Lobby.Epoch,Until=Time.realtimeSinceStartupAsDouble+3};}
  // Finish the original validated grip after the host's reliable custody receipt.
  // A new spawn/epoch, release, focus loss or hand selection cannot forge a claim.
  void SyncClaims(){if(peer==null||root==null||!root.Ready||pendingClaims.Count==0)return;var done=completedClaims;done.Clear();double now=Time.realtimeSinceStartupAsDouble;foreach(var pair in pendingClaims){var p=pair.Value;R24LanSpawn spawn;if(now>p.Until||p.Epoch!=peer.Lobby.Epoch||!peer.Lobby.TryReadSpawn(peer.Lobby.OwnActor,out spawn)||spawn.Revision!=p.Spawn){done.Add(pair.Key);continue;}bool owns=false;R24LanLootState weapon;R24LanFoodState food;R24LanGrenadeState grenade;if(p.Kind=="weapon")owns=peer.Lobby.TryReadLoot(p.Canonical,out weapon)&&weapon.Owner==peer.Lobby.OwnActor;else if(p.Kind=="grenade")owns=peer.Lobby.TryReadGrenade(p.Canonical,out grenade)&&grenade.Owner==peer.Lobby.OwnActor&&!grenade.Spent;else owns=peer.Lobby.TryReadFood(p.Canonical,out food)&&food.Owner==peer.Lobby.OwnActor&&!food.Consumed;
    if(!owns||now<p.Next)continue;p.Next=now+.1;if(!root.LanClaimHandHeld(p.Hand))continue;if(root.CompleteLanCatalogPickup(p.Local,p.Kind=="food"?"item":"weapon",p.Value,p.Kind=="grenade"?root.UI.Settings.DominantHand:p.Hand)){done.Add(pair.Key);root.UI.SetLoadoutStatus("COLLECTED");}}
   foreach(string id in done)pendingClaims.Remove(id);done.Clear();
  }
 }
}
#endif
