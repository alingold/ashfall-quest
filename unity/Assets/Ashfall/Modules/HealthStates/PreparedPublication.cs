using System;
namespace Ashfall.Modules.HealthStates
{
    public sealed partial class HealthService
    {
        private PreparedPublicationLease preparedPublication;
        internal bool HasPreparedPublication { get { lock(sync) { return preparedPublication!=null; } } }
        internal bool TryReservePublication(HealthReservationFamily family,double expiresAt,out PreparedPublicationLease lease)
        {
            lock(sync) {
                lease=null;double now=world.Now;
                // Reclaim only an expired, unpublished reservation. Old tokens remain closed.
                if(preparedPublication!=null&&Finite(now)&&now>=preparedPublication.ExpiresAt)preparedPublication.Abort();
                if(epochIngressClosed||preparedPublication!=null||eventSequence==Int64.MaxValue||!Finite(now)||now<0||now<lastTime||!Finite(expiresAt)||expiresAt<=now||expiresAt>now+5)return false;
                lease=new PreparedPublicationLease(this,family,eventSequence,now,expiresAt);preparedPublication=lease;return true;
            }
        }
        // Internal host primitive, not authorization or a standalone client API.
        internal sealed class PreparedPublicationLease
        {
            private readonly HealthService owner;
            private readonly double created,expires;
            internal readonly long BaseSequence,NextSequence;
            internal readonly HealthReservationFamily Family;
            internal bool CanCommit {get {return !closed&&!owner.epochIngressClosed&&ReferenceEquals(owner.preparedPublication,this);}}
            internal double ExpiresAt { get { return expires; } }
            private bool closed;
            internal PreparedPublicationLease(HealthService s,HealthReservationFamily family,long sequence,double now,double until)
            { owner=s;Family=family;BaseSequence=sequence;NextSequence=sequence+1;created=now;expires=until; }
            internal bool Validate()
            { lock(owner.sync) { double now=owner.world.Now;return CanCommit&&ReferenceEquals(owner.preparedPublication,this)&&owner.eventSequence==BaseSequence&&Finite(now)&&now>=created&&now>=owner.lastTime&&now<expires; } }
            // Call immediately after validate-all and the producer's publication on the host lane.
            // No allocation, callback or fallible authorization in this final release.
            internal void CompleteValidated()
            { lock(owner.sync) { if(closed)return;if(ReferenceEquals(owner.preparedPublication,this))owner.preparedPublication=null;closed=true; } }
            internal void Abort()
            { lock(owner.sync) { if(closed)return;if(ReferenceEquals(owner.preparedPublication,this))owner.preparedPublication=null;closed=true; } }
        }
    }
}

