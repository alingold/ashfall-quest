using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.Combat
{
    // One instance per session/authority epoch; call serially on the host simulation thread.
    // Evaluate reserves commands; Commit consumes hits only after atomic application.
    // Stale health revisions need a fresh snapshot/command before event expiry.
    public sealed partial class CombatRules
    {
        private readonly string session;
        private readonly long epoch, maximumAge;
        private readonly IAuthorityGate authority;
        private readonly ICombatEvidence evidence;
        private readonly IInteractionQuery query;
        private readonly int capacity;
        private readonly Dictionary<string,long> commands=new Dictionary<string,long>(StringComparer.Ordinal);
        private readonly Dictionary<string,WeaponLease> weapons=new Dictionary<string,WeaponLease>(StringComparer.Ordinal);
        private readonly Dictionary<long,HostEvent> events=new Dictionary<long,HostEvent>();
        private readonly Dictionary<string,HostHit> hits=new Dictionary<string,HostHit>(StringComparer.Ordinal);
        private readonly string hostActor;
        private readonly long maximumEventLifetime;
        private long eventHighWater;
        private bool applying;
        public const string ApiVersion="Combat-r2/2";
        private long now=-1;
        public CombatRules(string sessionId,long authorityEpoch,long maxAgeTicks,int replayCapacity,IAuthorityGate gate,ICombatEvidence source,IInteractionQuery interaction,string hostActorId="host",long maxEventLifetimeTicks=600)
        {
            if(String.IsNullOrEmpty(sessionId)||authorityEpoch<0||maxAgeTicks<0||replayCapacity<1||gate==null||source==null||interaction==null||String.IsNullOrEmpty(hostActorId)||maxEventLifetimeTicks<0) throw new ArgumentException("Combat configuration");
            hostActor=hostActorId;maximumEventLifetime=maxEventLifetimeTicks;session=sessionId;epoch=authorityEpoch;maximumAge=maxAgeTicks;capacity=replayCapacity;authority=gate;evidence=source;query=interaction;
        }
        // Trusted host lifecycle API, not network intent handlers. Serial is strictly
        // increasing for this session/epoch even after expired event records are pruned.
        private void Host(CommandContext c,long tick)
        {
            if(applying||!c.IsAuthority||c.ActorId!=hostActor||c.SessionId!=session||c.AuthorityEpoch!=epoch||
               tick<0||tick<now||c.Tick!=tick||!authority.Validate(c).Accepted)throw new ArgumentException("host-publication");
            now=tick;PruneEvents();
        }
        public WeaponLease Equip(CommandContext host,string instance,string owner,string archetype,long generation,long tick)
        {
            Host(host,tick);WeaponLease previous;
            if(String.IsNullOrEmpty(instance)||String.IsNullOrEmpty(owner)||String.IsNullOrEmpty(archetype)||generation<1)
                throw new ArgumentException("weapon");
            if(weapons.TryGetValue(instance,out previous))
            {
                if(generation<=previous.Generation)throw new ArgumentException("equip-generation");
                previous.Active=false;
            }
            else if(weapons.Count>=capacity)throw new InvalidOperationException("weapon-capacity");
            var lease=new WeaponLease(instance,owner,archetype,generation);weapons[instance]=lease;launchIssuers.Add(lease,this);return lease;
        }
        public void RetireWeapon(CommandContext host,WeaponLease weapon,long tick)
        {
            Host(host,tick);
            if(!KnownWeapon(weapon))throw new ArgumentException("weapon");
            weapon.Active=false; // Keep high generation; unrelated ledgers remain intact.
        }
        private bool KnownWeapon(WeaponLease w)
        { WeaponLease current;return w!=null&&weapons.TryGetValue(w.InstanceId,out current)&&Object.ReferenceEquals(w,current); }
        public HostEvent PublishEvent(CommandContext host,long serial,long expiresTick,DamageSource source,DamageKind kind,WeaponLease weapon,long tick)
        { return PublishRegisteredEvent(host,serial,expiresTick,source,kind,weapon,tick,null); }
        private HostEvent PublishRegisteredEvent(CommandContext host,long serial,long expiresTick,DamageSource source,DamageKind kind,WeaponLease weapon,long tick,SourceRadialExplosion radial)
        {
            Host(host,tick);
            if(serial<=eventHighWater||expiresTick<tick||expiresTick-tick>maximumEventLifetime||
               source<DamageSource.CurrentActorIntent||source>DamageSource.HostEnvironment||
               kind<DamageKind.Bullet||kind>DamageKind.Zone)throw new ArgumentException("event");
            if(source==DamageSource.HostEnvironment)
            { if(kind!=DamageKind.Zone||weapon!=null)throw new ArgumentException("environment"); }
            else if(kind==DamageKind.Zone||!KnownWeapon(weapon)||!weapon.Active||
                    (source==DamageSource.HostProjectile&&kind==DamageKind.Melee))
                throw new ArgumentException("weapon-event");
            if(events.Count>=capacity)throw new InvalidOperationException("event-capacity");
            var ev=new HostEvent(serial,tick,expiresTick,session,epoch,weapon==null?hostActor:weapon.OwnerId,kind,source,weapon,radial);
            events.Add(serial,ev);eventHighWater=serial;return ev;
        }
        private bool LiveEvent(HostEvent ev)
        { HostEvent current;return ev!=null&&events.TryGetValue(ev.Serial,out current)&&Object.ReferenceEquals(current,ev)&&now<=ev.ExpiresTick; }
        public HostHit PublishHit(CommandContext host,HostEvent ev,string subhit,string target,long contactEntry,long tick)
        {
            Host(host,tick);
            if(!LiveEvent(ev)||ev.SourceExplosion!=null&&ev.SourceExplosion.IsPendingLaunch||String.IsNullOrEmpty(subhit)||String.IsNullOrEmpty(target)||
                (ev.Kind==DamageKind.Melee&&contactEntry<=0))throw new ArgumentException("hit");
            string key=HitKey(ev,subhit,target);HostHit old;
            if(hits.TryGetValue(key,out old))
            { if(old.ContactEntry!=contactEntry)throw new ArgumentException("immutable-hit");return old; }
            if(hits.Count>=capacity)throw new InvalidOperationException("hit-capacity");
            var hit=new HostHit(ev,subhit,target,contactEntry);hits.Add(key,hit);return hit;
        }
        public HostHit PublishSwordHit(CommandContext host,HostEvent ev,string subhit,SwordEntry entry,long tick)
        {
            if(ev==null||ev.Kind!=DamageKind.Melee||entry.Weapon==null||!Object.ReferenceEquals(ev.Weapon,entry.Weapon))
                throw new ArgumentException("sword-identity");
            return PublishHit(host,ev,subhit,entry.TargetId,entry.Sequence,tick);
        }
        private static string HitKey(HostEvent ev,string subhit,string target)
        { return Key(ev.Serial.ToString(System.Globalization.CultureInfo.InvariantCulture),Key(subhit,target)); }
        private bool LiveHit(HostHit h)
        { HostHit registered;return h!=null&&LiveEvent(h.Event)&&hits.TryGetValue(HitKey(h.Event,h.SubhitId,h.TargetId),out registered)&&Object.ReferenceEquals(h,registered); }
        private void PruneEvents()
        {
            var expired=new List<long>();foreach(var ev in events)if(now>ev.Value.ExpiresTick)expired.Add(ev.Key);
            foreach(long id in expired)events.Remove(id);
            var dead=new List<string>();foreach(var hit in hits)if(!events.ContainsKey(hit.Value.Event.Serial))dead.Add(hit.Key);
            foreach(string id in dead)hits.Remove(id);
        }
        public RuleResult Commit(DamageDecision decision,ICombatApplication application,long serverTick)
        {
            if(applying||application==null||decision==null||!decision.Accepted||serverTick<now)return RuleResult.Reject("commit-context");
            now=serverTick;PruneEvents();HostHit h=decision.Hit;
            if(!LiveHit(h)||h.Consumed||!Object.ReferenceEquals(h.Pending,decision))return RuleResult.Reject("commit-stale");
            var ev=h.Event;
            if(ev.Source==DamageSource.CurrentActorIntent&&(!KnownWeapon(ev.Weapon)||!ev.Weapon.Active))
                return RuleResult.Reject("weapon-retired");
            long previous;
            if(ev.Kind==DamageKind.Melee&&(!KnownWeapon(ev.Weapon)||!ev.Weapon.Active||
               (ev.Weapon.Entries.TryGetValue(h.TargetId,out previous)&&h.ContactEntry<=previous)))
                return RuleResult.Reject("contact-replay");
            RuleResult applied;
            applying=true;
            try { applied=application.TryApply(decision); }
            finally { applying=false; }
            if(!applied.Accepted){h.Pending=null;return applied;}
            h.Consumed=true;h.Pending=null;
            if(ev.Kind==DamageKind.Melee)ev.Weapon.Entries[h.TargetId]=h.ContactEntry;
            return RuleResult.Accept();
        }
        public DamageDecision Evaluate(CommandContext c,string hitId,long serverTick)
        {
            if(applying||serverTick<0||serverTick<now)return DamageDecision.Reject("server-clock");
            now=serverTick;PruneEvents();
            if(!c.IsAuthority||c.SessionId!=session||c.AuthorityEpoch!=epoch||String.IsNullOrEmpty(c.ActorId)||String.IsNullOrEmpty(c.CommandId)||String.IsNullOrEmpty(hitId))return DamageDecision.Reject("authority-context");
            if(c.Tick<0||c.Tick>now||now-c.Tick>maximumAge)return DamageDecision.Reject("command-age");
            RuleResult gate=authority.Validate(c);if(!gate.Accepted)return DamageDecision.Reject("authority-gate:"+gate.Reason);
            Prune(commands);
            string command=Key(c.ActorId,c.CommandId);
            if(commands.ContainsKey(command))return DamageDecision.Reject("replay");
            if(commands.Count>=capacity)return DamageDecision.Reject("replay-capacity");
            commands.Add(command,c.Tick); // Invalid attempts cannot be retried with changed contents.
            HitEvidence e;
            if(!evidence.TryResolve(c,hitId,out e)||e==null)return DamageDecision.Reject("missing-evidence");
            HostHit identity=e.Identity;
            if(!LiveHit(identity)||identity.Consumed)return DamageDecision.Reject("hit-replay-or-expired");
            HostEvent ev=identity.Event;
            if(c.ActorId!=(ev.Source==DamageSource.CurrentActorIntent?ev.OwnerId:hostActor)||
               e.TargetId!=identity.TargetId||e.Kind!=ev.Kind||
               (ev.Weapon!=null&&e.WeaponId!=ev.Weapon.Archetype)||e.HealthRevision<0)
                return DamageDecision.Reject("hit-binding");
            if(ev.Source==DamageSource.CurrentActorIntent&&(!KnownWeapon(ev.Weapon)||!ev.Weapon.Active||!e.ActorAlive||!e.ActorInputReady))
                return DamageDecision.Reject("actor-intent-state");
            bool environment=ev.Source==DamageSource.HostEnvironment;
            SourceRadialExplosion radial=ev.SourceExplosion;
            var frozenPolicy=radial==null?null:radial.Policy(identity.TargetId);
            bool matchActive=frozenPolicy==null?e.MatchActive:frozenPolicy.Match;
            bool protectedTarget=frozenPolicy==null?e.Protected:frozenPolicy.Protected;
            bool allied=frozenPolicy==null?e.Allied:frozenPolicy.Allied;
            if(String.IsNullOrEmpty(e.TargetId)||!e.TargetAlive||!matchActive||protectedTarget)return DamageDecision.Reject("state");
            if(!environment&&allied&&ev.OwnerId!=e.TargetId&&!e.FriendlyFire)return DamageDecision.Reject("friendly-fire");
            if(!e.ViewOrigin.IsFinite||!e.Origin.IsFinite||!e.Point.IsFinite||!e.BladeBase.IsFinite||!e.TargetHead.IsFinite||!Finite(e.Health)||!Finite(e.Shield)||e.Health<=0||e.Shield<0||!Finite(e.DamageMultiplier)||e.DamageMultiplier<0)return DamageDecision.Reject("invalid-evidence");
            bool melee=e.Kind==DamageKind.Melee;
            if(e.Kind<DamageKind.Bullet||e.Kind>DamageKind.Zone)return DamageDecision.Reject("damage-kind");
            float amount;
            if(melee)
            {
                if(c.ActorId==e.TargetId||!Sword(e.WeaponId)||!Finite(e.HandSpeed)||e.HandSpeed<.2f||e.HandSpeed>12)return DamageDecision.Reject("sword-motion");
                if(Distance(e.Origin,e.Point)>=2.5||Distance(e.Point,new Float3(e.TargetHead.X,e.TargetHead.Y-.5f,e.TargetHead.Z))>=.8)return DamageDecision.Reject("melee-range");
                if(e.ContactEntry<=0)return DamageDecision.Reject("contact-entry");
                amount=e.WeaponId=="knife"?35:SwordDamage(e.HandSpeed);
            }
            else
            {
                if(radial!=null) {
                    double distance=Distance(radial.Origin,new Float3(e.TargetHead.X,e.TargetHead.Y-.5f,e.TargetHead.Z));
                    if(distance>radial.Radius)return DamageDecision.Reject("source-radial-range");
                    amount=radial.Power*(radial.Falloff==SourceRadialFalloff.Linear?(float)Math.Max(0,1-distance/radial.Radius):1);
                } else {
                if((!environment&&(!Finite(e.MaximumRange)||e.MaximumRange<=0||Distance(e.Origin,e.Point)>e.MaximumRange))||!Finite(e.BaseDamage)||e.BaseDamage<=0||!Finite(e.HeadRatio)||e.HeadRatio<0)return DamageDecision.Reject("damage-range");
                amount=e.BaseDamage*(e.Kind==DamageKind.Bullet&&e.Headshot?e.HeadRatio:1);
                }
            }
            float range=melee?2.5f:e.MaximumRange;
            if(!environment&&ev.SourceExplosion==null&&(!query.HasLineOfSight(c.ActorId,e.TargetId,melee?e.ViewOrigin:e.Origin,melee?e.BladeBase:e.Point,range)||
               (melee&&!query.HasLineOfSight(c.ActorId,e.TargetId,e.BladeBase,e.Point,range))))return DamageDecision.Reject("occluded");
            if(e.Kind==DamageKind.Bullet && e.Guard!=null)
            {
                float fraction; if(e.Guard.Intersects(e.Origin,e.Point,out fraction))return DamageDecision.Reject("directional-guard");
            }
            amount*=environment?1:e.DamageMultiplier;
            if(!environment&&e.WardenCoreClosed)amount*=.25f; // enemy.gd variant3 closed core armor.
            if(!Finite(amount)||amount<=0)return DamageDecision.Reject("zero-or-invalid-damage");
            if(melee)
            {
                if(e.ContactEntry!=identity.ContactEntry)return DamageDecision.Reject("contact-binding");
                long previous;
                if(ev.Weapon.Entries.TryGetValue(e.TargetId,out previous)&&identity.ContactEntry<=previous)return DamageDecision.Reject("contact-replay");
                if(!ev.Weapon.Entries.ContainsKey(e.TargetId)&&ev.Weapon.Entries.Count>=capacity)return DamageDecision.Reject("contact-capacity");
                if(!ev.Weapon.Entries.ContainsKey(e.TargetId))ev.Weapon.Entries.Add(e.TargetId,0);
            }
            bool direct=melee||environment;
            float shield=direct?0:Math.Min(e.Shield,amount);
            var decision=new DamageDecision(identity,e.HealthRevision,amount,Math.Min(e.Health,amount-shield),shield,direct);
            identity.Pending=decision; // A new snapshot supersedes any uncommitted decision.
            return decision;
        }
        private void Prune(Dictionary<string,long> ledger)
        { var expired=new List<string>();foreach(var p in ledger)if(now-p.Value>maximumAge)expired.Add(p.Key);foreach(string k in expired)ledger.Remove(k); }
        private static string Key(string a,string b){return a.Length+":"+a+b;}
        public static bool Sword(string id){return id=="katana"||id=="excalibur"||id=="cyberblade"||id=="knife";}
        public static float SwordDamage(float handSpeed){if(!Finite(handSpeed))throw new ArgumentException("handSpeed");return 25+50*Math.Min(1,Math.Max(0,handSpeed)/2);}
        private static bool Finite(float f){return !Single.IsNaN(f)&&!Single.IsInfinity(f);}
        private static double Distance(Float3 a,Float3 b){double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;return Math.Sqrt(x*x+y*y+z*z);}
    }
}


