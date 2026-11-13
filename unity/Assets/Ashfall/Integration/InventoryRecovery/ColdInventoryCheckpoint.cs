using Ashfall.RecoveryContracts;
using Wire=Ashfall.RecoveryContracts.RecoveryWire;
using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Handler=Ashfall.Modules.WeaponHandling.WeaponHandling;
namespace Ashfall.Integration.InventoryRecovery
{
 public sealed class WeaponCheckpointMember
 {
  public readonly string InstanceId,ReserveProvenance;
  public readonly Handler Handler;
  public WeaponCheckpointMember(string id,string provenance,Handler handler){Wire.Require(!String.IsNullOrEmpty(id)&&!String.IsNullOrEmpty(provenance)&&handler!=null);InstanceId=id;ReserveProvenance=provenance;Handler=handler;}
 }
 public sealed class QuarantinedInventory
 {
  public readonly Inventory Inventory;
  readonly Dictionary<string,Handler> weapons;
  readonly Dictionary<string,FiniteAmmoReserve> reserves;
  internal QuarantinedInventory(Inventory inventory,Dictionary<string,Handler> weapons,Dictionary<string,FiniteAmmoReserve> reserves){Inventory=inventory;this.weapons=weapons;this.reserves=reserves;}
  public Handler Weapon(string id){return weapons[id];}
  public FiniteAmmoReserve Reserve(string id){return reserves[id];}
 }
 public static class ColdInventoryCheckpoint
 {
  public const string Codec="InventoryRecovery/schema1";
  public static string Digest(byte[] image){return Wire.Hash(image);}
  public static string CatalogFingerprint(ItemCatalog catalog)
  {
   Wire.Require(catalog!=null);using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
    var defs=catalog.Definitions;w.Write(defs.Length);foreach(var d in defs){foreach(string s in new[]{d.Id,d.ProfileId,d.Label,d.AmmoFamily,d.FireMode,d.StackKey})Wire.Text(w,s??"");w.Write(d.CatalogIndex);w.Write(d.Category);w.Write(d.Active);w.Write(d.IsWeapon);w.Write(d.Tiered);w.Write(d.RoundsPerSecond);for(int i=1;i<=5;i++){var v=d.Stats(d.Tiered?i:1);w.Write(v.Body);w.Write(v.Head);w.Write(v.Recoil);w.Write(v.Spread);w.Write(v.Pellets);w.Write(v.MagazinePerHand);w.Write(v.Hands);}}return Wire.Hash(m.ToArray());}
  }
  // Call only on root's exclusive host lane. This cannot freeze external producers,
  // WeaponLifetimes or external producers; this is local owner accounting only.
  static bool UniqueReserve(HashSet<Handler> handlers,Handler candidate)
  { if(candidate==null)return false;foreach(var other in handlers)if(!Object.ReferenceEquals(other,candidate)&&candidate.SharesReserveWith(other))return false;return true; }
  public static byte[] Capture(CheckpointBinding binding,ItemCatalog catalog,Inventory inventory,WeaponCheckpointMember[] members)
  {
   Wire.Require(binding!=null&&inventory!=null&&members!=null&&members.Length<=64&&binding.Catalog==CatalogFingerprint(catalog));
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
    w.Write(0x31524341);w.Write(1);Wire.Text(w,Codec);binding.Write(w);Wire.Blob(w,InventoryCheckpointCodec.Capture(inventory,binding));w.Write(members.Length);
    var ids=new HashSet<string>(StringComparer.Ordinal);var handlers=new HashSet<Handler>();var provenance=new HashSet<string>(StringComparer.Ordinal);
    foreach(var member in members){Wire.Require(member!=null&&ids.Add(member.InstanceId)&&handlers.Add(member.Handler)&&UniqueReserve(handlers,member.Handler)&&provenance.Add(member.ReserveProvenance));Wire.Text(w,member.InstanceId);Wire.Text(w,member.ReserveProvenance);Wire.Blob(w,WeaponCheckpointCodec.Capture(member.Handler,binding,member.ReserveProvenance));}
    // All actual carried weapon identities must have exactly one mechanic. Additional
    // members can describe retired/suspended mechanics, but do not assert world custody.
    foreach(var item in inventory.Items)if(catalog.Get(item.DefinitionId).IsWeapon)Wire.Require(ids.Contains(item.InstanceId));
    w.Flush();byte[] b=m.ToArray();Wire.Require(b.Length<=Wire.Limit);return b;
   }
  }
  public static bool TryRestore(byte[] untrusted,TrustedCheckpointReceipt receipt,ItemCatalog catalog,IInventoryBoundary boundary,IAuthorityGate authority,out QuarantinedInventory result)
  {
   result=null;
   try {
    Wire.Require(receipt!=null&&boundary!=null&&authority!=null&&untrusted!=null&&untrusted.Length<=Wire.Limit);
    // Pin bytes once; neither caller mutation nor a second file acquisition can mix images.
    byte[] image=(byte[])untrusted.Clone();Wire.Require(Wire.Hash(image)==receipt.Digest&&CatalogFingerprint(catalog)==receipt.Expected.Catalog);
    using(var q=Wire.Reader(image)){
     Wire.Require(q.ReadInt32()==0x31524341&&q.ReadInt32()==1&&Wire.Text(q)==Codec);receipt.Expected.Check(q);
     Inventory inventory=InventoryCheckpointCodec.Restore(Wire.Blob(q),catalog,boundary,receipt.Expected);
     var weapons=new Dictionary<string,Handler>(StringComparer.Ordinal);var reserves=new Dictionary<string,FiniteAmmoReserve>(StringComparer.Ordinal);var provenance=new HashSet<string>(StringComparer.Ordinal);
     int count=Wire.Count(q,64);for(int i=0;i<count;i++){string id=Wire.Text(q),p=Wire.Text(q);Wire.Require(id.Length>0&&p.Length>0&&!weapons.ContainsKey(id)&&provenance.Add(p));FiniteAmmoReserve reserve;var weapon=WeaponCheckpointCodec.Restore(Wire.Blob(q),receipt.Expected,p,authority,out reserve);weapons.Add(id,weapon);reserves.Add(id,reserve);}
     foreach(var item in inventory.Items)if(catalog.Get(item.DefinitionId).IsWeapon)Wire.Require(weapons.ContainsKey(item.InstanceId));Wire.End(q);
     result=new QuarantinedInventory(inventory,weapons,reserves);return true;
    }
   }catch(InvalidDataException){return false;}catch(EndOfStreamException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
  }
 }
}

