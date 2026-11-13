using System;
using Ashfall.Core;
namespace Ashfall.Modules.Networking
{
    // Opaque host-issued capabilities. A peer name/hello is never authentication.
    public sealed class PeerLease
    {
        internal readonly AuthorityHost Owner;
        internal long IngressSequence;
        internal readonly System.Collections.Generic.HashSet<string> IngressSeen=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        internal readonly System.Collections.Generic.Queue<string> IngressOrder=new System.Collections.Generic.Queue<string>();
        public readonly string PeerId, ActorId, SessionId, ReplayIdentity;
        public readonly long Epoch, Generation;
        internal PeerLease(AuthorityHost owner,string peer,string actor,string replayIdentity,long generation)
        { Owner=owner; PeerId=peer; ActorId=actor; ReplayIdentity=replayIdentity; SessionId=owner.SessionId; Epoch=owner.Epoch; Generation=generation; }
    }
    public sealed class PeerReservation
    {
        internal readonly AuthorityHost Owner;
        internal readonly object History;
        internal readonly long Revision, HistoryRevision;
        internal bool Validated, Committed, Aborted;
        public readonly PeerLease Lease;
        public readonly long ExpiresAtTick;
        internal PeerReservation(AuthorityHost owner,PeerLease lease,object history,long revision,long historyRevision,long expiry)
        { Owner=owner; Lease=lease; History=history; Revision=revision; HistoryRevision=historyRevision; ExpiresAtTick=expiry; }
    }
    public sealed partial class AuthorityHost
    {
        private readonly PeerReservation[] reservations;
        private readonly System.Collections.Generic.Dictionary<string,PeerLease> leases;
        private readonly int historyCapacity;
        private long membershipRevision, clock;
        public int ActiveCount { get { return peers.Count; } }
        public int ReservedCount { get { int n=0; foreach(var r in reservations) if(r!=null) n++; return n; } }
        public int IdentityCount { get { return actors.Count; } }
        public int IdentityHistoryCapacity { get { return historyCapacity; } }
        // Trusted monotonic host ticks; expiry equality is expired. Expiration never touches active leases/history.
        private bool Sweep(long now)
        {
            if(now<clock) return false;
            clock=now;
            for(int i=0;i<reservations.Length;i++)
                if(reservations[i]!=null && now>=reservations[i].ExpiresAtTick) { reservations[i].Aborted=true; reservations[i]=null; }
            return true;
        }
        public RuleResult PrepareAdmission(string peer,string actorId,byte[] hello,long nowTick,long expiresAtTick,out PeerReservation reservation)
            { return PrepareAdmission(peer,actorId,actorId,hello,nowTick,expiresAtTick,out reservation); }
        // Canonical replayIdentity is supplied ONLY by the authenticated principal registry.
        // Remapping actor IDs must retain this identity across all same-epoch connections.
        public RuleResult PrepareAdmission(string peer,string actorId,string replayIdentity,byte[] hello,long nowTick,long expiresAtTick,out PeerReservation reservation)
        {
            reservation=null;
            if(nowTick<0 || expiresAtTick<=nowTick || !Sweep(nowTick)) return RuleResult.Reject("reservation-time");
            Packet p; if(!Wire.TryDecode(hello,out p) || p.Kind!=PacketKind.Hello) return RuleResult.Reject("hello");
            RuleResult check=Compatible(p); if(!check.Accepted) return check;
            if(!Wire.IsText(replayIdentity,64,false) || !Wire.IsText(peer,256,false) || !Wire.IsText(actorId,64,false) || p.ActorId!=actorId) return RuleResult.Reject("identity");
            if(peers.ContainsKey(peer) || peers.ContainsValue(actorId)) return RuleResult.Reject("already-admitted");
            foreach(var active in leases.Values) if(active.ReplayIdentity==replayIdentity) return RuleResult.Reject("already-admitted");
            int slot=-1, newHistories=0;
            for(int i=0;i<reservations.Length;i++)
            {
                var r=reservations[i]; if(r==null) { slot=i; continue; }
                if(r.Lease.PeerId==peer || r.Lease.ActorId==actorId || r.Lease.ReplayIdentity==replayIdentity) return RuleResult.Reject("already-reserved");
                if(!actors.ContainsKey(r.Lease.ReplayIdentity)) newHistories++;
            }
            if(peers.Count+ReservedCount>=maxActors) return RuleResult.Reject("peer-capacity");
            Actor a;
            if(!actors.TryGetValue(replayIdentity,out a))
            {
                if(actors.Count+newHistories>=historyCapacity) return RuleResult.Reject("identity-history-capacity");
                a=new Actor();
            }
            if(a.Revision==long.MaxValue || generation==long.MaxValue || membershipRevision==long.MaxValue) return RuleResult.Reject("generation-exhausted");
            var lease=new PeerLease(this,peer,actorId,replayIdentity,++generation);
            reservation=new PeerReservation(this,lease,a,membershipRevision,a.Revision,expiresAtTick);
            reservations[slot]=reservation; return RuleResult.Accept();
        }
        public RuleResult Validate(PeerReservation reservation,long nowTick)
        {
            if(nowTick<0 || !Sweep(nowTick)) return RuleResult.Reject("reservation-time");
            if(reservation==null || reservation.Owner!=this || reservation.Aborted || reservation.Committed || reservation.Lease.Epoch!=Epoch || reservation.Revision!=membershipRevision || ((Actor)reservation.History).Revision!=reservation.HistoryRevision || Array.IndexOf(reservations,reservation)<0)
                return RuleResult.Reject("stale-reservation");
            reservation.Validated=true; return RuleResult.Accept();
        }
        // Same serialized host lane: validate ALL participants, then commit immediately.
        // Storage/lease/Actor allocated during host construction/prepare; success has no callbacks/allocations.
        public PeerLease CommitValidated(PeerReservation reservation)
        {
            if(reservation!=null && reservation.Owner==this && reservation.Committed) return reservation.Lease;
            if(reservation==null || !reservation.Validated || !Validate(reservation,clock).Accepted) throw new InvalidOperationException("reservation-not-validated");
            PeerLease lease=reservation.Lease;
            if(!actors.ContainsKey(lease.ReplayIdentity)) actors.Add(lease.ReplayIdentity,(Actor)reservation.History);
            peers.Add(lease.PeerId,lease.ActorId); generations.Add(lease.PeerId,lease.Generation); leases.Add(lease.PeerId,lease);
            reservations[Array.IndexOf(reservations,reservation)]=null; reservation.Committed=true; membershipRevision++;
            return lease;
        }
        public void Abort(PeerReservation reservation)
        {
            if(reservation==null || reservation.Owner!=this || reservation.Committed || reservation.Aborted) return;
            int i=Array.IndexOf(reservations,reservation); if(i>=0) reservations[i]=null;
            reservation.Aborted=true;
        }
        public RuleResult Release(PeerLease lease)
        {
            PeerLease current;
            if(lease==null || lease.Owner!=this || lease.Epoch!=Epoch || !leases.TryGetValue(lease.PeerId,out current) || current!=lease) return RuleResult.Reject("stale-lease");
            peers.Remove(lease.PeerId); generations.Remove(lease.PeerId); leases.Remove(lease.PeerId);
            // Saturation fails subsequent prepares closed; never wrap a membership generation.
            if(membershipRevision<long.MaxValue) membershipRevision++;
            return RuleResult.Accept();
        }
        public RuleResult Receive(PeerLease lease,byte[] bytes,long hostTick)
        {
            PeerLease current;
            if(lease==null || lease.Owner!=this || lease.Epoch!=Epoch || !leases.TryGetValue(lease.PeerId,out current) || current!=lease) return RuleResult.Reject("stale-lease");
            return Receive(lease.PeerId,bytes,hostTick);
        }
    }
}
