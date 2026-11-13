from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall');p=R/'unity/Assets/Ashfall/Modules/Sessions/Session.cs';s=p.read_text()
s=s.replace('private readonly Dictionary<string, Member> players','private Dictionary<string, Member> players').replace('private readonly Dictionary<string, Reservation> reservations','private Dictionary<string, Reservation> reservations').replace('private readonly HashSet<string> issuedTokens','private HashSet<string> issuedTokens')
s=s.replace('private double lastTime;', 'private double lastTime;\n        private object admissionRevision=new object();')
# Every accepted mutation entry invalidates prepared admission; object avoids revision ABA/wrap.
s=s.replace('            lastTime=now;','            admissionRevision=new object(); lastTime=now;')
s=s.replace('            lifecycleTick=c.Tick; lastTime=now;', '            admissionRevision=new object(); lifecycleTick=c.Tick; lastTime=now;')
s=s.replace('            hostReceipt=c.Tick; lastTime=now;', '            admissionRevision=new object(); hostReceipt=c.Tick; lastTime=now;')
pos=s.index('        public RuleResult JoinTrusted(')
s=s[:pos]+'''        // Opaque in-memory capability; only trusted host lane may prepare/publish.
        public sealed class PreparedJoin
        {
            internal Session Owner; internal object Revision, NextRevision;
            internal Dictionary<string,Member> Players;
            internal Dictionary<string,Reservation> Reservations;
            internal HashSet<string> Tokens;
            internal CommandContext Context; internal double Time;
            internal bool Aborted, Committed, Validated;
            public string Token { get; internal set; }
            internal PreparedJoin() {}
        }
        public RuleResult PrepareJoinTrusted(CommandContext c,JoinRequest request,out PreparedJoin prepared)
        {
            prepared=null;
            if(!c.IsAuthority || c.SessionId!=Id || c.AuthorityEpoch!=Epoch || c.Tick<0 ||
                String.IsNullOrWhiteSpace(c.ActorId) || c.CommandId!="sessions-host/"+c.Tick.ToString(System.Globalization.CultureInfo.InvariantCulture))return RuleResult.Reject("host-context");
            RuleResult r=gate.Validate(c); if(!r.Accepted)return r;
            if(c.Tick<=hostReceipt)return RuleResult.Reject("host-replay");
            double now=clock.Now;
            if(Double.IsNaN(now) || Double.IsInfinity(now) || now<lastTime)return RuleResult.Reject("clock");
            if(request==null || request.ProtocolName!=Protocol.Name || request.Schema!=Protocol.Schema || request.ContentHash!=contentHash)return RuleResult.Reject("incompatible-content");
            if(players.ContainsKey(c.ActorId))return RuleResult.Reject("already-joined");
            Reservation saved=null;
            if(!String.IsNullOrEmpty(request.ReconnectToken))reservations.TryGetValue(request.ReconnectToken,out saved);
            if(saved!=null && saved.Until<=now)saved=null;
            if(Rules.Locked && saved==null)return RuleResult.Reject("locked");
            if(players.Count>=Rules.Capacity)return RuleResult.Reject("full");
            if(Stage!=SessionStage.Lobby && !Rules.LateJoin && saved==null)return RuleResult.Reject("late-join-disabled");
            if(request.Spectator && !Rules.Spectators)return RuleResult.Reject("spectators-disabled");
            Member p;
            if(saved!=null)
            {
                Member old=saved.Player;
                p=new Member {Id=c.ActorId,CanonicalId=old.CanonicalId,Name=old.Name,Token=old.Token,Team=old.Team,Revision=old.Revision,Deaths=old.Deaths,
                    Loaded=false,Waiting=old.Waiting,HasPosition=old.HasPosition,Spectator=old.Spectator,Position=old.Position,Life=old.Life,RespawnAt=old.RespawnAt,TeamLayout=old.TeamLayout};
                if(saved.Epoch!=Epoch) { ResetRound(p); if(p.TeamLayout!=teamLayout) { p.Team=BalancedTeam(); p.TeamLayout=teamLayout; } SetJoinLife(p); }
            }
            else
            {
                string token=tokens.Issue();
                if(String.IsNullOrWhiteSpace(token) || issuedTokens.Contains(token))return RuleResult.Reject("token-issuer");
                p=new Member {Id=c.ActorId,CanonicalId=c.ActorId,Name=CleanName(request.Name),Token=token,Team=BalancedTeam(),Spectator=request.Spectator,TeamLayout=teamLayout};
                SetJoinLife(p);
            }
            var nextPlayers=new Dictionary<string,Member>(players,StringComparer.Ordinal); nextPlayers.Add(p.Id,p);
            var nextReservations=new Dictionary<string,Reservation>(StringComparer.Ordinal);
            foreach(var pair in reservations)if(pair.Value.Until>now && (saved==null || pair.Key!=request.ReconnectToken))nextReservations.Add(pair.Key,pair.Value);
            var nextTokens=new HashSet<string>(issuedTokens,StringComparer.Ordinal); nextTokens.Add(p.Token);
            prepared=new PreparedJoin {Owner=this,Revision=admissionRevision,NextRevision=new object(),Players=nextPlayers,Reservations=nextReservations,Tokens=nextTokens,Context=c,Time=now,Token=p.Token};
            return RuleResult.Accept();
        }
        public RuleResult ValidateJoin(PreparedJoin p)
        {
            if(p==null || p.Owner!=this || p.Aborted || p.Committed || p.Revision!=admissionRevision || p.Context.AuthorityEpoch!=Epoch)return RuleResult.Reject("stale-join");
            RuleResult r=gate.Validate(p.Context); if(!r.Accepted)return r;
            // Equality freezes expiry eligibility between preparation and publication.
            if(clock.Now!=p.Time)return RuleResult.Reject("join-time-changed");
            p.Validated=true; return RuleResult.Accept();
        }
        public void CommitJoinValidated(PreparedJoin p)
        {
            if(p!=null && p.Owner==this && p.Committed)return;
            if(p==null || p.Owner!=this || !p.Validated || p.Aborted || p.Revision!=admissionRevision)throw new InvalidOperationException("join-not-validated");
            players=p.Players; reservations=p.Reservations; issuedTokens=p.Tokens;
            hostReceipt=p.Context.Tick; lastTime=p.Time; admissionRevision=p.NextRevision; p.Committed=true;
        }
        public void AbortJoin(PreparedJoin p)
        { if(p!=null && p.Owner==this && !p.Committed)p.Aborted=true; }
'''+s[pos:];p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/Networking/Authority.cs';s=p.read_text().replace('            long tick=Math.Max(a.Tick,p.Tick);','            if(a.Pending.Contains(receipt))return RuleResult.Reject("replay");\n            long tick=Math.Max(a.Tick,p.Tick);');p.write_text(s)
