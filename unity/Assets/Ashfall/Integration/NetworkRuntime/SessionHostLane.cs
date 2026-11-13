using System;
using Ashfall.Core;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
namespace Ashfall.Integration.NetworkRuntime
{
    // Exclusive dispatcher for the supplied coordinator. Do not mix direct external
    // coordinator dispatch with this lane. Ports remain pure/nonthrowing as contracted.
    public sealed class SessionHostLane
    {
        private readonly SessionNetworkCoordinator sessions;
        private readonly IAuthenticatedPeerPort registry;
        private readonly SessionHostChannel[] channels;
        private readonly int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        private bool busy;
        private readonly IBindingRetirement retirement;
        public SessionHostLane(SessionNetworkCoordinator sessions,IAuthenticatedPeerPort registry,int capacity)
            : this(sessions,registry,capacity,null) { }
        internal SessionHostLane(SessionNetworkCoordinator sessions,IAuthenticatedPeerPort registry,int capacity,IBindingRetirement retirement)
        {
            if(sessions==null || registry==null || capacity<1 || capacity>256)throw new ArgumentException("configuration");
            this.sessions=sessions;this.registry=registry;channels=new SessionHostChannel[capacity];
            this.retirement=retirement;
        }
        public SessionHostChannel CreateChannel(string connection,AuthenticatedPeer principal,string authority,int sendCapacity)
        {
            if(busy || thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("host-lane");
            for(int i=0;i<channels.Length;i++)if(channels[i]==null || channels[i].State==ChannelState.Closed)
            { var c=new SessionHostChannel(sessions,registry,connection,principal,authority,sendCapacity,retirement);channels[i]=c;return c; }
            throw new InvalidOperationException("channel-capacity");
        }
        public RuleResult DispatchOne(long tick)
        {
            if(busy || thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)return RuleResult.Reject("host-lane");busy=true;
            try
            {
                // Retire EVERY revoked lease before the shared queue can publish.
                foreach(var c in channels)if(c!=null) { RuleResult r=c.Reconcile(tick);if(!r.Accepted)return r; }
                foreach(var c in channels)if(c!=null && c.State==ChannelState.Active)return c.DispatchOne();
                return RuleResult.Reject("no-active-channel");
            } finally { busy=false; }
        }
    }
}
