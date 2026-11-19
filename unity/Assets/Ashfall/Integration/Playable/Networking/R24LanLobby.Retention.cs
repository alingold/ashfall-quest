using System;using System.Collections.Generic;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanLobby {
  // Reserve a non-actor owner so the existing loot snapshot withdraws a spent item's world copy.
  const string RetiredLootOwner="~retired";
  void ResetRespawnInventory(string actor)
  {
   CombatActor prior;if(!combat.TryGetValue(actor,out prior))return;
   ResetFoodPreparation(actor);sodas.Remove(actor);
   grenadeOwners.Remove(actor);
   EquippedWeapon equipped;if(equipment.TryGetValue(actor,out equipped)&&equipped.Blade!=null)equipped.Blade.Retire();equipment.Remove(actor);
   combat.Remove(actor);EnsureCombatActor(actor);combat[actor].Kills=prior.Kills;combat[actor].Score=prior.Score;
   if(actor==OwnActor)ammoStates.Clear();nextAmmo=0;
  }
  sealed class CarriedInventory {internal readonly Dictionary<string,CombatActor> Actors=new Dictionary<string,CombatActor>(StringComparer.Ordinal);internal readonly List<LootEntry> Weapons=new List<LootEntry>();internal readonly Dictionary<string,GrenadeOwner> GrenadeOwners=new Dictionary<string,GrenadeOwner>(StringComparer.Ordinal);internal readonly List<R24LanGrenadeState> Grenades=new List<R24LanGrenadeState>();internal readonly List<R24LanFoodState> Foods=new List<R24LanFoodState>();}
  CarriedInventory CaptureInventory(bool retain){if(!retain)return null;var saved=new CarriedInventory();foreach(var actor in combat){var member=sessions.Get(actor.Key);var health=member==null||hostHealth==null?null:hostHealth.Read(member.CanonicalActorId);if(health!=null&&health.Life!=Ashfall.Modules.HealthStates.LifeState.Dead)saved.Actors[actor.Key]=actor.Value;}foreach(var e in loot.Values)if(e.Owner!=""&&e.Owner!=RetiredLootOwner)saved.Weapons.Add(e);foreach(var owner in grenadeOwners)saved.GrenadeOwners[owner.Key]=owner.Value;foreach(var grenade in grenades.Values)if(grenade.Owner!=""&&!grenade.Spent)saved.Grenades.Add(grenade);foreach(var f in foods.Values)if(f.Owner!=""&&!f.Consumed)saved.Foods.Add(f);return saved;}
  void RestoreInventory(CarriedInventory saved){if(saved==null)return;foreach(var actor in saved.Actors)if(HasMember(actor.Key)){actor.Value.Kills=actor.Value.Score=0;actor.Value.Clocks.Clear();combat[actor.Key]=actor.Value;}foreach(var e in saved.Weapons)if(combat.ContainsKey(e.Owner)){loot[e.Id]=e;PublishLoot(e);}foreach(var f in saved.Foods)if(combat.ContainsKey(f.Owner)){foodOwnedAt[f.Id]=clock.Now;PublishFood(new R24LanFoodState(f.Id,f.Kind,f.Owner,f.Position,false,checked(++foodSequence)));}foreach(var owner in saved.GrenadeOwners)if(combat.ContainsKey(owner.Key)){owner.Value.State.Stow();owner.Value.At=clock.Now;grenadeOwners[owner.Key]=owner.Value;}foreach(var grenade in saved.Grenades)if(combat.ContainsKey(grenade.Owner))PublishGrenade(grenade.Id,grenade.Kind,grenade.Owner,grenade.Position,false);scoreDirty=true;}
 }
}
