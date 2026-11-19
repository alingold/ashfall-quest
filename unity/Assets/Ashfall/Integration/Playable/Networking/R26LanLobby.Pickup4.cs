using System;using System.Collections.Generic;using System.Text;using Ashfall.Core;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanLobby {
  // PICKUP4 diagnostics: the host slot ledger of one actor. Read-only; never feeds admission.
  internal string SlotAudit(string actor){Check();CombatActor c;if(State!=R24LanLobbyState.Hosting)return "not-host";if(actor==null||!combat.TryGetValue(actor,out c))return "no-inventory";
   var s=new StringBuilder();s.Append("cap=").Append(c.Capacity).Append(" w=").Append(WeaponSlots(c)).Append(" f=").Append(FoodSlots(actor)).Append(" g=").Append(GrenadeSlots(actor)).Append(" weapons=");
   foreach(var w in c.Weapons)s.Append(w.Key).Append(':').Append(w.Value.Profile).Append(',');s.Append(" foods=");foreach(var f in foods.Values)if(f.Owner==actor&&!f.Consumed)s.Append(f.Id).Append(':').Append(f.Kind).Append(',');
   s.Append(" grenades=");GrenadeOwner g;if(grenadeOwners.TryGetValue(actor,out g))foreach(var item in g.State.OwnedItems)s.Append(item.Id).Append('@').Append(item.Slot).Append(',');return s.ToString();}
  // PICKUP4 (r15): the host's own player is in-process, so its custody is decided twice: host admission
  // grants first, the local collect can still refuse afterwards (hand rearm after a swap, world/rack gates,
  // local room). Such a grant is a slot the player cannot see. The host session releases it here, where the
  // item really lies, without the reach/LOS checks a remote claimant needs (nothing moves).
  internal bool ReleaseOwnUncollectedWeapon(string id,Float3 at){Check();CombatActor c;LootEntry e;if(State!=R24LanLobbyState.Hosting||id==null||!at.IsFinite||!combat.TryGetValue(OwnActor,out c)||!c.Weapons.ContainsKey(id)||!loot.TryGetValue(id,out e)||e.Owner!=OwnActor)return false;c.Weapons.Remove(id);e.Owner="";e.At=at;PublishLoot(e);return true;}
  internal void CopyOwnFoodIds(List<string> into){Check();into.Clear();if(State!=R24LanLobbyState.Hosting)return;foreach(var f in foods.Values)if(f.Owner==OwnActor&&!f.Consumed)into.Add(f.Id);}
  internal bool ReleaseOwnUncollectedFood(string id,Float3 at){Check();R24LanFoodState f;if(State!=R24LanLobbyState.Hosting||id==null||!at.IsFinite||!foods.TryGetValue(id,out f)||f.Owner!=OwnActor||f.Consumed)return false;FoodPreparation prep;if(foodPreparations.TryGetValue(OwnActor,out prep)&&prep.Id==id)foodPreparations.Remove(OwnActor);foodOwnedAt.Remove(id);PublishFood(new R24LanFoodState(id,f.Kind,"",at,false,checked(++foodSequence)));return true;}
  internal void CopyOwnGrenadeIds(List<string> into){Check();into.Clear();if(State!=R24LanLobbyState.Hosting)return;foreach(var g in grenades.Values)if(g.Owner==OwnActor&&!g.Spent)into.Add(g.Id);}
  internal bool ReleaseOwnUncollectedGrenade(string id,Float3 at){Check();R24LanGrenadeState g;if(State!=R24LanLobbyState.Hosting||id==null||!at.IsFinite||!grenades.TryGetValue(id,out g)||g.Owner!=OwnActor||g.Spent)return false;GrenadeOwner owner;if(grenadeOwners.TryGetValue(OwnActor,out owner))owner.State.Drop(id);PublishGrenade(id,g.Kind,"",at,false);return true;}
  internal int SlotAuditUsed(string actor){CombatActor c;return actor!=null&&combat.TryGetValue(actor,out c)?WeaponSlots(c)+FoodSlots(actor)+GrenadeSlots(actor):-1;}
  internal int SlotAuditCapacity(string actor){CombatActor c;return actor!=null&&combat.TryGetValue(actor,out c)?c.Capacity:-1;}
 }
}
