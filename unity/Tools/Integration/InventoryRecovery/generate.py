from pathlib import Path
import json
r=Path.cwd();o=r/'campaign-staging/20261104-three-chats/unity/InventoryRecoveryR1'
assert not (r/'unity/Assets/Ashfall/Modules/InventoryItems/CheckpointState.cs').exists(), 'Construction history only; use run.py for verification'
blocks={}
for chunk in (o/'fields.txt').read_text().split('TYPE ')[1:]:
 lines=chunk.strip().splitlines();blocks[lines[0]]=lines[1:]
def guard(t):return 'Wire.Fields(typeof('+t.split('.')[-1]+'),'+json.dumps('\n'.join(sorted(blocks[t])))+');'
p=r/'unity/Assets/Ashfall/Modules/InventoryItems/CheckpointState.cs'
p.write_text('''using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Integration.InventoryRecovery;
namespace Ashfall.Modules.InventoryItems
{
 public sealed partial class Inventory
 {
  // Owner-private codec; raw bytes have no authority. Public cold entry is the composed factory.
  internal byte[] ExportCheckpoint(CheckpointBinding binding)
  {
   GUARD
   Wire.Require(!executing&&!preparedBusy&&session==binding.Session&&actor==binding.Owner&&epoch==binding.Epoch);
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
    w.Write(0x31495641);w.Write(1);w.Write(Capacity);Wire.Text(w,SelectedInstanceId);w.Write(Holstered);w.Write(retired);w.Write(revision);w.Write(lastTick);w.Write(retiredTick);
    Wire.Require(known.Count<=8192);w.Write(known.Count);var ids=new List<string>(known.Keys);ids.Sort(StringComparer.Ordinal);
    foreach(string id in ids){var i=known[id];Wire.Text(w,i.InstanceId);Wire.Text(w,i.DefinitionId);w.Write(i.Stars);}
    w.Write(items.Count);foreach(var i in items)Wire.Text(w,i.InstanceId);
    w.Write(groups.Count);foreach(var g in groups){w.Write(g.Count);foreach(var i in g)Wire.Text(w,i.InstanceId);}
    Wire.Require(history.Count<=4096&&commands.Count==history.Count);w.Write(history.Count);foreach(var h in history){Wire.Require(commands.Contains(h.Key));Wire.Text(w,h.Key);w.Write(h.Value);}
    w.Flush();byte[] b=m.ToArray();Wire.Require(b.Length<=Wire.Limit);return b;
   }
  }
  internal static Inventory RestoreCheckpoint(byte[] bytes,ItemCatalog catalog,IInventoryBoundary boundary,CheckpointBinding binding)
  {
   GUARD
   var n=new Inventory(catalog,boundary,binding.Session,binding.Owner,binding.Epoch,false);
   using(var q=Wire.Reader(bytes)){
    Wire.Require(q.ReadInt32()==0x31495641&&q.ReadInt32()==1);
    n.Capacity=q.ReadInt32();Wire.Require(n.Capacity>=InitialCapacity&&n.Capacity<=MaximumCapacity);
    n.SelectedInstanceId=Wire.Text(q);n.Holstered=Wire.Bool(q);n.retired=Wire.Bool(q);n.revision=q.ReadInt64();n.lastTick=q.ReadInt64();n.retiredTick=q.ReadInt64();Wire.Require(n.revision>=0&&n.lastTick>=-1&&n.retiredTick>=-1&&n.retiredTick<=n.lastTick);
    int count=Wire.Count(q,8192);for(int j=0;j<count;j++){string id=Wire.Text(q),definition=Wire.Text(q);int stars=q.ReadInt32();ItemDefinition d;Wire.Require(id.Length>0&&!n.known.ContainsKey(id)&&catalog.TryGet(definition,out d));d=catalog.Get(definition);Wire.Require(d.Active&&(d.IsWeapon||d.Id=="banana"||d.Id=="soda"||d.Id=="shaker"));d.Stats(stars);n.known.Add(id,new ItemInstance(id,definition,stars));}
    count=Wire.Count(q,12);var members=new HashSet<string>(StringComparer.Ordinal);for(int j=0;j<count;j++){string id=Wire.Text(q);Wire.Require(n.known.ContainsKey(id)&&members.Add(id));n.items.Add(n.known[id]);}
    count=Wire.Count(q,n.Capacity);var grouped=new HashSet<string>(StringComparer.Ordinal);for(int j=0;j<count;j++){int c=Wire.Count(q,2);Wire.Require(c>0);var g=new List<ItemInstance>();string stack=null;for(int k=0;k<c;k++){string id=Wire.Text(q);Wire.Require(members.Contains(id)&&grouped.Add(id));var i=n.known[id];string key=catalog.Get(i.DefinitionId).StackKey;if(k==0)stack=key;else Wire.Require(stack.Length>0&&stack==key);g.Add(i);}n.groups.Add(g);}
    Wire.Require(grouped.SetEquals(members)&&(members.Count==0?n.SelectedInstanceId==""&&n.Holstered:members.Contains(n.SelectedInstanceId)));
    count=Wire.Count(q,4096);long previous=-1;for(int j=0;j<count;j++){string id=Wire.Text(q);long tick=q.ReadInt64();Wire.Require(id.Length>0&&n.commands.Add(id)&&tick>=previous&&tick>=0&&tick<=n.lastTick);previous=tick;n.history.Enqueue(new KeyValuePair<string,long>(id,tick));}
    Wire.End(q);
   }
   return n;
  }
 }
}
'''.replace('GUARD',guard('Ashfall.Modules.InventoryItems.Inventory')))
# Finite reserve: ordinary functions unchanged; provenance is a root-bound opaque checkpoint name, not grant authority.
p=r/'unity/Assets/Ashfall/Modules/WeaponHandling/FiniteAmmoReserve.cs';s=p.read_text();idx=s.rfind('    }');s=s[:idx]+'''        internal byte[] ExportCheckpoint(string provenance)
        {
            Ashfall.Integration.InventoryRecovery.Wire.Require(!String.IsNullOrEmpty(provenance));
            using(var m=new System.IO.MemoryStream())using(var w=new System.IO.BinaryWriter(m)){
                Ashfall.Integration.InventoryRecovery.Wire.Text(w,provenance);w.Write(capacity);w.Write(available);w.Write(revision);return m.ToArray();}
        }
        internal static FiniteAmmoReserve RestoreCheckpoint(byte[] bytes,string provenance)
        {
            using(var q=Ashfall.Integration.InventoryRecovery.Wire.Reader(bytes)){
                Ashfall.Integration.InventoryRecovery.Wire.Require(Ashfall.Integration.InventoryRecovery.Wire.Text(q)==provenance);
                int capacity=q.ReadInt32(),available=q.ReadInt32();long revision=q.ReadInt64();
                Ashfall.Integration.InventoryRecovery.Wire.Require(capacity>=0&&capacity<=1000000&&available>=0&&available<=capacity&&revision>=0);
                var n=new FiniteAmmoReserve(capacity,available);n.revision=revision;Ashfall.Integration.InventoryRecovery.Wire.End(q);return n;}
        }
''' +s[idx:];p.write_text(s)
# Explicit generated primitive mechanical coverage. Constructor bindings / reserve / ledgers / helper handled separately.
fields=blocks['Ashfall.Modules.WeaponHandling.WeaponHandling'];exclude={'session','actor','epoch','authority','reserve','mechanism','capacity','interval','seen','order','flick','FireReservation'}
writes=[];reads=[]
for f in fields:
 name,typ=f.split(':',1)
 if name in exclude:continue
 if typ=='Ashfall.Core.Float3':
  writes+=['w.Write('+name+'.X);w.Write('+name+'.Y);w.Write('+name+'.Z);'];reads+=['n.'+name+'=new Float3(Wire.Float(q),Wire.Float(q),Wire.Float(q));']
 else:
  writes+=['w.Write(' + ('(int)' if typ.endswith('ReloadPhase') else '')+name+');']
  read={'System.Int64':'q.ReadInt64()','System.Int32':'q.ReadInt32()','System.Single':'Wire.Float(q)','System.Boolean':'Wire.Bool(q)','Ashfall.Modules.WeaponHandling.ReloadPhase':'(ReloadPhase)q.ReadInt32()'}[typ]
  reads+=['n.'+name+'='+read+';']
