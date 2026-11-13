using System;using System.Collections.Generic;using System.IO;using Ashfall.Core;using Ashfall.Integration.Playable.UI;using Ashfall.Integration.Playable.Consumables;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanLobby {
  const string CatalogCommand="playable.catalog/1";long catalogSerial;
  readonly Dictionary<string,long> catalogTokens=new Dictionary<string,long>(StringComparer.Ordinal);readonly HashSet<string> catalogItems=new HashSet<string>(StringComparer.Ordinal);
  void ClearCatalog(){catalogTokens.Clear();catalogItems.Clear();}
  bool LootAllowed(string id){return GameplayRules!=null&&(GameplayRules.Loot||catalogItems.Contains(id)&&(roundRules==null||GameplayRules.Catalog));}
  public bool RequestCatalog(string kind,string value,int stars,int hand,Float3 desired,out string id){Check();id=null;if(!R24CreativeCatalog.Contains(kind,value)||kind=="utility"||stars<1||stars>5||hand<0||hand>1||!desired.IsFinite)return false;long token=checked(++catalogSerial);id=OwnActor+"/catalog/"+token.ToString(System.Globalization.CultureInfo.InvariantCulture);if(State==R24LanLobbyState.Hosting)return ApplyCatalog(OwnActor,token,kind,value,stars,hand,desired,clock.Now);if(State!=R24LanLobbyState.Connected)return false;using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){w.Write(token);Write(w,kind,16);Write(w,value,16);w.Write((byte)stars);w.Write((byte)hand);w.Write(desired.X);w.Write(desired.Y);w.Write(desired.Z);w.Write(EstimatedHostTime);return QueueGameCommand(CatalogCommand,m.ToArray());}}
  bool ApplyCatalog(string actor,long token,string kind,string value,int stars,int hand,Float3 desired,double stamp){CombatActor inventory;R24LanRawTrackingSample raw;long prior;if(!InventoryLive||roundRules!=null&&!GameplayRules.Catalog||!R24CreativeCatalog.Contains(kind,value)||kind=="utility"||stars<1||stars>5||token<1||!desired.IsFinite||!Finite(stamp)||stamp>clock.Now+.05||clock.Now-stamp>.75||catalogTokens.TryGetValue(actor,out prior)&&token<=prior||!ExplosivePose(actor,hand,out raw,out inventory,true)||Distance2(raw.Pose.Root.Position,desired)>9||rawWorldClear==null||!rawWorldClear(raw.Pose.Head.Position,desired))return false;
   string id=actor+"/catalog/"+token.ToString(System.Globalization.CultureInfo.InvariantCulture);if(!Wire.IsText(id,128,false))return false;bool accepted;
   if(kind=="supply"||kind=="item"&&value=="backpack")accepted=RegisterSharedSupply(id,value,R24SupplyState.DefaultAmount(value),desired);
   else if(kind=="item")accepted=RegisterSharedFood(id,value,desired);
   else if(Ashfall.Integration.Playable.Explosives.R23GrenadeState.ValidKind(value))accepted=RegisterSharedGrenade(id,value,desired);
   else accepted=RegisterWorldWeapon(id,value,stars,desired);
   if(!accepted)return false;catalogTokens[actor]=token;catalogItems.Add(id);return true;
  }
  void ReadCatalog(string actor,byte[] bytes){try{using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Utf8)){long token=r.ReadInt64();string kind=Read(r,16),value=Read(r,16);int stars=r.ReadByte(),hand=r.ReadByte();var at=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());double stamp=r.ReadDouble();if(m.Position==m.Length)ApplyCatalog(actor,token,kind,value,stars,hand,at,stamp);}}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
 }
}
