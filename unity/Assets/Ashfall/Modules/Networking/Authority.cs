using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.Networking
{
    public sealed partial class AuthorityHost
    {
        private sealed class Actor
        {
            public readonly HashSet<string> Pending=new HashSet<string>(StringComparer.Ordinal);
            public long Sequence, Revision, Tick; public readonly HashSet<string> Seen=new HashSet<string>(StringComparer.Ordinal);
            public readonly Queue<string> Order=new Queue<string>();
        }
        private sealed class Work { public string Peer; public long Generation; public Packet Packet; public Actor Owner; public string Receipt; public long Tick; public PeerLease Lease; }
        private readonly Dictionary<string,string> peers;
        private long generation; private readonly Dictionary<string,long> generations;
        private readonly Dictionary<string,Actor> actors;
        private readonly BoundedQueue<Work> commands;
        private readonly int maxActors, replayWindow;
        private readonly long tickWindow;
        public readonly string SessionId, ContentHash;
        public long Epoch { get; private set; }
        public int PendingCount { get { return commands.Count; } }
        public AuthorityHost(string sessionId,string contentHash,long epoch,int maxActors,int queueCapacity,int replayWindow,long tickWindow)
            : this(sessionId,contentHash,epoch,maxActors,queueCapacity,replayWindow,tickWindow,Math.Max(maxActors,4096)) { }
        public AuthorityHost(string sessionId,string contentHash,long epoch,int maxActors,int queueCapacity,int replayWindow,long tickWindow,int identityHistoryCapacity)
        {
            if(!Wire.IsText(sessionId,64,false) || !Wire.IsHash(contentHash) || epoch<0 || maxActors<1 || replayWindow<1 || tickWindow<0 || identityHistoryCapacity<maxActors) throw new ArgumentException("configuration");
            SessionId=sessionId; ContentHash=contentHash; Epoch=epoch; this.maxActors=maxActors; this.replayWindow=replayWindow; this.tickWindow=tickWindow;
            historyCapacity=identityHistoryCapacity;
            peers=new Dictionary<string,string>(maxActors,StringComparer.Ordinal);
            generations=new Dictionary<string,long>(maxActors,StringComparer.Ordinal);
            leases=new Dictionary<string,PeerLease>(maxActors,StringComparer.Ordinal);
            actors=new Dictionary<string,Actor>(identityHistoryCapacity,StringComparer.Ordinal);
            reservations=new PeerReservation[maxActors];
            commands=new BoundedQueue<Work>(queueCapacity);
        }
        private RuleResult Compatible(Packet p)
        {
            if(p==null || p.Protocol!=Protocol.Name || p.Schema!=Protocol.Schema || p.ContentHash!=ContentHash) return RuleResult.Reject("compatibility");
            if(p.SessionId!=SessionId || p.Epoch!=Epoch) return RuleResult.Reject("session-epoch");
            return RuleResult.Accept();
        }
        // actorId comes from coordinator identity admission, never from the client packet.
        public RuleResult Admit(string transportPeer,string actorId,byte[] hello)
        {
            PeerReservation r;
            RuleResult result=PrepareAdmission(transportPeer,actorId,hello,clock,long.MaxValue,out r);
            if(!result.Accepted) return result;
            result=Validate(r,clock);
            if(!result.Accepted) { Abort(r); return result; }
            CommitValidated(r); return RuleResult.Accept();
        }
        // Compatibility only: transportPeer must be a unique immutable connection key.
        // Reusable keys and asynchronous disconnects MUST use Release(PeerLease).
        public void Disconnect(string transportPeer)
        { PeerLease lease; if(transportPeer!=null && leases.TryGetValue(transportPeer,out lease)) Release(lease); }
        public void AdvanceEpoch(long epoch)
        {
            if(epoch<=Epoch) throw new ArgumentException("epoch-must-increase");
            Epoch=epoch; peers.Clear(); generations.Clear(); leases.Clear(); actors.Clear(); commands.Clear();
            foreach(var r in reservations) if(r!=null) r.Aborted=true;
            Array.Clear(reservations,0,reservations.Length); membershipRevision=0; clock=0;
        }
        public RuleResult Receive(string transportPeer,byte[] bytes,long hostTick)
        {
            string actorId; if(transportPeer==null || !peers.TryGetValue(transportPeer,out actorId)) return RuleResult.Reject("not-admitted");
            Packet p; if(!Wire.TryDecode(bytes,out p) || p.Kind!=PacketKind.Command) return RuleResult.Reject("command");
            RuleResult check=Compatible(p); if(!check.Accepted) return check;
            if(p.ActorId!=actorId) return RuleResult.Reject("actor-spoof");
            if(hostTick<0 || p.Tick<0 || (p.Tick>hostTick ? p.Tick-hostTick : hostTick-p.Tick)>tickWindow) return RuleResult.Reject("tick-window");
            if(p.Sequence<=0 || String.IsNullOrEmpty(p.CommandId) || String.IsNullOrEmpty(p.FeatureId)) return RuleResult.Reject("command-id");
            PeerLease lease=leases[transportPeer];
            Actor a=actors[lease.ReplayIdentity];
            if(a.Revision==long.MaxValue || a.Sequence==long.MaxValue) return RuleResult.Reject("history-revision-exhausted");
            if(p.Sequence<=lease.IngressSequence || lease.IngressSeen.Contains(p.CommandId)) return RuleResult.Reject("replay");
            // Trusted, injective normalization occurs only after exact lease admission.
            // Canonical history survives departure; raw ingress numbering belongs to the lease.
            string receipt="network-generation/"+lease.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture)+"/"+p.CommandId.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+p.CommandId;
            if(a.Pending.Contains(receipt))return RuleResult.Reject("replay");
            long tick=Math.Max(a.Tick,p.Tick);
            if(!commands.TryAdd(new Work { Peer=transportPeer,Generation=lease.Generation,Packet=p,Owner=a,Receipt=receipt,Tick=tick,Lease=lease })) return RuleResult.Reject("backpressure");
            lease.IngressSequence=p.Sequence;
            a.Sequence++; a.Tick=tick; a.Pending.Add(receipt); a.Revision++;
            return RuleResult.Accept();
        }
        // Host stamps authority; clients have no serialized IsAuthority bit. Invalid domain commands remain consumed.
        public bool DispatchOne(ICommandSink sink,out RuleResult result)
        {
            if(sink==null) throw new ArgumentNullException("sink");
            Work w; if(!commands.TryTake(out w)) { result=RuleResult.Reject("empty"); return false; }
            string actor; Packet p=w.Packet;
            try
            {
                if(!peers.TryGetValue(w.Peer,out actor) || actor!=p.ActorId || generations[w.Peer]!=w.Generation || p.Epoch!=Epoch) { result=RuleResult.Reject("disconnected"); return true; }
                result=sink.Apply(new CommandContext(SessionId,actor,w.Receipt,w.Tick,Epoch,true),p.FeatureId,p.Payload);
                return true;
            }
            finally
            {
                // Pending protection survives disconnect and stays live during sink execution.
                // Discarded/rejected/throwing work is consumed too; never retry a partial mutation.
                w.Lease.IngressSeen.Add(p.CommandId); w.Lease.IngressOrder.Enqueue(p.CommandId);
                while(w.Lease.IngressOrder.Count>replayWindow) w.Lease.IngressSeen.Remove(w.Lease.IngressOrder.Dequeue());
                Actor a=w.Owner; if(a.Revision<long.MaxValue) a.Revision++; a.Pending.Remove(w.Receipt); a.Seen.Add(w.Receipt); a.Order.Enqueue(w.Receipt);
                while(a.Order.Count>replayWindow) a.Seen.Remove(a.Order.Dequeue());
            }
        }
    }
    // Opaque payload application is coordinator-owned. Sequence advances only after successful application.
    public sealed class ReplicationReceiver
    {
        private string authorityPeer; private readonly string session,content; private readonly long epoch;
        private long generation, requestId=1; private bool connected=true, framed;
        public long Generation { get { return generation; } }
        public ResyncRequest PendingResync { get { return NeedsSnapshot ? new ResyncRequest(generation,requestId,Sequence) : null; } }
        public long Sequence { get; private set; }
        public bool NeedsSnapshot { get; private set; }
        public ReplicationReceiver(string authorityPeer,string session,string content,long epoch)
        { if(String.IsNullOrEmpty(authorityPeer) || !Wire.IsText(session,64,false) || !Wire.IsHash(content) || epoch<0) throw new ArgumentException("configuration");
          this.authorityPeer=authorityPeer; this.session=session; this.content=content; this.epoch=epoch; Sequence=-1; NeedsSnapshot=true; }
        public RuleResult Receive(string peer,byte[] bytes,IReplicationSink sink)
        {
            if(framed || !connected) return RuleResult.Reject("generation-required");
            return ApplyState(peer,bytes,sink,false);
        }
        // Caller-owned authentication is mandatory; compatibility alone cannot bind a peer.
        // Preserve watermark: a new authority epoch/session requires a NEW receiver.
        public RuleResult BeginAuthenticatedReconnect(string peer,long nextGeneration,byte[] hello,IReplicationAdmission admission)
        {
            Packet p;
            if(String.IsNullOrEmpty(peer) || nextGeneration<=generation || admission==null) return RuleResult.Reject("generation");
            if(!Wire.TryDecode(hello,out p) || p.Kind!=PacketKind.Hello || !CompatibleState(p)) return RuleResult.Reject("compatibility");
            RuleResult r=admission.Authorize(peer,nextGeneration,session,content,epoch);
            if(!r.Accepted) return r;
            long nextRequest=checked(requestId+1);
            authorityPeer=peer; generation=nextGeneration; requestId=nextRequest;
            framed=true; connected=true; NeedsSnapshot=true; return RuleResult.Accept();
        }
        public void Disconnect(string peer,long connectionGeneration)
        {
            if(peer==authorityPeer && connectionGeneration==generation) { connected=false; RequireSnapshot(); }
        }
        // Retransmit PendingResync unchanged on timeout. It is consumed only after sink success.
        public RuleResult ReceiveFrame(string peer,byte[] bytes,IReplicationSink sink)
        {
            long g,id; byte[] state;
            if(!framed || !connected || peer!=authorityPeer || !ResyncWire.TryDecodeState(bytes,out g,out id,out state) || g!=generation)
                return RuleResult.Reject("generation");
            if(id!=0 && (!NeedsSnapshot || id!=requestId)) return RuleResult.Reject("resync-request");
            Packet p;
            if(!Wire.TryDecode(state,out p)) return RuleResult.Reject("authority-packet");
            bool recovery=NeedsSnapshot && id==requestId && p.Kind==PacketKind.Snapshot;
            if(p.Kind==PacketKind.Snapshot && NeedsSnapshot && !recovery) return RuleResult.Reject("resync-request");
            if(p.Kind==PacketKind.Delta && id!=0) return RuleResult.Reject("resync-request");
            return ApplyState(peer,state,sink,recovery);
        }
        private bool CompatibleState(Packet p)
        { return p.Protocol==Protocol.Name && p.Schema==Protocol.Schema && p.ContentHash==content && p.SessionId==session && p.Epoch==epoch; }
        private void RequireSnapshot()
        { if(!NeedsSnapshot) { requestId=checked(requestId+1); NeedsSnapshot=true; } }
        private RuleResult ApplyState(string peer,byte[] bytes,IReplicationSink sink,bool recovery)
        {
            if(sink==null) throw new ArgumentNullException("sink");
            Packet p;
            if(peer!=authorityPeer || !Wire.TryDecode(bytes,out p) || (p.Kind!=PacketKind.Snapshot && p.Kind!=PacketKind.Delta)) return RuleResult.Reject("authority-packet");
            if(!CompatibleState(p)) return RuleResult.Reject("compatibility");
            if(p.Sequence<0 || p.Sequence<Sequence || (p.Sequence==Sequence && !recovery)) return RuleResult.Reject("stale-state");
            if(p.Kind==PacketKind.Delta && (NeedsSnapshot || p.BaseSequence!=Sequence)) { RequireSnapshot(); return RuleResult.Reject("snapshot-required"); }
            RuleResult result;
            try { result=sink.Apply(p.Kind==PacketKind.Snapshot,p.Sequence,p.Payload); }
            catch { RequireSnapshot(); throw; }
            if(result.Accepted) { Sequence=p.Sequence; NeedsSnapshot=false; }
            else RequireSnapshot();
            return result;
        }
    }
}

