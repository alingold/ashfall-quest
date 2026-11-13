import pathlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/SessionStateReplicationR8'
p=R/'unity/Assets/Ashfall/Integration/Sessions/SessionNetworkCoordinator.cs'
before=S/'before'/p.relative_to(R)
assert p.read_bytes()==before.read_bytes(),'coordinator drift'
s=p.read_text()
patch=(S/'before/docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR2/ROOT-REVIEW-ONLY.patch').read_text()
addition=patch.split('+        // ROOT REVIEW CANDIDATE.')[1].split('         public RuleResult RecordPosition')[0]
addition='        // UNREVIEWED R8 retirement candidate. Exact R2 API, now owned here.\n'+ '\n'.join(line[1:] for line in addition.splitlines()[1:] if line.startswith('+'))+'\n'
assert 'CaptureBindingRetirement' not in s
s=s.replace('        public RuleResult RecordPosition',addition+'        public RuleResult RecordPosition')
s=s.replace('bool rehello=prior!=null && !prior.Active && session.Get(actor)!=null;','bool rehello=session.Get(actor)!=null;')
s=s.replace('        private readonly Queue<Binding> queuedOwners;', '        private HashSet<string> rosterActors=new HashSet<string>(StringComparer.Ordinal);\n        private readonly Queue<Binding> queuedOwners;')
s=s.replace('                if(!rehello)\n', '                var nextRoster=new HashSet<string>(StringComparer.Ordinal);\n                foreach(string known in rosterActors)if(session.Get(known)!=null)nextRoster.Add(known);\n                nextRoster.Add(actor);\n                if(!rehello)\n')
s=s.replace('                bindings=nextBindings;','                bindings=nextBindings; rosterActors=nextRoster;')
s=s.replace('bindings.Remove(peer); completedPeer=peer;', 'bindings.Remove(peer); rosterActors.Remove(b.Actor); completedPeer=peer;')
s=s.replace('        public RuleResult RecordPosition', '''        // Real Session outputs only; retained membership survives obsolete transport retirement.
        public MemberSnapshot[] CaptureMembers()
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching)throw new InvalidOperationException("reentry");
            var result=new List<MemberSnapshot>();
            foreach(string actor in rosterActors) { MemberSnapshot member=session.Get(actor); if(member!=null)result.Add(member); }
            result.Sort(delegate(MemberSnapshot a,MemberSnapshot b){return StringComparer.Ordinal.Compare(a.ActorId,b.ActorId);});
            return result.ToArray();
        }
        public RuleResult RecordPosition''')
p.write_text(s)
print('coordinator candidate applied')
