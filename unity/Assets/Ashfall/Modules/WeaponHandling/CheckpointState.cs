using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Wire=Ashfall.RecoveryContracts.RecoveryWire;
namespace Ashfall.Modules.WeaponHandling
{
 public sealed partial class WeaponHandling
 {
  internal byte[] ExportCheckpoint(CheckpointBinding binding,string provenance)
  {
   Wire.Fields(typeof(WeaponHandling),"FireReservation:System.Object\nFireReservationExpiry:System.Int64\nactor:System.String\nauthority:Ashfall.Core.IAuthorityGate\nboltGrab:Ashfall.Core.Float3\nboltTravel:System.Single\ncapacity:System.Int32\nchambered:System.Boolean\ncontact:System.Boolean\ncooldown:System.Single\nepoch:System.Int64\nflick:Ashfall.Modules.WeaponHandling.ClosureFlick\ngrab:Ashfall.Core.Float3\ngripToggle:System.Boolean\nidle:System.Single\ninterval:System.Single\nlastShotTick:System.Int64\nlatched:System.Boolean\nloaded:System.Int32\nmechanism:Ashfall.Modules.WeaponHandling.WeaponMechanism\norder:System.Collections.Generic.Queue`1[System.String]\noverrun:System.Single\npattern:System.Int32\npending:System.Boolean\nphase:Ashfall.Modules.WeaponHandling.ReloadPhase\npitch:System.Single\npreviousGrip:System.Boolean\nrecoil:System.Single\nreleaseGrip:System.Boolean\nreleaseTrigger:System.Boolean\nreserve:Ashfall.Modules.WeaponHandling.IAmmoReserve\nretired:System.Boolean\nrevision:System.Int64\nseated:System.Int32\nseen:System.Collections.Generic.HashSet`1[System.String]\nsequence:System.Int64\nsession:System.String\nshots:System.Int64\nsourceMechanics:Ashfall.Modules.WeaponHandling.WeaponHandling+SourceMechanics\nspent:System.Boolean\nsupported:System.Boolean\ntick:System.Int64\nyaw:System.Single");
   if(sourceMechanics!=null)throw new NotSupportedException("SourceExplosive checkpoint requires proposed WeaponMechanics/2; schema1 capture refused");
   Wire.Require(session==binding.Session&&actor==binding.Owner&&epoch==binding.Epoch&&FireReservation==null);
   var finite=reserve as FiniteAmmoReserve;Wire.Require(finite!=null);
   using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
    w.Write(0x31574841);w.Write(1);w.Write((int)mechanism);w.Write(capacity);w.Write(interval);
    w.Write(FireReservationExpiry);
    w.Write(revision);
    w.Write(tick);
    w.Write(sequence);
    w.Write(shots);
    w.Write((int)phase);
    w.Write(loaded);
    w.Write(seated);
    w.Write(pattern);
    w.Write(chambered);
    w.Write(spent);
    w.Write(pending);
    w.Write(supported);
    w.Write(latched);
    w.Write(previousGrip);
    w.Write(contact);
    w.Write(releaseGrip);
    w.Write(releaseTrigger);
    w.Write(grab.X);w.Write(grab.Y);w.Write(grab.Z);
    w.Write(boltGrab.X);w.Write(boltGrab.Y);w.Write(boltGrab.Z);
    w.Write(overrun);
    w.Write(lastShotTick);
    w.Write(retired);
    w.Write(boltTravel);
    w.Write(cooldown);
    w.Write(recoil);
    w.Write(pitch);
    w.Write(yaw);
    w.Write(idle);
    w.Write(gripToggle);
    Wire.Require(order.Count<=256&&seen.Count==order.Count);w.Write(order.Count);foreach(string id in order){Wire.Require(seen.Contains(id));Wire.Text(w,id);}
    WriteFlick(w,flick);Wire.Blob(w,finite.ExportCheckpoint(provenance));w.Flush();byte[] b=m.ToArray();Wire.Require(b.Length<=Wire.Limit);FiniteAmmoReserve checkedReserve;RestoreCheckpoint(b,binding,provenance,authority,out checkedReserve);return b;
   }
  }
  internal static WeaponHandling RestoreCheckpoint(byte[] bytes,CheckpointBinding binding,string provenance,IAuthorityGate authority,out FiniteAmmoReserve restoredReserve)
  {
   Wire.Fields(typeof(WeaponHandling),"FireReservation:System.Object\nFireReservationExpiry:System.Int64\nactor:System.String\nauthority:Ashfall.Core.IAuthorityGate\nboltGrab:Ashfall.Core.Float3\nboltTravel:System.Single\ncapacity:System.Int32\nchambered:System.Boolean\ncontact:System.Boolean\ncooldown:System.Single\nepoch:System.Int64\nflick:Ashfall.Modules.WeaponHandling.ClosureFlick\ngrab:Ashfall.Core.Float3\ngripToggle:System.Boolean\nidle:System.Single\ninterval:System.Single\nlastShotTick:System.Int64\nlatched:System.Boolean\nloaded:System.Int32\nmechanism:Ashfall.Modules.WeaponHandling.WeaponMechanism\norder:System.Collections.Generic.Queue`1[System.String]\noverrun:System.Single\npattern:System.Int32\npending:System.Boolean\nphase:Ashfall.Modules.WeaponHandling.ReloadPhase\npitch:System.Single\npreviousGrip:System.Boolean\nrecoil:System.Single\nreleaseGrip:System.Boolean\nreleaseTrigger:System.Boolean\nreserve:Ashfall.Modules.WeaponHandling.IAmmoReserve\nretired:System.Boolean\nrevision:System.Int64\nseated:System.Int32\nseen:System.Collections.Generic.HashSet`1[System.String]\nsequence:System.Int64\nsession:System.String\nshots:System.Int64\nsourceMechanics:Ashfall.Modules.WeaponHandling.WeaponHandling+SourceMechanics\nspent:System.Boolean\nsupported:System.Boolean\ntick:System.Int64\nyaw:System.Single");
   restoredReserve=null;
   using(var q=Wire.Reader(bytes)){
    Wire.Require(q.ReadInt32()==0x31574841&&q.ReadInt32()==1);var mechanism=(WeaponMechanism)q.ReadInt32();Wire.Require(Enum.IsDefined(typeof(WeaponMechanism),mechanism));
    int capacity=q.ReadInt32();float interval=Wire.Float(q);
    // Fresh placeholder is never returned or used; the decoded actual reserve is constructor-bound below.
    var n=new WeaponHandling(binding.Session,binding.Owner,binding.Epoch,mechanism,new FiniteAmmoReserve(0,0),authority);
    Wire.Require(capacity==n.capacity&&interval==n.interval);
    n.FireReservationExpiry=q.ReadInt64();
    n.revision=q.ReadInt64();
    n.tick=q.ReadInt64();
    n.sequence=q.ReadInt64();
    n.shots=q.ReadInt64();
    n.phase=(ReloadPhase)q.ReadInt32();
    n.loaded=q.ReadInt32();
    n.seated=q.ReadInt32();
    n.pattern=q.ReadInt32();
    n.chambered=Wire.Bool(q);
    n.spent=Wire.Bool(q);
    n.pending=Wire.Bool(q);
    n.supported=Wire.Bool(q);
    n.latched=Wire.Bool(q);
    n.previousGrip=Wire.Bool(q);
    n.contact=Wire.Bool(q);
    n.releaseGrip=Wire.Bool(q);
    n.releaseTrigger=Wire.Bool(q);
    n.grab=new Float3(Wire.Float(q),Wire.Float(q),Wire.Float(q));
    n.boltGrab=new Float3(Wire.Float(q),Wire.Float(q),Wire.Float(q));
    n.overrun=Wire.Float(q);
    n.lastShotTick=q.ReadInt64();
    n.retired=Wire.Bool(q);
    n.boltTravel=Wire.Float(q);
    n.cooldown=Wire.Float(q);
    n.recoil=Wire.Float(q);
    n.pitch=Wire.Float(q);
    n.yaw=Wire.Float(q);
    n.idle=Wire.Float(q);
    n.gripToggle=Wire.Bool(q);
    Wire.Require(Enum.IsDefined(typeof(ReloadPhase),n.phase)&&n.loaded>=0&&n.loaded<=capacity&&n.seated>=0&&n.seated<=capacity&&n.loaded+n.seated<=capacity&&n.pattern>=0&&n.pattern<30&&n.tick>=-1&&n.sequence>=-1&&n.shots>=0&&n.revision>=0&&n.lastShotTick>=-1&&n.lastShotTick<=n.tick&&n.cooldown>=0&&n.idle>=0&&n.recoil>=0&&n.overrun>=0);
    Wire.Require((!n.chambered||n.loaded>0&&!n.spent)&&(n.seated==0||n.phase==ReloadPhase.NeedsRack)&&(mechanism==WeaponMechanism.MagnumRevolver||(n.phase!=ReloadPhase.CylinderOpen&&n.phase!=ReloadPhase.CylinderLoaded)));
    int count=Wire.Count(q,256);for(int j=0;j<count;j++){string id=Wire.Text(q);Wire.Require(id.Length>0&&id.Length<=128&&n.seen.Add(id));n.order.Enqueue(id);}
    n.flick=ReadFlick(q);var finite=FiniteAmmoReserve.RestoreCheckpoint(Wire.Blob(q),provenance);Wire.End(q);
    // This private constructor binds the actual fresh reserve and copies the exact owner state.
    var actual=new WeaponHandling(n,finite);actual.FireReservationExpiry=n.FireReservationExpiry;restoredReserve=finite;return actual;
   }
  }
  // ClosureFlick is an owner-internal helper, not another aggregate. Its frozen layout is
  // audited explicitly because the readonly source helper is not partial. No external
  // Inventory, authority, lifetime, world or reserve private state is reflected.
  static FieldInfo FlickField(string name){return typeof(ClosureFlick).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);}
  static void WriteFlick(BinaryWriter w,ClosureFlick f)
  {
   Wire.Fields(typeof(ClosureFlick),"armed:System.Boolean\nclock:System.Single\nhistory:System.Collections.Generic.List`1[Ashfall.Modules.WeaponHandling.ClosureFlick+Sample]\nneutral:System.Single");
   w.Write((float)FlickField("clock").GetValue(f));w.Write((float)FlickField("neutral").GetValue(f));w.Write((bool)FlickField("armed").GetValue(f));
   var h=(IList)FlickField("history").GetValue(f);Wire.Require(h.Count<=1024);w.Write(h.Count);foreach(object sample in h){var t=sample.GetType();Wire.Fields(t,"Forward:Ashfall.Core.Float3\nPoint:Ashfall.Core.Float3\nTime:System.Single\nUp:Ashfall.Core.Float3");foreach(string key in new[]{"Point","Forward","Up"}){var v=(Float3)t.GetField(key).GetValue(sample);Wire.Require(v.IsFinite);w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}w.Write((float)t.GetField("Time").GetValue(sample));}
  }
  static ClosureFlick ReadFlick(BinaryReader q)
  {
   Wire.Fields(typeof(ClosureFlick),"armed:System.Boolean\nclock:System.Single\nhistory:System.Collections.Generic.List`1[Ashfall.Modules.WeaponHandling.ClosureFlick+Sample]\nneutral:System.Single");
   var f=new ClosureFlick();float clock=Wire.Float(q),neutral=Wire.Float(q);Wire.Require(clock>=0&&neutral>=0);FlickField("clock").SetValue(f,clock);FlickField("neutral").SetValue(f,neutral);FlickField("armed").SetValue(f,Wire.Bool(q));var h=(IList)FlickField("history").GetValue(f);Type t=h.GetType().GetGenericArguments()[0];Wire.Fields(t,"Forward:Ashfall.Core.Float3\nPoint:Ashfall.Core.Float3\nTime:System.Single\nUp:Ashfall.Core.Float3");int count=Wire.Count(q,1024);float previous=-1;for(int j=0;j<count;j++){object sample=Activator.CreateInstance(t);foreach(string key in new[]{"Point","Forward","Up"})t.GetField(key).SetValue(sample,new Float3(Wire.Float(q),Wire.Float(q),Wire.Float(q)));float time=Wire.Float(q);Wire.Require(time>=previous&&time<=clock);previous=time;t.GetField("Time").SetValue(sample,time);h.Add(sample);}return f;
  }
 }
public static class WeaponCheckpointCodec
 {
  public static byte[] Capture(WeaponHandling owner,CheckpointBinding binding,string provenance){if(owner==null||binding==null)throw new ArgumentNullException();return owner.ExportCheckpoint(binding,provenance);}
  public static WeaponHandling Restore(byte[] bytes,CheckpointBinding binding,string provenance,IAuthorityGate authority,out FiniteAmmoReserve reserve){if(binding==null||authority==null)throw new ArgumentNullException();return WeaponHandling.RestoreCheckpoint(bytes,binding,provenance,authority,out reserve);}
 }
}

