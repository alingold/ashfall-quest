using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.Sessions
{
    public enum SessionStage { Lobby, Loading, Match, Results }
    public enum SessionLife { Pending, Alive, Eliminated, Spectator }
    public sealed class SpawnSite
    {
        public readonly string Id;
        public readonly Float3 Feet;
        public readonly int Team; // -1 = shared
        public readonly bool Interior, CapsulePlatform;
        public SpawnSite(string id, Float3 feet, int team, bool interior, bool platform)
        { Id=id; Feet=feet; Team=team; Interior=interior; CapsulePlatform=platform; }
    }
    public sealed class SessionRules
    {
        public readonly string MapId, Mode, MapHash;
        public readonly int Teams, Capacity;
        public readonly bool Indoor, Locked, LateJoin, Spectators;
        public readonly double RespawnDelay;
        public SessionRules(string mapId, string mode, string mapHash, int teams, int capacity,
            bool indoor, bool locked, bool lateJoin, bool spectators, double respawnDelay)
        {
            if (String.IsNullOrWhiteSpace(mapId) || String.IsNullOrWhiteSpace(mapHash) ||
                (mode!="respawn" && mode!="revive") || teams<0 || teams>8 || capacity<1 || capacity>256 ||
                Double.IsNaN(respawnDelay) || Double.IsInfinity(respawnDelay) || respawnDelay<0)
                throw new ArgumentException("Invalid session rules");
            MapId=mapId; Mode=mode; MapHash=mapHash; Teams=teams; Capacity=capacity;
            Indoor=indoor; Locked=locked; LateJoin=lateJoin; Spectators=spectators; RespawnDelay=respawnDelay;
        }
    }
    public sealed class JoinRequest
    {
        public readonly string ProtocolName, ContentHash, Name, ReconnectToken;
        public readonly int Schema;
        public readonly bool Spectator;
        public JoinRequest(string protocol, int schema, string content, string name, string token, bool spectator)
        { ProtocolName=protocol; Schema=schema; ContentHash=content; Name=name; ReconnectToken=token; Spectator=spectator; }
    }
    public sealed class MemberSnapshot
    {
        public readonly string ActorId, CanonicalActorId, Name;
        public readonly int Team, SpawnRevision, Deaths;
        public readonly bool Loaded, WaitingRound, HasPosition;
        public readonly Float3 Position;
        public readonly SessionLife Life;
        internal MemberSnapshot(Member p)
        { ActorId=p.Id; CanonicalActorId=p.CanonicalId; Name=p.Name; Team=p.Team; SpawnRevision=p.Revision; Deaths=p.Deaths;
          Loaded=p.Loaded; WaitingRound=p.Waiting; HasPosition=p.HasPosition; Position=p.Position; Life=p.Life; }
    }
    internal sealed class Member
    {
        public object ConnectionIdentity=new object();
        public string Id, CanonicalId, Name, Token;
        public int Team, Revision, Deaths;
        public bool Loaded, Waiting, HasPosition, Spectator;
        public Float3 Position;
        public SessionLife Life;
        public double RespawnAt;
        public long TeamLayout;
    }
    internal sealed class Reservation
    { public Member Player; public long Epoch; public double Until; }

    // Single authority simulation lane. Snapshots are outputs for coordinator-owned
    // health/inventory/root adapters; no tracked child or cross-module mutation here.
    public sealed partial class Session
    {
        public const string ApiVersion = "Sessions/4";
        public const double LoadingTimeoutSeconds = 60;
        private readonly IAuthorityGate gate;
        private readonly ISessionClock clock;
        private readonly IReconnectTokens tokens;
        private readonly ISpawnWorld world;
        private readonly ISessionManagement management;
        private readonly string contentHash;
        private Dictionary<string, Member> players = new Dictionary<string, Member>(StringComparer.Ordinal);
        private Dictionary<string, Reservation> reservations = new Dictionary<string, Reservation>(StringComparer.Ordinal);
        private HashSet<string> issuedTokens = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> commands = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,long> ticks = new Dictionary<string,long>(StringComparer.Ordinal);
        private double lastTime;
        private object admissionRevision=new object();
        private readonly HashSet<object> checkpointReservations=new HashSet<object>();
        private long lifecycleTick = -1;
        private long hostReceipt = -1;
        private bool worldReady;
        private SessionStage loadingTarget;
        private long teamLayout;
        public double LoadingDeadline { get; private set; }
        public string LastLoadingRecovery { get; private set; }
        public string Id { get; private set; }
        public long Epoch { get; private set; }
        public SessionStage Stage { get; private set; }
        public SessionRules Rules { get; private set; }
        public Session(string id, string immutableContentHash, SessionRules rules, IAuthorityGate authority,
            ISessionClock time, IReconnectTokens tokenIssuer, ISpawnWorld spawnWorld, ISessionManagement managers)
        {
            if (String.IsNullOrWhiteSpace(id) || String.IsNullOrWhiteSpace(immutableContentHash) ||
                rules==null || authority==null || time==null || tokenIssuer==null || spawnWorld==null || managers==null)
                throw new ArgumentException("Missing session dependency");
            Id=id; contentHash=immutableContentHash; Rules=rules; gate=authority; clock=time; tokens=tokenIssuer; world=spawnWorld;
            management=managers; Stage=SessionStage.Lobby;
        }
        public MemberSnapshot Get(string actor)
        { Member p; return actor!=null && players.TryGetValue(actor,out p) ? new MemberSnapshot(p) : null; }
        // Opaque local-server incarnation identity; never a wire token or client claim.
        public object GetConnectionIdentity(string actor)
        { Member p; return actor!=null&&players.TryGetValue(actor,out p)?p.ConnectionIdentity:null; }
        public int Count { get { return players.Count; } }
        // Trusted coordinator queries the server's own unexpired token reservation;
        // never accept a caller-supplied canonical identity. This does not consume it.
        public string ReplayIdentity(string actor,JoinRequest request)
        {
            Member member; if(players.TryGetValue(actor,out member))return member.CanonicalId;
            Reservation saved; double now=clock.Now;
            if(request!=null && request.ProtocolName==Protocol.Name && request.Schema==Protocol.Schema && request.ContentHash==contentHash &&
                !String.IsNullOrEmpty(request.ReconnectToken) && reservations.TryGetValue(request.ReconnectToken,out saved) &&
                !Double.IsNaN(now) && !Double.IsInfinity(now) && now>=lastTime && saved.Until>now)return saved.Player.CanonicalId;
            return actor;
        }
        private RuleResult Validate(CommandContext c)
        {
            if (!c.IsAuthority || c.SessionId!=Id || c.AuthorityEpoch!=Epoch || c.Tick<0 ||
                String.IsNullOrWhiteSpace(c.ActorId) || String.IsNullOrWhiteSpace(c.CommandId)) return RuleResult.Reject("context");
            RuleResult external=gate.Validate(c); if(!external.Accepted)return external;
            double now=clock.Now;
            if (Double.IsNaN(now) || Double.IsInfinity(now) || now<lastTime) return RuleResult.Reject("clock");
            admissionRevision=new object(); lastTime=now;
            Member member; string identity=players.TryGetValue(c.ActorId,out member) ? member.CanonicalId : c.ActorId;
            long previous;
            if(ticks.TryGetValue(identity,out previous) && c.Tick<previous)return RuleResult.Reject("stale-tick");
            // Length framing avoids actor/command concatenation collisions. Fail closed
            // on budget exhaustion rather than evicting IDs and permitting replay.
            string key=identity.Length+":"+identity+c.CommandId;
            if(commands.Contains(key))return RuleResult.Reject("replay");
            if(commands.Count>=65536)return RuleResult.Reject("epoch-command-budget");
            commands.Add(key); ticks[identity]=c.Tick; Expire(); return RuleResult.Accept();
        }
        // Host-only lifecycle stream: one receipt watermark for this Session lifetime.
        // Canonical IDs bind each command to its sequence; no receipt eviction. Tick is
        // a host-owned sequence, NOT wall time. Clock.Now remains the only time source.
        // Sequence never resets on epoch or host migration; exhaustion fails closed.
        public RuleResult Lifecycle(CommandContext c, LifecycleAction action, SessionRules next, string mapHash, bool match, bool externalLoadingTimeout=false)
        {
            if (!c.IsAuthority || c.SessionId!=Id || c.AuthorityEpoch!=Epoch || c.Tick<0 ||
                String.IsNullOrWhiteSpace(c.ActorId) ||
                c.CommandId!="sessions-lifecycle/"+c.Tick.ToString(System.Globalization.CultureInfo.InvariantCulture))
                return RuleResult.Reject("lifecycle-context");
            RuleResult external=gate.Validate(c); if(!external.Accepted)return external;
            if(!management.CanManage(c))return RuleResult.Reject("manager-required");
            if(c.Tick<=lifecycleTick)return RuleResult.Reject("lifecycle-replay");
            double now=clock.Now;
            if(Double.IsNaN(now) || Double.IsInfinity(now) || now<lastTime)return RuleResult.Reject("clock");
            admissionRevision=new object(); lifecycleTick=c.Tick; lastTime=now; Expire();
            switch(action)
            {
                case LifecycleAction.AdvanceTime: return AdvanceLoadingCore();
                case LifecycleAction.BeginLoading: return BeginLoadingCore(next,match,externalLoadingTimeout);
                case LifecycleAction.WorldReady: return WorldReadyCore(mapHash);
                case LifecycleAction.Activate: return ActivateCore();
                case LifecycleAction.EnterResults: return EnterResultsCore();
                default: return RuleResult.Reject("lifecycle-action");
            }
        }
        public long LifecycleSequence { get { return lifecycleTick; } }
        private void Expire()
        { var gone=new List<string>(); foreach(var x in reservations)if(x.Value.Until<=lastTime)gone.Add(x.Key); foreach(string x in gone)reservations.Remove(x); }
        public RuleResult Join(CommandContext c, JoinRequest request, out string reconnectToken)
        {
            reconnectToken=null; RuleResult v=Validate(c); if(!v.Accepted)return v;
            return JoinCore(c,request,out reconnectToken);
        }
        private RuleResult JoinCore(CommandContext c, JoinRequest request, out string reconnectToken)
        {
            reconnectToken=null;
            if(request==null || request.ProtocolName!=Protocol.Name || request.Schema!=Protocol.Schema || request.ContentHash!=contentHash)
                return RuleResult.Reject("incompatible-content");
            if(players.ContainsKey(c.ActorId))return RuleResult.Reject("already-joined");
            Reservation saved=null;
            if(!String.IsNullOrEmpty(request.ReconnectToken))reservations.TryGetValue(request.ReconnectToken,out saved);
            if(Rules.Locked && saved==null)return RuleResult.Reject("locked");
            if(players.Count>=Rules.Capacity)return RuleResult.Reject("full");
            if(Stage!=SessionStage.Lobby && !Rules.LateJoin && saved==null)return RuleResult.Reject("late-join-disabled");
            if(request.Spectator && !Rules.Spectators)return RuleResult.Reject("spectators-disabled");
            Member p;
            if(saved!=null)
            {
                p=saved.Player; reservations.Remove(request.ReconnectToken);
                if(saved.Epoch!=Epoch)
                {
                    ResetRound(p);
                    SetJoinLife(p);
                }
            }
            else
            {
                string token=tokens.Issue();
                if(String.IsNullOrWhiteSpace(token) || issuedTokens.Contains(token))return RuleResult.Reject("token-issuer");
                issuedTokens.Add(token);
                p=new Member {CanonicalId=c.ActorId,Name=CleanName(request.Name),Token=token,Team=BalancedTeam(),Spectator=request.Spectator};
                p.TeamLayout=teamLayout; SetJoinLife(p);
            }
            if(p.TeamLayout!=teamLayout){p.Team=BalancedTeam();p.TeamLayout=teamLayout;}
            p.ConnectionIdentity=new object();p.Id=c.ActorId; p.Loaded=false; players.Add(p.Id,p); BalanceLobbyJoin(); reconnectToken=p.Token; return RuleResult.Accept();
        }
        // Trusted receipt stream cannot be populated by gameplay IDs, nor saturated
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
            admissionRevision=new object(); hostReceipt=c.Tick; lastTime=now; Expire(); return RuleResult.Accept();
        }
        // Opaque in-memory capability; only trusted host lane may prepare/publish.
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
                if(saved.Epoch!=Epoch) { ResetRound(p); SetJoinLife(p); }
            }
            else
            {
                string token=tokens.Issue();
                if(String.IsNullOrWhiteSpace(token) || issuedTokens.Contains(token))return RuleResult.Reject("token-issuer");
                p=new Member {Id=c.ActorId,CanonicalId=c.ActorId,Name=CleanName(request.Name),Token=token,Team=BalancedTeam(),Spectator=request.Spectator,TeamLayout=teamLayout};
                SetJoinLife(p);
            }
            if(p.TeamLayout!=teamLayout){p.Team=BalancedTeam();p.TeamLayout=teamLayout;}
            var nextPlayers=new Dictionary<string,Member>(players,StringComparer.Ordinal); nextPlayers.Add(p.Id,p);
            var nextReservations=new Dictionary<string,Reservation>(StringComparer.Ordinal);
            foreach(var pair in reservations)if(pair.Value.Until>now && (saved==null || pair.Key!=request.ReconnectToken))nextReservations.Add(pair.Key,pair.Value);
            var nextTokens=new HashSet<string>(issuedTokens,StringComparer.Ordinal); nextTokens.Add(p.Token);
            prepared=new PreparedJoin {Owner=this,Revision=admissionRevision,NextRevision=new object(),Players=nextPlayers,Reservations=nextReservations,Tokens=nextTokens,Context=c,Time=now,Token=p.Token};
            checkpointReservations.Add(prepared);return RuleResult.Accept();
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
            players=p.Players; BalanceLobbyJoin(); reservations=p.Reservations; issuedTokens=p.Tokens;
            hostReceipt=p.Context.Tick; lastTime=p.Time; admissionRevision=p.NextRevision; p.Committed=true;checkpointReservations.Remove(p);
        }
        public void AbortJoin(PreparedJoin p)
        { if(p!=null && p.Owner==this && !p.Committed){p.Aborted=true;checkpointReservations.Remove(p);} }
        public RuleResult JoinTrusted(CommandContext c, JoinRequest request, out string token)
        { token=null; RuleResult r=Trusted(c); return r.Accepted ? JoinCore(c,request,out token) : r; }
        public RuleResult DisconnectTrusted(CommandContext c, bool kicked)
        { RuleResult r=Trusted(c); return r.Accepted ? DisconnectCore(c,kicked) : r; }
        // Only room management may edit a lobby roster. The following match keeps
        // this layout when its team count matches the room's selection.
        public RuleResult SetLobbyTeams(CommandContext c,int count,string actor,int team)
        {
            RuleResult check=Validate(c);if(!check.Accepted)return check;
            if(!management.CanManage(c)||Stage!=SessionStage.Lobby||count<0||count>8||count==1)return RuleResult.Reject("host-lobby-teams");
            Member target=null;
            if(actor!=null&&(count!=Rules.Teams||team<0||team>=count||!players.TryGetValue(actor,out target)))return RuleResult.Reject("team-member");
            if(count!=Rules.Teams){
                if(teamLayout==Int64.MaxValue)return RuleResult.Reject("team-layout");
                Rules=new SessionRules(Rules.MapId,Rules.Mode,Rules.MapHash,count,Rules.Capacity,Rules.Indoor,Rules.Locked,Rules.LateJoin,Rules.Spectators,Rules.RespawnDelay);
                teamLayout++;var ids=new List<string>(players.Keys);ids.Sort(StringComparer.Ordinal);
                for(int i=0;i<ids.Count;i++){var p=players[ids[i]];p.Team=count==0?-1:i%count;p.TeamLayout=teamLayout;}
            }
            if(target!=null)target.Team=team;
            return RuleResult.Accept();
        }
        private readonly int[] lobbyTeamSizes=new int[8];
        private void BalanceLobbyJoin()
        {
            if(Stage!=SessionStage.Lobby||Rules.Teams<2)return;
            var counts=lobbyTeamSizes;Array.Clear(counts,0,counts.Length);
            foreach(var p in players.Values){if(p.HasPosition)return;counts[p.Team]++;}
            while(true){
                int least=0,most=0;
                for(int i=1;i<Rules.Teams;i++){if(counts[i]<counts[least])least=i;if(counts[i]>counts[most])most=i;}
                if(counts[most]-counts[least]<=1)return;
                Member move=null;foreach(var p in players.Values)if(p.Team==most&&(move==null||String.CompareOrdinal(p.Id,move.Id)>0))move=p;
                move.Team=least;counts[most]--;counts[least]++;
            }
        }
        private int BalancedTeam()
        {
            if(Rules.Teams==0)return -1;
            int[] counts=new int[Rules.Teams]; foreach(Member p in players.Values)if(p.Team>=0 && p.Team<counts.Length)counts[p.Team]++;
            int best=0; for(int i=1;i<counts.Length;i++)if(counts[i]<counts[best])best=i; return best;
        }
        private void SetJoinLife(Member p)
        {
            p.Waiting=!p.Spectator && Rules.Mode=="revive" &&
                (Stage==SessionStage.Match || Stage==SessionStage.Results ||
                 (Stage==SessionStage.Loading && loadingTarget==SessionStage.Match));
            p.Life=p.Spectator || p.Waiting ? SessionLife.Spectator : SessionLife.Pending;
        }
        public static string CleanName(string name)
        {
            // 24 UTF-16 units as before, but a surrogate pair is kept whole or
            // dropped whole: a split pair is invalid text that strict UTF-8
            // roster encoders (and any wire peer) must refuse.
            string source=(name??"").Trim(), result="";
            for(int i=0;i<source.Length;i++)
            {
                char c=source[i];
                if(Char.IsHighSurrogate(c))
                {
                    if(i+1<source.Length && Char.IsLowSurrogate(source[i+1])) { if(result.Length<=22)result+=source.Substring(i,2); i++; }
                    continue;
                }
                if(Char.IsLowSurrogate(c))continue;
                if(c>=32 && c!='[' && c!=']' && c!='<' && c!='>' && result.Length<24)result+=c;
            }
            return result.Length==0 ? "Player" : result;
        }
        public RuleResult Loaded(CommandContext c, string mapHash)
        {
            RuleResult v=Validate(c); if(!v.Accepted)return v;
            Member p; if(!players.TryGetValue(c.ActorId,out p))return RuleResult.Reject("not-joined");
            if(mapHash!=Rules.MapHash)return RuleResult.Reject("map-cache-mismatch");
            if(RecoverLoading())return RuleResult.Reject("loading-timeout");
            if(p.Loaded)return RuleResult.Reject("already-loaded");
            p.Loaded=true; return RuleResult.Accept();
        }
        // Host/admin authorization is a required gate policy for these global actions.
        public RuleResult BeginLoading(CommandContext c, SessionRules next, bool match)
        {
            RuleResult v=Validate(c); if(!v.Accepted)return v;
            if(!management.CanManage(c))return RuleResult.Reject("manager-required");
            return BeginLoadingCore(next, match);
        }
        private RuleResult BeginLoadingCore(SessionRules next, bool match, bool externalLoadingTimeout=false)
        {
            // Reserve one epoch for timeout invalidation, even at lifetime exhaustion.
            if(next==null || Epoch>=Int64.MaxValue-1 || (next.Teams!=Rules.Teams && teamLayout==Int64.MaxValue))return RuleResult.Reject("rules-or-epoch");
            bool redistribute=next.Teams!=Rules.Teams;
            if(redistribute)teamLayout++;
            Rules=next; Epoch++; Stage=SessionStage.Loading; worldReady=false;
            // A trusted owner can keep the barrier open while it disconnects unready peers.
            LoadingDeadline=externalLoadingTimeout?0:lastTime+LoadingTimeoutSeconds; LastLoadingRecovery=String.Empty;
            loadingTarget=match ? SessionStage.Match : SessionStage.Lobby;
            commands.Clear(); ticks.Clear();
            var actors=new List<string>(players.Keys); actors.Sort(StringComparer.Ordinal);
            for(int i=0;i<actors.Count;i++)
            { Member p=players[actors[i]]; ResetRound(p);
              if(redistribute)p.Team=Rules.Teams==0 ? -1 : i%Rules.Teams;
              p.TeamLayout=teamLayout; }
            return RuleResult.Accept();
        }
        private static void ResetRound(Member p)
        { p.Loaded=false; p.Waiting=false; p.HasPosition=false; p.Revision=0; p.Deaths=0; p.RespawnAt=0; p.Life=p.Spectator ? SessionLife.Spectator : SessionLife.Pending; }
        public RuleResult WorldReady(CommandContext c, string mapHash)
        {
            RuleResult v=Validate(c); if(!v.Accepted)return v;
            if(!management.CanManage(c))return RuleResult.Reject("manager-required");
            return WorldReadyCore(mapHash);
        }
        private RuleResult WorldReadyCore(string mapHash)
        {
            if(RecoverLoading())return RuleResult.Reject("loading-timeout");
            if(mapHash!=Rules.MapHash)return RuleResult.Reject("map-cache-mismatch");
            worldReady=true; return RuleResult.Accept();
        }
        public RuleResult Activate(CommandContext c)
        {
            RuleResult v=Validate(c); if(!v.Accepted)return v;
            if(!management.CanManage(c))return RuleResult.Reject("manager-required");
            return ActivateCore();
        }
        private RuleResult ActivateCore()
        {
            if(RecoverLoading())return RuleResult.Reject("loading-timeout");
            if(Stage!=SessionStage.Loading || !worldReady)return RuleResult.Reject("not-world-ready");
            foreach(Member p in players.Values)if(!p.Loaded)return RuleResult.Reject("players-loading");
            Stage=loadingTarget; LoadingDeadline=0; return RuleResult.Accept();
        }
        // Call on the serialized authority heartbeat even when no packets arrive.
        // Source uses strict now > deadline. Abort to warmup, never kick or spawn.
        // Compatibility heartbeat: its tick IS its receipt identity. It has no
        // action payload and cannot select a map/rules mutation. Prefer Lifecycle
        // with canonical IDs for all new coordinator calls.
        public RuleResult AdvanceLoading(CommandContext c)
        {
            if(String.IsNullOrWhiteSpace(c.CommandId))return RuleResult.Reject("context");
            return Lifecycle(new CommandContext(c.SessionId,c.ActorId,
                "sessions-lifecycle/"+c.Tick.ToString(System.Globalization.CultureInfo.InvariantCulture),
                c.Tick,c.AuthorityEpoch,c.IsAuthority),LifecycleAction.AdvanceTime,null,null,false);
        }
        private RuleResult AdvanceLoadingCore()
        {
            RecoverLoading(); return RuleResult.Accept();
        }
        private bool RecoverLoading()
        {
            if(Stage!=SessionStage.Loading || LoadingDeadline==0 || lastTime<=LoadingDeadline)return false;
            Stage=SessionStage.Lobby; Epoch++; worldReady=false; LoadingDeadline=0;
            LastLoadingRecovery="loading-timeout"; commands.Clear(); ticks.Clear();
            foreach(Member p in players.Values)ResetRound(p);
            return true;
        }
        public RuleResult EnterResults(CommandContext c)
        { RuleResult v=Validate(c); if(!v.Accepted)return v; if(!management.CanManage(c))return RuleResult.Reject("manager-required"); return EnterResultsCore(); }
        private RuleResult EnterResultsCore()
        { if(Stage!=SessionStage.Match)return RuleResult.Reject("not-match"); Stage=SessionStage.Results; return RuleResult.Accept(); }
        public RuleResult Eliminate(CommandContext c)
        {
            RuleResult v=Validate(c); if(!v.Accepted)return v;
            Member p; if(Stage!=SessionStage.Match || !players.TryGetValue(c.ActorId,out p) || p.Life!=SessionLife.Alive)return RuleResult.Reject("not-alive-match");
            p.Life=SessionLife.Eliminated; p.Deaths++; p.RespawnAt=lastTime+Rules.RespawnDelay; return RuleResult.Accept();
        }
        public RuleResult Disconnect(CommandContext c, bool kicked)
        {
            RuleResult v=Validate(c); if(!v.Accepted)return v;
            return DisconnectCore(c,kicked);
        }
        private RuleResult DisconnectCore(CommandContext c, bool kicked)
        {
            Member p; if(!players.TryGetValue(c.ActorId,out p))return RuleResult.Reject("not-joined");
            players.Remove(c.ActorId);
            if(!kicked)reservations.Add(p.Token,new Reservation {Player=p,Epoch=Epoch,Until=lastTime+30});
            return RuleResult.Accept();
        }
        // Trusted validated root replication can refresh a reconnect location without
        // incrementing SpawnRevision; a revision means a coordinator must place a root.
        public RuleResult RecordPosition(CommandContext c, Float3 feet)
        {
            RuleResult v=Validate(c); if(!v.Accepted)return v;
            Member p; if(!players.TryGetValue(c.ActorId,out p) || !p.Loaded || !p.HasPosition || !feet.IsFinite)return RuleResult.Reject("position-state");
            p.Position=feet; return RuleResult.Accept();
        }
        public RuleResult Spawn(CommandContext c)
        {
            RuleResult v=Validate(c); if(!v.Accepted)return v;
            Member p; if(!players.TryGetValue(c.ActorId,out p) || !p.Loaded || !worldReady ||
                (Stage!=SessionStage.Match && Stage!=SessionStage.Lobby))return RuleResult.Reject("not-ready");
            if(p.Life==SessionLife.Spectator)return RuleResult.Reject("spectator");
            if(p.Life==SessionLife.Eliminated && (Rules.Mode!="respawn" || lastTime<p.RespawnAt))return RuleResult.Reject("respawn-not-due");
            if(p.Life==SessionLife.Alive && p.HasPosition)
            {
                if(world.IsSafe(Rules.MapId,p.Id,p.Position))return RuleResult.Accept(); // same-epoch reconnect retains state
                return Place(p); // safety relocation preserves life/deaths
            }
            return Place(p);
        }
        private RuleResult Place(Member p)
        {
            SpawnSite[] sites=world.Sites(Rules.MapId,Stage==SessionStage.Lobby);
            SpawnSite best=null; double bestScore=Double.NegativeInfinity;
            if(sites!=null)foreach(SpawnSite s in sites)
            {
                if(s==null || String.IsNullOrEmpty(s.Id) || !s.Feet.IsFinite || (s.Team!=-1 && s.Team!=p.Team))continue;
                bool indoor=Rules.Indoor || Rules.MapId=="archive:battlefield-paintball-gods-pitdevil" || Rules.MapId=="archive:skb-paintball-gods-skblong";
                if(indoor && (!s.Interior || s.CapsulePlatform))continue;
                if(s.CapsulePlatform && (Stage!=SessionStage.Match || Rules.Mode!="revive" || Rules.Teams==0 || p.Deaths!=0 || s.Team!=p.Team))continue;
                if(!world.IsSafe(Rules.MapId,p.Id,s.Feet))continue;
                double score=1000;
                foreach(Member other in players.Values)
                    if(other!=p && other.Loaded && other.HasPosition && other.Life==SessionLife.Alive &&
                        !(Rules.Teams>0 && other.Team==p.Team))score=Math.Min(score,Distance(s.Feet,other.Position));
                // Prefer source team deployment sites on first revive-mode deployment.
                if(s.CapsulePlatform)score+=2000;
                if(best==null || score>bestScore || (score==bestScore && String.CompareOrdinal(s.Id,best.Id)<0))
                { best=s; bestScore=score; }
            }
            if(best==null)return RuleResult.Reject("no-safe-spawn");
            if(p.Revision==Int32.MaxValue)return RuleResult.Reject("spawn-revision-exhausted");
            p.Position=best.Feet; p.HasPosition=true; p.Revision++; p.Life=SessionLife.Alive; p.RespawnAt=0;
            return RuleResult.Accept();
        }
        private static double Distance(Float3 a, Float3 b)
        { double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;return Math.Sqrt(x*x+y*y+z*z); }
    }
}
