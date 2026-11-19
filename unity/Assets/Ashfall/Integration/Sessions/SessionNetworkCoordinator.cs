using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;

namespace Ashfall.Integration.Sessions
{
    // Candidate composition; one serialized lane, trusted ports must not throw/reenter.
    // No packet can invoke global lifecycle, elimination, or manufacture authority.
    public sealed class SessionNetworkCoordinator : ICommandSink, IAuthorityGate
    {
        private sealed class Binding { public string Actor; public bool Active; public long Generation; public PeerLease Lease; public long AuthenticatedGeneration; public bool DispatchRevoked; }
        private Dictionary<string,Binding> bindings=new Dictionary<string,Binding>(StringComparer.Ordinal);
        // Exact parallel queue, exclusively written after accepted network ingress.
        // Preallocated to the same bound; stale network work consumes its owner too.
        private HashSet<string> rosterActors=new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<Binding> queuedOwners;
        private Binding queuedOwner;
        private object registryIdentity;
        private IDispatchAuthorization dispatchAuthorization;
        private readonly Session session;
        private readonly AuthorityHost network;
        private readonly ISessionPrincipal principals;
        private readonly ISessionPlacement placement;
        private readonly IAuthorityGate lifecycleAuthority;
        private CommandContext current;
        private bool applying;
        private bool dispatching;
        private bool admitting;
        private readonly int hostThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        public int IdentityHistoryCount { get { return network.IdentityCount; } }
        private long admissionSequence;
        private string completedPeer;
        private long completedGeneration=-1;
        private bool completedKick;
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        public SessionNetworkCoordinator(string id,string hash,SessionRules rules,ISessionClock clock,
            IReconnectTokens tokens,ISpawnWorld world,ISessionManagement managers,IAuthorityGate lifecycleAuthority,
            ISessionPrincipal principals,ISessionPlacement placement,int queueCapacity,int replayWindow,long tickWindow)
        {
            if(principals==null || placement==null || lifecycleAuthority==null)throw new ArgumentNullException("ports");
            this.principals=principals; this.placement=placement; this.lifecycleAuthority=lifecycleAuthority;
            queuedOwners=new Queue<Binding>(queueCapacity);
            network=new AuthorityHost(id,hash,0,rules.Capacity,queueCapacity,replayWindow,tickWindow);
            session=new Session(id,hash,rules,this,clock,tokens,world,managers);
        }
        public long Epoch { get { return session.Epoch; } }
        public SessionStage Stage { get { return session.Stage; } }
        public long LifecycleSequence { get { return session.LifecycleSequence; } }
        public MemberSnapshot Get(string actor) { return session.Get(actor); }
        // Opaque actual Session incarnation, never a caller-selected generation.
        public object GetConnectionIdentity(string actor) { return session.GetConnectionIdentity(actor); }
        public int PendingDispatches { get { return queuedOwners.Count; } }
        // Trusted diagnostic capture only; no mutable binding or authority escapes.
        public string CaptureDispatchDiagnostics()
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)throw new InvalidOperationException("reentry");
            var text=new StringBuilder();
            text.Append("epoch=").Append(Epoch).Append(";stage=").Append(Stage)
                .Append(";networkPending=").Append(network.PendingCount).Append(";ownerPending=").Append(queuedOwners.Count);
            int index=0;
            foreach(Binding b in queuedOwners)
            {
                Binding live; bool same=bindings.TryGetValue(b.Lease.PeerId,out live) && Object.ReferenceEquals(b,live);
                MemberSnapshot m=session.Get(b.Actor);
                text.Append(";owner[").Append(index++).Append("]=peer:").Append(b.Lease.PeerId)
                    .Append(",actor:").Append(b.Actor).Append(",lease:").Append(b.Generation)
                    .Append(",auth:").Append(b.AuthenticatedGeneration).Append(",epoch:").Append(b.Lease.Epoch)
                    .Append(",active:").Append(b.Active).Append(",revoked:").Append(b.DispatchRevoked)
                    .Append(",currentBinding:").Append(same).Append(",member:").Append(m!=null)
                    .Append(",loaded:").Append(m!=null && m.Loaded);
            }
            return text.ToString();
        }
        RuleResult IAuthorityGate.Validate(CommandContext c)
        {
            return applying && c.SessionId==current.SessionId && c.ActorId==current.ActorId &&
                c.CommandId==current.CommandId && c.Tick==current.Tick && c.AuthorityEpoch==current.AuthorityEpoch && c.IsAuthority
                ? RuleResult.Accept() : RuleResult.Reject("coordinator-dispatch-required");
        }
        private CommandContext Admission(string actor,long tick)
        { return new CommandContext(network.SessionId,actor,"sessions-host/"+(++admissionSequence).ToString(CultureInfo.InvariantCulture),admissionSequence,Epoch,true); }
        private void Start(CommandContext c) { current=c; applying=true; }
        private void Finish()
        {
            applying=false;
            if(network.Epoch!=Epoch)
            {
                network.AdvanceEpoch(Epoch); queuedOwners.Clear();
                foreach(Binding b in bindings.Values)b.Active=false;
            }
        }
        // Caller supplies authentic transport peer, host tick and untrusted hello/request.
        public RuleResult Admit(string peer,byte[] hello,JoinRequest request,long hostTick,out string token)
        {
            token=null;
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching || hostTick<0 || admissionSequence==Int64.MaxValue)return RuleResult.Reject("coordinator-state");
            admitting=true;
            PeerReservation reservation=null;
            Session.PreparedJoin join=null;
            try
            {
                string actor;
                if(!principals.Resolve(peer,out actor))return RuleResult.Reject("principal");
                Binding prior;
                if(bindings.TryGetValue(peer,out prior) && prior.Actor!=actor)return RuleResult.Reject("principal-changed");
                RuleResult r=network.PrepareAdmission(peer,actor,session.ReplayIdentity(actor,request),hello,hostTick,Int64.MaxValue,out reservation);
                if(!r.Accepted)return r;
                bool rehello=session.Get(actor)!=null;
                // Allocate the entire binding publication before final validation.
                var nextBindings=new Dictionary<string,Binding>(bindings,StringComparer.Ordinal);
                nextBindings[peer]=new Binding {Actor=actor,Active=true,Generation=reservation.Lease.Generation,Lease=reservation.Lease};
                var nextRoster=new HashSet<string>(StringComparer.Ordinal);
                foreach(string known in rosterActors)if(session.Get(known)!=null)nextRoster.Add(known);
                nextRoster.Add(actor);
                if(!rehello)
                {
                    Start(Admission(actor,hostTick));
                    r=session.PrepareJoinTrusted(current,request,out join);
                    if(!r.Accepted)return r;
                    r=session.ValidateJoin(join);
                    if(!r.Accepted)return r;
                }
                r=network.Validate(reservation,hostTick);
                if(!r.Accepted)return r;
                // Pure stable host lane: no callbacks, allocations or fallible Join after this point.
                network.CommitValidated(reservation);
                if(join!=null) { session.CommitJoinValidated(join); token=join.Token; }
                bindings=nextBindings; rosterActors=nextRoster;
                return RuleResult.Accept();
            }
            finally
            {
                session.AbortJoin(join); network.Abort(reservation);
                Finish(); admitting=false;
            }
        }
        public RuleResult Receive(string peer,byte[] bytes,long hostTick)
        { return Receive(peer,ConnectionGeneration(peer),bytes,hostTick); }
        // Async transport callbacks must retain the lease generation captured at bind.
        public RuleResult Receive(string peer,long generation,byte[] bytes,long hostTick)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return RuleResult.Reject("reentry");
            Binding b; if(peer==null || !bindings.TryGetValue(peer,out b) || !b.Active)return RuleResult.Reject("not-admitted");
            if(generation!=b.Generation)return RuleResult.Reject("connection-generation");
            if(b.DispatchRevoked || (dispatchAuthorization!=null && b.AuthenticatedGeneration<=0))return RuleResult.Reject("dispatch-revoked");
            RuleResult received=network.Receive(b.Lease,bytes,hostTick);
            if(received.Accepted)queuedOwners.Enqueue(b);
            return received;
        }
        public bool DispatchOne(out RuleResult result)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching) { result=RuleResult.Reject("reentry"); return false; }
            dispatching=true;
            try
            {
                if(queuedOwners.Count==0) { result=RuleResult.Reject("empty"); return false; }
                queuedOwner=queuedOwners.Dequeue();
                return network.DispatchOne(this,out result);
            }
            finally { queuedOwner=null; dispatching=false; }
        }
        RuleResult ICommandSink.Apply(CommandContext c,string featureId,byte[] payload)
        {
            if(applying || !dispatching)return RuleResult.Reject("coordinator-dispatch-required");
            Binding owner=queuedOwner, active;
            if(owner==null || owner.DispatchRevoked || !owner.Active || owner.Actor!=c.ActorId ||
                owner.Lease.SessionId!=c.SessionId || owner.Lease.Epoch!=c.AuthorityEpoch ||
                !bindings.TryGetValue(owner.Lease.PeerId,out active) || !Object.ReferenceEquals(owner,active))
                return RuleResult.Reject("dispatch-owner");
            MemberSnapshot member=session.Get(c.ActorId);
            if(member==null || member.CanonicalActorId!=owner.Lease.ReplayIdentity)return RuleResult.Reject("dispatch-canonical");
            if(dispatchAuthorization!=null)
            {
                if(owner.AuthenticatedGeneration<=0)return RuleResult.Reject("dispatch-unbound");
                RuleResult authorized=dispatchAuthorization.Validate(owner.Lease,network.ContentHash,owner.AuthenticatedGeneration);
                if(!authorized.Accepted)return authorized;
            }
            // Length-framed server prefix makes all client IDs disjoint from host
            // receipts even when a caller preclaims sessions-admit/ or sessions-host/.
            c=new CommandContext(c.SessionId,c.ActorId,"sessions-client/"+c.CommandId.Length.ToString(CultureInfo.InvariantCulture)+":"+c.CommandId,c.Tick,c.AuthorityEpoch,true);
            Start(c);
            try
            {
                RuleResult r;
                if(featureId=="sessions.loaded/1")
                {
                    string hash; try { hash=Utf8.GetString(payload); } catch(DecoderFallbackException) { return RuleResult.Reject("payload"); }
                    r=session.Loaded(c,hash);
                }
                else if(featureId=="sessions.spawn/1" && payload.Length==0)
                {
                    r=session.Spawn(c);
                    if(r.Accepted)placement.Publish(c.ActorId,Epoch,session.Get(c.ActorId));
                }
                else r=RuleResult.Reject("session-feature");
                return r;
            }
            finally { Finish(); }
        }
        public RuleResult Lifecycle(CommandContext context,LifecycleAction action,SessionRules next,string mapHash,bool match,bool externalLoadingTimeout=false)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return RuleResult.Reject("reentry");
            RuleResult r=lifecycleAuthority.Validate(context); if(!r.Accepted)return r;
            Start(context);
            try { return session.Lifecycle(context,action,next,mapHash,match,externalLoadingTimeout); }
            finally { Finish(); }
        }
        public RuleResult SetLobbyTeams(CommandContext context,int count,string actor,int team)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread||admitting||applying||dispatching)return RuleResult.Reject("reentry");
            RuleResult r=lifecycleAuthority.Validate(context);if(!r.Accepted)return r;
            Start(context);try{return session.SetLobbyTeams(context,count,actor,team);}finally{Finish();}
        }
        // Trusted authoritative health event; deliberately absent from client feature dispatch.
        public RuleResult Eliminate(CommandContext context)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return RuleResult.Reject("reentry");
            RuleResult r=lifecycleAuthority.Validate(context); if(!r.Accepted)return r;
            Start(context); try { return session.Eliminate(context); } finally { Finish(); }
        }
        // Bind one shared trusted registry for this coordinator's entire lifetime.
        public RuleResult BindDispatchAuthorization(object identity,IDispatchAuthorization authorization)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return RuleResult.Reject("reentry");
            if(identity==null || authorization==null)return RuleResult.Reject("dispatch-registry");
            if(registryIdentity!=null)return Object.ReferenceEquals(registryIdentity,identity) ? RuleResult.Accept() : RuleResult.Reject("dispatch-registry-replacement");
            registryIdentity=identity; dispatchAuthorization=authorization; return RuleResult.Accept();
        }
        public RuleResult BindDispatchGeneration(string peer,long leaseGeneration,long authenticatedGeneration)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return RuleResult.Reject("reentry");
            Binding b;
            if(dispatchAuthorization==null || authenticatedGeneration<=0 || peer==null || !bindings.TryGetValue(peer,out b) ||
                !b.Active || b.DispatchRevoked || b.Generation!=leaseGeneration || b.AuthenticatedGeneration!=0)return RuleResult.Reject("dispatch-generation");
            admitting=true;
            try
            {
                RuleResult r=dispatchAuthorization.Validate(b.Lease,network.ContentHash,authenticatedGeneration);
                if(!r.Accepted)return r;
                b.AuthenticatedGeneration=authenticatedGeneration; return RuleResult.Accept();
            }
            finally { admitting=false; }
        }
        public RuleResult RevokeDispatch(string peer,long leaseGeneration)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return RuleResult.Reject("reentry");
            Binding b;
            if(peer==null || !bindings.TryGetValue(peer,out b) || b.Generation!=leaseGeneration)return RuleResult.Reject("dispatch-generation");
            b.DispatchRevoked=true; return RuleResult.Accept();
        }
        public long ConnectionGeneration(string peer)
        { Binding b; return peer!=null && bindings.TryGetValue(peer,out b) && b.Active ? b.Generation : -1; }
        // UNREVIEWED R8 retirement candidate. Exact R2 API, now owned here.
        // Opaque receipt captures the actual binding BEFORE an epoch can obsolete it.
        public sealed class ObsoleteBindingReceipt
        {
            private readonly SessionNetworkCoordinator owner;
            private readonly object binding;
            private readonly long epoch;
            private bool completed;
            internal ObsoleteBindingReceipt(SessionNetworkCoordinator owner,object binding,long epoch)
            { this.owner=owner; this.binding=binding; this.epoch=epoch; }
            internal SessionNetworkCoordinator Owner { get { return owner; } }
            internal object Binding { get { return binding; } }
            internal long Epoch { get { return epoch; } }
            internal bool Completed { get { return completed; } set { completed=value; } }
        }
        public int RetainedBindingCount { get { return bindings.Count; } }
        public ObsoleteBindingReceipt CaptureBindingRetirement(string peer,long leaseGeneration,long authenticatedGeneration)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return null;
            Binding b;
            if(peer==null || !bindings.TryGetValue(peer,out b) || !b.Active || b.DispatchRevoked ||
                b.Generation!=leaseGeneration || b.AuthenticatedGeneration!=authenticatedGeneration || authenticatedGeneration<=0 || b.Lease.Epoch!=Epoch)return null;
            return new ObsoleteBindingReceipt(this,b,Epoch);
        }
        public RuleResult RetireObsoleteBinding(ObsoleteBindingReceipt receipt,long expectedCurrentEpoch)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return RuleResult.Reject("reentry");
            if(receipt==null || receipt.Owner!=this)return RuleResult.Reject("retirement-owner");
            if(receipt.Completed)return RuleResult.Accept();
            Binding b=(Binding)receipt.Binding;
            if(expectedCurrentEpoch!=Epoch || receipt.Epoch>=Epoch || b.Lease.Epoch!=receipt.Epoch || b.Active)return RuleResult.Reject("retirement-epoch");
            // Finish has ALREADY advanced the real AuthorityHost and cleared both
            // queues/leases. Never call Session.Disconnect on surviving membership.
            // A replacement with the same key is a different Binding and survives.
            Binding currentBinding;
            b.DispatchRevoked=true;
            if(bindings.TryGetValue(b.Lease.PeerId,out currentBinding) && Object.ReferenceEquals(b,currentBinding))bindings.Remove(b.Lease.PeerId);
            receipt.Completed=true;
            return RuleResult.Accept();
        }

        // Real Session outputs only; retained membership survives obsolete transport retirement.
        public MemberSnapshot[] CaptureMembers()
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)throw new InvalidOperationException("reentry");
            var result=new List<MemberSnapshot>();
            foreach(string actor in rosterActors) { MemberSnapshot member=session.Get(actor); if(member!=null)result.Add(member); }
            result.Sort(delegate(MemberSnapshot a,MemberSnapshot b){return StringComparer.Ordinal.Compare(a.ActorId,b.ActorId);});
            return result.ToArray();
        }
        // Called only by the composed host's rejected-pose safety owner. A
        // packet cannot select this method or manufacture its lifecycle authority.
        public RuleResult RelocateRoot(CommandContext context,string actor,int expectedRevision,Float3 lastAccepted)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread||admitting||applying||dispatching)return RuleResult.Reject("reentry");
            var result=lifecycleAuthority.Validate(context);if(!result.Accepted)return result;
            bool active=false;foreach(var binding in bindings.Values)if(binding.Active&&binding.Actor==actor)active=true;
            if(!active)return RuleResult.Reject("not-admitted");
            Start(context);try{result=session.RelocateRoot(context,actor,expectedRevision,lastAccepted);if(result.Accepted)placement.Publish(actor,Epoch,session.Get(actor));return result;}finally{Finish();}
        }
        // Reuse the actual Session PreparedLifeProjection against a committed
        // snapshot read from its trusted HealthService, not a client life DTO.
        public RuleResult ProjectCommittedHealth(CommandContext context,string actor,Ashfall.Modules.HealthStates.HealthService producer,double expires)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread||admitting||applying||dispatching)return RuleResult.Reject("reentry");
            var allowed=lifecycleAuthority.Validate(context);if(!allowed.Accepted)return allowed;
            if(producer==null||!producer.Authorize(context).Accepted)return RuleResult.Reject("health-producer-scope");
            var member=session.Get(actor);if(member==null||!member.Loaded||!member.HasPosition)return RuleResult.Reject("actual-placement-required");
            Start(context);
            try{return producer.Serialize(delegate{
                var snapshot=producer.Read(member.CanonicalActorId);if(snapshot==null||!snapshot.Connected)return RuleResult.Reject("connected-health-snapshot-required");
                Session.PreparedLifeProjection projection;
                if(!session.PrepareLifeProjection(context,actor,member.CanonicalActorId,snapshot,expires,null,out projection))return RuleResult.Reject("health-projection-prepare");
                if(!projection.Validate()){projection.Abort();return RuleResult.Reject("health-projection-stale");}
                projection.CommitValidated();return RuleResult.Accept();
            });}finally{Finish();}
        }
        // The actual approved source site and both prepared owners must validate
        // together; a committed Health-only resurrection is not Session spawn.
        public RuleResult RespawnConfiguredHealth(CommandContext context,string actor,Ashfall.Modules.HealthStates.HealthService producer,SpawnSite sourceSite,float health,float shield,double expires)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread||admitting||applying||dispatching)return RuleResult.Reject("reentry");
            var allowed=lifecycleAuthority.Validate(context);if(!allowed.Accepted)return allowed;
            if(producer==null||!producer.Authorize(context).Accepted)return RuleResult.Reject("health-producer-scope");
            var member=session.Get(actor);if(member==null||!member.Loaded||!member.HasPosition||sourceSite==null)return RuleResult.Reject("actual-placement-required");
            Start(context);
            try{return producer.Serialize(delegate{
                var snapshot=producer.Read(member.CanonicalActorId);if(snapshot==null||!snapshot.Connected)return RuleResult.Reject("connected-health-snapshot-required");
                Ashfall.Modules.HealthStates.HealthService.PreparedRespawn preparedHealth=null;Session.PreparedLifeProjection preparedSession=null;
                try{
                    if(!producer.PrepareConfiguredRespawn(context,snapshot.Id,snapshot.Revision,snapshot.LifeGeneration,snapshot.ConnectionGeneration,health,shield,expires,out preparedHealth))return RuleResult.Reject("health-respawn-prepare");
                    if(!session.PrepareLifeProjection(context,actor,member.CanonicalActorId,preparedHealth.Proposed,expires,sourceSite,out preparedSession))return RuleResult.Reject("session-respawn-prepare");
                    if(!preparedHealth.Validate()||!preparedSession.Validate())return RuleResult.Reject("joint-respawn-stale");
                    preparedHealth.CommitValidated();preparedSession.CommitValidated();placement.Publish(actor,Epoch,session.Get(actor));return RuleResult.Accept();
                }finally{if(preparedSession!=null)preparedSession.Abort();if(preparedHealth!=null)preparedHealth.Abort();}
            });}finally{Finish();}
        }
        // Source warmup below-map reset: actual Alive/Lobby placement and
        // configured Health reset commit together, with no manufactured death.
        public RuleResult RecoverWarmupHealth(CommandContext context,string actor,Ashfall.Modules.HealthStates.HealthService producer,SpawnSite sourceSite,float health,float shield,double expires)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread||admitting||applying||dispatching)return RuleResult.Reject("reentry");
            var allowed=lifecycleAuthority.Validate(context);if(!allowed.Accepted)return allowed;
            if(producer==null||!producer.Authorize(context).Accepted)return RuleResult.Reject("health-producer-scope");
            var member=session.Get(actor);if(member==null||!member.Loaded||!member.HasPosition||sourceSite==null)return RuleResult.Reject("actual-placement-required");
            Start(context);
            try{return producer.Serialize(delegate{
                var snapshot=producer.Read(member.CanonicalActorId);if(snapshot==null||!snapshot.Connected)return RuleResult.Reject("connected-health-snapshot-required");
                Ashfall.Modules.HealthStates.HealthService.PreparedRespawn preparedHealth=null;Session.PreparedLifeProjection preparedSession=null;
                try{
                    if(!producer.PrepareConfiguredWarmupSpawn(context,snapshot.Id,snapshot.Revision,snapshot.LifeGeneration,snapshot.ConnectionGeneration,health,shield,expires,out preparedHealth))return RuleResult.Reject("health-warmup-prepare");
                    if(!session.PrepareWarmupReplacement(context,actor,member.CanonicalActorId,preparedHealth.Proposed,expires,sourceSite,out preparedSession))return RuleResult.Reject("session-warmup-prepare");
                    if(!preparedHealth.Validate()||!preparedSession.Validate())return RuleResult.Reject("joint-warmup-stale");
                    preparedHealth.CommitValidated();preparedSession.CommitValidated();placement.Publish(actor,Epoch,session.Get(actor));return RuleResult.Accept();
                }finally{if(preparedSession!=null)preparedSession.Abort();if(preparedHealth!=null)preparedHealth.Abort();}
            });}finally{Finish();}
        }
        public LifeProjection ReadLifeProjection(string canonicalActor)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread||admitting||applying||dispatching)throw new InvalidOperationException("reentry");
            return session.ProjectedLife(canonicalActor);
        }
        public RuleResult RecordPosition(CommandContext context,Float3 feet)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return RuleResult.Reject("reentry");
            RuleResult r=lifecycleAuthority.Validate(context); if(!r.Accepted)return r;
            bool active=false; foreach(Binding b in bindings.Values)if(b.Active && b.Actor==context.ActorId)active=true;
            if(!active)return RuleResult.Reject("not-admitted");
            Start(context); try { return session.RecordPosition(context,feet); } finally { Finish(); }
        }
        public RuleResult Disconnect(string peer,long hostTick,bool kicked)
        { return Disconnect(peer,ConnectionGeneration(peer),hostTick,kicked); }
        public RuleResult Disconnect(string peer,long generation,long hostTick,bool kicked)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching || hostTick<0 || admissionSequence==Int64.MaxValue)return RuleResult.Reject("coordinator-state");
            // One bounded retry receipt: only the exact most recent completed event.
            // A delayed departure cannot revoke a replacement generation.
            if(peer==completedPeer && generation==completedGeneration && kicked==completedKick)return RuleResult.Accept();
            Binding b; if(peer==null || !bindings.TryGetValue(peer,out b))return RuleResult.Reject("not-admitted");
            if(generation!=b.Generation)return RuleResult.Reject("connection-generation");
            Start(Admission(b.Actor,hostTick));
            RuleResult r;
            try { r=session.DisconnectTrusted(current,kicked); } finally { Finish(); }
            if(r.Accepted) { if(b.Active)network.Release(b.Lease); bindings.Remove(peer); rosterActors.Remove(b.Actor); completedPeer=peer; completedGeneration=generation; completedKick=kicked; }
            return r;
        }
    }
}
