using System;using System.Collections.Generic;using System.IO;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.Networking {
 public sealed class R24LanFoodState {public readonly string Id,Kind,Owner;public readonly Float3 Position;public readonly bool Consumed;public readonly long Revision;internal R24LanFoodState(string id,string kind,string owner,Float3 at,bool consumed,long revision){Id=id;Kind=kind;Owner=owner;Position=at;Consumed=consumed;Revision=revision;}}
 public sealed partial class R24LanLobby {
  const string FoodFeature="playable.food/1",FoodCommand="playable.food.command/1";
  readonly Dictionary<string,R24LanFoodState> foods=new Dictionary<string,R24LanFoodState>(StringComparer.Ordinal);readonly Dictionary<string,double> foodOwnedAt=new Dictionary<string,double>(StringComparer.Ordinal);long foodSequence;
  sealed class SodaDose{internal float Left;internal double At;internal long Life;}
  readonly Dictionary<string,SodaDose> sodas=new Dictionary<string,SodaDose>(StringComparer.Ordinal);
  public R24LanFoodState[] SharedFoods{get{return CopySnapshot(foods.Values);}}
  Func<string,bool> foodAvailability;
  public void BindFoodAvailability(Func<string,bool> available){Check();foodAvailability=available;}
  // Reuse the consumed tombstone for an unowned source withdrawn by the host.
  // Keep its ID/revision until the epoch ends so full sweeps cannot resurrect it.
  public bool RetireWorldFood(string id){Check();R24LanFoodState food;if(State!=R24LanLobbyState.Hosting||id==null||!foods.TryGetValue(id,out food)||food.Owner!=""||food.Consumed)return false;PublishFood(new R24LanFoodState(id,food.Kind,"",food.Position,true,checked(++foodSequence)));return true;}
  static bool FoodKind(string kind){return kind=="banana"||kind=="soda"||kind=="shaker";}
  void ClearFood(){ownFoodReset=null;foreach(var peer in peers.Values)peer.FoodReset=null;foodPreparations.Clear();foods.Clear();foodOwnedAt.Clear();sodas.Clear();foreach(var p in peers.Values)p.Foods.Clear();}
  int FoodSlots(string actor){var counts=new Dictionary<string,int>(StringComparer.Ordinal);foreach(var f in foods.Values)if(f.Owner==actor&&!f.Consumed){int count;counts.TryGetValue(f.Kind,out count);counts[f.Kind]=count+1;}int slots=0;foreach(var count in counts)slots+=count.Key=="banana"?count.Value:(count.Value+1)/2;return slots;}
  public bool RegisterSharedFood(string id,string kind,Float3 at){Check();if(State!=R24LanLobbyState.Hosting||SelectedMap==null||SelectedMap.Cancelled||!Wire.IsText(id,128,false)||!FoodKind(kind)||!at.IsFinite||foods.Count>=4096||foods.ContainsKey(id))return false;PublishFood(new R24LanFoodState(id,kind,"",at,false,checked(++foodSequence)));return true;}
  public bool TryReadFood(string id,out R24LanFoodState state){Check();state=null;return id!=null&&foods.TryGetValue(id,out state);}
  public bool RequestFood(string id,int hand,int action,Float3 at){Check();if(!Wire.IsText(id,128,false)||hand<0||hand>1||action<0||action>4||!at.IsFinite)return false;if(State==R24LanLobbyState.Hosting)return ApplyFood(OwnActor,id,hand,action,at);if(State!=R24LanLobbyState.Connected)return false;
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,id,128);w.Write((byte)hand);w.Write((byte)action);w.Write(at.X);w.Write(at.Y);w.Write(at.Z);return QueueGameCommand(FoodCommand,m.ToArray());}}
  public string LastGainRejection{get;private set;}
  bool GainHost(string actor,float hp,float shield){var member=sessions.Get(actor);if(member==null)return false;var h=hostHealth.Read(member.CanonicalActorId);if(h==null||h.Life!=LifeState.Alive)return false;
   var result=AsMember(member.CanonicalActorId,delegate(CommandContext c){HealthService.PreparedGain gain;if(!hostHealth.PrepareGain(c,c.CommandId,c.ActorId,h.Revision,h.LifeGeneration,h.ConnectionGeneration,hp,shield,out gain))return RuleResult.Reject("gain-refused");if(!gain.Validate()){gain.Abort();return RuleResult.Reject("gain-stale");}usingGain(gain);return RuleResult.Accept();});LastGainRejection=result.Accepted?null:result.Reason;SyncHostHealth();return result.Accepted;}
  static void usingGain(HealthService.PreparedGain gain){try{gain.CommitValidated();}finally{gain.Abort();}}
  bool ApplyFood(string actor,string id,int hand,int action,Float3 at){R24LanFoodState f;CombatActor c;R24LanRawTrackingSample raw;var member=sessions.Get(actor);
   if(!InventoryLive||!LootAllowed(id)||member==null||member.Life!=SessionLife.Alive||hand<0||hand>1||!at.IsFinite||!foods.TryGetValue(id,out f)||f.Consumed||!combat.TryGetValue(actor,out c)||!Fresh(actor,clock.Now,out raw)||(raw.Pose.Flags&(hand==0?2:4))==0)return false;
   var handAt=hand==0?raw.Pose.Left.Position:raw.Pose.Right.Position;
   if(action==0){if(f.Owner!=""||foodAvailability!=null&&!foodAvailability(id)||Distance2(handAt,f.Position)>4||Distance2(raw.Pose.Root.Position,f.Position)>20.25||rawWorldClear==null||!rawWorldClear(raw.Pose.Head.Position,f.Position))return false;
    int same=0;foreach(var x in foods.Values)if(x.Owner==actor&&!x.Consumed&&x.Kind==f.Kind)same++;if(WeaponSlots(c)+GrenadeSlots(actor)+FoodSlots(actor)+(f.Kind=="banana"||same%2==0?1:0)>c.Capacity)return false;foodOwnedAt[id]=clock.Now;PublishFood(new R24LanFoodState(id,f.Kind,actor,f.Position,false,checked(++foodSequence)));return true;}
   if(f.Owner!=actor)return false;
   if(action==3)return EquipFood(actor,f,hand,raw.Pose);if(action==4){foodPreparations.Remove(actor);return true;}
   if(action==2){if(Distance2(raw.Pose.Root.Position,at)>9||rawWorldClear==null||!rawWorldClear(raw.Pose.Head.Position,at))return false;foodPreparations.Remove(actor);foodOwnedAt.Remove(id);PublishFood(new R24LanFoodState(id,f.Kind,"",at,false,checked(++foodSequence)));return true;}
   if(!FoodReady(actor,id,hand))return false;
   var h=hostHealth.Read(member.CanonicalActorId);if(h==null||h.Life!=LifeState.Alive||f.Kind!="banana"&&h.Shield>=100)return false;
   if(f.Kind=="soda"){SodaDose dose;if(!sodas.TryGetValue(actor,out dose)||dose.Life!=h.LifeGeneration){dose=new SodaDose{At=clock.Now,Life=h.LifeGeneration};sodas[actor]=dose;}dose.Left+=50;}
   else if(!GainHost(actor,f.Kind=="banana"?100:0,f.Kind=="shaker"?50:0))return false;
   foodPreparations.Remove(actor);foodOwnedAt.Remove(id);PublishFood(new R24LanFoodState(id,f.Kind,actor,f.Position,true,checked(++foodSequence)));return true;
  }
  public float PendingSharedSoda(string actor){SodaDose d;return sodas.TryGetValue(actor,out d)?d.Left:0;}
  void TickSodas(){if(!InventoryLive)return;foreach(var entry in sodas){var member=sessions.Get(entry.Key);var h=member==null?null:hostHealth.Read(member.CanonicalActorId);var dose=entry.Value;if(h==null||!h.Connected||h.Life!=LifeState.Alive||dose.Life!=h.LifeGeneration){dose.Left=0;continue;}float amount=Math.Min(dose.Left,(float)Math.Max(0,clock.Now-dose.At)*5);dose.At=clock.Now;if(amount>0&&GainHost(entry.Key,0,amount))dose.Left-=amount;}}
  byte[] FoodFrame(R24LanFoodState f){using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,f.Id,128);Write(w,f.Kind,16);w.Write(f.Owner!="");if(f.Owner!="")Write(w,f.Owner,64);w.Write(f.Position.X);w.Write(f.Position.Y);w.Write(f.Position.Z);w.Write(f.Consumed);return Packet(PacketKind.Snapshot,FoodFeature,OwnActor,m.ToArray(),f.Revision);}}
  void PublishFood(R24LanFoodState f){foods[f.Id]=f;var bytes=FoodFrame(f);foreach(var p in peers.Values)if(p.Admitted)p.Foods[f.Id]=bytes;}
  void ReadFood(Packet packet){try{using(var m=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(m,Utf8)){string id=Read(r,128),kind=Read(r,16),owner=r.ReadBoolean()?Read(r,64):"";var at=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());bool used=r.ReadBoolean();R24LanFoodState prior;if(m.Position!=m.Length||packet.Sequence<1||packet.BaseSequence!=-1||!FoodKind(kind)||!at.IsFinite||foods.Count>=4096&&!foods.ContainsKey(id)||foods.TryGetValue(id,out prior)&&prior.Revision>=packet.Sequence)return;foods[id]=new R24LanFoodState(id,kind,owner,at,used,packet.Sequence);}}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
  void ReadFoodCommand(string actor,byte[] bytes){try{using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Utf8)){string id=Read(r,128);int hand=r.ReadByte(),action=r.ReadByte();var at=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());if(m.Position==m.Length&&action<=4)ApplyFood(actor,id,hand,action,at);}}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
  void FlushFood(string connection,Peer p){string sent=null;foreach(var entry in p.Foods){if(!Send(connection,entry.Value))return;sent=entry.Key;break;}if(sent!=null)p.Foods.Remove(sent);}
 }
}
