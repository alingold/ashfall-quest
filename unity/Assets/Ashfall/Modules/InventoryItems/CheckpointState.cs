using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.RecoveryContracts;
using Wire=Ashfall.RecoveryContracts.RecoveryWire;
namespace Ashfall.Modules.InventoryItems
{
 public sealed partial class Inventory
 {
  // Owner-private codec; raw bytes have no authority. Public cold entry is the composed factory.
  internal byte[] ExportCheckpoint(CheckpointBinding binding)
  {
   Wire.Fields(typeof(Inventory),"<Capacity>k__BackingField:System.Int32\n<Holstered>k__BackingField:System.Boolean\n<SelectedInstanceId>k__BackingField:System.String\nactor:System.String\nboundary:Ashfall.Modules.InventoryItems.IInventoryBoundary\ncatalog:Ashfall.Modules.InventoryItems.ItemCatalog\ncommands:System.Collections.Generic.HashSet`1[System.String]\nepoch:System.Int64\nexecuting:System.Boolean\ngroups:System.Collections.Generic.List`1[System.Collections.Generic.List`1[Ashfall.Modules.InventoryItems.ItemInstance]]\nhistory:System.Collections.Generic.Queue`1[System.Collections.Generic.KeyValuePair`2[System.String,System.Int64]]\nitems:System.Collections.Generic.List`1[Ashfall.Modules.InventoryItems.ItemInstance]\nknown:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.InventoryItems.ItemInstance]\nlastTick:System.Int64\npreparedBusy:System.Boolean\nretired:System.Boolean\nretiredTick:System.Int64\nrevision:System.Int64\nsession:System.String");
   Wire.Require(!executing&&!preparedBusy&&session==binding.Session&&actor==binding.Owner&&epoch==binding.Epoch);
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
    w.Write(0x31495641);w.Write(1);w.Write(Capacity);Wire.Text(w,SelectedInstanceId);w.Write(Holstered);w.Write(retired);w.Write(revision);w.Write(lastTick);w.Write(retiredTick);
    Wire.Require(known.Count<=8192);w.Write(known.Count);var ids=new List<string>(known.Keys);ids.Sort(StringComparer.Ordinal);
    foreach(string id in ids){var i=known[id];Wire.Text(w,i.InstanceId);Wire.Text(w,i.DefinitionId);w.Write(i.Stars);}
    w.Write(items.Count);foreach(var i in items)Wire.Text(w,i.InstanceId);
    w.Write(groups.Count);foreach(var g in groups){w.Write(g.Count);foreach(var i in g)Wire.Text(w,i.InstanceId);}
    Wire.Require(history.Count<=256&&commands.Count==history.Count);w.Write(history.Count);foreach(var h in history){Wire.Require(commands.Contains(h.Key));Wire.Text(w,h.Key);w.Write(h.Value);}
    w.Flush();byte[] b=m.ToArray();Wire.Require(b.Length<=Wire.Limit);RestoreCheckpoint(b,catalog,boundary,binding);return b;
   }
  }
  internal static Inventory RestoreCheckpoint(byte[] bytes,ItemCatalog catalog,IInventoryBoundary boundary,CheckpointBinding binding)
  {
   Wire.Fields(typeof(Inventory),"<Capacity>k__BackingField:System.Int32\n<Holstered>k__BackingField:System.Boolean\n<SelectedInstanceId>k__BackingField:System.String\nactor:System.String\nboundary:Ashfall.Modules.InventoryItems.IInventoryBoundary\ncatalog:Ashfall.Modules.InventoryItems.ItemCatalog\ncommands:System.Collections.Generic.HashSet`1[System.String]\nepoch:System.Int64\nexecuting:System.Boolean\ngroups:System.Collections.Generic.List`1[System.Collections.Generic.List`1[Ashfall.Modules.InventoryItems.ItemInstance]]\nhistory:System.Collections.Generic.Queue`1[System.Collections.Generic.KeyValuePair`2[System.String,System.Int64]]\nitems:System.Collections.Generic.List`1[Ashfall.Modules.InventoryItems.ItemInstance]\nknown:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.InventoryItems.ItemInstance]\nlastTick:System.Int64\npreparedBusy:System.Boolean\nretired:System.Boolean\nretiredTick:System.Int64\nrevision:System.Int64\nsession:System.String");
   var n=new Inventory(catalog,boundary,binding.Session,binding.Owner,binding.Epoch,false);
   using(var q=Wire.Reader(bytes)){
    Wire.Require(q.ReadInt32()==0x31495641&&q.ReadInt32()==1);
    n.Capacity=q.ReadInt32();Wire.Require(n.Capacity>=InitialCapacity&&n.Capacity<=MaximumCapacity);
    n.SelectedInstanceId=Wire.Text(q);n.Holstered=Wire.Bool(q);n.retired=Wire.Bool(q);n.revision=q.ReadInt64();n.lastTick=q.ReadInt64();n.retiredTick=q.ReadInt64();Wire.Require(n.revision>=0&&n.lastTick>=-1&&n.retiredTick>=-1&&n.retiredTick<=n.lastTick);
    int count=Wire.Count(q,8192);for(int j=0;j<count;j++){string id=Wire.Text(q),definition=Wire.Text(q);int stars=q.ReadInt32();ItemDefinition d;Wire.Require(id.Length>0&&!n.known.ContainsKey(id)&&catalog.TryGet(definition,out d));d=catalog.Get(definition);Wire.Require(d.Active&&(d.IsWeapon||d.Id=="banana"||d.Id=="soda"||d.Id=="shaker"));d.Stats(stars);n.known.Add(id,new ItemInstance(id,definition,stars));}
    count=Wire.Count(q,12);var members=new HashSet<string>(StringComparer.Ordinal);for(int j=0;j<count;j++){string id=Wire.Text(q);Wire.Require(n.known.ContainsKey(id)&&members.Add(id));n.items.Add(n.known[id]);}
    count=Wire.Count(q,n.Capacity);var grouped=new HashSet<string>(StringComparer.Ordinal);for(int j=0;j<count;j++){int c=Wire.Count(q,2);Wire.Require(c>0);var g=new List<ItemInstance>();string stack=null;for(int k=0;k<c;k++){string id=Wire.Text(q);Wire.Require(members.Contains(id)&&grouped.Add(id));var i=n.known[id];string key=catalog.Get(i.DefinitionId).StackKey;if(k==0)stack=key;else Wire.Require(stack.Length>0&&stack==key);g.Add(i);}n.groups.Add(g);}
    Wire.Require(grouped.SetEquals(members)&&(members.Count==0?n.SelectedInstanceId==""&&n.Holstered:members.Contains(n.SelectedInstanceId)));
    count=Wire.Count(q,256);long previous=-1;for(int j=0;j<count;j++){string id=Wire.Text(q);long tick=q.ReadInt64();Wire.Require(id.Length>0&&n.commands.Add(id)&&tick>=previous&&tick>=0&&tick<=n.lastTick);previous=tick;n.history.Enqueue(new KeyValuePair<string,long>(id,tick));}
    Wire.End(q);
   }
   return n;
  }
 }
public static class InventoryCheckpointCodec
 {
  public static byte[] Capture(Inventory owner,CheckpointBinding binding){if(owner==null||binding==null)throw new ArgumentNullException();return owner.ExportCheckpoint(binding);}
  public static Inventory Restore(byte[] bytes,ItemCatalog catalog,IInventoryBoundary boundary,CheckpointBinding binding){if(binding==null||catalog==null||boundary==null)throw new ArgumentNullException();return Inventory.RestoreCheckpoint(bytes,catalog,boundary,binding);}
 }
}
