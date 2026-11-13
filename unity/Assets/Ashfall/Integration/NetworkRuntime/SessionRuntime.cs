using System;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
namespace Ashfall.Integration.NetworkRuntime
{
    // Owns the coordinator: callers cannot receive/drain its queue directly.
    // Auth registry, clocks, world and authority remain externally trusted ports.
    public sealed class SessionRuntimeHost
    {
        private sealed class Principals : ISessionPrincipal
        {
            private readonly IAuthenticatedPeerPort peers;
            internal Principals(IAuthenticatedPeerPort peers) { this.peers=peers; }
            public bool Resolve(string connection,out string actor)
            { AuthenticatedPeer p; bool ok=peers.TryResolve(connection,out p) && p!=null && !p.Authority; actor=ok?p.Actor:null; return ok; }
        }
        private readonly SessionNetworkCoordinator sessions;
        private readonly SessionHostLane lane;
        private readonly IAuthenticatedPeerPort peers;
        private readonly string authority;
        private readonly int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        public SessionRuntimeHost(string id,string hash,string authority,SessionRules rules,ISessionClock clock,
            IReconnectTokens tokens,ISpawnWorld world,ISessionManagement managers,IAuthorityGate lifecycle,
            IAuthenticatedPeerPort peers,ISessionPlacement placement,int queueCapacity,int replayWindow,long tickWindow)
        {
            if(peers==null || !Wire.IsText(authority,64,false))throw new ArgumentException("configuration");
            this.peers=peers;this.authority=authority;
            sessions=new SessionNetworkCoordinator(id,hash,rules,clock,tokens,world,managers,lifecycle,new Principals(peers),placement,queueCapacity,replayWindow,tickWindow);
            lane=new SessionHostLane(sessions,peers,rules.Capacity,new CoordinatorBindingRetirement(sessions));
        }
        public long Epoch { get { return sessions.Epoch; } }
        public int PendingDispatches { get { return sessions.PendingDispatches; } }
        public string CaptureDispatchDiagnostics() { return sessions.CaptureDispatchDiagnostics(); }
        public MemberSnapshot Get(string actor) { return sessions.Get(actor); }
        public SessionHostChannel Open(string connection,int sendCapacity)
        {
            if(thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("host-lane");
            AuthenticatedPeer p;
            if(!peers.TryResolve(connection,out p) || p==null || p.Authority || p.Epoch!=Epoch)throw new ArgumentException("principal");
            return lane.CreateChannel(connection,p,authority,sendCapacity);
        }
        public RuleResult DispatchOne(long tick) { return lane.DispatchOne(tick); }
        public RuleResult Lifecycle(CommandContext context,LifecycleAction action,SessionRules next,string mapHash,bool match)
        { return sessions.Lifecycle(context,action,next,mapHash,match); }
    }
    // New instance for each trusted generation/epoch, with wire sequence restarting1.
    // A failed native send retains the EXACT frame; client does not persist numbers.
    public sealed class SessionRuntimeClient
    {
        private readonly SessionClientChannel channel;
        private readonly string actor;
        private readonly AuthenticatedPeer authority;
        private byte[] command;
        private long sequence;
        private bool busy;
        private readonly int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        public SessionRuntimeClient(IAuthenticatedPeerPort peers,string connection,string actor,AuthenticatedPeer authority)
        { channel=new SessionClientChannel(peers,connection,actor,authority);this.actor=actor;this.authority=authority; }
        public ChannelState State { get { return channel.State; } }
        public SessionReplica View { get { return channel.View; } }
        public byte[] Offer() { return channel.Offer(); }
        public RuleResult Bind(long generation,byte[] hello) { return channel.Bind(generation,hello); }
        public RuleResult Receive(long generation,byte[] frame) { return channel.Receive(generation,frame); }
        public RuleResult Request(double now,IFrameSender sender) { return channel.Request(now,sender); }
        public RuleResult PrepareCommand(string id,string feature,byte[] payload,long tick)
        {
            if(thread!=System.Threading.Thread.CurrentThread.ManagedThreadId || busy)return RuleResult.Reject("client-lane");
            if(channel.State!=ChannelState.Active || command!=null || sequence==long.MaxValue || tick<0)return RuleResult.Reject("command-state");
            byte[] next;
            try { next=Wire.Encode(new Packet {Kind=PacketKind.Command,SessionId=authority.Session,ContentHash=authority.Content,Epoch=authority.Epoch,
                ActorId=actor,Sequence=sequence+1,CommandId=id,FeatureId=feature,Payload=payload,Tick=tick}); }
            catch(ArgumentException) { return RuleResult.Reject("command-frame"); }
            command=next;sequence++;return RuleResult.Accept();
        }
        public RuleResult FlushCommand(IFrameSender sender)
        {
            if(thread!=System.Threading.Thread.CurrentThread.ManagedThreadId || busy)return RuleResult.Reject("client-lane");
            if(command==null)return RuleResult.Reject("empty");busy=true;
            try { RuleResult r=channel.SendCommand(command,sender);if(r.Accepted || channel.State==ChannelState.Closed)command=null;return r; }
            finally { busy=false; }
        }
        public RuleResult Close() { if(busy)return RuleResult.Reject("client-lane");RuleResult r=channel.Close();if(r.Accepted)command=null;return r; }
    }
}
