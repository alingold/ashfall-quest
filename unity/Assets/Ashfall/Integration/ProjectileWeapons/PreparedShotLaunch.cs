using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.ProjectileWeapons
{
    // Actual mechanical shot + Combat reservation. Root supplies the canonical
    // binding; this API does not authenticate caller generations or grant content.
    public sealed class PreparedShotLaunch
    {
        readonly IPreparedWeaponFire fire;
        readonly LaunchedProjectileCapability launch;
        bool validated,aborted,committed;
        readonly string session; readonly long epoch;
        static readonly List<PreparedShotLaunch> pending=new List<PreparedShotLaunch>(128);
        // Tracks this actual aggregate token family through successful Validate.
        // Other WeaponLifetimes families remain their owner's checkpoint obligation.
        public static RuleResult ValidateColdCapture(string sessionId,long authorityEpoch)
        {
            foreach(var token in pending)if(token.session==sessionId&&token.epoch==authorityEpoch)return RuleResult.Reject("projectile-prepared-shot-outstanding");
            return RuleResult.Reject("combat-launch-capability-restoration-unavailable");
        }
        PreparedShotLaunch(IPreparedWeaponFire shot,LaunchedProjectileCapability projectile,CommandContext host){fire=shot;launch=projectile;session=host.SessionId;epoch=host.AuthorityEpoch;}
        public CommittedWeaponShot Shot {get{return committed?fire.Result:null;}}
        public LaunchedProjectileCapability Launch {get{return committed?launch:null;}}
        public static RuleResult Prepare(WeaponLifetimes weapons,IWeaponFireBinding binding,CommandContext host,CommandContext actor,
            WeaponLease lease,string launchId,HandlingInput input,long life,long connection,long expires,out PreparedShotLaunch prepared)
        {
            prepared=null;
            if(pending.Count>=128)return RuleResult.Reject("projectile-prepared-shot-capacity");
            if(weapons==null||binding==null||lease==null||lease.OwnerId!=actor.ActorId||host.SessionId!=actor.SessionId||
                host.AuthorityEpoch!=actor.AuthorityEpoch||host.Tick!=actor.Tick)return RuleResult.Reject("shot-launch-binding");
            // Current upstream has NO frag/zone/SMAW prepared mechanics. Never use
            // a rifle's round to authorize radial source content.
            if(lease.Archetype=="frag"||lease.Archetype=="zone"||lease.Archetype=="smaw")return RuleResult.Reject("source-projectile-mechanics-unsupported");
            IPreparedWeaponFire fire;
            var r=weapons.PrepareFire(actor,lease.InstanceId,input,lease.Generation,life,connection,expires,binding,out fire);
            if(!r.Accepted)return r;
            LaunchedProjectileCapability launch;
            r=CombatRules.PrepareLaunchedProjectile(host,lease,launchId,actor.CommandId,life,connection,input.Sequence,expires,1,DamageKind.Bullet,null,out launch);
            if(!r.Accepted){fire.Abort();return r;}
            prepared=new PreparedShotLaunch(fire,launch,host);pending.Add(prepared);return RuleResult.Accept();
        }
        public RuleResult Validate(CommandContext host)
        {
            validated=false;
            if(aborted||committed)return RuleResult.Reject("shot-launch-stale");
            var r=fire.Validate(host.Tick);if(!r.Accepted)return r;
            r=launch.Validate(host);if(!r.Accepted)return r;
            validated=true;return RuleResult.Accept();
        }
        // Same exclusive lane as final Validate; no operations between these commits.
        public void CommitValidated(){if(!aborted&&!committed&&validated){fire.CommitValidated();launch.CommitValidated();committed=true;pending.Remove(this);}}
        public void Abort(){if(!committed){aborted=true;validated=false;fire.Abort();launch.Abort();pending.Remove(this);}}
    }
}
