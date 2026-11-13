import pathlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
p=R/'unity/Assets/Ashfall/Integration/Sessions/SessionNetworkCoordinator.cs'
s=p.read_text()
s=s.replace('public PeerLease Lease;','public PeerLease Lease; public long AuthenticatedGeneration; public bool DispatchRevoked;')
s=s.replace('private readonly Session session;','''// Exact parallel queue, exclusively written after accepted network ingress.
        // Preallocated to the same bound; stale network work consumes its owner too.
        private readonly Queue<Binding> queuedOwners;
        private Binding queuedOwner;
        private object registryIdentity;
        private IDispatchAuthorization dispatchAuthorization;
        private readonly Session session;''')
s=s.replace('network=new AuthorityHost','queuedOwners=new Queue<Binding>(queueCapacity);\n            network=new AuthorityHost')
s=s.replace('network.AdvanceEpoch(Epoch);','network.AdvanceEpoch(Epoch); queuedOwners.Clear();')
s=s.replace('return network.Receive(b.Lease,bytes,hostTick);','''if(b.DispatchRevoked || (dispatchAuthorization!=null && b.AuthenticatedGeneration<=0))return RuleResult.Reject("dispatch-revoked");
            RuleResult received=network.Receive(b.Lease,bytes,hostTick);
            if(received.Accepted)queuedOwners.Enqueue(b);
            return received;''')
s=s.replace('try { return network.DispatchOne(this,out result); }\n            finally { dispatching=false; }','''try
            {
                if(queuedOwners.Count==0) { result=RuleResult.Reject("empty"); return false; }
                queuedOwner=queuedOwners.Dequeue();
                return network.DispatchOne(this,out result);
            }
            finally { queuedOwner=null; dispatching=false; }''')
s=s.replace('// Length-framed server prefix','''Binding owner=queuedOwner, active;
            if(owner==null || owner.DispatchRevoked || !owner.Active || owner.Actor!=c.ActorId ||
                owner.Lease.SessionId!=c.SessionId || owner.Lease.Epoch!=c.AuthorityEpoch ||
                !bindings.TryGetValue(owner.Lease.PeerId,out active) || !Object.ReferenceEquals(owner,active))
                return RuleResult.Reject("dispatch-owner");
            if(dispatchAuthorization!=null)
            {
                if(owner.AuthenticatedGeneration<=0)return RuleResult.Reject("dispatch-unbound");
                RuleResult authorized=dispatchAuthorization.Validate(owner.Lease,network.ContentHash,owner.AuthenticatedGeneration);
                if(!authorized.Accepted)return authorized;
            }
            // Length-framed server prefix''')
pos=s.index('        public long ConnectionGeneration(')
s=s[:pos]+'''        // Bind one shared trusted registry for this coordinator's entire lifetime.
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
            RuleResult r=dispatchAuthorization.Validate(b.Lease,network.ContentHash,authenticatedGeneration);
            if(!r.Accepted)return r;
            b.AuthenticatedGeneration=authenticatedGeneration; return RuleResult.Accept();
        }
        public RuleResult RevokeDispatch(string peer,long leaseGeneration)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)return RuleResult.Reject("reentry");
            Binding b;
            if(peer==null || !bindings.TryGetValue(peer,out b) || b.Generation!=leaseGeneration)return RuleResult.Reject("dispatch-generation");
            b.DispatchRevoked=true; return RuleResult.Accept();
        }
''' +s[pos:]
p.write_text(s)
p=R/'unity/Assets/Ashfall/Integration/SessionReplication/Host.cs';s=p.read_text()
s=s.replace('binding=new PeerBinding(peers,session,content,epoch);','''binding=new PeerBinding(peers,session,content,epoch);
            RuleResult registry=sessions.BindDispatchAuthorization(peers,new RegistryDispatchAuthorization(peers));
            if(!registry.Accepted)throw new ArgumentException(registry.Reason);''')
s=s.replace('if(sessionGeneration<=0)throw new InvalidOperationException("missing-session-lease");','''if(sessionGeneration<=0)throw new InvalidOperationException("missing-session-lease");
                RuleResult dispatch=sessions.BindDispatchGeneration(connection,sessionGeneration,p.Generation);
                if(!dispatch.Accepted)return dispatch;''')
s=s.replace('departure=new Departure(connection,generation,sessionGeneration,binding.Epoch,kicked);','''RuleResult revoked=sessions.RevokeDispatch(connection,sessionGeneration);
                    if(!revoked.Accepted)return revoked;
                    departure=new Departure(connection,generation,sessionGeneration,binding.Epoch,kicked);''')
p.write_text(s)
