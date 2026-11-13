using System;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
namespace Ashfall.Integration.NetworkRuntime
{
    // One immutable physical connection per object; do not reuse after Closed.
    // External auth and JoinRequest provisioning are required coordinator inputs.
    public sealed class SessionHostChannel
    {
        private readonly SessionNetworkCoordinator sessions;
        private readonly SessionReplicationHost host;
        private readonly IAuthenticatedPeerPort registry;
        private readonly string connection;
        private readonly AuthenticatedPeer principal;
        private readonly byte[][] outgoing;
        private readonly int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        private int head,count;
        private bool busy;
        private readonly IBindingRetirement retirement;
        private object retirementReceipt;
        public ChannelState State { get; private set; }
        public int PendingSends { get { return count; } }
        public string CaptureDiagnostics()
        {
            if(busy || thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("host-lane");
            AuthenticatedPeer p;bool found=registry.TryResolve(connection,out p) && p!=null;
            var member=sessions.Get(principal.Actor);
            return "channel="+State+";connection="+connection+";capturedAuth="+principal.Generation+
                ";capturedEpoch="+principal.Epoch+";registry="+found+";registryAuth="+(found?p.Generation:-1)+
                ";registryEpoch="+(found?p.Epoch:-1)+";current="+Current()+";domainMember="+(member!=null)+
                ";domainLoaded="+(member!=null && member.Loaded)+";pendingSends="+count+";"+sessions.CaptureDispatchDiagnostics();
        }
        internal SessionHostChannel(SessionNetworkCoordinator sessions,IAuthenticatedPeerPort registry,string connection,
            AuthenticatedPeer principal,string authority,int sendCapacity,IBindingRetirement retirement)
        {
            if(sessions==null || registry==null || principal==null || principal.Authority ||
                !Wire.IsText(connection,256,false) || principal.Generation<=0 || sendCapacity<1)throw new ArgumentException("configuration");
            this.sessions=sessions; this.registry=registry; this.connection=connection; this.principal=principal;
            outgoing=new byte[sendCapacity][];
            this.retirement=retirement;
            host=new SessionReplicationHost(sessions,registry,principal.Actor,authority,principal.Session,principal.Content,principal.Epoch);
        }
        private bool Enter() { if(busy || thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)return false; busy=true; return true; }
        private bool Current()
        {
            AuthenticatedPeer p;
            return sessions.Epoch==principal.Epoch && registry.TryResolve(connection,out p) && p!=null && !p.Authority &&
                p.Actor==principal.Actor && p.Session==principal.Session && p.Content==principal.Content &&
                p.Epoch==principal.Epoch && p.Generation==principal.Generation;
        }
        private void Enqueue(byte[] bytes) { outgoing[(head+count)%outgoing.Length]=bytes; count++; }
        private void Clear() { Array.Clear(outgoing,0,outgoing.Length); head=0; count=0; }
        private RuleResult Live()
        {
            if(State!=ChannelState.Active)return RuleResult.Reject("channel-state");
            if(Current())return RuleResult.Accept();
            State=ChannelState.Closing; Clear(); return RuleResult.Reject("registry-or-epoch-revoked");
        }
        public RuleResult Admit(byte[] offer,JoinRequest join,long tick,out string token)
        {
            token=null; if(!Enter())return RuleResult.Reject("host-lane");
            try
            {
                if(State!=ChannelState.Unbound || !Current())return RuleResult.Reject("principal");
                byte[] ack; RuleResult r=host.Admit(connection,offer,join,tick,out token,out ack);
                if(r.Accepted) {
                    if(retirement!=null)retirementReceipt=retirement.Capture(connection,sessions.ConnectionGeneration(connection),principal.Generation);
                    Enqueue(ack); State=ChannelState.Active;
                } return r;
            } finally { busy=false; }
        }
        public RuleResult Receive(long capturedGeneration,byte[] bytes,long tick,double now)
        {
            if(!Enter())return RuleResult.Reject("host-lane");
            try
            {
                RuleResult r=Live(); if(!r.Accepted)return r;
                if(capturedGeneration!=principal.Generation)return RuleResult.Reject("connection-generation");
                ResyncRequest request;
                if(ResyncWire.TryDecodeRequest(bytes,out request))
                {
                    if(count==outgoing.Length)return RuleResult.Reject("send-backpressure");
                    byte[] reply; r=host.Respond(connection,bytes,now,out reply); if(r.Accepted)Enqueue(reply); return r;
                }
                Packet p; if(!Wire.TryDecode(bytes,out p) || p.Kind!=PacketKind.Command)return RuleResult.Reject("command-frame");
                return host.ReceiveCommand(connection,capturedGeneration,bytes,tick);
            } finally { busy=false; }
        }
        internal RuleResult DispatchOne()
        {
            if(!Enter())return RuleResult.Reject("host-lane");
            try { RuleResult r=Live(); if(!r.Accepted)return r; host.DispatchOne(out r); return r; }
            finally { busy=false; }
        }
        internal RuleResult Reconcile(long tick)
        {
            if(State==ChannelState.Closed || State==ChannelState.Unbound)return RuleResult.Accept();
            if(State==ChannelState.Active && Current())return RuleResult.Accept();
            return Close(tick);
        }
        public RuleResult FlushOne(IFrameSender sender)
        {
            if(sender==null)throw new ArgumentNullException("sender");
            if(!Enter())return RuleResult.Reject("host-lane");
            try
            {
                RuleResult r=Live(); if(!r.Accepted)return r;
                if(count==0)return RuleResult.Reject("empty");
                r=sender.TrySend(outgoing[head]);
                if(r.Accepted) { outgoing[head]=null; head=(head+1)%outgoing.Length; count--; } return r;
            } finally { busy=false; }
        }
        // Retains exact departure receipt on failure, including registry revocation.
        // Caller retries with a valid host tick; never repairs by resetting credentials.
        public RuleResult Close(long tick)
        {
            if(!Enter())return RuleResult.Reject("host-lane");
            try
            {
                if(State==ChannelState.Closed)return RuleResult.Accept();
                Clear(); if(State==ChannelState.Unbound) { State=ChannelState.Closed; return RuleResult.Accept(); }
                State=ChannelState.Closing;
                if(sessions.Epoch>principal.Epoch)
                {
                    RuleResult retired=retirement==null ? RuleResult.Reject("coordinator-retirement-api-required") : retirement.Retire(retirementReceipt,sessions.Epoch);
                    if(retired.Accepted)State=ChannelState.Closed;
                    return retired;
                }
                RuleResult r=host.Disconnect(connection,principal.Generation,tick,false);
                if(r.Accepted)State=ChannelState.Closed; return r;
            } finally { busy=false; }
        }
    }
}
