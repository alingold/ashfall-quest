using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
namespace Ashfall.Modules.Combat
{
    public sealed partial class CombatRules
    {
        private static void ArchiveFields()
        {
            RecoveryWire.Fields(typeof(CombatRules),"applying:System.Boolean\nauthority:Ashfall.Core.IAuthorityGate\ncapacity:System.Int32\ncommands:System.Collections.Generic.Dictionary`2[System.String,System.Int64]\nepoch:System.Int64\neventHighWater:System.Int64\nevents:System.Collections.Generic.Dictionary`2[System.Int64,Ashfall.Modules.Combat.HostEvent]\nevidence:Ashfall.Modules.Combat.ICombatEvidence\nhits:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.Combat.HostHit]\nhostActor:System.String\nmaximumAge:System.Int64\nmaximumEventLifetime:System.Int64\nnow:System.Int64\nquery:Ashfall.Core.IInteractionQuery\nsession:System.String\nweapons:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.Combat.WeaponLease]");
            RecoveryWire.Fields(typeof(WeaponLease),"Active:System.Boolean\nArchetype:System.String\nEntries:System.Collections.Generic.Dictionary`2[System.String,System.Int64]\nGeneration:System.Int64\nInstanceId:System.String\nOwnerId:System.String\nTrackerClaimed:System.Boolean");
            RecoveryWire.Fields(typeof(HostEvent),"AuthorityEpoch:System.Int64\nBornTick:System.Int64\nExpiresTick:System.Int64\nKind:Ashfall.Modules.Combat.DamageKind\nOwnerId:System.String\nSerial:System.Int64\nSessionId:System.String\nSource:Ashfall.Modules.Combat.DamageSource\nSourceExplosion:Ashfall.Modules.Combat.SourceRadialExplosion\nWeapon:Ashfall.Modules.Combat.WeaponLease");
            RecoveryWire.Fields(typeof(HostHit),"Consumed:System.Boolean\nContactEntry:System.Int64\nEvent:Ashfall.Modules.Combat.HostEvent\nPending:Ashfall.Modules.Combat.DamageDecision\nSubhitId:System.String\nTargetId:System.String");
            RecoveryWire.Fields(typeof(SourceRadialExplosion),"IsPendingLaunch:System.Boolean\nLaunchIdentity:System.String\nSourceCommand:System.String\nfalloff:Ashfall.Modules.Combat.SourceRadialFalloff\norigin:Ashfall.Core.Float3\npolicies:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.Combat.SourceRadialExplosion+TargetPolicy]\npower:System.Single\nprovenance:Ashfall.Modules.Combat.SourceRadialProvenance\nradius:System.Single");
            RecoveryWire.Fields(typeof(SourceRadialProvenance),"CatalogId:System.String\nContentSha256:System.String\nSourceRevision:System.String");
        }
        // Internal composition schema, including retired leases still referenced by launched events.
        private Dictionary<HostHit,int> WriteCheckpoint(BinaryWriter w,IEnumerable<HostHit> bindings)
        {return WriteCheckpoint(w,bindings,false);}
        private Dictionary<HostHit,int> WriteCheckpoint(BinaryWriter w,IEnumerable<HostHit> bindings,bool projectile)
        {
            ArchiveFields();
            if(applying)throw new InvalidOperationException("combat-applying");
            var hs=new List<HostHit>(hits.Values);foreach(var h in bindings)if(!hs.Contains(h))hs.Add(h);
            var es=new List<HostEvent>(events.Values);foreach(var h in hs){if(h.Pending!=null)throw new InvalidOperationException("combat-pending");if(!es.Contains(h.Event))es.Add(h.Event);}
            var ws=new List<WeaponLease>(weapons.Values);foreach(var e in es)if(e.Weapon!=null&&!ws.Contains(e.Weapon))ws.Add(e.Weapon);
            if(hs.Count>capacity*2||es.Count>capacity*3||ws.Count>capacity*4)throw new InvalidOperationException("combat-checkpoint-bounds");
            w.Write(projectile?2:1);w.Write(session);w.Write(epoch);w.Write(hostActor);w.Write(capacity);w.Write(maximumAge);w.Write(maximumEventLifetime);w.Write(now);w.Write(eventHighWater);
            w.Write(commands.Count);foreach(var p in commands){w.Write(p.Key);w.Write(p.Value);}
            w.Write(ws.Count);foreach(var p in ws){w.Write(p.InstanceId);w.Write(p.OwnerId);w.Write(p.Archetype);w.Write(p.Generation);w.Write(p.Active);w.Write(p.TrackerClaimed);w.Write(p.Entries.Count);foreach(var en in p.Entries){w.Write(en.Key);w.Write(en.Value);}}
            w.Write(weapons.Count);foreach(var p in weapons){w.Write(p.Key);w.Write(ws.IndexOf(p.Value));}
            w.Write(es.Count);foreach(var e in es){w.Write(e.Serial);w.Write(e.BornTick);w.Write(e.ExpiresTick);w.Write(e.OwnerId);w.Write((int)e.Kind);w.Write((int)e.Source);w.Write(e.Weapon==null?-1:ws.IndexOf(e.Weapon));var x=e.SourceExplosion;if(projectile){WriteProjectileMarker(w,x);continue;}w.Write(x!=null);if(x!=null){w.Write(x.Provenance.SourceRevision);w.Write(x.Provenance.CatalogId);w.Write(x.Provenance.ContentSha256);CheckpointWire.Vector(w,x.Origin);w.Write(x.Radius);w.Write(x.Power);w.Write((int)x.Falloff);}}
            w.Write(events.Count);foreach(var p in events){w.Write(p.Key);w.Write(es.IndexOf(p.Value));}
            var index=new Dictionary<HostHit,int>();w.Write(hs.Count);for(int i=0;i<hs.Count;i++){var h=hs[i];index.Add(h,i);w.Write(es.IndexOf(h.Event));w.Write(h.SubhitId);w.Write(h.TargetId);w.Write(h.ContactEntry);w.Write(h.Consumed);}
            w.Write(hits.Count);foreach(var p in hits){w.Write(p.Key);w.Write(index[p.Value]);}return index;
        }
        // Target is a new unpublished CombatRules created by the cold bridge factory.
        private HostHit[] ReadCheckpoint(BinaryReader r)
        {return ReadCheckpoint(r,false);}
        private HostHit[] ReadCheckpoint(BinaryReader r,bool projectile)
        {
            ArchiveFields();
            if(commands.Count!=0||weapons.Count!=0||events.Count!=0||hits.Count!=0||now!=-1)throw new InvalidOperationException("combat-pristine");
            if(r.ReadInt32()!=(projectile?2:1)||CheckpointWire.Text(r)!=session||r.ReadInt64()!=epoch||CheckpointWire.Text(r)!=hostActor||r.ReadInt32()!=capacity||r.ReadInt64()!=maximumAge||r.ReadInt64()!=maximumEventLifetime)throw new InvalidDataException("combat-identity");
            now=CheckpointWire.Number(r,-1,Int64.MaxValue);eventHighWater=CheckpointWire.Number(r,0,Int64.MaxValue);
            int n=CheckpointWire.Count(r,capacity);for(int i=0;i<n;i++)commands.Add(CheckpointWire.Text(r),CheckpointWire.Number(r,0,now));
            var ws=new WeaponLease[CheckpointWire.Count(r,capacity*4)];
            for(int i=0;i<ws.Length;i++){var p=new WeaponLease(CheckpointWire.Text(r),CheckpointWire.Text(r),CheckpointWire.Text(r),CheckpointWire.Number(r,1,Int64.MaxValue));p.Active=RecoveryWire.Bool(r);p.TrackerClaimed=RecoveryWire.Bool(r);n=CheckpointWire.Count(r,capacity);for(int j=0;j<n;j++)p.Entries.Add(CheckpointWire.Text(r),CheckpointWire.Number(r,0,Int64.MaxValue));ws[i]=p;}
            n=CheckpointWire.Count(r,capacity);for(int i=0;i<n;i++){string key=CheckpointWire.Text(r);var p=ws[Index(r,ws.Length)];if(key!=p.InstanceId)throw new InvalidDataException("combat-lease-key");weapons.Add(key,p);}
            var es=new HostEvent[CheckpointWire.Count(r,capacity*3)];var serials=new HashSet<long>();
            for(int i=0;i<es.Length;i++) {
                long serial=CheckpointWire.Number(r,1,eventHighWater),born=CheckpointWire.Number(r,0,Math.Max(now,0)),expires=CheckpointWire.Number(r,born,Int64.MaxValue);string owner=CheckpointWire.Text(r);
                var kind=(DamageKind)CheckpointWire.Count(r,3);var source=(DamageSource)CheckpointWire.Count(r,2);int wi=r.ReadInt32();if(wi< -1||wi>=ws.Length||!serials.Add(serial)||expires-born>maximumEventLifetime)throw new InvalidDataException("combat-event");var weapon=wi<0?null:ws[wi];SourceRadialExplosion radial=null;
                if(projectile)radial=ReadProjectileMarker(r);
                else if(RecoveryWire.Bool(r)){var provenance=new SourceRadialProvenance(CheckpointWire.Text(r),CheckpointWire.Text(r),CheckpointWire.Text(r));var origin=CheckpointWire.Vector(r);float radius=CheckpointWire.Amount(r,Single.MaxValue),power=CheckpointWire.Amount(r,Single.MaxValue);var falloff=(SourceRadialFalloff)CheckpointWire.Count(r,1);if(radius<=0||power<=0||source!=DamageSource.HostProjectile||kind!=DamageKind.Area)throw new InvalidDataException("combat-radial");radial=new SourceRadialExplosion(provenance,origin,radius,power,falloff);}
                if(source==DamageSource.HostEnvironment?(kind!=DamageKind.Zone||weapon!=null||owner!=hostActor):(weapon==null||weapon.OwnerId!=owner||kind==DamageKind.Zone||source==DamageSource.HostProjectile&&kind==DamageKind.Melee))throw new InvalidDataException("combat-event-binding");
                es[i]=new HostEvent(serial,born,expires,session,epoch,owner,kind,source,weapon,radial);
            }
            n=CheckpointWire.Count(r,capacity);for(int i=0;i<n;i++){long key=r.ReadInt64();var e=es[Index(r,es.Length)];if(key!=e.Serial)throw new InvalidDataException("combat-event-key");events.Add(key,e);}
            var hs=new HostHit[CheckpointWire.Count(r,capacity*2)];var physical=new HashSet<string>();for(int i=0;i<hs.Length;i++){var e=es[Index(r,es.Length)];string sub=CheckpointWire.Text(r),target=CheckpointWire.Text(r);if(projectile&&e.SourceExplosion!=null&&e.SourceExplosion.IsPendingLaunch)throw new InvalidDataException("projectile-pending-hit");long entry=CheckpointWire.Number(r,0,Int64.MaxValue);var h=new HostHit(e,sub,target,entry);h.Consumed=RecoveryWire.Bool(r);if(e.Kind==DamageKind.Melee&&entry==0||!physical.Add(HitKey(e,sub,target)))throw new InvalidDataException("combat-physical-hit");hs[i]=h;}
            n=CheckpointWire.Count(r,capacity);for(int i=0;i<n;i++){string key=CheckpointWire.Text(r);var h=hs[Index(r,hs.Length)];if(key!=HitKey(h.Event,h.SubhitId,h.TargetId)||!events.ContainsKey(h.Event.Serial))throw new InvalidDataException("combat-hit-key");hits.Add(key,h);}return hs;
        }
        // Archive indices are immutable; callers cannot alter the owner's mapping.
        public sealed class ArchiveBindings
        {
            readonly Dictionary<HostHit,int> indices;
            internal ArchiveBindings(Dictionary<HostHit,int> value){indices=value;}
            public int this[HostHit hit] {get{return indices[hit];}}
        }
        public ArchiveBindings CaptureArchive(BinaryWriter writer,IEnumerable<HostHit> retained)
        {return new ArchiveBindings(WriteCheckpoint(writer,retained));}
        // Always reconstruct a NEW unpublished owner. Never import into a live rules object.
        public static CombatRules RestoreArchive(BinaryReader reader,string session,long epoch,long maxAge,int capacity,
            IAuthorityGate authority,ICombatEvidence facts,IInteractionQuery query,string host,out HostHit[] bindings)
        {
            bindings=null;
            var value=new CombatRules(session,epoch,maxAge,capacity,authority,facts,query,host);
            var decoded=value.ReadCheckpoint(reader);bindings=decoded;return value;
        }
        private static class CheckpointWire
        {
        internal static int Count(BinaryReader r,int maximum){int n=r.ReadInt32();if(n<0||n>maximum)throw new InvalidDataException("checkpoint-count");return n;}
        internal static long Number(BinaryReader r,long minimum,long maximum){long n=r.ReadInt64();if(n<minimum||n>maximum)throw new InvalidDataException("checkpoint-number");return n;}
        internal static string Text(BinaryReader r){return Text(r,512,false);}
        internal static string Text(BinaryReader r,int maximum,bool empty)
        {
            uint length=0;int shift=0;
            while(true){byte b=r.ReadByte();if(shift==28&&(b&240)!=0)throw new InvalidDataException("checkpoint-string-length");length|=(uint)(b&127)<<shift;if((b&128)==0)break;shift+=7;if(shift>28)throw new InvalidDataException("checkpoint-string-length");}
            if(length>(uint)maximum*4||length>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("checkpoint-string-length");
            byte[] bytes=r.ReadBytes((int)length);string s=new UTF8Encoding(false,true).GetString(bytes);
            if((!empty&&s.Length==0)||s.Length>maximum)throw new InvalidDataException("checkpoint-string");return s;
        }
        internal static float Amount(BinaryReader r,float max){float n=r.ReadSingle();if(Single.IsNaN(n)||Single.IsInfinity(n)||n<0||n>max)throw new InvalidDataException("checkpoint-amount");return n;}
        internal static double Time(BinaryReader r){double n=r.ReadDouble();if(Double.IsNaN(n)||Double.IsInfinity(n)||n<0)throw new InvalidDataException("checkpoint-time");return n;}
        internal static void Vector(BinaryWriter w,Float3 p){w.Write(p.X);w.Write(p.Y);w.Write(p.Z);}
        internal static Float3 Vector(BinaryReader r){var v=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());if(!v.IsFinite)throw new InvalidDataException("checkpoint-vector");return v;}
        }
        public static int Index(BinaryReader r,int length){int index=r.ReadInt32();if(index<0||index>=length)throw new InvalidDataException("checkpoint-index");return index;}
    }
}

