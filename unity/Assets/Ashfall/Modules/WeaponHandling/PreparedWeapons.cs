using System;
using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
    // Host reserve must advance Revision even when a mutation later restores the count.
    public interface IRevisionedAmmoReserve : IAmmoReserve { long Revision { get; } }
    public interface IPreparedWeaponTransfer
    {
        RuleResult Validate();
        void CommitValidated();
        void Abort();
        PreparedWeaponResult Result { get; }
    }
    public sealed class PreparedWeaponResult
    {
        public readonly string InstanceId, ActorId;
        public readonly long Epoch;
        public readonly HandlingSnapshot Snapshot;
        public readonly WeaponHandling Handler;
        internal PreparedWeaponResult(string id, string actor, long epoch, WeaponHandling handler)
        { InstanceId=id; ActorId=actor; Epoch=epoch; Handler=handler; Snapshot=handler.Snapshot; }
    }
    public interface IPreparedWeaponSuspension
    {
        RuleResult Validate();
        void CommitValidated();
        void Abort();
    }
    public sealed partial class WeaponHandling
    {
        long revision;
        internal long Revision { get { return revision; } }
        internal bool Live { get { return !retired; } }
        internal WeaponHandling SuspendedCandidate()
        { var n=new WeaponHandling(this,VersionedReserve);n.Suspend();n.overrun=0;n.revision++;return n; }
        internal IRevisionedAmmoReserve VersionedReserve { get { return reserve as IRevisionedAmmoReserve; } }
        // One owner lane reservation for every prepared operation family. Retain
        // the identity through validation; only its owner may consume/release it.
        internal bool ReserveOperation(object token,long expiry,long now)
        {
            if(FireReservation!=null && (!(FireReservation is IPreparedWeaponFire) && !(FireReservation is IPreparedSourceLaunch)||now<=FireReservationExpiry))return false;
            FireReservation=token;FireReservationExpiry=expiry;return true;
        }
        internal void ReleaseOperation(object token)
        {if(Object.ReferenceEquals(FireReservation,token))FireReservation=null;}
        // Read-only cross-assembly metadata; never expose the mutable reserve.
        public bool TryReadReserve(out int available,out long reserveRevision)
        {
            available=0;reserveRevision=0;var actual=VersionedReserve;if(actual==null)return false;
            long before=actual.Revision;int count=actual.Available;
            if(count<0||before<0||actual.Revision!=before)return false;
            available=count;reserveRevision=before;return true;
        }
        public bool SharesReserveWith(WeaponHandling other)
        {return other!=null&&Object.ReferenceEquals(reserve,other.reserve);}
        internal RuleResult ValidatePreparation(CommandContext c)
        {
            if(retired || !c.IsAuthority || c.SessionId!=session || c.ActorId!=actor || c.AuthorityEpoch!=epoch ||
                String.IsNullOrEmpty(c.CommandId) || c.CommandId.Length>128 || c.Tick<0 || c.Tick<tick || seen.Contains(c.CommandId))
                return RuleResult.Reject("stale prepared envelope");
            return authority.Validate(c);
        }
        internal void RetirePrepared() { retired=true; revision++; }
        public RuleResult PrepareSuspension(out IPreparedWeaponSuspension prepared)
        {
            prepared=null;if(retired)return RuleResult.Reject("retired");
            var token=new Suspension(this);
            if(!ReserveOperation(token,long.MaxValue,0))return RuleResult.Reject("prepared operation busy");
            prepared=token;return RuleResult.Accept();
        }
        sealed class Suspension : IPreparedWeaponSuspension
        {
            readonly WeaponHandling owner;readonly long version;bool validated,done,aborted;
            internal Suspension(WeaponHandling owner){this.owner=owner;version=owner.revision;}
            public RuleResult Validate(){validated=!aborted&&!done&&owner.FireReservation==this&&owner.Live&&owner.revision==version;return validated?RuleResult.Accept():RuleResult.Reject("stale suspension");}
            public void CommitValidated(){if(!validated||done||aborted||owner.revision!=version||!owner.Live||owner.FireReservation!=this)return;owner.Suspend();owner.overrun=0;owner.revision++;done=true;owner.ReleaseOperation(this);}
            public void Abort(){if(!done){aborted=true;validated=false;owner.ReleaseOperation(this);}}
        }
    }
}
