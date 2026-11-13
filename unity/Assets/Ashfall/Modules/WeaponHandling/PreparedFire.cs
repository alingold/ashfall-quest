using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.WeaponHandling
{
    // Privileged host-lane port. Root must bind canonical life/connection, equipped
    // inventory generation and authenticated frame here. Strings alone grant nothing.
    public interface IWeaponFireBinding
    {
        RuleResult Validate(CommandContext command, string instanceId, long equipGeneration,
            long lifeGeneration, long connectionGeneration, long frameSequence);
    }
    public interface IPreparedWeaponFire
    {
        RuleResult Validate(long authoritativeTick);
        void CommitValidated();
        void Abort();
        CommittedWeaponShot Result { get; }
    }
    // Evidence of ONE committed shot; no method on this object can fire or refill.
    // Combat must reserve its own launch/serial capacity before this shot commits.
    public sealed class CommittedWeaponShot
    {
        public readonly CommandContext Command;
        public readonly string InstanceId;
        public readonly long EquipGeneration, LifeGeneration, ConnectionGeneration, FrameSequence, ExpiresTick;
        public readonly WeaponMechanism Mechanism;
        public readonly HandlingSnapshot Before, After;
        internal CommittedWeaponShot(CommandContext c, string id, long equip, long life, long connection,
            long frame, long expires, WeaponMechanism mechanism, HandlingSnapshot before, HandlingSnapshot after)
        { Command=c; InstanceId=id; EquipGeneration=equip; LifeGeneration=life; ConnectionGeneration=connection;
          FrameSequence=frame; ExpiresTick=expires; Mechanism=mechanism; Before=before; After=after; }
    }
    public sealed partial class WeaponHandling
    {
        internal object FireReservation;
        internal long FireReservationExpiry;
        internal WeaponMechanism FireMechanism { get { return mechanism; } }
        // Fire may call Refund(0) when the last service-rifle round ejects. No
        // positive reserve operation is supported by this preparation boundary.
        internal sealed class FireReserve : IAmmoReserve
        {
            readonly int available;
            internal bool Touched;
            internal FireReserve(int count) { available=count; }
            public int Available { get { return available; } }
            public int Take(int n) { if(n!=0) Touched=true; return 0; }
            public int Refund(int n) { if(n!=0) Touched=true; return 0; }
        }
        internal WeaponHandling(WeaponHandling original, IAmmoReserve isolated)
        {
            session=original.session; actor=original.actor; epoch=original.epoch;
            authority=original.authority; reserve=isolated; mechanism=original.mechanism;
            capacity=original.capacity; interval=original.interval;
            PublishFireState(original);
            seen=new HashSet<string>(original.seen,StringComparer.Ordinal);
            order=new Queue<string>(original.order); flick=original.flick.Copy();
        }
        // Assignment-only publication. The candidate is private and all mutable
        // ledgers were copied before Apply; the original handler stays playable.
        internal void PublishFireState(WeaponHandling n)
        {
            sourceMechanics=n.sourceMechanics;
            tick=n.tick; sequence=n.sequence; shots=n.shots; phase=n.phase;
            loaded=n.loaded; seated=n.seated; pattern=n.pattern; chambered=n.chambered;
            spent=n.spent; pending=n.pending; supported=n.supported; latched=n.latched;
            previousGrip=n.previousGrip; contact=n.contact; releaseGrip=n.releaseGrip;
            releaseTrigger=n.releaseTrigger; grab=n.grab; boltGrab=n.boltGrab; overrun=n.overrun;
            lastShotTick=n.lastShotTick; retired=n.retired; boltTravel=n.boltTravel;
            cooldown=n.cooldown; recoil=n.recoil; pitch=n.pitch; yaw=n.yaw; idle=n.idle;
            gripToggle=n.gripToggle; revision=n.revision; seen=n.seen; order=n.order; flick=n.flick;
        }
    }
    public sealed partial class WeaponLifetimes
    {
        // Expiry is in authoritative simulation ticks, never client wall time.
        public const long MaximumPreparedFireTicks = 120;
        public RuleResult PrepareFire(CommandContext c, string instanceId, HandlingInput input,
            long equipGeneration, long lifeGeneration, long connectionGeneration, long expiresTick,
            IWeaponFireBinding binding, out IPreparedWeaponFire prepared)
        {
            prepared=null; Entry entry;
            if(binding==null || !Valid(c,instanceId) || instanceId.Length>128 || equipGeneration<0 ||
                lifeGeneration<0 || connectionGeneration<0 || expiresTick<c.Tick ||
                expiresTick-c.Tick>MaximumPreparedFireTicks || input.Sequence<0 ||
                !entries.TryGetValue(instanceId,out entry) || entry.Active==null ||
                entry.Actor!=c.ActorId || entry.Epoch!=c.AuthorityEpoch)
                return RuleResult.Reject("invalid prepared fire identity");
            WeaponHandling original=entry.Active;
            if(original.IsSourceExplosive)return RuleResult.Reject("use source launch API");
            if(original.FireReservation!=null && (!(original.FireReservation is IPreparedWeaponFire)||c.Tick<=original.FireReservationExpiry))
                return RuleResult.Reject("prepared fire busy");
            // Expired tokens lose their reservation identity and can never validate.
            if(original.FireReservation!=null) original.FireReservation=null;
            IRevisionedAmmoReserve reserve=original.VersionedReserve;
            if(reserve==null) return RuleResult.Reject("prepared fire requires revisioned reserve");
            long version=original.Revision, ammoVersion=reserve.Revision;
            if(version==long.MaxValue || original.Snapshot.Shots==long.MaxValue) return RuleResult.Reject("weapon revision exhausted");
            RuleResult r=original.ValidatePreparation(c); if(!r.Accepted)return r;
            r=binding.Validate(c,instanceId,equipGeneration,lifeGeneration,connectionGeneration,input.Sequence);
            if(!r.Accepted)return r;
            var isolated=new WeaponHandling.FireReserve(reserve.Available);
            var next=new WeaponHandling(original,isolated);
            r=next.Apply(c,HandlingAction.Fire,input);
            if(!r.Accepted)return r;
            if(isolated.Touched || next.Snapshot.Shots!=original.Snapshot.Shots+1)
                return RuleResult.Reject("unsupported fire reserve operation");
            // Reentrant/callback mutations during preparation invalidate this copy.
            if(entry.Active!=original || original.Revision!=version || reserve.Revision!=ammoVersion)
                return RuleResult.Reject("fire changed during preparation");
            var token=new FirePreparation(entry,original,next,reserve,version,ammoVersion,c,instanceId,
                equipGeneration,lifeGeneration,connectionGeneration,input.Sequence,expiresTick,binding);
            if(!original.ReserveOperation(token,expiresTick,c.Tick))return RuleResult.Reject("prepared operation busy");
            prepared=token;
            return RuleResult.Accept();
        }
        sealed class FirePreparation : IPreparedWeaponFire
        {
            readonly Entry entry;
            readonly WeaponHandling original,next;
            readonly IRevisionedAmmoReserve reserve;
            readonly long version,ammoVersion,expires;
            readonly IWeaponFireBinding binding;
            readonly CommittedWeaponShot result;
            bool validated,committed,aborted;
            long observedTick;
            internal FirePreparation(Entry e, WeaponHandling o, WeaponHandling n, IRevisionedAmmoReserve r,
                long v,long av,CommandContext c,string id,long equip,long life,long connection,long frame,
                long expiry,IWeaponFireBinding b)
            {
                entry=e;original=o;next=n;reserve=r;version=v;ammoVersion=av;expires=expiry;binding=b;
                observedTick=c.Tick;
                result=new CommittedWeaponShot(c,id,equip,life,connection,frame,expiry,o.FireMechanism,o.Snapshot,n.Snapshot);
            }
            public CommittedWeaponShot Result { get { return committed?result:null; } }
            bool Current()
            { return original.FireReservation==this && entry.Active==original && original.Live && original.Revision==version &&
                reserve.Revision==ammoVersion && entry.Actor==result.Command.ActorId && entry.Epoch==result.Command.AuthorityEpoch; }
            public RuleResult Validate(long authoritativeTick)
            {
                validated=false;
                if(authoritativeTick>expires){original.ReleaseOperation(this);aborted=true;}
                if(aborted || committed || authoritativeTick<observedTick || authoritativeTick>expires || !Current())
                    return RuleResult.Reject("stale prepared fire");
                observedTick=authoritativeTick;
                RuleResult r=original.ValidatePreparation(result.Command); if(!r.Accepted)return r;
                r=binding.Validate(result.Command,result.InstanceId,result.EquipGeneration,result.LifeGeneration,
                    result.ConnectionGeneration,result.FrameSequence);
                if(!r.Accepted)return r;
                if(!Current())return RuleResult.Reject("fire changed during validation");
                validated=true;return RuleResult.Accept();
            }
            // PRECONDITION: successful final Validate-all in the same exclusive host
            // lane, with no intervening module operation, callback or clock advance.
            // After that point this contains no fallible guard/callback/allocation.
            public void CommitValidated()
            {
                if(committed || aborted || !validated || !Current())return;
                original.PublishFireState(next); original.FireReservation=null; committed=true;
            }
            public void Abort() { if(!committed){aborted=true;validated=false;
                if(original.FireReservation==this)original.FireReservation=null;} }
        }
    }
}

