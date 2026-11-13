using Ashfall.Core;
namespace Ashfall.Modules.Combat
{
    public enum DamageKind { Bullet, Melee, Area, Zone }
    public enum DamageSource { CurrentActorIntent, HostProjectile, HostEnvironment }
    // Capability objects are created only by CombatRules host publication methods.
    // Transport accepts opaque hit IDs only; never expose publication to client dispatch.
    public sealed class WeaponLease
    {
        public readonly string InstanceId, OwnerId, Archetype;
        public readonly long Generation;
        internal bool Active=true, TrackerClaimed;
        internal readonly System.Collections.Generic.Dictionary<string,long> Entries=new System.Collections.Generic.Dictionary<string,long>();
        internal WeaponLease(string instance,string owner,string archetype,long generation)
        { InstanceId=instance;OwnerId=owner;Archetype=archetype;Generation=generation; }
    }
    public sealed class HostEvent
    {
        public readonly long Serial, BornTick, ExpiresTick;
        public readonly string SessionId, OwnerId;
        public readonly long AuthorityEpoch;
        public readonly DamageKind Kind;
        public readonly DamageSource Source;
        public readonly WeaponLease Weapon;
        public readonly SourceRadialExplosion SourceExplosion;
        internal HostEvent(long serial,long born,long expires,string session,long epoch,string owner,DamageKind kind,DamageSource source,WeaponLease weapon,SourceRadialExplosion radial=null)
        { SourceExplosion=radial;Serial=serial;BornTick=born;ExpiresTick=expires;SessionId=session;AuthorityEpoch=epoch;OwnerId=owner;Kind=kind;Source=source;Weapon=weapon; }
    }
    public sealed class HostHit
    {
        public readonly HostEvent Event;
        public readonly string SubhitId, TargetId;
        public readonly long ContactEntry;
        internal bool Consumed;
        internal DamageDecision Pending;
        internal HostHit(HostEvent ev,string subhit,string target,long contact)
        { Event=ev;SubhitId=subhit;TargetId=target;ContactEntry=contact; }
    }
    // Atomic CAS boundary. Reject without mutation on stale revision. Accept exactly once
    // by session/epoch/event/subhit/target. Must not throw after a mutation or reenter rules.
    public interface ICombatApplication
    {
        RuleResult TryApply(DamageDecision decision);
    }
    // All fields come from the server's simulation, NEVER deserialized client hit claims.
    public sealed class HitEvidence
    {
        public HostHit Identity;
        public long HealthRevision;
        public string TargetId, WeaponId;
        public DamageKind Kind;
        public bool ActorAlive, ActorInputReady, TargetAlive, MatchActive, Allied, FriendlyFire, Protected;
        public bool WardenCoreClosed, Headshot;
        public float Health, Shield, BaseDamage, DamageMultiplier, HeadRatio;
        public Float3 Origin, ViewOrigin, BladeBase, Point, TargetHead;
        public float MaximumRange, HandSpeed;
        public long ContactEntry;
        public BulletGuard Guard; // null if no currently usable two-hand katana guard.
    }
    // Coordinator adapts authoritative weapon ownership, poses, life/team/rules and physics.
    // TryResolve maps an opaque ID to a previously published HostHit and a fresh health
    // snapshot/revision. Current intent requires live/input-ready equipped ownership;
    // projectiles use their frozen launch attribution after death/disconnect/retirement.
    // Environment hit publication requires host zone-inclusion verification (no weapon ray).
    // Damage, pools, teams, range and pose data are host facts, never client fields.
    public interface ICombatEvidence
    {
        bool TryResolve(CommandContext context, string hitId, out HitEvidence evidence);
    }
    public sealed class DamageDecision
    {
        public readonly bool Accepted;
        public readonly string Reason, TargetId, SessionId, SubhitId, OwnerId, WeaponInstanceId, WeaponArchetype;
        public readonly long EquipGeneration, ContactEntry;
        public readonly DamageSource Source;
        public readonly DamageKind Kind;
        public readonly long AuthorityEpoch, LogicalEvent, ExpectedRevision;
        public readonly float Requested, HealthLoss, ShieldLoss;
        public readonly bool DirectHealth;
        internal readonly HostHit Hit;
        internal DamageDecision(HostHit hit,long revision,float requested,float health,float shield,bool direct)
        {
            Hit=hit;Accepted=true;Reason="";TargetId=hit.TargetId;SessionId=hit.Event.SessionId;
            AuthorityEpoch=hit.Event.AuthorityEpoch;LogicalEvent=hit.Event.Serial;SubhitId=hit.SubhitId;
            OwnerId=hit.Event.OwnerId;Source=hit.Event.Source;Kind=hit.Event.Kind;ContactEntry=hit.ContactEntry;
            WeaponInstanceId=hit.Event.Weapon==null?null:hit.Event.Weapon.InstanceId;
            WeaponArchetype=hit.Event.Weapon==null?null:hit.Event.Weapon.Archetype;
            EquipGeneration=hit.Event.Weapon==null?0:hit.Event.Weapon.Generation;
            ExpectedRevision=revision;Requested=requested;HealthLoss=health;ShieldLoss=shield;DirectHealth=direct;
        }
        private DamageDecision(string reason){Reason=reason;Accepted=false;}
        public static DamageDecision Reject(string reason){return new DamageDecision(reason);}
    }
}
