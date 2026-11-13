using System;
using Ashfall.Core;
using Ashfall.Modules.Networking;
namespace Ashfall.Integration.SessionReplication
{
    // Extension advertisement in EXISTING AFN1 Hello. No new wire protocol/stack.
    public static class SessionReplicationHello
    {
        public const string Extension="sessions.afr1/1";
        public static byte[] Encode(string actor,string session,string content,long epoch)
        { return Wire.Encode(new Packet {Kind=PacketKind.Hello,ActorId=actor,SessionId=session,ContentHash=content,Epoch=epoch,FeatureId=Extension}); }
        internal static bool Matches(byte[] data,AuthenticatedPeer p)
        {
            Packet h; return p!=null && Wire.TryDecode(data,out h) && h.Kind==PacketKind.Hello &&
                h.Protocol==Protocol.Name && h.Schema==Protocol.Schema && h.ActorId==p.Actor && h.SessionId==p.Session &&
                h.ContentHash==p.Content && h.Epoch==p.Epoch && h.FeatureId==Extension && h.Payload.Length==0;
        }
    }
    internal sealed class PeerBinding
    {
        private readonly IAuthenticatedPeerPort peers;
        internal readonly string Session,Content;
        internal readonly long Epoch;
        internal string Connection;
        internal AuthenticatedPeer Record;
        internal PeerBinding(IAuthenticatedPeerPort peers,string session,string content,long epoch)
        {
            if(peers==null || !Wire.IsText(session,64,false) || !Wire.IsHash(content) || epoch<0)throw new ArgumentException("configuration");
            this.peers=peers; Session=session; Content=content; Epoch=epoch;
        }
        internal AuthenticatedPeer Resolve(string c,bool authority)
        {
            AuthenticatedPeer p;
            return !String.IsNullOrEmpty(c) && peers.TryResolve(c,out p) && p!=null && Wire.IsText(p.Actor,64,false) &&
                p.Session==Session && p.Content==Content && p.Epoch==Epoch && p.Generation>0 && p.Authority==authority ? p : null;
        }
        internal bool Current(string c,bool authority)
        {
            AuthenticatedPeer p=Resolve(c,authority);
            return Record!=null && c==Connection && p!=null && p.Actor==Record.Actor && p.Generation==Record.Generation;
        }
    }
    // One recipient per object. Create on the serialized simulation lane. Retain for
    // same identity/epoch reconnects; genuine epoch changes require central reset.
    public sealed class SessionReplicationClient : IReplicationAdmission,IReplicationSink
    {
        private readonly PeerBinding binding;
        private readonly ReplicationReceiver receiver;
        private readonly string actor; private string authorityActor;
        private bool busy,offered,applying;
        private double lastNow=-1,lastRequest=Double.NegativeInfinity;
        private long sentGeneration,sentRequest;
        public SessionReplica View { get; private set; }
        public long Sequence { get { return receiver.Sequence; } }
        public bool NeedsSnapshot { get { return receiver.NeedsSnapshot; } }
        public SessionReplicationClient(IAuthenticatedPeerPort peers,string actor,string session,string content,long epoch)
        { if(!Wire.IsText(actor,64,false))throw new ArgumentException("actor"); this.actor=actor;
          binding=new PeerBinding(peers,session,content,epoch); receiver=new ReplicationReceiver("unbound",session,content,epoch); }
        public byte[] Offer()
        { if(busy)throw new InvalidOperationException("reentry"); offered=true; return SessionReplicationHello.Encode(actor,binding.Session,binding.Content,binding.Epoch); }
        public RuleResult Bind(string connection,byte[] authorityHello)
        {
            if(busy)return RuleResult.Reject("reentry"); busy=true;
            try
            {
                AuthenticatedPeer p=binding.Resolve(connection,true);
                if(!offered || !SessionReplicationHello.Matches(authorityHello,p) || (authorityActor!=null && p.Actor!=authorityActor))return RuleResult.Reject("negotiation");
                // Authorize re-resolves the trusted record before receiver commit.
                RuleResult r=receiver.BeginAuthenticatedReconnect(connection,p.Generation,authorityHello,this);
                if(r.Accepted) { binding.Connection=connection; binding.Record=p; authorityActor=p.Actor; offered=false; }
                return r;
            }
            finally { busy=false; }
        }
        RuleResult IReplicationAdmission.Authorize(string peer,long generation,string session,string content,long epoch)
        { AuthenticatedPeer p=binding.Resolve(peer,true); return busy && p!=null && p.Generation==generation && p.Session==session && p.Content==content && p.Epoch==epoch
              ? RuleResult.Accept() : RuleResult.Reject("principal"); }
        public RuleResult Receive(string connection,byte[] data)
        {
            if(busy)return RuleResult.Reject("reentry"); busy=true;
            try
            {
                if(!binding.Current(connection,true))return RuleResult.Reject("principal");
                long generation,id; byte[] body; Packet state;
                if(!ResyncWire.TryDecodeState(data,out generation,out id,out body) || !Wire.TryDecode(body,out state))return RuleResult.Reject("frame");
                if(state.ActorId!=authorityActor)return RuleResult.Reject("authority-actor");
                if(id!=0 && (generation!=sentGeneration || id!=sentRequest))return RuleResult.Reject("resync-request");
                applying=true; return receiver.ReceiveFrame(connection,data,this);
            }
            finally { applying=false; busy=false; }
        }
        RuleResult IReplicationSink.Apply(bool full,long sequence,byte[] payload)
        {
            SessionReplica next;
            if(!applying || !full || !MemberCodec.Decode(payload,actor,out next))return RuleResult.Reject("member-snapshot");
            View=next; return RuleResult.Accept(); // validate completely, then one atomic reference swap; no callbacks
        }
        public RuleResult Request(double now,out byte[] data)
        {
            data=null; if(busy)return RuleResult.Reject("reentry"); busy=true;
            try
            {
                if(Double.IsNaN(now) || Double.IsInfinity(now) || now<0 || now<lastNow)return RuleResult.Reject("clock"); lastNow=now;
                if(!binding.Current(binding.Connection,true))return RuleResult.Reject("principal");
                ResyncRequest r=receiver.PendingResync; if(r==null)return RuleResult.Reject("synchronized");
                if(r.Generation==sentGeneration && r.RequestId==sentRequest && now-lastRequest<1)return RuleResult.Reject("retry-time");
                data=ResyncWire.EncodeRequest(r); lastRequest=now; sentGeneration=r.Generation; sentRequest=r.RequestId; return RuleResult.Accept();
            }
            finally { busy=false; }
        }
        public RuleResult Disconnect(string connection,long generation)
        {
            if(busy)return RuleResult.Reject("reentry"); busy=true;
            try { receiver.Disconnect(connection,generation); if(binding.Record!=null && binding.Connection==connection && binding.Record.Generation==generation)binding.Record=null;
                  return RuleResult.Accept(); } finally { busy=false; }
        }
    }
}


