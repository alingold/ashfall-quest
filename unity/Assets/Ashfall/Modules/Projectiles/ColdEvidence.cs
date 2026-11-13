using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Combat;
namespace Ashfall.Modules.Projectiles
{
    public sealed partial class CombatProjectileAdapter
    {
        // Evidence export only: it deliberately cannot reconstruct opaque Combat
        // capabilities, frozen target policies, bridge receipts or lane ACK state.
        // No caller may use these bytes as a Combat checkpoint or restore authority.
        public byte[] CaptureColdEvidence(CommandContext c)
        {
            ProjectileWire.Need(Host(c));
            ProjectileWire.Fields(typeof(SourceLaunch),"Capability Expires Weapon");
            ProjectileWire.Fields(typeof(CombatProjectileAdapter),"batches bridge delivery epoch explosionSerials explosions facts friendlyFire gate health host launchHighWater launches map multiplier producerColdCheckpoint provenance publication retired serialHighWater session sourceLaunches targetBindings terminal weapons world");
            using(var stream=new ProjectileWire.BoundedStream())using(var w=new BinaryWriter(stream)){
                w.Write(0x35455041);w.Write(1);ProjectileWire.Text(w,session);w.Write(epoch);ProjectileWire.Text(w,host);ProjectileWire.Text(w,map);w.Write(c.Tick);
                w.Write(friendlyFire);w.Write(multiplier);w.Write(producerColdCheckpoint);w.Write(delivery);w.Write(serialHighWater);w.Write(launchHighWater);
                w.Write(provenance!=null);if(provenance!=null){ProjectileWire.Text(w,provenance.SourceRevision);ProjectileWire.Text(w,provenance.CatalogId);ProjectileWire.Text(w,provenance.ContentSha256);}
                w.Write(weapons.Count);foreach(var key in Keys(weapons)){ProjectileWire.Text(w,key);Lease(w,weapons[key]);}
                var dead=new List<WeaponLease>(retired);dead.Sort((a,b)=>StringComparer.Ordinal.Compare(a.InstanceId+"/"+a.Generation,b.InstanceId+"/"+b.Generation));w.Write(dead.Count);foreach(var lease in dead)Lease(w,lease);
                w.Write(launches.Count);foreach(var key in Keys(launches)){ProjectileWire.Text(w,key);Event(w,launches[key]);}
                w.Write(sourceLaunches.Count);foreach(var key in Keys(sourceLaunches)){var s=sourceLaunches[key];var cap=s.Capability;ProjectileWire.Text(w,key);Lease(w,s.Weapon);w.Write(s.Expires);ProjectileWire.Text(w,cap.LaunchId);ProjectileWire.Text(w,cap.InstanceId);ProjectileWire.Text(w,cap.OwnerId);ProjectileWire.Text(w,cap.SourceCommand);w.Write(cap.EquipGeneration);w.Write(cap.LifeGeneration);w.Write(cap.ConnectionGeneration);w.Write(cap.FrameSequence);w.Write(cap.ExpiresTick);w.Write((int)cap.Kind);w.Write(cap.Committed);w.Write(cap.MaximumPhysicalSubhits);}
                w.Write(explosions.Count);foreach(var key in Keys(explosions)){ProjectileWire.Text(w,key);Event(w,explosions[key]);}
                w.Write(explosionSerials.Count);foreach(var key in Keys(explosionSerials)){ProjectileWire.Text(w,key);w.Write(explosionSerials[key]);}
                w.Write(batches.Count);foreach(var key in Keys(batches)){ProjectileWire.Text(w,key);LongText(w,batches[key]);}
                w.Write(targetBindings.Count);foreach(var key in Keys(targetBindings)){ProjectileWire.Text(w,key);var h=targetBindings[key];w.Write(h!=null);if(h!=null){ProjectileWire.Text(w,h.Id);w.Write(h.Team);w.Write(h.Deaths);w.Write(h.Revision);w.Write(h.LifeGeneration);w.Write(h.ConnectionGeneration);w.Write((int)h.Life);w.Write(h.Health);w.Write(h.Shield);w.Write(h.DownedUntil);w.Write(h.RespawnAt);w.Write(h.ProtectionUntil);w.Write(h.ReadyUntil);w.Write(h.Connected);w.Write(h.Charge);}}
                w.Write(facts.Count);foreach(var key in Keys(facts)){var f=facts[key];ProjectileWire.Text(w,key);Event(w,f.Identity.Event);ProjectileWire.Text(w,f.Identity.SubhitId);ProjectileWire.Text(w,f.Identity.TargetId);w.Write(f.Identity.ContactEntry);ProjectileWire.V(w,f.Origin);ProjectileWire.V(w,f.Point);ProjectileWire.V(w,f.TargetHead);w.Write(f.MaximumRange);w.Write(f.BaseDamage);w.Write(f.DamageMultiplier);w.Write(f.HeadRatio);w.Write(f.FriendlyFire);}
                w.Write(terminal.Count);foreach(var key in Keys(terminal)){ProjectileWire.Text(w,key);w.Write(terminal[key].Accepted);ProjectileWire.Text(w,terminal[key].Reason??"");}
                // A digest is only a link. Upstream owns complete pending/ACK encoding.
                w.Write(publication!=null);if(publication!=null){ProjectileWire.Text(w,publication.CheckpointDigest??"");w.Write(publication.CheckpointGeneration);w.Write(publication.RecoveryRequired);}
                w.Flush();ProjectileWire.Need(stream.Length<=ProjectileWire.Limit);return stream.ToArray();
            }
        }
        static List<string> Keys<T>(Dictionary<string,T> d){var keys=new List<string>(d.Keys);keys.Sort(StringComparer.Ordinal);return keys;}
        static void LongText(BinaryWriter w,string s){byte[] b=new System.Text.UTF8Encoding(false,true).GetBytes(s);ProjectileWire.Need(b.Length<=262144);w.Write(b.Length);w.Write(b);}
        static void Lease(BinaryWriter w,WeaponLease lease){ProjectileWire.Text(w,lease.InstanceId);ProjectileWire.Text(w,lease.OwnerId);ProjectileWire.Text(w,lease.Archetype);w.Write(lease.Generation);}
        static void Event(BinaryWriter w,HostEvent ev){w.Write(ev.Serial);w.Write(ev.BornTick);w.Write(ev.ExpiresTick);ProjectileWire.Text(w,ev.SessionId);w.Write(ev.AuthorityEpoch);ProjectileWire.Text(w,ev.OwnerId);w.Write((int)ev.Kind);w.Write((int)ev.Source);w.Write(ev.Weapon!=null);if(ev.Weapon!=null)Lease(w,ev.Weapon);}
        public RuleResult ValidateColdContinuation(CommandContext c,string evidenceDigest)
        {
            if(!Host(c))return RuleResult.Reject("host-publication");
            // A self digest is not a source-bound trusted restoration receipt.
            return RuleResult.Reject("combat-launch-capability-restoration-unavailable");
        }
    }
}
