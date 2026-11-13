using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
namespace Ashfall.Modules.Combat
{
    public sealed partial class LaunchedProjectileCapability
    {
        internal long ArchiveObserved {get{return observed;}}
        internal bool ArchiveAborted {get{return aborted;}}
        internal bool ArchiveOwnedBy(CombatRules rules){return Object.ReferenceEquals(owner,rules);}
        internal HostEvent[] ArchiveSlots {get{return reserved;}}
        internal void WritePendingArchive(BinaryWriter w)
        {
            RecoveryWire.Fields(typeof(LaunchedProjectileCapability),"ConnectionGeneration:System.Int64\nEquipGeneration:System.Int64\nExpiresTick:System.Int64\nFrameSequence:System.Int64\nInstanceId:System.String\nKind:Ashfall.Modules.Combat.DamageKind\nLaunchId:System.String\nLifeGeneration:System.Int64\nOwnerId:System.String\nSourceCommand:System.String\naborted:System.Boolean\narchiveQuarantine:System.Boolean\ncommitted:System.Boolean\nimpacts:Ashfall.Modules.Combat.HostEvent[]\nobserved:System.Int64\nowner:Ashfall.Modules.Combat.CombatRules\nphysical:Ashfall.Modules.Combat.SourceRadialExplosion[]\nprovenance:Ashfall.Modules.Combat.SourceRadialProvenance\nreserved:Ashfall.Modules.Combat.HostEvent[]\nvalidated:System.Boolean");
            if(!committed||aborted)throw new InvalidOperationException("projectile-prepared-reservation");
            // SourceRadialExplosion has no owner policy decode port. Never drop a
            // realized impact, frozen policy or a partially acknowledged target.
            for(int i=0;i<impacts.Length;i++)if(impacts[i]!=null||physical[i]!=null)throw new InvalidOperationException("projectile-realized-policy-owner-port-missing");
            w.Write(LaunchId);w.Write(SourceCommand);w.Write(LifeGeneration);w.Write(ConnectionGeneration);w.Write(FrameSequence);w.Write(observed);
            w.Write(provenance!=null);if(provenance!=null){w.Write(provenance.SourceRevision);w.Write(provenance.CatalogId);w.Write(provenance.ContentSha256);}
            w.Write(reserved.Length);foreach(var e in reserved)w.Write(e.Serial);
        }
        internal static LaunchedProjectileCapability ReadPendingArchive(BinaryReader r,CombatRules rules,Dictionary<long,HostEvent> events,HashSet<long> used)
        {
            string id=CombatRules.ProjectileText(r,64),command=CombatRules.ProjectileText(r,128);
            long life=r.ReadInt64(),connection=r.ReadInt64(),frame=r.ReadInt64(),observed=r.ReadInt64();
            SourceRadialProvenance source=null;
            if(RecoveryWire.Bool(r)){string revision=CombatRules.ProjectileText(r,512),catalog=CombatRules.ProjectileText(r,512),sha=CombatRules.ProjectileText(r,64);source=new SourceRadialProvenance(revision,catalog,sha);RecoveryWire.Require(source.ContentSha256==sha);}
            int count=RecoveryWire.Count(r,22);RecoveryWire.Require(count>=2);
            var slots=new HostEvent[count];
            for(int i=0;i<count;i++){
                long serial=r.ReadInt64();HostEvent e;RecoveryWire.Require(events.TryGetValue(serial,out e)&&used.Add(serial));slots[i]=e;
                var x=e.SourceExplosion;
                RecoveryWire.Require(x!=null&&x.IsPendingLaunch&&x.LaunchIdentity==id&&x.SourceCommand==command&&e.Source==DamageSource.HostProjectile);
                if(i>0)RecoveryWire.Require(e.Serial==slots[0].Serial+i&&Object.ReferenceEquals(e.Weapon,slots[0].Weapon)&&e.BornTick==slots[0].BornTick&&e.ExpiresTick==slots[0].ExpiresTick&&e.Kind==slots[0].Kind);
            }
            RecoveryWire.Require(life>=0&&connection>=0&&frame>=0&&observed>=slots[0].BornTick&&observed<=slots[0].ExpiresTick&&(slots[0].Kind==DamageKind.Bullet||slots[0].Kind==DamageKind.Area&&source!=null));
            var value=new LaunchedProjectileCapability(rules,slots,id,command,life,connection,frame,source);
            value.observed=observed;value.committed=true;value.archiveQuarantine=true;return value;
        }
    }
    public sealed partial class CombatRules
    {
        sealed class ProjectileArchiveStream:MemoryStream
        {
            public override void Write(byte[] buffer,int offset,int count){RecoveryWire.Require(count>=0&&Position<=RecoveryWire.Limit-count);base.Write(buffer,offset,count);}
            public override void WriteByte(byte value){RecoveryWire.Require(Position<RecoveryWire.Limit);base.WriteByte(value);}
            public override void SetLength(long value){RecoveryWire.Require(value>=0&&value<=RecoveryWire.Limit);base.SetLength(value);}
        }
        sealed class ProjectileArchiveWriter:BinaryWriter
        {
            internal ProjectileArchiveWriter(Stream stream):base(stream,new UTF8Encoding(false,true)){}
            public override void Write(string value){RecoveryWire.Require(!String.IsNullOrEmpty(value)&&value.Length<=512);base.Write(value);}
        }
        static readonly ConditionalWeakTable<CombatRules,List<LaunchedProjectileCapability>> projectileArchives=new ConditionalWeakTable<CombatRules,List<LaunchedProjectileCapability>>();
        void TrackProjectileArchive(LaunchedProjectileCapability value)
        {
            var list=projectileArchives.GetOrCreateValue(this);
            // Only terminal aborted/expired committed tokens may leave this local registry.
            list.RemoveAll(x=>x.ArchiveAborted||x.Committed&&x.ExpiresTick<now);
            if(list.Count>=capacity)throw new InvalidOperationException("projectile-archive-capacity");list.Add(value);
        }
        static void WriteProjectileMarker(BinaryWriter w,SourceRadialExplosion x)
        {
            if(x!=null&&!x.IsPendingLaunch)throw new InvalidOperationException("projectile-realized-policy-owner-port-missing");
            w.Write(x!=null);if(x!=null){w.Write(x.LaunchIdentity);w.Write(x.SourceCommand);}
        }
        static SourceRadialExplosion ReadProjectileMarker(BinaryReader r)
        {return RecoveryWire.Bool(r)?SourceRadialExplosion.PendingLaunch(ProjectileText(r,64),ProjectileText(r,128)):null;}
        internal static string ProjectileText(BinaryReader r,int maximum){return CheckpointWire.Text(r,maximum,false);}
        // A detached owner graph. There is deliberately no rules/lease publication port.
        public sealed class PendingProjectileArchive
        {
            readonly CombatRules owner;
            readonly LaunchedProjectileCapability[] launches;
            readonly HostHit[] hits;
            readonly string build,catalog;readonly long generation;
            internal PendingProjectileArchive(CombatRules rules,LaunchedProjectileCapability[] values,HostHit[] bindings,string sourceBuild,string sourceCatalog,long sourceGeneration){owner=rules;launches=values;hits=bindings;build=sourceBuild;catalog=sourceCatalog;generation=sourceGeneration;}
            public LaunchedProjectileCapability[] Launches {get{return (LaunchedProjectileCapability[])launches.Clone();}}
            public HostHit[] HitBindings {get{return (HostHit[])hits.Clone();}}
            public byte[] Recapture(){return owner.CapturePendingProjectileArchive(build,catalog,generation,hits);}
        }
        // APCB/schema1 contains Combat/2. Combat/1 and composed checkpoint/3 stay unchanged.
        // Root must pin build/catalog/generation plus the full digest independently.
        public byte[] CapturePendingProjectileArchive(string build,string catalog,long generation,IEnumerable<HostHit> retained)
        {
            if(String.IsNullOrEmpty(build)||String.IsNullOrEmpty(catalog)||generation<1||retained==null)throw new ArgumentException("projectile-archive-binding");
            List<LaunchedProjectileCapability> registered;projectileArchives.TryGetValue(this,out registered);
            var launches=new List<LaunchedProjectileCapability>();var slots=new HashSet<long>();
            if(registered!=null)foreach(var x in registered){
                if(x.ArchiveAborted)continue;
                if(!x.Committed)throw new InvalidOperationException("projectile-prepared-reservation");
                if(x.ExpiresTick<now)continue;
                if(!x.ArchiveOwnedBy(this))throw new InvalidOperationException("projectile-foreign-owner");
                foreach(var e in x.ArchiveSlots){HostEvent current;if(!events.TryGetValue(e.Serial,out current)||!Object.ReferenceEquals(current,e)||!slots.Add(e.Serial))throw new InvalidOperationException("projectile-slot-binding");}
                launches.Add(x);
            }
            foreach(var e in events.Values)if(e.SourceExplosion!=null&&e.SourceExplosion.IsPendingLaunch&&!slots.Contains(e.Serial))throw new InvalidOperationException("projectile-untracked-launch");
            using(var m=new ProjectileArchiveStream())using(var w=new ProjectileArchiveWriter(m)){
                w.Write(0x42435041);w.Write(1);w.Write(build);w.Write(catalog);w.Write(generation);
                WriteCheckpoint(w,retained,true);w.Write(launches.Count);foreach(var x in launches)x.WritePendingArchive(w);
                w.Flush();RecoveryWire.Require(m.Length<=RecoveryWire.Limit);return m.ToArray();
            }
        }
        public static PendingProjectileArchive RestorePendingProjectileArchive(byte[] bytes,string expectedDigest,string build,string catalog,long generation,
            string session,long epoch,long maxAge,int capacity,IAuthorityGate gate,ICombatEvidence facts,IInteractionQuery query,string host)
        {
            RecoveryWire.Require(bytes!=null&&bytes.Length<=RecoveryWire.Limit&&expectedDigest!=null&&RecoveryWire.Hash(bytes)==expectedDigest&&generation>0&&!String.IsNullOrEmpty(build)&&!String.IsNullOrEmpty(catalog));
            using(var r=RecoveryWire.Reader(bytes)){
                RecoveryWire.Require(r.ReadInt32()==0x42435041&&r.ReadInt32()==1&&ProjectileText(r,512)==build&&ProjectileText(r,512)==catalog&&r.ReadInt64()==generation);
                var rules=new CombatRules(session,epoch,maxAge,capacity,gate,facts,query,host);var hits=rules.ReadCheckpoint(r,true);
                var values=new LaunchedProjectileCapability[RecoveryWire.Count(r,capacity)];var used=new HashSet<long>();var ids=new HashSet<string>(StringComparer.Ordinal);var commands=new HashSet<string>(StringComparer.Ordinal);
                for(int i=0;i<values.Length;i++){
                    var x=LaunchedProjectileCapability.ReadPendingArchive(r,rules,rules.events,used);
                    RecoveryWire.Require(ids.Add(x.LaunchId)&&commands.Add(Key(x.OwnerId,x.SourceCommand))&&x.ExpiresTick>=rules.now&&x.ArchiveObserved<=rules.now);values[i]=x;rules.TrackProjectileArchive(x);
                }
                foreach(var e in rules.events.Values)if(e.SourceExplosion!=null&&e.SourceExplosion.IsPendingLaunch)RecoveryWire.Require(used.Contains(e.Serial));
                RecoveryWire.End(r);
                // Reject alternate string encodings, ordering, aliases and all other
                // noncanonical images against the actual owner re-encoder.
                RecoveryWire.Require(RecoveryWire.Hash(rules.CapturePendingProjectileArchive(build,catalog,generation,hits))==expectedDigest);
                return new PendingProjectileArchive(rules,values,hits,build,catalog,generation);
            }
        }
    }
}
