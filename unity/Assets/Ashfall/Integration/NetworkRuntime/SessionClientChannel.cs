using System;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Integration.SessionReplication;
namespace Ashfall.Integration.NetworkRuntime
{
    public sealed class SessionClientChannel
    {
        private readonly SessionReplicationClient client;
        private readonly IAuthenticatedPeerPort registry;
        private readonly string connection,actor;
        private readonly AuthenticatedPeer authority;
        private readonly int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        private byte[] pending;
        private bool busy;
        public ChannelState State { get; private set; }
        public SessionReplica View { get { return client.View; } }
        public SessionClientChannel(IAuthenticatedPeerPort registry,string connection,string actor,AuthenticatedPeer authority)
        {
            if(registry==null || authority==null || !authority.Authority || authority.Generation<=0 || !Wire.IsText(connection,256,false))throw new ArgumentException("configuration");
            this.registry=registry;this.connection=connection;this.actor=actor;this.authority=authority;
            client=new SessionReplicationClient(registry,actor,authority.Session,authority.Content,authority.Epoch);
        }
        private bool Enter() { if(busy || thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)return false;busy=true;return true; }
        private bool Current()
        {
            AuthenticatedPeer p;
            return registry.TryResolve(connection,out p) && p!=null && p.Authority && p.Actor==authority.Actor && p.Session==authority.Session &&
                p.Content==authority.Content && p.Epoch==authority.Epoch && p.Generation==authority.Generation;
        }
        private RuleResult Live()
        {
            if(State!=ChannelState.Active)return RuleResult.Reject("channel-state");
            if(Current())return RuleResult.Accept();CloseCore();return RuleResult.Reject("registry-revoked");
        }
        public byte[] Offer()
        {
            if(!Enter())throw new InvalidOperationException("client-lane");
            try { if(State!=ChannelState.Unbound || !Current())throw new InvalidOperationException("principal");return client.Offer(); }
            finally { busy=false; }
        }
        public RuleResult Bind(long capturedGeneration,byte[] hello)
        {
            if(!Enter())return RuleResult.Reject("client-lane");
            try
            {
                if(State!=ChannelState.Unbound || capturedGeneration!=authority.Generation || !Current())return RuleResult.Reject("principal");
                RuleResult r=client.Bind(connection,hello);if(r.Accepted)State=ChannelState.Active;return r;
            } finally { busy=false; }
        }
        public RuleResult Receive(long capturedGeneration,byte[] frame)
        {
            if(!Enter())return RuleResult.Reject("client-lane");
            try { RuleResult r=Live();if(!r.Accepted)return r;return capturedGeneration==authority.Generation?client.Receive(connection,frame):RuleResult.Reject("connection-generation"); }
            finally { busy=false; }
        }
        // One retained recovery request; native pressure retries identical bytes.
        public RuleResult Request(double now,IFrameSender sender)
        {
            if(sender==null)throw new ArgumentNullException("sender");if(!Enter())return RuleResult.Reject("client-lane");
            try
            {
                RuleResult r=Live();if(!r.Accepted)return r;
                if(pending==null) { r=client.Request(now,out pending);if(!r.Accepted)return r; }
                r=sender.TrySend(pending);if(r.Accepted)pending=null;return r;
            } finally { busy=false; }
        }
        public RuleResult SendCommand(byte[] bytes,IFrameSender sender)
        {
            if(sender==null)throw new ArgumentNullException("sender");if(!Enter())return RuleResult.Reject("client-lane");
            try
            {
                RuleResult r=Live();if(!r.Accepted)return r;Packet p;
                if(!Wire.TryDecode(bytes,out p) || p.Kind!=PacketKind.Command || p.ActorId!=actor || p.Protocol!=Protocol.Name || p.Schema!=Protocol.Schema ||
                    p.SessionId!=authority.Session || p.ContentHash!=authority.Content || p.Epoch!=authority.Epoch)return RuleResult.Reject("command-identity");
                return sender.TrySend(bytes);
            } finally { busy=false; }
        }
        private void CloseCore() { pending=null;client.Disconnect(connection,authority.Generation);State=ChannelState.Closed; }
        public RuleResult Close()
        {
            if(!Enter())return RuleResult.Reject("client-lane");try { if(State!=ChannelState.Closed)CloseCore();return RuleResult.Accept(); }finally { busy=false; }
        }
    }
}
