using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using System.Collections.Generic;
namespace Ashfall.Modules.Combat
{
    public enum SourceRadialFalloff { Constant, Linear }
    // Trusted catalog input: exact accepted source revision, catalog and content digest.
    // This is provenance, not authentication. Only the host simulation calls publication.
    public sealed class SourceRadialProvenance
    {
        public readonly string SourceRevision, CatalogId, ContentSha256;
        public SourceRadialProvenance(string revision,string catalog,string sha256)
        {
            if(String.IsNullOrEmpty(revision)||String.IsNullOrEmpty(catalog)||sha256==null||sha256.Length!=64)throw new ArgumentException("radial provenance");
            foreach(char c in sha256)if(!Uri.IsHexDigit(c))throw new ArgumentException("content digest");
            SourceRevision=revision;CatalogId=catalog;ContentSha256=sha256.ToLowerInvariant();
        }
    }
    public sealed class SourceRadialExplosion
    {
        public const string PolicyId="ashfall-source-radial-v1";
        internal sealed class TargetPolicy
        {
            internal readonly bool Match,Protected,Allied;
            internal TargetPolicy(bool match,bool protection,bool allied){Match=match;Protected=protection;Allied=allied;}
        }
        readonly Dictionary<string,TargetPolicy> policies=new Dictionary<string,TargetPolicy>(StringComparer.Ordinal);
        internal TargetPolicy Policy(string target){TargetPolicy p;return policies.TryGetValue(target,out p)?p:null;}
        internal void FreezeTargets(HealthService service,IHealthWorld world,string owner,string[] targets)
        {
            if(targets==null||targets.Length>256)throw new ArgumentException("radial-target-policy");
            var actor=service.Read(owner);
            foreach(string id in targets){if(policies.ContainsKey(id))continue;var victim=service.Read(id);
                if(victim==null)continue;
                if(policies.Count>=256)throw new InvalidOperationException("radial-target-policy-capacity");
                policies.Add(id,new TargetPolicy(world.InMatch,world.Now<victim.ProtectionUntil,actor!=null&&actor.Team>=0&&actor.Team==victim.Team));}
        }
        readonly SourceRadialProvenance provenance;
        public SourceRadialProvenance Provenance {get{Ready();return provenance;}}
        readonly Float3 origin;
        readonly float radius,power;
        readonly SourceRadialFalloff falloff;
        internal readonly bool IsPendingLaunch;
        void Ready(){if(IsPendingLaunch)throw new InvalidOperationException("projectile-producer-checkpoint-unsupported");}
        public Float3 Origin {get{Ready();return origin;}}
        public float Radius {get{Ready();return radius;}}
        public float Power {get{Ready();return power;}}
        public SourceRadialFalloff Falloff {get{Ready();return falloff;}}
        internal readonly string LaunchIdentity,SourceCommand;
        SourceRadialExplosion(string id,string command){IsPendingLaunch=true;LaunchIdentity=id;SourceCommand=command;}
        internal static SourceRadialExplosion PendingLaunch(string id,string command){return new SourceRadialExplosion(id,command);}
        internal SourceRadialExplosion(SourceRadialProvenance provenance,Float3 origin,float radius,float power,SourceRadialFalloff falloff)
        { this.provenance=provenance;this.origin=origin;this.radius=radius;this.power=power;this.falloff=falloff; }
    }
    public sealed partial class CombatRules
    {
        // Separate typed host publication. Generic Area and HostEnvironment cannot opt in.
        public HostEvent PublishSourceExplosion(CommandContext host,long serial,long expiresTick,WeaponLease launchedWeapon,
            SourceRadialProvenance provenance,Float3 origin,float radius,float power,SourceRadialFalloff falloff,long tick)
        {
            if(provenance==null||!origin.IsFinite||!Finite(radius)||radius<=0||!Finite(power)||power<=0||
               (falloff!=SourceRadialFalloff.Constant&&falloff!=SourceRadialFalloff.Linear))throw new ArgumentException("source radial");
            return PublishRegisteredEvent(host,serial,expiresTick,DamageSource.HostProjectile,DamageKind.Area,launchedWeapon,tick,
                new SourceRadialExplosion(provenance,origin,radius,power,falloff));
        }
    }
}
