from pathlib import Path
p=Path(r'C:\Users\aling\Documents\ashfall\unity\Assets\Ashfall\Integration\Sessions\SessionNetworkCoordinator.cs');s=p.read_text().replace('private readonly Dictionary<string,Binding> bindings','private Dictionary<string,Binding> bindings').replace('private bool dispatching;', 'private bool dispatching;\n        private bool admitting;\n        public int IdentityHistoryCount { get { return network.IdentityCount; } }')
s=s.replace('if(applying || dispatching', 'if(admitting || applying || dispatching').replace('if(applying)return', 'if(admitting || applying || dispatching)return')
a=s.index('            string actor;\n',s.index('public RuleResult Admit'));b=s.index('        public RuleResult Receive(',a)
s=s[:a]+'''            admitting=true;
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
                bool rehello=prior!=null && !prior.Active && session.Get(actor)!=null;
                // Allocate the entire binding publication before final validation.
                var nextBindings=new Dictionary<string,Binding>(bindings,StringComparer.Ordinal);
                nextBindings[peer]=new Binding {Actor=actor,Active=true,Generation=reservation.Lease.Generation,Lease=reservation.Lease};
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
                bindings=nextBindings;
                return RuleResult.Accept();
            }
            finally
            {
                session.AbortJoin(join); network.Abort(reservation);
                Finish(); admitting=false;
            }
        }
'''+s[b:];p.write_text(s)