p=r/'unity/Assets/Ashfall/Modules/WeaponHandling/CheckpointState.cs'
p.write_text('''using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Ashfall.Core;
using Ashfall.Integration.InventoryRecovery;
namespace Ashfall.Modules.WeaponHandling
{
 public sealed partial class WeaponHandling
 {
  internal byte[] ExportCheckpoint(CheckpointBinding binding,string provenance)
  {
   GUARD
   Wire.Require(session==binding.Session&&actor==binding.Owner&&epoch==binding.Epoch&&FireReservation==null);
   var finite=reserve as FiniteAmmoReserve;Wire.Require(finite!=null);
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
    w.Write(0x31574841);w.Write(1);w.Write((int)mechanism);w.Write(capacity);w.Write(interval);
    WRITES
    Wire.Require(order.Count<=256&&seen.Count==order.Count);w.Write(order.Count);foreach(string id in order){Wire.Require(seen.Contains(id));Wire.Text(w,id);}
    WriteFlick(w,flick);Wire.Blob(w,finite.ExportCheckpoint(provenance));w.Flush();byte[] b=m.ToArray();Wire.Require(b.Length<=Wire.Limit);return b;
   }
  }
  internal static WeaponHandling RestoreCheckpoint(byte[] bytes,CheckpointBinding binding,string provenance,IAuthorityGate authority,out FiniteAmmoReserve restoredReserve)
  {
   GUARD
   restoredReserve=null;
   using(var q=Wire.Reader(bytes)){
    Wire.Require(q.ReadInt32()==0x31574841&&q.ReadInt32()==1);var mechanism=(WeaponMechanism)q.ReadInt32();Wire.Require(Enum.IsDefined(typeof(WeaponMechanism),mechanism));
    int capacity=q.ReadInt32();float interval=Wire.Float(q);
    // Fresh placeholder is never returned or used; the decoded actual reserve is constructor-bound below.
    var n=new WeaponHandling(binding.Session,binding.Owner,binding.Epoch,mechanism,new FiniteAmmoReserve(0,0),authority);
    Wire.Require(capacity==n.capacity&&interval==n.interval);
    READS
    Wire.Require(Enum.IsDefined(typeof(ReloadPhase),n.phase)&&n.loaded>=0&&n.loaded<=capacity&&n.seated>=0&&n.seated<=capacity&&n.loaded+n.seated<=capacity&&n.pattern>=0&&n.pattern<30&&n.tick>=-1&&n.sequence>=-1&&n.shots>=0&&n.revision>=0&&n.lastShotTick>=-1&&n.lastShotTick<=n.tick&&n.cooldown>=0&&n.idle>=0&&n.recoil>=0&&n.overrun>=0);
    int count=Wire.Count(q,256);for(int j=0;j<count;j++){string id=Wire.Text(q);Wire.Require(id.Length>0&&id.Length<=128&&n.seen.Add(id));n.order.Enqueue(id);}
    n.flick=ReadFlick(q);var finite=FiniteAmmoReserve.RestoreCheckpoint(Wire.Blob(q),provenance);Wire.End(q);
    // This private constructor binds the actual fresh reserve and copies the exact owner state.
    var actual=new WeaponHandling(n,finite);restoredReserve=finite;return actual;
   }
  }
  // ClosureFlick is an owner-internal helper, not another aggregate. Its frozen layout is
  // audited explicitly because the readonly source helper is not partial. No external
  // Inventory, authority, lifetime, world or reserve private state is reflected.
  static FieldInfo FlickField(string name){return typeof(ClosureFlick).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);}
  static void WriteFlick(BinaryWriter w,ClosureFlick f)
  {
   FGUARD
   w.Write((float)FlickField("clock").GetValue(f));w.Write((float)FlickField("neutral").GetValue(f));w.Write((bool)FlickField("armed").GetValue(f));
   var h=(IList)FlickField("history").GetValue(f);Wire.Require(h.Count<=1024);w.Write(h.Count);foreach(object sample in h){var t=sample.GetType();Wire.Fields(t,"Forward:Ashfall.Core.Float3\\nPoint:Ashfall.Core.Float3\\nTime:System.Single\\nUp:Ashfall.Core.Float3");foreach(string key in new[]{"Point","Forward","Up"}){var v=(Float3)t.GetField(key).GetValue(sample);Wire.Require(v.IsFinite);w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}w.Write((float)t.GetField("Time").GetValue(sample));}
  }
  static ClosureFlick ReadFlick(BinaryReader q)
  {
   FGUARD
   var f=new ClosureFlick();float clock=Wire.Float(q),neutral=Wire.Float(q);Wire.Require(clock>=0&&neutral>=0);FlickField("clock").SetValue(f,clock);FlickField("neutral").SetValue(f,neutral);FlickField("armed").SetValue(f,Wire.Bool(q));var h=(IList)FlickField("history").GetValue(f);Type t=h.GetType().GetGenericArguments()[0];Wire.Fields(t,"Forward:Ashfall.Core.Float3\\nPoint:Ashfall.Core.Float3\\nTime:System.Single\\nUp:Ashfall.Core.Float3");int count=Wire.Count(q,1024);float previous=-1;for(int j=0;j<count;j++){object sample=Activator.CreateInstance(t);foreach(string key in new[]{"Point","Forward","Up"})t.GetField(key).SetValue(sample,new Float3(Wire.Float(q),Wire.Float(q),Wire.Float(q)));float time=Wire.Float(q);Wire.Require(time>=previous&&time<=clock);previous=time;t.GetField("Time").SetValue(sample,time);h.Add(sample);}return f;
  }
 }
}
'''.replace('FGUARD',guard('Ashfall.Modules.WeaponHandling.ClosureFlick')).replace('GUARD',guard('Ashfall.Modules.WeaponHandling.WeaponHandling')).replace('WRITES','\n    '.join(writes)).replace('READS','\n    '.join(reads)))
