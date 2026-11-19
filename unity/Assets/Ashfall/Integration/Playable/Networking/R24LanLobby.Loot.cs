using System;using System.Collections.Generic;using System.IO;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.Networking {
 // PICKUP2: shared admission/UI budget. A weapon, including grapple or melee, uses one slot.
 public static class R26InventorySlots {
  public static bool HasRoom(int capacity,int weapons,int food,int grenades){return weapons+food+grenades<capacity;}
  public static bool RetainsLocal(bool retain,string session,string map,string preparedSession,string preparedMap){return retain&&session==preparedSession&&map==preparedMap;}
  public static bool ResetLocal(bool requested,bool retaining){return requested&&!retaining;}
 }
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
  internal long LastRefusedWeaponSwap {get;private set;}
  void ClearLoot(){LastRefusedWeaponSwap=0;loot.Clear();lootStates.Clear();pickupRejectionLogged.Clear();foreach(var p in peers.Values)p.Loot.Clear();}
  internal void CopyLootStates(List<R24LanLootState> into){Check();into.Clear();foreach(var state in lootStates.Values)into.Add(state);}
  public bool RegisterWorldWeapon(string id,string profile,int stars,Float3 at){
   Check();if(State!=R24LanLobbyState.Hosting||SelectedMap==null||SelectedMap.Cancelled||!Wire.IsText(id,128,false)||!at.IsFinite||loot.Count>=4096||loot.ContainsKey(id)||stars<1||stars>5)return false;
   int capacity=R24LanLoadout.Capacity(profile,stars);if(capacity<=0&&!R24LanLoadout.Melee(profile))return false;
   var w=new CombatWeapon{Profile=profile,Family=R24LanLoadout.Family(profile),Stars=stars,Capacity=capacity,Ammo=capacity,RocketReloads=profile=="smaw"?2:0};if(R24LanLoadout.Paired(profile))w.Hands=new[]{(capacity+1)/2,capacity/2};var e=new LootEntry{Id=id,Weapon=w,At=at};loot.Add(id,e);PublishLoot(e);return true;
  }
  public bool TryReadLoot(string id,out R24LanLootState state){Check();state=null;return id!=null&&lootStates.TryGetValue(id,out state);}
  public bool RequestLoot(string id,int hand,bool drop,Float3 at){Check();if(!Wire.IsText(id,128,false)||hand<0||hand>1||!at.IsFinite)return false;if(State==R24LanLobbyState.Hosting)return ApplyLoot(OwnActor,id,hand,drop,at);
   if(State!=R24LanLobbyState.Connected)return false;using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,id,128);w.Write((byte)hand);w.Write(drop);w.Write(at.X);w.Write(at.Y);w.Write(at.Z);return QueueGameCommand(LootCommand,m.ToArray());}}
  // A full gun-for-gun swap carries the offered gun on the same ordered command.
  internal bool RequestWeaponSwap(string incoming,string offered,int hand,Float3 at){
   Check();if(!Wire.IsText(incoming,128,false)||!Wire.IsText(offered,128,false)||hand<0||hand>1||!at.IsFinite)return false;
   if(State==R24LanLobbyState.Hosting)return ApplyWeaponSwap(OwnActor,incoming,offered,hand,at);
   if(State!=R24LanLobbyState.Connected)return false;
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,incoming,128);w.Write((byte)hand);w.Write(false);w.Write(at.X);w.Write(at.Y);w.Write(at.Z);Write(w,offered,128);return QueueGameCommand(LootCommand,m.ToArray());}
  }
  bool ApplyWeaponSwap(string actor,string incoming,string offered,int hand,Float3 at){
   string why=LootRejection(actor,incoming,hand,false,at,true)??LootRejection(actor,offered,hand,true,at,false);
   if(why==null&&!R26InventorySlots.HasRoom(combat[actor].Capacity,WeaponSlots(combat[actor])-1,FoodSlots(actor),GrenadeSlots(actor)))why="slots-full";
   if(why!=null)return RejectPickup(actor,"weapon-swap",incoming,why);
   CommitLoot(actor,offered,true,at);CommitLoot(actor,incoming,false,at);return true;
  }
  bool ApplyLoot(string actor,string id,int hand,bool drop,Float3 at){
   string why=LootRejection(actor,id,hand,drop,at,false);if(why!=null)return RejectPickup(actor,drop?"weapon-drop":"weapon",id,why);CommitLoot(actor,id,drop,at);return true;
  }
  internal string StarterDropId(string actor,string profile)
  {
   R24LanHealthState health;
   return TryReadHealth(actor,out health)&&health.Deaths>0?actor+"/"+profile+"/"+health.Deaths.ToString(System.Globalization.CultureInfo.InvariantCulture):actor+"/"+profile;
  }
  void CommitLoot(string actor,string id,bool drop,Float3 at){CombatActor c=combat[actor];LootEntry e;
   if(drop){CombatWeapon owned=c.Weapons[id];if(!loot.TryGetValue(id,out e)){e=new LootEntry{Id=StarterDropId(actor,id),Weapon=owned};loot.Add(e.Id,e);}c.Weapons.Remove(id);e.Owner="";e.At=at;PublishLoot(e);return;}
   e=loot[id];c.Weapons.Add(id,e.Weapon);e.Owner=actor;PublishLoot(e);
  }
  // MP-PICKUP: the exact host admission, one reason per refusal. ignoreSlots previews
  // whether a full inventory could swap (the root makes room first, like single-player).
  string LootRejection(string actor,string id,int hand,bool drop,Float3 at,bool ignoreSlots){
   if(!InventoryLive)return "inventory-closed";if(!LootAllowed(id))return "rules";if(hand<0||hand>1||!at.IsFinite)return "payload";var member=sessions.Get(actor);CombatActor c;R24LanRawTrackingSample sample;LootEntry e;
   if(member==null||member.Life!=Ashfall.Modules.Sessions.SessionLife.Alive)return "not-alive";if(!combat.TryGetValue(actor,out c))return "no-inventory";if(!Fresh(actor,clock.Now,out sample))return "tracking";
   if(drop){CombatWeapon owned;if(!c.Weapons.TryGetValue(id,out owned))return "not-owned";if(owned.Profile=="smaw"&&owned.Ammo==0&&owned.RocketReloads==0)return "spent";if(Distance2(sample.Pose.Root.Position,at)>9)return "drop-reach";if(rawWorldClear==null||!rawWorldClear(sample.Pose.Head.Position,at))return "drop-world";
    if(!loot.TryGetValue(id,out e)){if(loot.Count>=4096)return "loot-full";string unique=StarterDropId(actor,id);if(!Wire.IsText(unique,128,false)||loot.ContainsKey(unique))return "drop-id";}else if(e.Owner!=actor)return "custody";return null;}
   if(!loot.TryGetValue(id,out e))return "unknown";if(e.Weapon.Profile=="smaw"&&e.Weapon.Ammo==0&&e.Weapon.RocketReloads==0)return "spent";if(e.Owner!="")return e.Owner==actor?"owned":"claimed";if(c.Weapons.ContainsKey(id))return "duplicate";
   if(!ignoreSlots&&!R26InventorySlots.HasRoom(c.Capacity,WeaponSlots(c),FoodSlots(actor),GrenadeSlots(actor)))return "slots-full";if((sample.Pose.Flags&(hand==0?2:4))==0)return "hand-untracked";
   if(Distance2(sample.Pose.Root.Position,e.At)>20.25)return "body-reach";if(Distance2(hand==0?sample.Pose.Left.Position:sample.Pose.Right.Position,e.At)>4)return "hand-reach";if(rawWorldClear==null||!rawWorldClear(sample.Pose.Head.Position,e.At))return "line-of-sight";return null;
  }
  // Host composition: would the host's own collect be admitted (optionally ignoring
  // only the free-slot rule)? A refused swap must not drop the held item first.
  public bool PreviewOwnLoot(string id,int hand,bool ignoreSlots,out string reason){Check();reason=State!=R24LanLobbyState.Hosting?"not-host":!Wire.IsText(id,128,false)?"payload":LootRejection(OwnActor,id,hand,false,new Float3(),ignoreSlots);return reason==null;}
  // Ordered command lane position; a change proves a make-room drop was queued before the collect.
  internal long CommandSequence{get{return gameCommandSequence;}}
  // Refusal diagnostics never feed admission. The development sink sees each item at most once per second.
  public int RejectedPickups{get;private set;}public string LastPickupRejection{get;private set;}
  public Action<string> PickupRejectionLog;readonly Dictionary<string,double> pickupRejectionLogged=new Dictionary<string,double>(StringComparer.Ordinal);
  bool RejectPickup(string actor,string kind,string id,string why){RejectedPickups++;LastPickupRejection=why;var log=PickupRejectionLog;if(log==null)return false;string key=kind+"/"+id;double last;if(pickupRejectionLogged.TryGetValue(key,out last)&&clock.Now-last<1)return false;if(pickupRejectionLogged.Count>=256)pickupRejectionLogged.Clear();pickupRejectionLogged[key]=clock.Now;log("ASHFALL pickup: host refused "+kind+" '"+id+"' for "+actor+" ("+why+")");return false;}
  void PublishLoot(LootEntry e){e.Revision=checked(++lootSequence);var state=new R24LanLootState(e.Id,e.Weapon.Profile,e.Owner,e.Weapon.Stars,e.At,e.Revision);lootStates[e.Id]=state;byte[] bytes;
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,e.Id,128);Write(w,e.Weapon.Profile,16);w.Write((byte)e.Weapon.Stars);w.Write(e.Owner!="");if(e.Owner!="")Write(w,e.Owner,64);w.Write(e.At.X);w.Write(e.At.Y);w.Write(e.At.Z);bytes=m.ToArray();}
   var frame=Packet(PacketKind.Snapshot,LootFeature,OwnActor,bytes,e.Revision);foreach(var p in peers.Values)if(p.Admitted)p.Loot[e.Id]=frame;
  }
  void ReadLoot(Packet packet){
   // A negative swap receipt contains only a zero byte. A loot row always includes its ID and pose.
   if(packet.Payload.Length==1){if(packet.Payload[0]==0&&packet.BaseSequence==-1&&packet.Sequence>LastRefusedWeaponSwap)LastRefusedWeaponSwap=packet.Sequence;return;}
   try{using(var m=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(m,Utf8)){string id=Read(r,128),profile=Read(r,16);int stars=r.ReadByte();string owner=r.ReadBoolean()?Read(r,64):"";var at=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());R24LanLootState prior;
   if(m.Position!=m.Length||packet.Sequence<1||packet.BaseSequence!=-1||stars<1||stars>5||!at.IsFinite||R24LanLoadout.Capacity(profile,stars)<=0&&!R24LanLoadout.Melee(profile)||lootStates.Count>=4096&&!lootStates.ContainsKey(id))return;
   if(lootStates.TryGetValue(id,out prior)&&prior.Revision>=packet.Sequence)return;lootStates[id]=new R24LanLootState(id,profile,owner,stars,at,packet.Sequence);}}
   catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
  void ReadLootCommand(string actor,byte[] bytes){try{using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Utf8)){string id=Read(r,128);int hand=r.ReadByte();bool drop=r.ReadBoolean();var at=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());if(m.Position==m.Length)ApplyLoot(actor,id,hand,drop,at);else if(!drop){string offered=Read(r,128);if(m.Position==m.Length&&!ApplyWeaponSwap(actor,id,offered,hand,at)){
    foreach(var client in peers.Values)if(client.Admitted&&client.Actor==actor){client.Loot[""]=Packet(PacketKind.Snapshot,LootFeature,OwnActor,new byte[]{0},client.LastGameCommand);break;}
   }}}}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
  void FlushLoot(string connection,Peer p){string sent=null;foreach(var entry in p.Loot){if(!Send(connection,entry.Value))return;sent=entry.Key;break;}if(sent!=null)p.Loot.Remove(sent);}
  void RetireCombatCustody(string actor){if(State!=R24LanLobbyState.Hosting)return;Float3 at;R24LanRawTrackingHistory history;R24LanSpawn spawn;
   if(rawTracking.TryGetValue(actor,out history)&&history.Latest!=null)at=history.Latest.Pose.Root.Position;else if(spawnStates.TryGetValue(actor,out spawn))at=spawn.Feet;else return;at=new Float3(at.X,at.Y+1,at.Z);
   CombatActor owner;if(combat.TryGetValue(actor,out owner))foreach(var weapon in owner.Weapons){LootEntry entry;if(!loot.TryGetValue(weapon.Key,out entry)){string id=StarterDropId(actor,weapon.Key);if(loot.Count>=4096||!Wire.IsText(id,128,false)||loot.ContainsKey(id))continue;entry=new LootEntry{Id=id,Weapon=weapon.Value};loot.Add(id,entry);}entry.Owner="";entry.At=at;PublishLoot(entry);}
   var heldFoods=new List<R24LanFoodState>();foreach(var food in foods.Values)if(food.Owner==actor&&!food.Consumed)heldFoods.Add(food);foreach(var food in heldFoods){foodOwnedAt.Remove(food.Id);PublishFood(new R24LanFoodState(food.Id,food.Kind,"",at,false,checked(++foodSequence)));}sodas.Remove(actor);foreach(var grenade in SharedGrenades)if(grenade.Owner==actor&&!grenade.Spent)PublishGrenade(grenade.Id,grenade.Kind,"",at,false);grenadeOwners.Remove(actor);EquippedWeapon equipped;if(equipment.TryGetValue(actor,out equipped)&&equipped.Blade!=null)equipped.Blade.Retire();equipment.Remove(actor);
  }
  void ReplayLoot(Peer peer){ReplayExplosives();foreach(var e in loot.Values){PublishLoot(e);}}
 }
}
