using System;using System.Collections.Generic;using System.IO;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.Networking {
 public sealed class R24LanLootState {
  public readonly string Id,Profile,Owner;public readonly int Stars;public readonly Float3 Position;public readonly long Revision;
  internal R24LanLootState(string id,string profile,string owner,int stars,Float3 at,long revision){Id=id;Profile=profile;Owner=owner;Stars=stars;Position=at;Revision=revision;}
 }
 public sealed partial class R24LanLobby {
  const string LootFeature="playable.loot/1",LootCommand="playable.loot.command/1";
  sealed class LootEntry {internal string Id,Owner="";internal Float3 At;internal CombatWeapon Weapon;internal long Revision;}
  readonly Dictionary<string,LootEntry> loot=new Dictionary<string,LootEntry>(StringComparer.Ordinal);
  readonly Dictionary<string,R24LanLootState> lootStates=new Dictionary<string,R24LanLootState>(StringComparer.Ordinal);long lootSequence;
  public R24LanLootState[] LootStates{get{return CopySnapshot(lootStates.Values);}}
  void ClearLoot(){loot.Clear();lootStates.Clear();foreach(var p in peers.Values)p.Loot.Clear();}
  public bool RegisterWorldWeapon(string id,string profile,int stars,Float3 at){
   Check();if(State!=R24LanLobbyState.Hosting||SelectedMap==null||SelectedMap.Cancelled||!Wire.IsText(id,128,false)||!at.IsFinite||loot.Count>=4096||loot.ContainsKey(id)||stars<1||stars>5)return false;
   int capacity=R24LanLoadout.Capacity(profile,stars);if(capacity<=0&&!R24LanLoadout.Melee(profile))return false;
   var w=new CombatWeapon{Profile=profile,Family=R24LanLoadout.Family(profile),Stars=stars,Capacity=capacity,Ammo=capacity,RocketReloads=profile=="smaw"?2:0};if(R24LanLoadout.Paired(profile))w.Hands=new[]{(capacity+1)/2,capacity/2};var e=new LootEntry{Id=id,Weapon=w,At=at};loot.Add(id,e);PublishLoot(e);return true;
  }
  public bool TryReadLoot(string id,out R24LanLootState state){Check();state=null;return id!=null&&lootStates.TryGetValue(id,out state);}
  public bool RequestLoot(string id,int hand,bool drop,Float3 at){Check();if(!Wire.IsText(id,128,false)||hand<0||hand>1||!at.IsFinite)return false;if(State==R24LanLobbyState.Hosting)return ApplyLoot(OwnActor,id,hand,drop,at);
   if(State!=R24LanLobbyState.Connected)return false;using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,id,128);w.Write((byte)hand);w.Write(drop);w.Write(at.X);w.Write(at.Y);w.Write(at.Z);return QueueGameCommand(LootCommand,m.ToArray());}}
  bool ApplyLoot(string actor,string id,int hand,bool drop,Float3 at){
   if(!InventoryLive||!LootAllowed(id)||hand<0||hand>1||!at.IsFinite)return false;var member=sessions.Get(actor);CombatActor c;R24LanRawTrackingSample sample;LootEntry e;
   if(member==null||member.Life!=Ashfall.Modules.Sessions.SessionLife.Alive||!combat.TryGetValue(actor,out c)||!Fresh(actor,clock.Now,out sample))return false;
   if(drop){CombatWeapon owned;if(!c.Weapons.TryGetValue(id,out owned)||owned.Profile=="smaw"&&owned.Ammo==0&&owned.RocketReloads==0||Distance2(sample.Pose.Root.Position,at)>9||rawWorldClear==null||!rawWorldClear(sample.Pose.Head.Position,at))return false;
    if(!loot.TryGetValue(id,out e)){if(loot.Count>=4096)return false;string unique=actor+"/"+id;if(!Wire.IsText(unique,128,false)||loot.ContainsKey(unique))return false;e=new LootEntry{Id=unique,Weapon=owned};loot.Add(unique,e);}else if(e.Owner!=actor)return false;
    c.Weapons.Remove(id);e.Owner="";e.At=at;PublishLoot(e);return true;}
   if(!loot.TryGetValue(id,out e)||e.Weapon.Profile=="smaw"&&e.Weapon.Ammo==0&&e.Weapon.RocketReloads==0||e.Owner!=""||c.Weapons.ContainsKey(id)||WeaponSlots(c)+FoodSlots(actor)+GrenadeSlots(actor)>=c.Capacity||(sample.Pose.Flags&(hand==0?2:4))==0||Distance2(sample.Pose.Root.Position,e.At)>20.25||Distance2(hand==0?sample.Pose.Left.Position:sample.Pose.Right.Position,e.At)>4||rawWorldClear==null||!rawWorldClear(sample.Pose.Head.Position,e.At))return false;
   c.Weapons.Add(id,e.Weapon);e.Owner=actor;PublishLoot(e);return true;
  }
  void PublishLoot(LootEntry e){e.Revision=checked(++lootSequence);var state=new R24LanLootState(e.Id,e.Weapon.Profile,e.Owner,e.Weapon.Stars,e.At,e.Revision);lootStates[e.Id]=state;byte[] bytes;
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,e.Id,128);Write(w,e.Weapon.Profile,16);w.Write((byte)e.Weapon.Stars);w.Write(e.Owner!="");if(e.Owner!="")Write(w,e.Owner,64);w.Write(e.At.X);w.Write(e.At.Y);w.Write(e.At.Z);bytes=m.ToArray();}
   var frame=Packet(PacketKind.Snapshot,LootFeature,OwnActor,bytes,e.Revision);foreach(var p in peers.Values)if(p.Admitted)p.Loot[e.Id]=frame;
  }
  void ReadLoot(Packet packet){try{using(var m=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(m,Utf8)){string id=Read(r,128),profile=Read(r,16);int stars=r.ReadByte();string owner=r.ReadBoolean()?Read(r,64):"";var at=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());R24LanLootState prior;
   if(m.Position!=m.Length||packet.Sequence<1||packet.BaseSequence!=-1||stars<1||stars>5||!at.IsFinite||R24LanLoadout.Capacity(profile,stars)<=0&&!R24LanLoadout.Melee(profile)||lootStates.Count>=4096&&!lootStates.ContainsKey(id))return;
   if(lootStates.TryGetValue(id,out prior)&&prior.Revision>=packet.Sequence)return;lootStates[id]=new R24LanLootState(id,profile,owner,stars,at,packet.Sequence);}}
   catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
  void ReadLootCommand(string actor,byte[] bytes){try{using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Utf8)){string id=Read(r,128);int hand=r.ReadByte();bool drop=r.ReadBoolean();var at=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());if(m.Position==m.Length)ApplyLoot(actor,id,hand,drop,at);}}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
  void FlushLoot(string connection,Peer p){string sent=null;foreach(var entry in p.Loot){if(!Send(connection,entry.Value))return;sent=entry.Key;break;}if(sent!=null)p.Loot.Remove(sent);}
  void RetireCombatCustody(string actor){if(State!=R24LanLobbyState.Hosting)return;Float3 at;R24LanRawTrackingHistory history;R24LanSpawn spawn;
   if(rawTracking.TryGetValue(actor,out history)&&history.Latest!=null)at=history.Latest.Pose.Root.Position;else if(spawnStates.TryGetValue(actor,out spawn))at=spawn.Feet;else return;at=new Float3(at.X,at.Y+1,at.Z);
   CombatActor owner;if(combat.TryGetValue(actor,out owner))foreach(var weapon in owner.Weapons){LootEntry entry;if(!loot.TryGetValue(weapon.Key,out entry)){string id=actor+"/"+weapon.Key;if(loot.Count>=4096||!Wire.IsText(id,128,false)||loot.ContainsKey(id))continue;entry=new LootEntry{Id=id,Weapon=weapon.Value};loot.Add(id,entry);}entry.Owner="";entry.At=at;PublishLoot(entry);}
   var heldFoods=new List<R24LanFoodState>();foreach(var food in foods.Values)if(food.Owner==actor&&!food.Consumed)heldFoods.Add(food);foreach(var food in heldFoods){foodOwnedAt.Remove(food.Id);PublishFood(new R24LanFoodState(food.Id,food.Kind,"",at,false,checked(++foodSequence)));}sodas.Remove(actor);foreach(var grenade in SharedGrenades)if(grenade.Owner==actor&&!grenade.Spent)PublishGrenade(grenade.Id,grenade.Kind,"",at,false);grenadeOwners.Remove(actor);EquippedWeapon equipped;if(equipment.TryGetValue(actor,out equipped)&&equipped.Blade!=null)equipped.Blade.Retire();equipment.Remove(actor);
  }
  void ReplayLoot(Peer peer){ReplayExplosives();foreach(var e in loot.Values){PublishLoot(e);}}
 }
}
