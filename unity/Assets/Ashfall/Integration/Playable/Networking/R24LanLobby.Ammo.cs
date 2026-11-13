using System;using System.Collections.Generic;using System.IO;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking {
 public sealed class R24LanAmmoState {
  public readonly string Actor,Instance,Profile,Family;public readonly long Epoch,Revision,Acknowledged;public readonly int Loaded,Left,Right,Reserve;
  internal R24LanAmmoState(string actor,string id,string profile,string family,long epoch,long revision,long ack,int loaded,int left,int right,int reserve){Actor=actor;Instance=id;Profile=profile;Family=family;Epoch=epoch;Revision=revision;Acknowledged=ack;Loaded=loaded;Left=left;Right=right;Reserve=reserve;}
 }
 public sealed partial class R24LanLobby {
  const string AmmoFeature="playable.ammo/1";readonly Dictionary<string,R24LanAmmoState> ammoStates=new Dictionary<string,R24LanAmmoState>(StringComparer.Ordinal);long ammoRevision;double nextAmmo;
  void ClearAmmo(){ammoStates.Clear();nextAmmo=0;foreach(var peer in peers.Values){peer.Ammo.Clear();peer.Resources=null;}}
  public bool TryReadOwnAmmo(string id,out R24LanAmmoState state){Check();state=null;return id!=null&&ammoStates.TryGetValue(id,out state);}
  public bool OwnsAmmo(R24LanAmmoState state){R24LanAmmoState current;return state!=null&&state.Actor==OwnActor&&state.Epoch==Epoch&&ammoStates.TryGetValue(state.Instance,out current)&&Object.ReferenceEquals(state,current)&&(State==R24LanLobbyState.Hosting||state.Acknowledged>=ammoPredictionSequence);}
  void TickAmmo(){if(State!=R24LanLobbyState.Hosting||clock.Now<nextAmmo)return;nextAmmo=clock.Now+.1;
   foreach(var actor in combat){long ack=actor.Key==OwnActor?gameCommandSequence:0;Peer target=null;if(actor.Key!=OwnActor){foreach(var peer in peers.Values)if(peer.Admitted&&peer.Actor==actor.Key){target=peer;ack=peer.LastGameCommand;break;}if(target==null)continue;}
    PublishResources(actor.Key,actor.Value,target,ack);
    if(target!=null){var retired=new List<string>();foreach(string key in target.Ammo.Keys)if(!actor.Value.Weapons.ContainsKey(key))retired.Add(key);foreach(string key in retired)target.Ammo.Remove(key);}
    foreach(var entry in actor.Value.Weapons){var weapon=entry.Value;int reserve=weapon.Profile=="smaw"?weapon.RocketReloads:0;if(weapon.Family!=null)actor.Value.Reserves.TryGetValue(weapon.Family,out reserve);var row=new R24LanAmmoState(actor.Key,entry.Key,weapon.Profile,weapon.Family,Epoch,checked(++ammoRevision),ack,weapon.Ammo,weapon.Hands==null?-1:weapon.Hands[0],weapon.Hands==null?-1:weapon.Hands[1],reserve);
     if(target==null){ammoStates[entry.Key]=row;continue;}using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,row.Instance,128);Write(w,row.Profile,16);w.Write(row.Acknowledged);w.Write(row.Loaded);w.Write(row.Left);w.Write(row.Right);w.Write(row.Reserve);target.Ammo[entry.Key]=Packet(PacketKind.Snapshot,AmmoFeature,OwnActor,m.ToArray(),row.Revision);}
    }
   }
  }
  void ReadAmmo(Packet packet){try{using(var m=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(m,Utf8)){string id=Read(r,128),profile=Read(r,16);long ack=r.ReadInt64();int loaded=r.ReadInt32(),left=r.ReadInt32(),right=r.ReadInt32(),reserve=r.ReadInt32();R24LanAmmoState prior;int cap=R24LanLoadout.Capacity(profile,5);
   bool paired=R24LanLoadout.Paired(profile);if(m.Position!=m.Length||packet.BaseSequence!=-1||packet.Sequence<1||ack<0||ack>gameCommandSequence||reserve<0||cap<1&&!R24LanLoadout.Melee(profile)||loaded<0||loaded>cap||(paired?(left<0||right<0||left>cap/2||right>cap/2||left+right!=loaded):(left!=-1||right!=-1))||ammoStates.Count>=4104&&!ammoStates.ContainsKey(id))return;
   if(ammoStates.TryGetValue(id,out prior)&&prior.Revision>=packet.Sequence)return;ammoStates[id]=new R24LanAmmoState(OwnActor,id,profile,R24LanLoadout.Family(profile),Epoch,packet.Sequence,ack,loaded,left,right,reserve);
  }}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
  void FlushAmmo(string connection,Peer peer){var sent=new List<string>();int budget=6;foreach(var row in peer.Ammo){if(budget--<=0||!Send(connection,row.Value))break;sent.Add(row.Key);}foreach(string id in sent)peer.Ammo.Remove(id);}

 }
}
