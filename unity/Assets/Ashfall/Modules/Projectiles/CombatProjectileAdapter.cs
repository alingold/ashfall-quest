using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration;
using Ashfall.Integration.CombatPublication;
using Ashfall.Modules.Persistence;
namespace Ashfall.Modules.Projectiles
{
    // Host composition only. Uses actual registered capabilities, never replacement rules.
    public sealed partial class CombatProjectileAdapter : IProjectileDamage,ISourceProjectileDamage,ICombatEvidence
    {
        readonly CombatHealthBridge bridge;
        readonly HealthPublicationLane publication;
        readonly HealthService health;
        readonly IHealthWorld world;
        readonly bool producerColdCheckpoint;
        readonly Dictionary<string,HealthSnapshot> targetBindings=new Dictionary<string,HealthSnapshot>(StringComparer.Ordinal);
        readonly string session,host,map;readonly long epoch;
        readonly IAuthorityGate gate;readonly bool friendlyFire;readonly float multiplier;
        readonly HashSet<WeaponLease> retired=new HashSet<WeaponLease>();
        readonly Dictionary<string,WeaponLease> weapons=new Dictionary<string,WeaponLease>(StringComparer.Ordinal);
        readonly Dictionary<string,HostEvent> launches=new Dictionary<string,HostEvent>(StringComparer.Ordinal);
        readonly Dictionary<string,HitEvidence> facts=new Dictionary<string,HitEvidence>(StringComparer.Ordinal);
        readonly Dictionary<string,string> batches=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly Dictionary<string,RuleResult> terminal=new Dictionary<string,RuleResult>(StringComparer.Ordinal);
        sealed class SourceLaunch { public WeaponLease Weapon; public long Expires; public LaunchedProjectileCapability Capability; }
        readonly Dictionary<string,SourceLaunch> sourceLaunches=new Dictionary<string,SourceLaunch>(StringComparer.Ordinal);
        readonly Dictionary<string,HostEvent> explosions=new Dictionary<string,HostEvent>(StringComparer.Ordinal);
        readonly Dictionary<string,long> explosionSerials=new Dictionary<string,long>(StringComparer.Ordinal);
        readonly SourceRadialProvenance provenance;
        long delivery,serialHighWater,launchHighWater;
        public CombatProjectileAdapter(HealthService health,IHealthWorld world,IAuthorityGate authority,IInteractionQuery structural,string sessionId,long authorityEpoch,string hostActor,string mapRevision,bool allowFriendlyFire,float damageMultiplier,SourceRadialProvenance sourceProvenance=null,AtomicSettingsStore journal=null,string checkpointBinding=null)
        {
            if(String.IsNullOrEmpty(mapRevision)||!ProjectileSimulation.Finite(damageMultiplier)||damageMultiplier<0)throw new ArgumentException("map-rules");
            session=sessionId;epoch=authorityEpoch;host=hostActor;map=mapRevision;gate=authority;friendlyFire=allowFriendlyFire;multiplier=damageMultiplier;
            provenance=sourceProvenance;this.health=health;this.world=world;producerColdCheckpoint=checkpointBinding!=null;
            bridge=new CombatHealthBridge(health,world,this,authority,structural,session,epoch,host);
            if(journal!=null)publication=new HealthPublicationLane(health,bridge,journal,session,epoch,host,2048,checkpointBinding);
            else if(checkpointBinding!=null)throw new ArgumentException("checkpoint-requires-journal");
        }
        bool Host(CommandContext c){return c.IsAuthority&&c.ActorId==host&&c.SessionId==session&&c.AuthorityEpoch==epoch&&gate.Validate(c).Accepted;}
        // Called from trusted equipped-weapon integration, not Launch or a network handler.
        public WeaponLease Equip(CommandContext c,string instance,string owner,string kind,long generation)
        {if(!Host(c)||weapons.Count>=128)throw new ArgumentException("host-weapon");var w=bridge.Equip(c,instance,owner,kind,generation,c.Tick);WeaponLease old;if(weapons.TryGetValue(instance,out old))retired.Remove(old);weapons[instance]=w;return w;}
        public void Retire(CommandContext c,WeaponLease weapon){bridge.Retire(c,weapon,c.Tick);retired.Add(weapon);}
        public RuleResult Register(CommandContext c,string id,long serial,long expires,string instance,string owner,string kind,long generation)
        {
            if(producerColdCheckpoint)return RuleResult.Reject("projectile-producer-checkpoint-unsupported");
            WeaponLease w;
            if(!Host(c)||String.IsNullOrEmpty(id)||id.Length>64||sourceLaunches.ContainsKey(id)||launches.ContainsKey(id)||launches.Count+sourceLaunches.Count>=128||instance==null||!weapons.TryGetValue(instance,out w)||retired.Contains(w)||w.OwnerId!=owner||w.Generation!=generation||w.Archetype!=kind||kind!="frag"&&kind!="smaw"&&kind!="zone")return RuleResult.Reject("projectile-registration");
            try{launches.Add(id,bridge.PublishEvent(c,serial,expires,DamageSource.HostProjectile,DamageKind.Area,w,c.Tick));serialHighWater=Math.Max(serialHighWater,serial);launchHighWater=Math.Max(launchHighWater,serial);return RuleResult.Accept();}
            catch(ArgumentException e){return RuleResult.Reject(e.Message);}catch(InvalidOperationException e){return RuleResult.Reject(e.Message);}
        }
        public RuleResult RegisterSource(CommandContext c,string id,long serial,long expires,string instance,string owner,string kind,long generation)
        {
            if(producerColdCheckpoint)return RuleResult.Reject("projectile-producer-checkpoint-unsupported");
            WeaponLease w;
            if(!Host(c)||provenance==null||String.IsNullOrEmpty(id)||id.Length>64||launches.ContainsKey(id)||sourceLaunches.ContainsKey(id)||launches.Count+sourceLaunches.Count>=128||serial<=launchHighWater||expires<c.Tick||expires-c.Tick>600||instance==null||!weapons.TryGetValue(instance,out w)||retired.Contains(w)||w.OwnerId!=owner||w.Generation!=generation||w.Archetype!=kind||kind!="frag"&&kind!="smaw"&&kind!="zone")return RuleResult.Reject("source-registration");
            var ownerState=health.Read(owner);if(ownerState==null||!ownerState.Connected||ownerState.Life!=LifeState.Alive)return RuleResult.Reject("source-owner-lifecycle");
            LaunchedProjectileCapability capability;
            // Source simulation is already a privileged host producer. It reserves
            // future physical subhits here, while the exact lease is still active.
            var admission=CombatRules.PrepareLaunchedProjectile(c,w,id,c.CommandId,ownerState.LifeGeneration,ownerState.ConnectionGeneration,0,expires,kind=="zone"?21:1,DamageKind.Area,provenance,out capability);
            if(!admission.Accepted)return admission;
            admission=capability.Validate(c);if(!admission.Accepted){capability.Abort();return admission;}
            capability.CommitValidated();
            sourceLaunches.Add(id,new SourceLaunch{Weapon=w,Expires=expires,Capability=capability});launchHighWater=serial;serialHighWater=Math.Max(serialHighWater,serial);return RuleResult.Accept();
        }
        static string Key(string id,int sub,string target){return id.Length+":"+id+sub.ToString(CultureInfo.InvariantCulture)+":"+target;}
        public RuleResult Radial(CommandContext c,string id,int sub,Float3 center,float radius,float power,bool falloff,Target[] targets,RadialPolicy policy)
        {return RadialCore(c,id,sub,center,radius,power,falloff,targets,policy,false);}
        // Freeze the actual physical batch on the host lane BEFORE retiring its lease.
        // Publishes capabilities only: no Health damage, command, or ammo debit.
        // This cannot reserve an unknown future impact at weapon-fire time.
        public RuleResult FreezeRadial(CommandContext c,string id,int sub,Float3 center,float radius,float power,bool falloff,Target[] targets,RadialPolicy policy)
        {return RadialCore(c,id,sub,center,radius,power,falloff,targets,policy,true);}
        RuleResult RadialCore(CommandContext c,string id,int sub,Float3 center,float radius,float power,bool falloff,Target[] targets,RadialPolicy policy,bool freezeOnly)
        {
            HostEvent ev=null;SourceLaunch source=null;
            if(!Host(c)||(policy!=RadialPolicy.StructuralLineOfSightStaged&&policy!=RadialPolicy.SourceRadialV1))return RuleResult.Reject("source-through-walls-unresolved-or-authority");
            bool typed=policy==RadialPolicy.SourceRadialV1;
            if(id==null||(typed?!sourceLaunches.TryGetValue(id,out source):!launches.TryGetValue(id,out ev)))return RuleResult.Reject("radial-registration-policy");
            if(c.Tick>(typed?source.Expires:ev.ExpiresTick))return RuleResult.Reject("event-expired");
            if(sub<0||sub>20||!center.IsFinite||!ProjectileSimulation.Finite(radius)||radius<=0||!ProjectileSimulation.Finite(power)||power<=0||targets==null||targets.Length>256)return RuleResult.Reject("radial-values");
            // Validate the complete immutable physical batch before capability publication.
            var unique=new Dictionary<string,Target>(StringComparer.Ordinal);
            foreach(var t in targets)
            {
                if(t==null||String.IsNullOrEmpty(t.Id)||t.Id.Length>64||t.Provenance!=map||!t.Center.IsFinite||ProjectileSimulation.Length(ProjectileSimulation.Sub(t.Center,center))>radius)return RuleResult.Reject("radial-provenance-range");
                Target old;if(unique.TryGetValue(t.Id,out old)&&ProjectileSimulation.Length(ProjectileSimulation.Sub(old.Center,t.Center))>0)return RuleResult.Reject("radial-duplicate-conflict");unique[t.Id]=t;
                HitEvidence prior;if(facts.TryGetValue(Key(id,sub,t.Id),out prior)&&(ProjectileSimulation.Length(ProjectileSimulation.Sub(prior.Origin,center))>0||ProjectileSimulation.Length(ProjectileSimulation.Sub(prior.Point,t.Center))>0||prior.MaximumRange!=radius||prior.BaseDamage!=Amount(center,t,radius,power,falloff)))return RuleResult.Reject("physical-retry-rebound");
            }
            var ordered=new List<string>(unique.Keys);ordered.Sort(StringComparer.Ordinal);
            string fingerprint=center.X.ToString("R",CultureInfo.InvariantCulture)+","+center.Y.ToString("R",CultureInfo.InvariantCulture)+","+center.Z.ToString("R",CultureInfo.InvariantCulture)+"/"+radius.ToString("R",CultureInfo.InvariantCulture)+"/"+power.ToString("R",CultureInfo.InvariantCulture)+"/"+falloff+"/"+policy;
            foreach(string targetId in ordered){var t=unique[targetId];fingerprint+="/"+targetId.Length+":"+targetId+":"+t.Center.X.ToString("R",CultureInfo.InvariantCulture)+","+t.Center.Y.ToString("R",CultureInfo.InvariantCulture)+","+t.Center.Z.ToString("R",CultureInfo.InvariantCulture);}
            string batch=Key(id,sub,"");string frozen;
            if(batches.TryGetValue(batch,out frozen)){if(frozen!=fingerprint)return RuleResult.Reject("physical-batch-rebound");}
            else {
                if(batches.Count>=4096||targetBindings.Count+ordered.Count>4096)return RuleResult.Reject("batch-capacity");
                batches.Add(batch,fingerprint);
                foreach(string targetId in ordered)targetBindings.Add(Key(id,sub,targetId),health.Read(targetId));
            }
            if(typed&&!explosions.TryGetValue(batch,out ev)) {
                long serial;
                if(!explosionSerials.TryGetValue(batch,out serial)) {
                    if(serialHighWater==Int64.MaxValue)return RuleResult.Reject("serial-overflow");
                    serial=++serialHighWater;explosionSerials.Add(batch,serial);
                }
                try {ev=source.Capability.PublishImpact(c,sub,center,radius,power,falloff?SourceRadialFalloff.Linear:SourceRadialFalloff.Constant);source.Capability.FreezeTargetPolicy(c,sub,health,world,ordered.ToArray());explosions.Add(batch,ev);}
                catch(ArgumentException e){return RuleResult.Reject(e.Message);}catch(InvalidOperationException e){return RuleResult.Reject(e.Message);}
            }
            string failure=null,retry=null;
            foreach(string targetId in ordered)
            {
                var t=unique[targetId];
                float amount=Amount(center,t,radius,power,falloff);if(amount<=0)continue;
                string key=Key(id,sub,t.Id);HitEvidence evidence;
                RuleResult cached;if(terminal.TryGetValue(key,out cached)){if(!cached.Accepted)failure=cached.Reason;continue;}
                if(!facts.TryGetValue(key,out evidence))
                {
                    HealthSnapshot bound=targetBindings[key],current=health.Read(t.Id);
                    if(bound==null||current==null||!bound.Connected||bound.Life!=LifeState.Alive||bound.LifeGeneration!=current.LifeGeneration||bound.ConnectionGeneration!=current.ConnectionGeneration){terminal[key]=RuleResult.Reject("collision-lifecycle");failure="collision-lifecycle";continue;}
                    if(facts.Count>=4096)return RuleResult.Reject("projectile-hit-capacity");
                    try
                    {
                        var hit=bridge.PublishHit(c,key,ev,sub.ToString(CultureInfo.InvariantCulture),t.Id,0,c.Tick);
                        evidence=new HitEvidence{Identity=hit,Origin=center,Point=t.Center,TargetHead=new Float3(t.Center.X,t.Center.Y+.5f,t.Center.Z),MaximumRange=radius,BaseDamage=amount,DamageMultiplier=multiplier,HeadRatio=1,FriendlyFire=friendlyFire};facts.Add(key,evidence);
                    }
                    catch(ArgumentException e){var rejection=RuleResult.Reject(e.Message);if(ProjectileDelivery.Classify(rejection)==DeliveryDisposition.TerminalNoOp){terminal[key]=rejection;failure=e.Message;}else retry=e.Message;continue;}catch(InvalidOperationException e){retry=e.Message;continue;}
                }
            }
            if(freezeOnly)return retry!=null?RuleResult.Reject(retry):failure!=null?RuleResult.Reject(failure):RuleResult.Accept();
            foreach(string targetId in ordered)
            {
                string key=Key(id,sub,targetId);HitEvidence evidence;
                if(terminal.ContainsKey(key)||!facts.TryGetValue(key,out evidence))continue;
                if(delivery==Int64.MaxValue)return RuleResult.Reject("delivery-overflow");
                var request=new CommandContext(session,host,"projectile-delivery/"+(++delivery).ToString(CultureInfo.InvariantCulture),c.Tick,epoch,true);
                // Policy rejection (ally, protection, occlusion) is a real no-damage outcome.
                RuleResult result=publication==null?bridge.Apply(request,key,c.Tick,true).Result:publication.Combat(c,request,key,c.Tick,true);
                // Freeze physical policy outcomes across late delivery and state changes.
                if(result.Accepted||result.Reason=="state"||result.Reason=="friendly-fire"||result.Reason=="occluded"||result.Reason=="collision-lifecycle"||result.Reason=="loss-or-protection")terminal[key]=result;
                if(!result.Accepted){if(ProjectileDelivery.Classify(result)==DeliveryDisposition.Retryable)retry=result.Reason;else failure=result.Reason;}
            }
            if(retry!=null)return RuleResult.Reject(retry);
            return failure==null?RuleResult.Accept():RuleResult.Reject(failure);
        }
        static float Amount(Float3 center,Target t,float radius,float power,bool falloff){return power*(falloff?(float)Math.Max(0,1-ProjectileSimulation.Length(ProjectileSimulation.Sub(t.Center,center))/radius):1);}
        public bool TryResolve(CommandContext c,string id,out HitEvidence evidence){HitEvidence frozen;if(!facts.TryGetValue(id,out frozen)){evidence=null;return false;}evidence=new HitEvidence{Identity=frozen.Identity,Origin=frozen.Origin,Point=frozen.Point,TargetHead=frozen.TargetHead,MaximumRange=frozen.MaximumRange,BaseDamage=frozen.BaseDamage,DamageMultiplier=frozen.DamageMultiplier,HeadRatio=frozen.HeadRatio,FriendlyFire=frozen.FriendlyFire};return true;}
        public RuleResult Acknowledge(CommandContext c,long sequence){return publication==null?bridge.Acknowledge(c,sequence):RuleResult.Reject("durable-ack-required");}
        // Journal ownership precedes bridge ACK. The coordinator must route ALL other
        // Health producers through this same lane; this adapter does not own their calls.
        // Cold Health/Combat restore exists upstream, but projectile simulation/fact
        // checkpoint recovery is not implemented: reopened adapters fail closed.
        public bool RecoveryRequired {get{return publication!=null&&publication.RecoveryRequired;}}
        public string CheckpointDigest {get{return publication==null?null:publication.CheckpointDigest;}}
        public RuleResult ValidateProducerRetirement(CommandContext c){return !Host(c)?RuleResult.Reject("host-publication"):RuleResult.Reject("projectile-producer-retirement-checkpoint-unsupported");}
        public RuleResult ServicePublication(CommandContext c){return publication==null?RuleResult.Reject("journal-missing"):publication.Synchronize(c);}
        public HealthPublicationLane.Packet[] OfferPublication(CommandContext c){if(publication==null)throw new InvalidOperationException("journal-missing");return publication.Offer(c);}
        public RuleResult AcknowledgePublication(CommandContext c,long sequence){return publication==null?RuleResult.Reject("journal-missing"):publication.Acknowledge(c,sequence);}
        public CombatHealthBridge.Receipt Pump(CommandContext c){return bridge.Pump(c);}
    }
}

