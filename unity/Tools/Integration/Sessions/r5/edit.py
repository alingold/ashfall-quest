from prepare import R
p=R/'unity/Assets/Ashfall/Modules/Sessions/Session.cs'
s=p.read_text().replace('public readonly string ActorId, Name;','public readonly string ActorId, CanonicalActorId, Name;').replace('ActorId=p.Id; Name=p.Name;','ActorId=p.Id; CanonicalActorId=p.CanonicalId; Name=p.Name;').replace('public string Id, Name, Token;','public string Id, CanonicalId, Name, Token;').replace('"Sessions/3"','"Sessions/4"').replace('private long lifecycleTick = -1;','private long lifecycleTick = -1;\n        private long hostReceipt = -1;')
pos=s.index('        private int BalancedTeam()')
s=s[:pos]+'''        // Trusted receipt stream cannot be populated by gameplay IDs, nor saturated
        // by gameplay traffic. Gate must authenticate this entry independently.
        private RuleResult Trusted(CommandContext c)
        {
            if(!c.IsAuthority || c.SessionId!=Id || c.AuthorityEpoch!=Epoch || c.Tick<0 ||
                String.IsNullOrWhiteSpace(c.ActorId) || c.CommandId!="sessions-host/"+c.Tick.ToString(System.Globalization.CultureInfo.InvariantCulture))
                return RuleResult.Reject("host-context");
            RuleResult r=gate.Validate(c); if(!r.Accepted)return r;
            if(c.Tick<=hostReceipt)return RuleResult.Reject("host-replay");
            double now=clock.Now;
            if(Double.IsNaN(now) || Double.IsInfinity(now) || now<lastTime)return RuleResult.Reject("clock");
            hostReceipt=c.Tick; lastTime=now; Expire(); return RuleResult.Accept();
        }
        public RuleResult JoinTrusted(CommandContext c, JoinRequest request, out string token)
        { token=null; RuleResult r=Trusted(c); return r.Accepted ? JoinCore(c,request,out token) : r; }
        public RuleResult DisconnectTrusted(CommandContext c, bool kicked)
        { RuleResult r=Trusted(c); return r.Accepted ? DisconnectCore(c,kicked) : r; }
''' +s[pos:]
s=s.replace('if(request==null || request.ProtocolName', 'return JoinCore(c,request,out reconnectToken);\n        }\n        private RuleResult JoinCore(CommandContext c, JoinRequest request, out string reconnectToken)\n        {\n            reconnectToken=null;\n            if(request==null || request.ProtocolName',1)
s=s.replace('p=new Member {Name=', 'p=new Member {CanonicalId=c.ActorId,Name=')
s=s.replace('Member p; if(!players.TryGetValue(c.ActorId,out p))return RuleResult.Reject("not-joined");\n            players.Remove', 'return DisconnectCore(c,kicked);\n        }\n        private RuleResult DisconnectCore(CommandContext c, bool kicked)\n        {\n            Member p; if(!players.TryGetValue(c.ActorId,out p))return RuleResult.Reject("not-joined");\n            players.Remove')
p.write_text(s)
p=R/'unity/Assets/Ashfall/Integration/Sessions/SessionNetworkCoordinator.cs';s=p.read_text()
s=s.replace('public string Actor; public bool Active;', 'public string Actor; public bool Active; public long Generation;')
s=s.replace('private long admissionSequence;', 'private long admissionSequence;\n        private long connectionSequence;')
s=s.replace('"sessions-admit/"+(++admissionSequence).ToString(CultureInfo.InvariantCulture),tick,Epoch,true)', '"sessions-host/"+(++admissionSequence).ToString(CultureInfo.InvariantCulture),admissionSequence,Epoch,true)')
s=s.replace('if(applying || hostTick<0 || admissionSequence==Int64.MaxValue)', 'if(applying || dispatching || hostTick<0 || admissionSequence==Int64.MaxValue || connectionSequence==Int64.MaxValue)')
s=s.replace('prior.Active=true; return', 'prior.Active=true; prior.Generation=++connectionSequence; return')
s=s.replace('r=session.Join(current,request,out token)', 'r=session.JoinTrusted(current,request,out token)')
s=s.replace('Actor=actor,Active=true','Actor=actor,Active=true,Generation=++connectionSequence')
s=s.replace('if(applying) { result=', 'if(applying || dispatching) { result=')
s=s.replace('            Start(c);\n            try\n            {\n                RuleResult r;', '''            // Length-framed server prefix makes all client IDs disjoint from host
            // receipts even when a caller preclaims sessions-admit/ or sessions-host/.
            c=new CommandContext(c.SessionId,c.ActorId,"sessions-client/"+c.CommandId.Length.ToString(CultureInfo.InvariantCulture)+":"+c.CommandId,c.Tick,c.AuthorityEpoch,true);
            Start(c);
            try
            {
                RuleResult r;''')
s=s.replace('        public RuleResult Disconnect(string peer,long hostTick,bool kicked)\n        {', '''        public long ConnectionGeneration(string peer)
        { Binding b; return peer!=null && bindings.TryGetValue(peer,out b) && b.Active ? b.Generation : -1; }
        public RuleResult RecordPosition(CommandContext context,Float3 feet)
        {
            if(applying || dispatching)return RuleResult.Reject("reentry");
            RuleResult r=lifecycleAuthority.Validate(context); if(!r.Accepted)return r;
            bool active=false; foreach(Binding b in bindings.Values)if(b.Active && b.Actor==context.ActorId)active=true;
            if(!active)return RuleResult.Reject("not-admitted");
            Start(context); try { return session.RecordPosition(context,feet); } finally { Finish(); }
        }
        public RuleResult Disconnect(string peer,long hostTick,bool kicked)
        { return Disconnect(peer,ConnectionGeneration(peer),hostTick,kicked); }
        public RuleResult Disconnect(string peer,long generation,long hostTick,bool kicked)
        {''')
s=s.replace('Binding b; if(!bindings.TryGetValue(peer,out b))return RuleResult.Reject("not-admitted");', 'Binding b; if(peer==null || !bindings.TryGetValue(peer,out b))return RuleResult.Reject("not-admitted");\n            if(generation!=b.Generation)return RuleResult.Reject("connection-generation");')
s=s.replace('session.Disconnect(current,kicked)', 'session.DisconnectTrusted(current,kicked)')
p.write_text(s)
