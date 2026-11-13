using System;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
namespace Ashfall.Integration.SessionReplication
{
    public sealed class SessionReplicationHost
    {
        private readonly SessionNetworkCoordinator sessions;
        private readonly PeerBinding binding;
        private readonly string actor,authority;
        private bool busy;
        private long maxGeneration,requestId,minimum,sequence=-1;
        private double lastNow=-1,lastReply=Double.NegativeInfinity;
        private byte[] previous;
        private long sessionGeneration;
        private sealed class Departure
        {
            internal readonly string Connection;
            internal readonly long Generation, SessionGeneration, Epoch;
            internal readonly bool Kicked;
            internal bool Completed;
            internal Departure(string connection,long generation,long sessionGeneration,long epoch,bool kicked)
            { Connection=connection; Generation=generation; SessionGeneration=sessionGeneration; Epoch=epoch; Kicked=kicked; }
        }
        private Departure departure;
        public bool DeparturePending { get { return departure!=null && !departure.Completed; } }
        public SessionReplicationHost(SessionNetworkCoordinator sessions,IAuthenticatedPeerPort peers,string actor,string authority,string session,string content,long epoch)
        {
            if(sessions==null || !Wire.IsText(actor,64,false) || !Wire.IsText(authority,64,false))throw new ArgumentException("configuration");
            this.sessions=sessions; this.actor=actor; this.authority=authority; binding=new PeerBinding(peers,session,content,epoch);
            RuleResult registry=sessions.BindDispatchAuthorization(peers,new RegistryDispatchAuthorization(peers));
            if(!registry.Accepted)throw new ArgumentException(registry.Reason);
        }
        // Offer response is returned ONLY after extension agreement and actual Session.Join.
        // Tokens are issued solely by Sessions' supplied issuer, never this integration.
        public RuleResult Admit(string connection,byte[] offer,JoinRequest join,long tick,out string token,out byte[] response)
        {
            token=null; response=null; if(busy)return RuleResult.Reject("reentry"); busy=true;
            try
            {
                AuthenticatedPeer p=binding.Resolve(connection,false);
                if(sessions.Epoch!=binding.Epoch || p==null || p.Actor!=actor || !SessionReplicationHello.Matches(offer,p))return RuleResult.Reject("negotiation");
                if(binding.Record!=null || DeparturePending || p.Generation<=maxGeneration)return RuleResult.Reject("generation");
                RuleResult r=sessions.Admit(connection,offer,join,tick,out token); if(!r.Accepted)return r;
                // Sessions' principal resolver must agree with the authenticated registry.
                if(sessions.Get(actor)==null)throw new InvalidOperationException("principal-port-inconsistency");
                sessionGeneration=sessions.ConnectionGeneration(connection);
                if(sessionGeneration<=0)throw new InvalidOperationException("missing-session-lease");
                RuleResult dispatch=sessions.BindDispatchGeneration(connection,sessionGeneration,p.Generation);
                if(!dispatch.Accepted)return dispatch;
                binding.Connection=connection; binding.Record=p; maxGeneration=p.Generation; requestId=0;
                lastReply=Double.NegativeInfinity;
                response=SessionReplicationHello.Encode(authority,binding.Session,binding.Content,binding.Epoch); return r;
            }
            finally { busy=false; }
        }
        private bool Current(string connection)
        { return !DeparturePending && sessions.Epoch==binding.Epoch && binding.Current(connection,false) &&
            sessions.ConnectionGeneration(connection)==sessionGeneration && sessions.Get(actor)!=null; }
        private Packet Capture()
        {
            byte[] payload=MemberCodec.Encode(sessions.Get(actor));
            bool same=previous!=null && previous.Length==payload.Length;
            if(same)for(int i=0;i<payload.Length;i++)if(previous[i]!=payload[i]) {same=false;break;}
            if(!same) { sequence=checked(sequence+1); previous=payload; }
            return new Packet {Kind=PacketKind.Snapshot,SessionId=binding.Session,ContentHash=binding.Content,Epoch=binding.Epoch,
                ActorId=authority,Sequence=sequence,Payload=payload};
        }
        public RuleResult Respond(string connection,byte[] bytes,double now,out byte[] response)
        {
            response=null; if(busy)return RuleResult.Reject("reentry"); busy=true;
            try
            {
                if(!Current(connection))return RuleResult.Reject("principal");
                if(Double.IsNaN(now) || Double.IsInfinity(now) || now<0 || now<lastNow)return RuleResult.Reject("clock"); lastNow=now;
                ResyncRequest r;
                if(!ResyncWire.TryDecodeRequest(bytes,out r) || r.Generation!=binding.Record.Generation)return RuleResult.Reject("request-generation");
                if(r.RequestId<requestId || (r.RequestId==requestId && r.MinimumSequence!=minimum))return RuleResult.Reject("request-replay");
                if(now-lastReply<0.25)return RuleResult.Reject("rate");
                // Limit even requests whose minimum cannot be fulfilled.
                lastReply=now; Packet state=Capture();
                if(state.Sequence<r.MinimumSequence)return RuleResult.Reject("minimum-sequence");
                requestId=r.RequestId; minimum=r.MinimumSequence;
                response=ResyncWire.EncodeState(binding.Record.Generation,r.RequestId,state); return RuleResult.Accept();
            }
            finally { busy=false; }
        }
        public RuleResult Publish(string connection,out byte[] data)
        {
            data=null; if(busy)return RuleResult.Reject("reentry"); busy=true;
            try { if(!Current(connection))return RuleResult.Reject("principal"); data=ResyncWire.EncodeState(binding.Record.Generation,0,Capture()); return RuleResult.Accept(); }
            finally { busy=false; }
        }
        public RuleResult ReceiveCommand(string connection,byte[] data,long tick)
        { return ReceiveCommand(connection,binding.Record==null ? -1 : binding.Record.Generation,data,tick); }
        // Async callbacks MUST supply the authenticated generation captured at bind.
        // The compatibility overload requires a unique immutable connection key.
        public RuleResult ReceiveCommand(string connection,long generation,byte[] data,long tick)
        {
            if(busy)return RuleResult.Reject("reentry"); busy=true;
            try { return Current(connection) && generation==binding.Record.Generation ? sessions.Receive(connection,sessionGeneration,data,tick) : RuleResult.Reject("principal"); }
            finally { busy=false; }
        }
        public bool DispatchOne(out RuleResult result)
        {
            if(busy) { result=RuleResult.Reject("reentry"); return false; } busy=true;
            try { if(!Current(binding.Connection)) { result=RuleResult.Reject("principal"); return false; } return sessions.DispatchOne(out result); } finally { busy=false; }
        }
        public RuleResult Disconnect(string connection,long generation,long tick,bool kicked)
        {
            if(busy)return RuleResult.Reject("reentry"); busy=true;
            try
            {
                if(departure==null || departure.Connection!=connection || departure.Generation!=generation)
                {
                    if(DeparturePending || binding.Record==null || binding.Connection!=connection || binding.Record.Generation!=generation)return RuleResult.Reject("generation");
                    // Allocate the trusted cleanup receipt BEFORE revocation. Never
                    // resolve a replacement connection while retrying this receipt.
                    Departure pending=new Departure(connection,generation,sessionGeneration,binding.Epoch,kicked);
                    RuleResult revoked=sessions.RevokeDispatch(connection,sessionGeneration);
                    if(!revoked.Accepted)return revoked;
                    departure=pending;
                    binding.Record=null;
                }
                if(departure.Kicked!=kicked)return RuleResult.Reject("departure-policy");
                if(departure.Completed)return RuleResult.Accept();
                if(sessions.Epoch!=departure.Epoch)return RuleResult.Reject("departure-epoch");
                RuleResult r=sessions.Disconnect(departure.Connection,departure.SessionGeneration,tick,departure.Kicked);
                if(r.Accepted)departure.Completed=true;
                return r;
            }
            finally { busy=false; }
        }
    }
}

