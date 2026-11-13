using System;using System.IO;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking {
 public sealed class R24LanFoodReset {public readonly string Id;public readonly long Epoch,Sequence;internal R24LanFoodReset(string id,long epoch,long sequence){Id=id;Epoch=epoch;Sequence=sequence;}}
 public sealed partial class R24LanLobby {
  const string FoodResetFeature="playable.food.reset/1";long foodResetSequence;R24LanFoodReset ownFoodReset;
  public bool TryReadOwnFoodReset(out R24LanFoodReset state){state=ownFoodReset;return state!=null&&state.Epoch==Epoch;}
  void ResetFoodPreparation(string actor){FoodPreparation prep;if(!foodPreparations.TryGetValue(actor,out prep))return;foodPreparations.Remove(actor);long sequence=checked(++foodResetSequence);if(actor==OwnActor){ownFoodReset=new R24LanFoodReset(prep.Id,Epoch,sequence);return;}using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,prep.Id,128);var frame=Packet(PacketKind.Snapshot,FoodResetFeature,OwnActor,m.ToArray(),sequence);foreach(var peer in peers.Values)if(peer.Admitted&&peer.Actor==actor)peer.FoodReset=frame;}}
  void ReadFoodReset(Packet packet){if(packet.BaseSequence!=-1||packet.Sequence<1||ownFoodReset!=null&&ownFoodReset.Sequence>=packet.Sequence)return;try{using(var m=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(m,Utf8)){string id=Read(r,128);if(m.Position!=m.Length)return;ownFoodReset=new R24LanFoodReset(id,Epoch,packet.Sequence);}}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
 }
}
