using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;

namespace Ashfall.Integration
{
    // Candidate coordinator composition. Every callback must be a pure, stable host read.
    // This monitor covers HealthService mutations, not external geometry/world mutation.
    public sealed partial class CombatHealthBridge : ICombatEvidence
    {
        readonly HealthService health;
        readonly IHealthWorld world;
        readonly ICombatEvidence facts;
        readonly CombatRules rules;
        readonly string session, host;
        readonly long epoch;
        readonly int capacity;
        sealed class Collision { public HostHit Hit; public long Life, Connection; }
        readonly Dictionary<string,Collision> hits=new Dictionary<string,Collision>(StringComparer.Ordinal);
        readonly Dictionary<HostHit,Collision> collisionBindings=new Dictionary<HostHit,Collision>();
        readonly Dictionary<string,Receipt> completed=new Dictionary<string,Receipt>(StringComparer.Ordinal);
        readonly Dictionary<string,string> completedPrincipals=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly Dictionary<string,Delivery> deliveries=new Dictionary<string,Delivery>(StringComparer.Ordinal);
        readonly SortedDictionary<long,HealthTransition> outbox=new SortedDictionary<long,HealthTransition>();
        const int OutboxCapacity=16384;
        long lastTick=-1, offeredSequence;
        sealed class Delivery { public string Hit; public long Tick; }
        public sealed class Receipt
        {
            public readonly RuleResult Result;
            public readonly DamageReceipt Damage;
            public readonly int PendingHealthEvents;
            readonly HealthTransition[] events;
            public HealthTransition[] Events { get { return (HealthTransition[])events.Clone(); } }
            internal Receipt(RuleResult result,DamageReceipt damage,HealthTransition[] transitions,int pending=0)
            { Result=result;Damage=damage;events=(HealthTransition[])transitions.Clone();PendingHealthEvents=pending; }
        }
        sealed class Application : ICombatApplication
        {
            readonly HealthService health;
            readonly CommandContext context;
            readonly bool revivable;
            public DamageReceipt Applied;
            public Application(HealthService h,CommandContext c,bool revive) {health=h;context=c;revivable=revive;}
            public RuleResult TryApply(DamageDecision d)
            {
                Applied=health.ApplyDamage(context,LogicalId(d),d.TargetId,d.ExpectedRevision,d.HealthLoss,d.ShieldLoss,revivable);
                return Applied.Result;
            }
        }
        public CombatHealthBridge(HealthService service,IHealthWorld environment,ICombatEvidence hostFacts,
            IAuthorityGate authority,IInteractionQuery query,string sessionId,long authorityEpoch,string hostActor="host",int eventCapacity=4096)
        {
            if(service==null||environment==null||hostFacts==null||String.IsNullOrEmpty(hostActor)||eventCapacity<1)throw new ArgumentException("Bridge dependencies");
            health=service;world=environment;facts=hostFacts;session=sessionId;epoch=authorityEpoch;host=hostActor;capacity=eventCapacity;
            rules=new CombatRules(sessionId,authorityEpoch,256,eventCapacity,authority,this,query,hostActor);
        }
        public WeaponLease Equip(CommandContext c,string instance,string owner,string archetype,long generation,long tick)
        { return health.Serialize(()=>rules.Equip(c,instance,owner,archetype,generation,tick)); }
        public void Retire(CommandContext c,WeaponLease weapon,long tick)
        { health.Serialize(()=>{rules.RetireWeapon(c,weapon,tick);return true;}); }
        public HostEvent PublishEvent(CommandContext c,long serial,long expires,DamageSource source,DamageKind kind,WeaponLease weapon,long tick)
        { return health.Serialize(()=>rules.PublishEvent(c,serial,expires,source,kind,weapon,tick)); }
        public HostEvent PublishSourceExplosion(CommandContext c,long serial,long expires,WeaponLease weapon,
            SourceRadialProvenance provenance,Float3 origin,float radius,float power,SourceRadialFalloff falloff,long tick)
        { return health.Serialize(()=>rules.PublishSourceExplosion(c,serial,expires,weapon,provenance,origin,radius,power,falloff,tick)); }
        public HostHit PublishHit(CommandContext c,string id,HostEvent ev,string subhit,string target,long contact,long tick)
        {
            return health.Serialize(()=>{
                if(String.IsNullOrEmpty(id)||id.Length>256||completed.ContainsKey(id))throw new ArgumentException("Opaque hit identity");
                HostHit hit=rules.PublishHit(c,ev,subhit,target,contact,tick);
                Prune(tick);Collision previous;
                if(hits.TryGetValue(id,out previous)) {
                    if(!Object.ReferenceEquals(previous.Hit,hit))throw new ArgumentException("Opaque hit rebound");
                    return previous.Hit;
                }
                if(hits.Count>=capacity)throw new InvalidOperationException("Hit bindings full");
                // Opaque aliases are delivery names, not fresh physical collisions.
                // Bind the capability lifecycle once, even after target respawn/reconnect.
                Collision canonical;
                if(collisionBindings.TryGetValue(hit,out canonical)){hits.Add(id,canonical);return hit;}
                HealthSnapshot victim=health.Read(target);
                if(victim==null||!victim.Connected||victim.Life!=LifeState.Alive)throw new ArgumentException("Collision target lifecycle");
                canonical=new Collision{Hit=hit,Life=victim.LifeGeneration,Connection=victim.ConnectionGeneration};
                collisionBindings.Add(hit,canonical);hits.Add(id,canonical);return hit;
            });
        }
        void Prune(long tick)
        {
            var remove=new List<string>();foreach(var p in hits)if(tick>p.Value.Hit.Event.ExpiresTick)remove.Add(p.Key);
            foreach(string key in remove)hits.Remove(key);
            var expired=new List<HostHit>();foreach(var p in collisionBindings)if(tick>p.Key.Event.ExpiresTick)expired.Add(p.Key);
            foreach(var key in expired)collisionBindings.Remove(key);
            remove.Clear();foreach(var p in deliveries)if(tick>=p.Value.Tick&&tick-p.Value.Tick>256)remove.Add(p.Key);
            foreach(string key in remove)deliveries.Remove(key);
        }
        HealthTransition[] Capture()
        {
            foreach(var transition in health.DrainTransitionPrefix(OutboxCapacity-outbox.Count))outbox.Add(transition.Sequence,transition);
            var result=new HealthTransition[outbox.Count];outbox.Values.CopyTo(result,0);if(result.Length>0)offeredSequence=Math.Max(offeredSequence,result[result.Length-1].Sequence);return result;
        }
        Receipt Result(RuleResult result,DamageReceipt damage)
        { return new Receipt(result,damage,Capture(),health.PendingTransitionCount); }
        // Call before/after every external Health producer, and after acknowledging a full batch.
        // Durable persistence and all-producer scheduling remain host integration obligations.
        public Receipt Pump(CommandContext c)
        { return health.Serialize(()=>{RuleResult auth=health.Authorize(c);if(!auth.Accepted||c.ActorId!=host)return new Receipt(RuleResult.Reject("outbox-pump"),null,new HealthTransition[0]);return Result(health.PendingTransitionCount>OutboxCapacity-outbox.Count?RuleResult.Reject("publication-pressure"):RuleResult.Accept(),null);}); }
        public RuleResult Acknowledge(CommandContext c,long sequence)
        {
            return health.Serialize(()=>{
                RuleResult auth=health.Authorize(c);
                if(!auth.Accepted||c.ActorId!=host||sequence<0||sequence>offeredSequence)return RuleResult.Reject("outbox-ack");
                var remove=new List<long>();foreach(var p in outbox)if(p.Key<=sequence)remove.Add(p.Key);
                foreach(long id in remove)outbox.Remove(id);return RuleResult.Accept();
            });
        }
        public Receipt Apply(CommandContext c,string opaqueHit,long serverTick,bool revivable)
        {
            return health.Serialize(()=>{
                RuleResult auth=health.Authorize(c);
                if(!auth.Accepted)return new Receipt(auth,null,new HealthTransition[0]);
                if(serverTick<0||serverTick<lastTick||c.Tick>serverTick||serverTick-c.Tick>256||String.IsNullOrEmpty(opaqueHit))
                    return Result(RuleResult.Reject("bridge-clock-or-hit"),null);
                lastTick=serverTick;Prune(serverTick);
                var applicationContext=new CommandContext(session,host,"combat-app/1:"+Digest(c.ActorId,c.CommandId),c.Tick,epoch,true);
                RuleResult synchronized=health.Synchronize(applicationContext);
                if(!synchronized.Accepted)return Result(synchronized,null);
                string delivery=Digest(c.ActorId,c.CommandId);
                Delivery prior;
                if(deliveries.TryGetValue(delivery,out prior)&&prior.Hit!=opaqueHit)
                    return Result(RuleResult.Reject("delivery-rebound"),null);
                if(!deliveries.ContainsKey(delivery)) {
                    if(deliveries.Count>=4096)return Result(RuleResult.Reject("bridge-capacity"),null);
                    deliveries.Add(delivery,new Delivery{Hit=opaqueHit,Tick=c.Tick});
                }
                Receipt old;
                if(completed.TryGetValue(opaqueHit,out old)) {
                    if(completedPrincipals[opaqueHit]!=c.ActorId)return Result(RuleResult.Reject("hit-principal"),null);
                    // Reauthenticate retries; retain original event IDs for reliable redelivery.
                    return Result(old.Result,old.Damage);
                }
                Collision collision;
                if(!hits.TryGetValue(opaqueHit,out collision))return Result(RuleResult.Reject("unknown-hit"),null);
                HostHit bound=collision.Hit;
                HealthSnapshot victim=health.Read(bound.TargetId);
                if(victim==null||victim.LifeGeneration!=collision.Life||victim.ConnectionGeneration!=collision.Connection)
                    return Result(RuleResult.Reject("collision-lifecycle"),null);
                // Own all earlier producer events before admitting another damage mutation.
                Capture();
                if(health.PendingTransitionCount>0)return Result(RuleResult.Reject("publication-pressure"),null);
                if(c.ActorId!=(bound.Event.Source==DamageSource.CurrentActorIntent?bound.Event.OwnerId:host))
                    return Result(RuleResult.Reject("hit-principal"),null);
                if(completed.Count>=8192||outbox.Count>OutboxCapacity-65)
                    return Result(RuleResult.Reject("bridge-capacity"),null);
                RuleResult ready=health.PrepareDamage(applicationContext);
                if(!ready.Accepted)return Result(ready,null);
                if(!deliveries.ContainsKey(delivery))deliveries.Add(delivery,new Delivery{Hit=opaqueHit,Tick=c.Tick});
                DamageDecision decision=rules.Evaluate(c,opaqueHit,serverTick);
                if(!decision.Accepted)return Result(RuleResult.Reject(decision.Reason),null);
                var application=new Application(health,applicationContext,revivable);
                RuleResult applied=rules.Commit(decision,application,serverTick);
                Receipt receipt=new Receipt(applied,application.Applied,new HealthTransition[0]);
                // Retryable revision failures must not become terminal bridge receipts.
                if(application.Applied!=null&&!application.Applied.Retryable) {
                    completed.Add(opaqueHit,receipt);completedPrincipals.Add(opaqueHit,c.ActorId);
                }
                return Result(receipt.Result,receipt.Damage);
            });
        }
        bool ICombatEvidence.TryResolve(CommandContext c,string id,out HitEvidence evidence)
        {
            evidence=null;Collision collision;HitEvidence source;
            if(!hits.TryGetValue(id,out collision)||!facts.TryResolve(c,id,out source)||source==null||!Object.ReferenceEquals(source.Identity,collision.Hit))return false;
            HostHit identity=collision.Hit;
            HealthSnapshot target=health.Read(identity.TargetId),actor=health.Read(identity.Event.OwnerId);
            if(target==null||target.LifeGeneration!=collision.Life||target.ConnectionGeneration!=collision.Connection)return false;
            evidence=new HitEvidence {
                Identity=identity,HealthRevision=target.Revision,TargetId=identity.TargetId,Kind=identity.Event.Kind,
                WeaponId=identity.Event.Weapon==null?null:identity.Event.Weapon.Archetype,
                ActorAlive=actor!=null&&actor.Connected&&actor.Life==LifeState.Alive,ActorInputReady=source.ActorInputReady,
                TargetAlive=target.Connected&&target.Life==LifeState.Alive,MatchActive=world.InMatch,
                Protected=world.Now<target.ProtectionUntil,Allied=actor!=null&&actor.Team>=0&&actor.Team==target.Team,
                FriendlyFire=source.FriendlyFire,WardenCoreClosed=source.WardenCoreClosed,Headshot=source.Headshot,
                Health=target.Health,Shield=target.Shield,BaseDamage=source.BaseDamage,DamageMultiplier=source.DamageMultiplier,HeadRatio=source.HeadRatio,
                Origin=source.Origin,ViewOrigin=source.ViewOrigin,BladeBase=source.BladeBase,Point=source.Point,TargetHead=source.TargetHead,
                MaximumRange=source.MaximumRange,HandSpeed=source.HandSpeed,ContactEntry=source.ContactEntry,Guard=source.Guard
            };
            return true;
        }
        public static string LogicalId(DamageDecision d)
        {
            if(d==null||!d.Accepted)throw new ArgumentException("Accepted decision required");
            return "combat/1:"+Digest(d.SessionId,d.AuthorityEpoch.ToString(CultureInfo.InvariantCulture),d.LogicalEvent.ToString(CultureInfo.InvariantCulture),d.SubhitId,d.TargetId);
        }
        static string Digest(params string[] fields)
        {
            using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes,new UTF8Encoding(false,true))) {
                foreach(string field in fields) {byte[] data=new UTF8Encoding(false,true).GetBytes(field);writer.Write(data.Length);writer.Write(data);}
                writer.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
    }
}
