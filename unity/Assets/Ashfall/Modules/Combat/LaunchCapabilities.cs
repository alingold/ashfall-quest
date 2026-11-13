using System;
using System.Runtime.CompilerServices;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Modules.Combat
{
    // Privileged host-lane API. A real Combat-issued lease locates its issuing rules;
    // caller booleans, transport objects and foreign leases cannot create authority.
    public sealed partial class LaunchedProjectileCapability
    {
        readonly CombatRules owner;
        readonly HostEvent[] reserved, impacts;
        readonly SourceRadialProvenance provenance;
        readonly SourceRadialExplosion[] physical;
        public readonly string LaunchId, InstanceId, OwnerId, SourceCommand;
        public readonly long EquipGeneration, LifeGeneration, ConnectionGeneration, FrameSequence, ExpiresTick;
        public readonly DamageKind Kind;
        bool validated,committed,aborted;
        bool archiveQuarantine;
        long observed;
        internal LaunchedProjectileCapability(CombatRules rules,HostEvent[] slots,string id,string command,
            long life,long connection,long frame,SourceRadialProvenance source)
        {
            owner=rules;reserved=slots;impacts=new HostEvent[slots.Length-1];physical=new SourceRadialExplosion[slots.Length-1];provenance=source;
            LaunchId=id;SourceCommand=command;var first=slots[0];InstanceId=first.Weapon.InstanceId;
            OwnerId=first.OwnerId;EquipGeneration=first.Weapon.Generation;LifeGeneration=life;
            ConnectionGeneration=connection;FrameSequence=frame;ExpiresTick=first.ExpiresTick;Kind=first.Kind;observed=first.BornTick;
        }
        public bool Committed {get{return committed;}}
        public int MaximumPhysicalSubhits {get{return impacts.Length;}}
        public RuleResult Validate(CommandContext host)
        {
            validated=false;
            if(archiveQuarantine)return RuleResult.Reject("projectile-archive-quarantine");
            if(aborted||committed||host.Tick<observed)return RuleResult.Reject("launch-reservation-stale");
            var r=owner.ValidateLaunch(host,reserved);if(!r.Accepted)return r;
            observed=host.Tick;validated=true;return RuleResult.Accept();
        }
        // Adjacent Validate-all / Commit-all on one exclusive lane. No callbacks or allocations.
        public void CommitValidated(){if(!archiveQuarantine&&!aborted&&validated)committed=true;}
        public void Abort(){if(!committed&&!aborted){aborted=true;validated=false;owner.AbortLaunch(reserved);}}
        // Snapshot actual host Health/world facts once. No caller policy flags.
        public void FreezeTargetPolicy(CommandContext host,int sub,HealthService health,IHealthWorld world,string[] targets)
        {
            if(archiveQuarantine||!committed||health==null||world==null||sub<0||sub>=impacts.Length||impacts[sub]==null||impacts[sub].SourceExplosion==null)
                throw new ArgumentException("radial-target-policy");
            owner.ValidateImpactPolicy(host,impacts[sub]);
            if(!health.Authorize(host).Accepted)throw new ArgumentException("radial-target-policy-authority");
            impacts[sub].SourceExplosion.FreezeTargets(health,world,OwnerId,targets);
        }
        public HostEvent PublishImpact(CommandContext host,int physicalSubhit,Float3 origin,float radius,float power,SourceRadialFalloff falloff)
        {
            if(archiveQuarantine||!committed||aborted||physicalSubhit<0||physicalSubhit>=impacts.Length)throw new ArgumentException("launch-impact");
            HostEvent old=impacts[physicalSubhit];var frozen=physical[physicalSubhit];
            if(frozen!=null&&(frozen.Origin.X!=origin.X||frozen.Origin.Y!=origin.Y||frozen.Origin.Z!=origin.Z||frozen.Radius!=radius||frozen.Power!=power||frozen.Falloff!=falloff))throw new ArgumentException("immutable-impact");
            var record=frozen??new SourceRadialExplosion(provenance,origin,radius,power,falloff);
            var ev=owner.PublishLaunchImpact(host,reserved[physicalSubhit],old,provenance,origin,radius,power,falloff);
            impacts[physicalSubhit]=ev;physical[physicalSubhit]=record;return ev;
        }
    }
    public sealed partial class CombatRules
    {
        static readonly ConditionalWeakTable<WeaponLease,CombatRules> launchIssuers=new ConditionalWeakTable<WeaponLease,CombatRules>();
        // Reserve all central event positions before ammo publication. This is a host
        // simulation port, never a network handler or an ammo entitlement API.
        public static RuleResult PrepareLaunchedProjectile(CommandContext host,WeaponLease weapon,string launchId,string sourceCommand,
            long life,long connection,long frame,long expires,int maximumSubhits,DamageKind kind,SourceRadialProvenance provenance,
            out LaunchedProjectileCapability prepared)
        {
            prepared=null;CombatRules rules;
            if(weapon==null||!launchIssuers.TryGetValue(weapon,out rules))return RuleResult.Reject("launch-issuer");
            return rules.PrepareLaunch(host,weapon,launchId,sourceCommand,life,connection,frame,expires,maximumSubhits,kind,provenance,out prepared);
        }
        RuleResult PrepareLaunch(CommandContext host,WeaponLease weapon,string id,string command,long life,long connection,long frame,
            long expires,int count,DamageKind kind,SourceRadialProvenance provenance,out LaunchedProjectileCapability prepared)
        {
            prepared=null;
            try {Host(host,host.Tick);}catch(ArgumentException){return RuleResult.Reject("host-publication");}
            if(!KnownWeapon(weapon)||!weapon.Active||String.IsNullOrEmpty(id)||id.Length>64||String.IsNullOrEmpty(command)||command.Length>128||
                life<0||connection<0||frame<0||count<1||count>21||expires<now||expires-now>maximumEventLifetime||
                kind!=DamageKind.Area&&kind!=DamageKind.Bullet||kind==DamageKind.Area&&provenance==null)
                return RuleResult.Reject("launch-envelope");
            // Keep an event-backed replay tombstone until original expiry. The existing
            // checkpoint writer encounters its pending marker and refuses capture,
            // rather than silently losing this new producer's cold identity.
            foreach(var existing in events.Values){var marker=existing.SourceExplosion;if(marker!=null&&marker.IsPendingLaunch&&
                (marker.LaunchIdentity==id||existing.OwnerId==weapon.OwnerId&&marker.SourceCommand==command))return RuleResult.Reject("launch-replay");}
            count++;
            if(events.Count>capacity-count||eventHighWater>Int64.MaxValue-count)return RuleResult.Reject("launch-capacity");
            var slots=new HostEvent[count];
            for(int i=0;i<count;i++)slots[i]=new HostEvent(eventHighWater+i+1,now,expires,session,epoch,weapon.OwnerId,kind,DamageSource.HostProjectile,weapon,SourceRadialExplosion.PendingLaunch(id,command));
            var token=new LaunchedProjectileCapability(this,slots,id,command,life,connection,frame,provenance);
            TrackProjectileArchive(token);
            foreach(var ev in slots)events.Add(ev.Serial,ev);eventHighWater+=count;prepared=token;return RuleResult.Accept();
        }
        internal RuleResult ValidateLaunch(CommandContext host,HostEvent[] slots)
        {
            try{Host(host,host.Tick);}catch(ArgumentException){return RuleResult.Reject("host-publication");}
            if(!KnownWeapon(slots[0].Weapon)||!slots[0].Weapon.Active)return RuleResult.Reject("weapon-retired");
            foreach(var ev in slots)if(!LiveEvent(ev))return RuleResult.Reject("launch-reservation-stale");
            return RuleResult.Accept();
        }
        internal void ValidateImpactPolicy(CommandContext host,HostEvent impact){Host(host,host.Tick);if(!LiveEvent(impact))throw new ArgumentException("launch-expired");}
        internal void AbortLaunch(HostEvent[] slots)
        {foreach(var ev in slots){HostEvent current;if(events.TryGetValue(ev.Serial,out current)&&Object.ReferenceEquals(current,ev))events.Remove(ev.Serial);}}
        internal HostEvent PublishLaunchImpact(CommandContext host,HostEvent slot,HostEvent old,SourceRadialProvenance provenance,
            Float3 origin,float radius,float power,SourceRadialFalloff falloff)
        {
            Host(host,host.Tick);
            if(!origin.IsFinite||!Finite(radius)||radius<=0||!Finite(power)||power<=0||
               falloff!=SourceRadialFalloff.Constant&&falloff!=SourceRadialFalloff.Linear)throw new ArgumentException("source radial");
            if(old!=null){var x=old.SourceExplosion;if(!LiveEvent(old)||x!=null&&(x.Origin.X!=origin.X||x.Origin.Y!=origin.Y||x.Origin.Z!=origin.Z||x.Radius!=radius||x.Power!=power||x.Falloff!=falloff))throw new ArgumentException("immutable-impact");return old;}
            if(!LiveEvent(slot))throw new ArgumentException("launch-expired");
            var radial=slot.Kind==DamageKind.Area?new SourceRadialExplosion(provenance,origin,radius,power,falloff):null;
            var ev=new HostEvent(slot.Serial,slot.BornTick,slot.ExpiresTick,session,epoch,slot.OwnerId,slot.Kind,slot.Source,slot.Weapon,radial);
            events[slot.Serial]=ev;return ev;
        }
    }
}
