using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.InventoryTransfers;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.RecoveryContracts;
using Wire=Ashfall.RecoveryContracts.RecoveryWire;
namespace Ashfall.Integration.WorldWeaponTransfers
{
 // Read-only reconstruction. This type never adopts geometry or authorizes ingress.
 public sealed class WorldTransferHistory
 {
  readonly Dictionary<string,WorldWeaponRecord> world=new Dictionary<string,WorldWeaponRecord>(StringComparer.Ordinal);
  readonly List<WeaponWorldEvent> pending=new List<WeaponWorldEvent>();
  public WorldWeaponRecord[] Records {get{var a=new WorldWeaponRecord[world.Count];world.Values.CopyTo(a,0);return a;}}
  public WeaponWorldEvent[] Pending {get{return pending.ToArray();}}
  public long Sequence {get;private set;}
  public long Intent {get;private set;}
  public bool UnresolvedDecision {get;private set;}
  public bool UnfinishedPrepare {get;private set;}
  internal static byte[] Prefix(byte[][] frames)
  {
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
    foreach(var f in frames){Wire.Require(f!=null&&f.Length>0&&f.Length<=1048576&&m.Length+f.Length+36<=Wire.Limit);w.Write(f.Length);w.Write(f);using(var sha=System.Security.Cryptography.SHA256.Create())w.Write(sha.ComputeHash(f));}
    w.Flush();return m.ToArray();
   }
  }
  public static WorldTransferHistory Decode(byte[] prefix)
  {
   var h=new WorldTransferHistory();WeaponWorldEvent prepared=null;bool decision=false;
   using(var r=Wire.Reader(prefix)){
    while(r.BaseStream.Position<r.BaseStream.Length){
     int size=Wire.Count(r,1048576);Wire.Require(size>0&&r.BaseStream.Length-r.BaseStream.Position>=size+32L);
     byte[] frame=r.ReadBytes(size),hash=r.ReadBytes(32);using(var sha=System.Security.Cryptography.SHA256.Create())Wire.Require(Convert.ToBase64String(hash)==Convert.ToBase64String(sha.ComputeHash(frame)));
     using(var q=new BinaryReader(new MemoryStream(frame,false),new UTF8Encoding(false,true))){
      int stage=q.ReadByte();long id=q.ReadInt64();Wire.Require(id>0);
      if(stage==1){
       Wire.Require(prepared==null&&h.Intent<long.MaxValue&&id==h.Intent+1);h.Intent=id;
       long seq=q.ReadInt64();Wire.Require(h.Sequence<long.MaxValue&&seq==h.Sequence+1&&q.ReadString()=="ashfall-weapon-intent-v1");
       string session=Text(q),actor=Text(q);long epoch=q.ReadInt64();string command=Text(q);long tick=q.ReadInt64(),connection=q.ReadInt64(),life=q.ReadInt64(),deadline=q.ReadInt64();
       Wire.Require(session.Length>0&&actor.Length>0&&command.Length>0&&epoch>=0&&tick>=0&&connection>=0&&life>=0&&deadline>tick);
       Text(q);Wire.Bool(q);bool drop=Wire.Bool(q);int count=Wire.Count(q,64);Wire.Require(count>0);var records=new WorldWeaponRecord[count];var ids=new HashSet<string>(StringComparer.Ordinal);var bodies=new HashSet<string>(StringComparer.Ordinal);
       for(int i=0;i<count;i++){
        string instance=Text(q),definition=Text(q);int stars=q.ReadInt32();Wire.Require(instance.Length>0&&definition.Length>0&&stars>=1&&stars<=5&&ids.Add(instance));
        string body=Text(q),semantic=Text(q),lease=Text(q);long gen=q.ReadInt64();var pos=new Float3(Wire.Float(q),Wire.Float(q),Wire.Float(q));
        var place=new WeaponPlacement(instance,body,semantic,lease,gen,pos);Wire.Require(bodies.Add(body));
        string rs=Text(q),ra=Text(q);long re=q.ReadInt64(),rc=q.ReadInt64(),rl=q.ReadInt64(),rd=q.ReadInt64(),rg=q.ReadInt64();int reserve=q.ReadInt32();long revision=q.ReadInt64();
        Wire.Require(rs==session&&ra.Length>0&&re>=0&&rc>=0&&rl>=0&&rd>0&&rg>0&&rg<=seq&&reserve>=0&&revision>=0);
        var m=new HandlingSnapshot();m.Phase=(ReloadPhase)q.ReadInt32();m.Loaded=q.ReadInt32();m.Seated=q.ReadInt32();
        m.Chambered=Wire.Bool(q);m.SpentChamber=Wire.Bool(q);m.Supported=Wire.Bool(q);m.ClaimsSupport=Wire.Bool(q);m.TriggerReleaseRequired=Wire.Bool(q);
        m.BoltTravel=Wire.Float(q);m.RecoilDistance=Wire.Float(q);m.RecoilPitch=Wire.Float(q);m.RecoilYaw=Wire.Float(q);m.Shots=q.ReadInt64();m.ReloadPending=Wire.Bool(q);m.CooldownSeconds=Wire.Float(q);m.IdleSeconds=Wire.Float(q);m.RecoilPattern=q.ReadInt32();
        int capacity=definition=="longwatch"?WeaponHandling.MagazineCapacity(WeaponMechanism.PrecisionRifle):definition=="cairn6"?WeaponHandling.MagazineCapacity(WeaponMechanism.MagnumRevolver):0;
        Wire.Require(capacity>0&&(definition!="longwatch"||stars==1)&&(int)m.Phase>=0&&(int)m.Phase<=6&&m.Loaded>=0&&m.Loaded<=capacity&&m.Seated>=0&&m.Seated<=capacity&&m.Loaded+m.Seated<=capacity&&m.RecoilPattern>=0&&m.RecoilPattern<30&&m.Shots>=0&&m.CooldownSeconds>=0&&m.IdleSeconds>=0&&!(m.Chambered&&m.SpentChamber));
        records[i]=new WorldWeaponRecord(new ItemInstance(instance,definition,stars),place,m,reserve,rs,ra,re,rc,rl,rd,rg,revision);
        if(drop)Wire.Require(ra==actor&&re==epoch&&rc==connection&&rl==life&&rd==deadline&&rg==seq);
       }
       prepared=new WeaponWorldEvent(seq,drop,records);decision=false;
      }else if(stage==2){Wire.Require(prepared!=null&&!decision&&id==h.Intent);decision=true;}
      else if(stage==3){
       Wire.Require(prepared!=null&&decision&&id==h.Intent&&h.pending.Count<256);
       foreach(var record in prepared.Records){
        if(prepared.Dropped){Wire.Require(!h.world.ContainsKey(record.Item.InstanceId));foreach(var old in h.world.Values)Wire.Require(old.Placement.BodyId!=record.Placement.BodyId);Wire.Require(h.world.Count<64);h.world.Add(record.Item.InstanceId,record);}
        else {WorldWeaponRecord old;Wire.Require(h.world.TryGetValue(record.Item.InstanceId,out old)&&Same(old,record));h.world.Remove(record.Item.InstanceId);}
       }
       h.Sequence=prepared.Sequence;h.pending.Add(prepared);prepared=null;decision=false;
      }else if(stage==4){Wire.Require(prepared!=null&&id==h.Intent);prepared=null;decision=false;}
      else if(stage==5){Wire.Require(prepared==null);int index=h.pending.FindIndex(delegate(WeaponWorldEvent e){return e.Sequence==id;});Wire.Require(index>=0);h.pending.RemoveAt(index);}
      else Wire.Require(false);
      Wire.End(q);
     }
    }
   }
   h.UnfinishedPrepare=prepared!=null;h.UnresolvedDecision=decision;return h;
  }
  // BinaryReader's legacy string encoding is bounded by the already bounded frame;
  // tighten semantic text length as well; malformed UTF8 throws.
  static string Text(BinaryReader r){string s=r.ReadString();Wire.Require(Encoding.UTF8.GetByteCount(s)<=1024);return s;}
  internal static bool Same(WorldWeaponRecord a,WorldWeaponRecord b)
  {
   return a.Item.InstanceId==b.Item.InstanceId&&a.Item.DefinitionId==b.Item.DefinitionId&&a.Item.Stars==b.Item.Stars&&a.Placement.BodyId==b.Placement.BodyId&&a.Placement.SemanticOwner==b.Placement.SemanticOwner&&a.Placement.LeaseId==b.Placement.LeaseId&&a.Placement.Generation==b.Placement.Generation&&a.Placement.Position.Equals(b.Placement.Position)&&a.Session==b.Session&&a.Actor==b.Actor&&a.Epoch==b.Epoch&&a.ConnectionLife==b.ConnectionLife&&a.Life==b.Life&&a.Deadline==b.Deadline&&a.Generation==b.Generation&&a.Reserve==b.Reserve&&a.ReserveRevision==b.ReserveRevision&&a.Mechanics.Equals(b.Mechanics);
  }
 }
 public sealed class QuarantinedWorldTransfer
 {
  public readonly SessionWeapons Weapons;public readonly Inventory Inventory;public readonly WorldTransferHistory History;
  internal QuarantinedWorldTransfer(SessionWeapons w,Inventory i,WorldTransferHistory h){Weapons=w;Inventory=i;History=h;}
  // Deliberately no live coordinator, binding, geometry lease or adoption-success flag.
 }
 public sealed partial class WorldWeaponCoordinator
 {
  public byte[] CaptureWorldCheckpoint(CheckpointBinding binding,LifetimeCheckpointIdentity[] identities,Inventory inventory)
  {
   lock(lane){
    Wire.Fields(typeof(WorldWeaponCoordinator),"clock:Ashfall.Integration.WorldWeaponTransfers.IWeaponHostClock\ngeometry:Ashfall.Integration.WorldWeaponTransfers.IWeaponGeometry\nintent:System.Int64\njournal:Ashfall.Integration.WorldWeaponTransfers.WeaponTransferJournal\nlane:System.Object\nmaxLease:System.Int64\noutstanding:Ashfall.Integration.WorldWeaponTransfers.WorldWeaponCoordinator+Operation\npending:System.Collections.Generic.List`1[Ashfall.Integration.WorldWeaponTransfers.WeaponWorldEvent]\nrecoveryRequired:System.Boolean\nsequence:System.Int64\nweapons:Ashfall.Integration.InventoryTransfers.SessionWeapons\nworld:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Integration.WorldWeaponTransfers.WorldWeaponRecord]");
    Wire.Require(journal!=null&&!journal.Faulted&&!recoveryRequired&&outstanding==null&&binding!=null&&inventory!=null);
    byte[] prefix=WorldTransferHistory.Prefix(journal.ReadFrames());var h=WorldTransferHistory.Decode(prefix);
    Wire.Require(!h.UnfinishedPrepare&&h.Sequence==sequence&&h.Intent==intent&&h.Records.Length==world.Count&&h.Pending.Length==pending.Count);
    foreach(var record in h.Records){WorldWeaponRecord actual;Wire.Require(world.TryGetValue(record.Item.InstanceId,out actual)&&WorldTransferHistory.Same(actual,record));}
    for(int j=0;j<pending.Count;j++){
     var a=pending[j];var b=h.Pending[j];Wire.Require(a.Sequence==b.Sequence&&a.Dropped==b.Dropped&&a.Records.Length==b.Records.Length);
     for(int k=0;k<a.Records.Length;k++)Wire.Require(WorldTransferHistory.Same(a.Records[k],b.Records[k]));
    }
    byte[] custody=weapons.CaptureCustodyCheckpoint(binding,identities),items=InventoryCheckpointCodec.Capture(inventory,binding);
    ValidateMembers(weapons,inventory,h,binding,identities.Length);
    using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
     w.Write(0x35545741);w.Write(1);binding.Write(w);Wire.Blob(w,prefix);Wire.Blob(w,custody);Wire.Blob(w,items);w.Flush();Wire.Require(m.Length<=Wire.Limit);return m.ToArray();
    }
   }
  }
  // Supported envelope scope is one exact Inventory plus all retained world members.
  // Other carried owners/pending lifetimes require a future multi-owner envelope.
  static void ValidateMembers(SessionWeapons weapons,Inventory inventory,WorldTransferHistory history,CheckpointBinding binding,int expectedCount)
  {
   var ids=new HashSet<string>(StringComparer.Ordinal);int count=0;
   foreach(var record in history.Records){
    string actor;long epoch,revision;int reserve;HandlingSnapshot mechanics;
    Wire.Require(ids.Add(record.Item.InstanceId)&&weapons.WorldState(record.Item,true,out actor,out epoch,out mechanics,out reserve,out revision)&&actor==record.Actor&&epoch==record.Epoch&&reserve==record.Reserve&&revision==record.ReserveRevision&&mechanics.Equals(record.Mechanics));count++;
   }
   foreach(var item in inventory.Items){
    Wire.Require(ids.Add(item.InstanceId));
    // Nonweapon items are preserved by the actual inventory codec.
    if(item.DefinitionId=="longwatch"||item.DefinitionId=="cairn6"){
     Wire.Require(weapons.IsActive(item,binding.Owner,binding.Epoch));count++;
    }
   }
   Wire.Require(count==expectedCount);
  }
  public static QuarantinedWorldTransfer RestoreWorldCheckpoint(byte[] image,byte[] exactDurablePrefix,CheckpointBinding expected,ItemCatalog catalog,IInventoryBoundary inventoryBoundary,IWeaponLifetimeAuthority approval,IAuthorityGate authority)
  {
   Wire.Require(expected!=null&&exactDurablePrefix!=null);
   using(var r=Wire.Reader(image)){
    Wire.Require(r.ReadInt32()==0x35545741&&r.ReadInt32()==1);expected.Check(r);byte[] prefix=Wire.Blob(r);
    Wire.Require(Wire.Hash(prefix)==Wire.Hash(exactDurablePrefix));var h=WorldTransferHistory.Decode(prefix);Wire.Require(!h.UnfinishedPrepare);
    byte[] custody=Wire.Blob(r);var w=SessionWeapons.RestoreQuarantinedCustody(custody,expected,approval,authority);
    var i=InventoryCheckpointCodec.Restore(Wire.Blob(r),catalog,inventoryBoundary,expected);Wire.End(r);
    int members;using(var c=Wire.Reader(custody)){Wire.Require(c.ReadInt32()==0x31535741&&c.ReadInt32()==1);expected.Check(c);c.ReadInt32();Wire.Blob(c);members=Wire.Count(c,64);}
    ValidateMembers(w,i,h,expected,members);
    return new QuarantinedWorldTransfer(w,i,h);
   }
  }
 }
}

