using System;
using Ashfall.Core;
namespace Ashfall.Modules.HealthStates
{
    public enum HealthReservationFamily { Gain, Advance, Respawn }
    public sealed class HealthEpochAccounting
    {
        public readonly bool IngressClosed;
        public readonly int Gains, Advances, Respawns, Publications, PendingTransitions;
        public readonly HealthReservationFamily? AbortedFamily;
        internal HealthEpochAccounting(bool closed,PreparedCounts counts,int pending,HealthReservationFamily? aborted)
        {IngressClosed=closed;Gains=counts.Gains;Advances=counts.Advances;Respawns=counts.Respawns;Publications=counts.Publications;PendingTransitions=pending;AbortedFamily=aborted;}
    }
    internal struct PreparedCounts {internal int Gains,Advances,Respawns,Publications;}
    public sealed partial class HealthService
    {
        private bool epochIngressClosed;
        private HealthReservationFamily? epochAbortedFamily;
        // The registry keeps this capability private. A retained service has no getter for it.
        public sealed class EpochClosure
        {
            readonly HealthService owner;
            internal EpochClosure(HealthService service){owner=service;}
            public HealthEpochAccounting Close()
            {return owner.AtomicHealth(delegate{
                lock(owner.sync) {
                    if(!owner.epochIngressClosed) {
                        owner.epochIngressClosed=true;
                        if(owner.preparedPublication!=null){owner.epochAbortedFamily=owner.preparedPublication.Family;owner.preparedPublication.Abort();}
                        foreach(var r in owner.respawnReservations)if(r!=null)r.Abort();
                    }
                    return owner.EpochAccounting;
                }
            });}
            // Closed owner recovery only; does not reopen ingress or authorize adoption.
            public byte[] CaptureClosedCheckpoint(CommandContext host,string sourceBinding)
            {lock(owner.sync){if(!owner.epochIngressClosed)throw new InvalidOperationException("health-not-closed");return owner.Checkpoint(host,sourceBinding,true);}}
        }
        public static HealthService CreateEpochParticipant(string session,long epoch,int teams,bool respawn,IAuthorityGate gate,IHealthWorld world,IInteractionQuery query,out EpochClosure closure)
        {var service=new HealthService(session,epoch,teams,respawn,gate,world,query);closure=new EpochClosure(service);return service;}
        public HealthEpochAccounting EpochAccounting
        {
            get {lock(sync){var c=new PreparedCounts();if(preparedPublication!=null){c.Publications=1;switch(preparedPublication.Family){case HealthReservationFamily.Gain:c.Gains=1;break;case HealthReservationFamily.Advance:c.Advances=1;break;case HealthReservationFamily.Respawn:c.Respawns=1;break;}}return new HealthEpochAccounting(epochIngressClosed,c,transitions.Count,epochAbortedFamily);}}
        }
    }
}
