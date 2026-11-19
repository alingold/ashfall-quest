using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable.Networking
{
    public enum R24LanLobbyState { Offline, Hosting, Connecting, Connected }
    public sealed class R24LanMember
    {
        public readonly string Actor,Name;public readonly int Team;public readonly bool Host,WorldLoaded;
        public R24LanMember(string actor,string name,int team,bool host,bool loaded){Actor=actor;Name=name;Team=team;Host=host;WorldLoaded=loaded;}
    }
    // Main-thread LAN owner over pumped transport and canonical Session membership.
    // Host authorities resolve gameplay; R24LanSession adapts the native scene.
    public sealed partial class R24LanLobby:IDisposable
    {
        public const int Capacity=24;public const ushort DefaultPort=24760;
        const string Offer="playable.lobby.offer/1",Join="playable.lobby.join/1",Roster="playable.lobby.roster/1";
        readonly IPumpedTransport transport;readonly Action<string> closePeer;readonly Func<string> newId;
        readonly int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        readonly Dictionary<string,Peer> peers=new Dictionary<string,Peer>(StringComparer.Ordinal);
        readonly Dictionary<string,string> principals=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly string[] expired=new string[Capacity];readonly Clock clock=new Clock();
        readonly string content;SessionNetworkCoordinator sessions;long tick,revision;bool disposed,pumping,changed,started;
        string serverPeer,displayName,hostActor;double deadline;byte[] clientHello;
        R24LanMember[] members=new R24LanMember[0],incoming;long incomingRevision=-1,committedRevision=-1;int incomingCount;
        sealed class Peer{internal string Actor,Name;internal long LastTeamPing;internal double NextTeamPing,Deadline,NextPose,SpawnBudgetAt,PendingSpawnUntil;internal int SpawnBudget;internal long LastPose,PendingSpawnSequence;internal bool Admitted;internal byte[] Offer,MapState,ClockReply;internal byte[][] Roster;internal int Next;internal readonly Dictionary<string,byte[]> Spawns=new Dictionary<string,byte[]>(StringComparer.Ordinal);internal readonly Dictionary<string,byte[]> Health=new Dictionary<string,byte[]>(StringComparer.Ordinal);internal readonly Dictionary<string,long> SentPoses=new Dictionary<string,long>(StringComparer.Ordinal);internal readonly Dictionary<string,byte[]> Pods=new Dictionary<string,byte[]>(StringComparer.Ordinal);internal readonly Dictionary<string,byte[]> Loot=new Dictionary<string,byte[]>(StringComparer.Ordinal);internal readonly Dictionary<string,byte[]> Supplies=new Dictionary<string,byte[]>(StringComparer.Ordinal);internal readonly Dictionary<string,byte[]> Foods=new Dictionary<string,byte[]>(StringComparer.Ordinal);internal readonly Dictionary<string,byte[]> Grenades=new Dictionary<string,byte[]>(StringComparer.Ordinal);internal readonly Dictionary<string,R24LanProjectileState> Projectiles=new Dictionary<string,R24LanProjectileState>(StringComparer.Ordinal);internal long ProjectileSequence;internal readonly Dictionary<string,byte[]> Ammo=new Dictionary<string,byte[]>(StringComparer.Ordinal);internal readonly Dictionary<string,Ashfall.Modules.Construction.PlacedPanel> Panels=new Dictionary<string,Ashfall.Modules.Construction.PlacedPanel>(StringComparer.Ordinal);internal readonly HashSet<string> SentPanels=new HashSet<string>(StringComparer.Ordinal);internal long PanelSequence;internal byte[] Resources,FoodReset;internal byte[][] ScoreFrames;internal int NextScore;internal long LastGameCommand;}
        sealed class Clock:ISessionClock{internal double Value;public double Now{get{return Value;}}}
        sealed class Tokens:IReconnectTokens{public string Issue(){return Guid.NewGuid().ToString("N");}}
        sealed class Principals:ISessionPrincipal
        {readonly Dictionary<string,string> values;internal Principals(Dictionary<string,string> values){this.values=values;}public bool Resolve(string peer,out string actor){return values.TryGetValue(peer,out actor);}}
        sealed class HostGate:IAuthorityGate,ISessionManagement
        {readonly string actor;internal HostGate(string actor){this.actor=actor;}public bool CanManage(CommandContext c){return c.IsAuthority&&c.ActorId==actor;}public RuleResult Validate(CommandContext c){return CanManage(c)?RuleResult.Accept():RuleResult.Reject("host-only");}}
        public event Action Changed;
        public R24LanLobbyState State{get;private set;}
        public string Status{get;private set;}public string SessionId{get;private set;}public string OwnActor{get;private set;}
        public string RoomName{get;private set;}public string MapId{get;private set;}public string MapManifestHash{get;private set;}
        public int RejectedFrames{get;private set;}public int DroppedUnreliableFrames{get;private set;}
        public long Epoch{get;private set;}
        public R24LanMember[] Members{get{return (R24LanMember[])members.Clone();}}
        // Allocation-free roster reads for per-frame callers; Members clones on every call.
        public int RoomCapacity{get{return State==R24LanLobbyState.Hosting&&sessions!=null?(roundRules??warmupRules).MaxPlayers:SelectedMap!=null&&SelectedMap.SourceRules!=null?SelectedMap.SourceRules.MaxPlayers:offeredCapacity;}}
        int offeredCapacity=Capacity;
        public int MemberCount{get{return members.Length;}}
        public int LoadedCount{get{int n=0;for(int i=0;i<members.Length;i++)if(members[i].WorldLoaded)n++;return n;}}
        public bool OwnWorldLoaded{get{for(int i=0;i<members.Length;i++)if(members[i].Actor==OwnActor)return members[i].WorldLoaded;return false;}}

        // Caller-owned snapshots preserve collection isolation without an intermediate list.
        static T[] CopySnapshot<T>(ICollection<T> values)
        {
            var snapshot = new T[values.Count];
            values.CopyTo(snapshot, 0);
            return snapshot;
        }
        public R24LanLobby(IPumpedTransport transport,Action<string> closePeer,Func<string> newId,string content,bool nativeMapLoading=false)
        {if(transport==null||closePeer==null||newId==null||!Wire.IsHash(content))throw new ArgumentException("LAN lobby configuration");this.transport=transport;this.closePeer=closePeer;this.newId=newId;this.content=content;this.nativeMapLoading=nativeMapLoading;Status="Not connected";}
        void Check(){if(disposed)throw new ObjectDisposedException("R24LanLobby");if(thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("LAN lobby main thread");}
        void Dirty(string status){Status=status;changed=true;}
        static string Text(string text,int bytes,string fallback)
        {text=(text??"").Trim();if(text.Length==0)text=fallback;if(!Wire.IsText(text,bytes,false))throw new ArgumentException("LAN lobby text");foreach(char c in text)if(Char.IsControl(c))throw new ArgumentException("LAN lobby text");return text;}
        string Id(){string id=newId();if(!Wire.IsText(id,64,false))throw new InvalidOperationException("LAN identity issuer");return id;}
        public void StartHost(string room,string name,string map,string mapManifestHash,int teams=2,R24PlayableMatchRules sourceRules=null)
        {
            Check();if(started||State!=R24LanLobbyState.Offline||!Wire.IsHash(mapManifestHash))throw new InvalidOperationException("LAN host state/map identity");started=true;
            RoomName=Text(room,64,"ASHFALL FRIENDS");displayName=Text(name,64,"Host");MapId=Text(map,128,"range");MapManifestHash=mapManifestHash;OwnActor=Id();hostActor=OwnActor;SessionId=Id();sessionTeams=teams;
            warmupRules=sourceRules??FallbackRoomRules(MapId,teams);if(warmupRules.MapId!=MapId||warmupRules.Teams!=teams||warmupRules.MaxPlayers>Capacity)throw new ArgumentException("Actual host warmup source rules required");
            var gate=new HostGate(OwnActor);var rules=new SessionRules(MapId,warmupRules.Mode,mapManifestHash,teams,warmupRules.MaxPlayers,false,warmupRules.Locked,warmupRules.LateJoin,warmupRules.Spectators,warmupRules.RespawnDelay);
            sessions=new SessionNetworkCoordinator(SessionId,content,rules,clock,new Tokens(),spawnWorld,gate,gate,new Principals(principals),new Placement(this),128,128,600);
            principals.Add("local-host",OwnActor);string token;var result=sessions.Admit("local-host",Packet(PacketKind.Hello,Join,OwnActor,new byte[0],0),new JoinRequest(Protocol.Name,Protocol.Schema,content,displayName,null,false),++tick,out token);
            if(!result.Accepted)throw new InvalidOperationException("Host membership: "+result.Reason);
            State=R24LanLobbyState.Hosting;PublishRoster();
        }
        public void StartClient(string peer,string name,double now)
        {Check();if(started||State!=R24LanLobbyState.Offline||!Finite(now)||now<0)throw new InvalidOperationException("LAN client state");started=true;serverPeer=Text(peer,256,"invalid");displayName=Text(name,64,"Player");clock.Value=now;deadline=now+10;State=R24LanLobbyState.Connecting;Dirty("Connecting to LAN host");}
        public void TransportConnected(string peer)
        {
            Check();if(State!=R24LanLobbyState.Hosting)return;
            if(peers.Count>=RoomCapacity-1||peers.ContainsKey(peer)){closePeer(peer);return;}
            var p=new Peer{Actor=Id(),Deadline=clock.Now+10};peers.Add(peer,p);principals.Add(peer,p.Actor);
            p.Offer=Packet(PacketKind.Hello,Offer,p.Actor,EncodeOffer(),0);
        }
        public void TransportDisconnected(string peer)
        {
            if(disposed)return;Check();
            if(State==R24LanLobbyState.Hosting){Peer p;if(!peers.TryGetValue(peer,out p))return;if(p.Admitted){var departing=sessions.Get(p.Actor);var result=sessions.Disconnect(peer,++tick,false);if(!result.Accepted)throw new InvalidOperationException("Lobby departure: "+result.Reason);if(departing!=null&&!departing.Loaded)NoteUnreadyMapDeparture(p.Name);}peers.Remove(peer);principals.Remove(peer);RemovePresence(p.Actor);PublishRoster();}
            else if(peer==serverPeer&&(State==R24LanLobbyState.Connecting||State==R24LanLobbyState.Connected)){ClearHealth();members=new R24LanMember[0];incoming=null;clientHello=null;State=R24LanLobbyState.Offline;Dirty("LAN host disconnected");}
        }
        public void Tick(double now)
        {
            Check();if(pumping||!Finite(now)||now<clock.Now)throw new InvalidOperationException("LAN lobby clock/reentry");clock.Value=now;pumping=true;
            try{
                transport.Pump();string peer;byte[] bytes;int budget=2048;
                while(budget-->0&&transport.TryReceive(out peer,out bytes))Receive(peer,bytes);
                if(State==R24LanLobbyState.Hosting){int count=0;foreach(var entry in peers){if(!entry.Value.Admitted&&now>=entry.Value.Deadline)expired[count++]=entry.Key;}for(int i=0;i<count;i++){string id=expired[i];expired[i]=null;closePeer(id);TransportDisconnectedIfRetained(id);}foreach(var entry in peers)Flush(entry.Key,entry.Value);}
                else if(State==R24LanLobbyState.Connecting){if(now>=deadline){string lost=serverPeer;State=R24LanLobbyState.Offline;clientHello=null;incoming=null;ClearHealth();members=new R24LanMember[0];Dirty("LAN connection timed out");closePeer(lost);}else if(clientHello!=null&&Send(serverPeer,clientHello))clientHello=null;}
                TickMaps();TickRoundClock();TickSharedRounds();TickPresence();TickRawTracking();TickSpawns();TickHealth();TickPods();TickSodas();TickConstruction();TickExplosives();TickAmmo();PublishScores();TickGameCommands();TickPulls();TickWeb();TickMeleeExpansion();TickVehicles();
            }finally{pumping=false;}
            if(changed){changed=false;var handler=Changed;if(handler!=null)handler();}
        }
        void TransportDisconnectedIfRetained(string peer){if(peers.ContainsKey(peer))TransportDisconnected(peer);}
        bool Send(string peer,byte[] bytes)
        {
            try{transport.Send(peer,bytes,true);return true;}
            catch(InvalidOperationException){return false;} // UTP send pressure retains exact pending frame.
        }
        void Flush(string id,Peer peer)
        {
            if(peer.Offer!=null){if(!Send(id,peer.Offer))return;peer.Offer=null;}
            if(peer.MapState!=null){if(!Send(id,peer.MapState))return;peer.MapState=null;}
            if(peer.ClockReply!=null&&SendPresence(id,peer.ClockReply))peer.ClockReply=null;
            if(peer.Roster!=null){while(peer.Next<peer.Roster.Length){if(!Send(id,peer.Roster[peer.Next]))return;peer.Next++;}peer.Roster=null;peer.Next=0;}
            if(peer.Resources!=null&&Send(id,peer.Resources))peer.Resources=null;FlushExplosives(id,peer);FlushAmmo(id,peer);FlushPanels(id,peer);FlushFood(id,peer);if(peer.FoodReset!=null&&Send(id,peer.FoodReset))peer.FoodReset=null;FlushSupplies(id,peer);FlushLoot(id,peer);FlushSpawns(id,peer);FlushHealth(id,peer);FlushPods(id,peer);
            FlushScorePages(id,peer);
        }
        void Receive(string connection,byte[] bytes)
        {
            Packet p;if(!Wire.TryDecode(bytes,out p)||p.Protocol!=Protocol.Name||p.Schema!=Protocol.Schema||p.ContentHash!=content||p.Epoch<0){Reject(connection);return;}
            if(State!=R24LanLobbyState.Hosting&&connection==serverPeer){
                if(State==R24LanLobbyState.Connecting&&SessionId==null&&p.Kind==PacketKind.Hello&&p.FeatureId==Offer)Epoch=p.Epoch;
                else if(SessionId!=null&&p.SessionId==SessionId&&p.ActorId==hostActor&&p.Kind==PacketKind.Snapshot&&p.FeatureId==MapStateFeature){ReadMapState(p);return;}
            }
            if(p.Epoch<Epoch)return;
            if(p.Epoch!=Epoch){
                // Unreliable presence/clock frames are unordered with the reliable
                // map transition and still flow while send pressure retains it.
                // Drop them; reliable out-of-order epochs still fail closed.
                if(State!=R24LanLobbyState.Hosting&&connection==serverPeer&&(p.Kind==PacketKind.Delta||p.Kind==PacketKind.Snapshot&&p.FeatureId==ClockFeature)){DroppedUnreliableFrames++;return;}
                Reject(connection);return;
            }
            if(State==R24LanLobbyState.Hosting){
                Peer peer;if(!peers.TryGetValue(connection,out peer)||p.SessionId!=SessionId||p.ActorId!=peer.Actor){Reject(connection);return;}
                if(peer.Admitted){if(p.Kind==PacketKind.Delta&&p.FeatureId==TeamPingFeature){if(clock.Now>=peer.NextTeamPing&&p.Sequence>peer.LastTeamPing){peer.NextTeamPing=clock.Now+.2;peer.LastTeamPing=p.Sequence;ReceiveTeamPing(p,true);}return;}if(p.Kind==PacketKind.Command&&p.FeatureId==WebFeature){ReceiveWeb(p,true);return;}if(p.Kind==PacketKind.Command&&p.FeatureId==SpawnRequestFeature){ReadSpawnRequest(connection,p);return;}if(p.Kind==PacketKind.Command&&(ReadAwpCueCommand(peer,p)||ReadGameCommand(peer,p)))return;if(p.Kind==PacketKind.Delta&&p.FeatureId==RawTrackingFeature){ReadRawTracking(peer,p);return;}if(p.Kind==PacketKind.Delta&&p.FeatureId==Presence){ReadClientPresence(peer,p);return;}if(p.Kind==PacketKind.Delta&&p.FeatureId==ClockFeature){ReadClockRequest(peer,p);return;}if(p.Kind==PacketKind.Command&&p.FeatureId==MapLoadedFeature){ReadMapLoaded(connection,p);return;}Reject(connection);return;}
                if(p.Kind!=PacketKind.Hello||p.FeatureId!=Join){Reject(connection);return;}
                string name;if(!ReadName(p.Payload,out name)){Reject(connection);return;}
                string token;var result=sessions.Admit(connection,bytes,new JoinRequest(Protocol.Name,Protocol.Schema,content,name,null,false),++tick,out token);if(!result.Accepted){Reject(connection);return;}
                peer.Admitted=true;ReplayWeb(connection);ReplayPulls(connection);ReplayVehicles(connection);foreach(var panel in sharedPanels)peer.Panels[panel.Key]=panel.Value;foreach(var pod in podStates.Values)peer.Pods[pod.Actor]=Packet(PacketKind.Snapshot,PodStateFeature,OwnActor,EncodePod(pod),pod.Sequence);ReplayLoot(peer);foreach(var food in foods.Values)peer.Foods[food.Id]=FoodFrame(food);foreach(var item in sharedSupplies.Values)peer.Supplies[item.Id]=SupplyFrame(item);peer.Name=name;peer.Offer=null;if(mapFrame!=null)peer.MapState=mapFrame;if(retainedScoreboard!=null)scoreDirty=true;PublishRoster();return;
            }
            if(connection!=serverPeer){Reject(connection);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==MeleeHitFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadMeleeHit(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==VehicleFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadVehicle(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==PullFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadPull(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==WebFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReceiveWeb(p,false);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==HealthStateFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadHealthState(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==SpawnStateFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadSpawnState(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==SpawnRejectedFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadSpawnRejected(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==PanelFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadPanel(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==ResourcesFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadResources(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.SessionId==SessionId&&p.ActorId==hostActor){if(p.FeatureId==FoodResetFeature){ReadFoodReset(p);return;}if(p.FeatureId==GrenadeFeature){ReadGrenade(p);return;}if(p.FeatureId==ProjectileFeature){ReadProjectile(p);return;}}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==AmmoFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadAmmo(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==FoodFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadFood(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==SupplyFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadSupply(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==LootFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadLoot(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==AwpCueFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadAwpCueSnapshot(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==ScoreFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadScores(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==PodStateFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadPod(p);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Snapshot&&p.FeatureId==ClockFeature&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadClockReply(p);return;}
            if((State==R24LanLobbyState.Connected||State==R24LanLobbyState.Connecting)&&p.Kind==PacketKind.Delta&&p.FeatureId==TeamPingFeature&&p.SessionId==SessionId){if(State==R24LanLobbyState.Connected)ReceiveTeamPing(p,false);return;}
            if(State==R24LanLobbyState.Connected&&p.Kind==PacketKind.Delta&&p.FeatureId==Presence&&p.SessionId==SessionId){ReadServerPresence(p);return;}
            // Host relays unreliable presence once this peer is admitted, which can
            // precede (or overtake a retransmitted) complete reliable roster.
            if(State==R24LanLobbyState.Connecting&&SessionId!=null&&p.Kind==PacketKind.Delta&&p.FeatureId==Presence&&p.SessionId==SessionId){DroppedUnreliableFrames++;return;}
            if(State==R24LanLobbyState.Connecting&&p.Kind==PacketKind.Hello&&p.FeatureId==Offer){
                if(SessionId!=null||!ReadOffer(p.Payload)){Reject(connection);return;}SessionId=p.SessionId;OwnActor=p.ActorId;
                if(!Wire.IsText(SessionId,64,false)||!Wire.IsText(OwnActor,64,false)){Reject(connection);return;}
                clientHello=Packet(PacketKind.Hello,Join,OwnActor,EncodeName(displayName),0);Dirty("Joining "+RoomName);return;
            }
            if((State==R24LanLobbyState.Connecting||State==R24LanLobbyState.Connected)&&p.Kind==PacketKind.Snapshot&&p.FeatureId==Roster&&p.SessionId==SessionId&&p.ActorId==hostActor){ReadRoster(p);return;}
            Reject(connection);
        }
        void Reject(string peer){RejectedFrames++;closePeer(peer);if(State==R24LanLobbyState.Hosting)TransportDisconnectedIfRetained(peer);else if(peer==serverPeer){ClearHealth();members=new R24LanMember[0];State=R24LanLobbyState.Offline;Dirty("LAN connection rejected (version or protocol)");}}
        // Lobby selection is roster metadata, not a world/Health epoch or loading barrier.
        string roomMapChoice,incomingRoomMapChoice;long roomMapChoiceEpoch=-1;
        public string RoomMapId{get{return roomMapChoiceEpoch==Epoch&&(SelectedMap==null||SelectedMap.Cancelled)?roomMapChoice:MapId;}}
        public bool SelectRoomMap(string id)
        {
            Check();if(State!=R24LanLobbyState.Hosting||!Wire.IsText(id,128,false)||SelectedMap!=null&&!SelectedMap.Cancelled)return false;
            if(RoomMapId==id)return true;roomMapChoice=id;roomMapChoiceEpoch=Epoch;PublishRoster();return true;
        }
        void PublishRoster()
        {
            var source=sessions.CaptureMembers();var next=new R24LanMember[source.Length];
            for(int i=0;i<source.Length;i++){var m=source[i];next[i]=new R24LanMember(m.ActorId,m.Name,m.Team,m.ActorId==OwnActor,m.Loaded);}
            members=next;revision++;
            var frames=new byte[next.Length][];for(int i=0;i<next.Length;i++)frames[i]=Packet(PacketKind.Snapshot,Roster,OwnActor,EncodeMember(i,next.Length,next[i],RoomMapId),revision);
            // Roster is replaceable state: a newer complete revision supersedes
            // unsent older roster rows; reliable commands are never discarded.
            foreach(var peer in peers.Values)if(peer.Admitted){peer.Roster=frames;peer.Next=0;}
            Dirty(MapRosterStatus("Hosting LAN lobby: "+members.Length+" / "+RoomCapacity));
        }
        void ReadRoster(Packet packet)
        {
            int index,count;string choice;R24LanMember member;if(packet.Sequence<=committedRevision||!DecodeMember(packet.Payload,out index,out count,out member,out choice)){if(packet.Sequence<=committedRevision)return;Reject(serverPeer);return;}
            if(packet.Sequence<incomingRevision)return;
            if(packet.Sequence>incomingRevision){incomingRevision=packet.Sequence;incoming=new R24LanMember[count];incomingCount=0;incomingRoomMapChoice=choice;}
            if(incoming.Length!=count||incoming[index]!=null||incomingRoomMapChoice!=choice){Reject(serverPeer);return;}incoming[index]=member;incomingCount++;
            if(incomingCount!=count)return;
            var actors=new HashSet<string>(StringComparer.Ordinal);bool self=false;int hosts=0;
            foreach(var row in incoming){if(!actors.Add(row.Actor)){Reject(serverPeer);return;}self|=row.Actor==OwnActor;if(row.Host){hosts++;if(row.Actor!=hostActor){Reject(serverPeer);return;}}}
            if(!self||hosts!=1){Reject(serverPeer);return;}
            members=incoming;roomMapChoice=incomingRoomMapChoice;roomMapChoiceEpoch=Epoch;incoming=null;committedRevision=incomingRevision;PrunePresence();PruneHealth();State=R24LanLobbyState.Connected;Dirty(MapRosterStatus("Connected to "+RoomName+": "+members.Length+" / "+RoomCapacity));
        }
        byte[] Packet(PacketKind kind,string feature,string actor,byte[] payload,long sequence)
        {return Wire.Encode(new Packet{Kind=kind,FeatureId=feature,ActorId=actor,SessionId=SessionId,ContentHash=content,Epoch=Epoch,Sequence=sequence,BaseSequence=-1,Payload=payload});}
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        static void Write(BinaryWriter w,string text,int max){if(!Wire.IsText(text,max,false))throw new ArgumentException("Lobby text");byte[] bytes=Utf8.GetBytes(text);w.Write((ushort)bytes.Length);w.Write(bytes);}
        static string Read(BinaryReader r,int max){int count=r.ReadUInt16();if(count<1||count>max||count>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException();return Utf8.GetString(r.ReadBytes(count));}
        byte[] EncodeOffer(){using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,RoomName,64);Write(w,MapId,128);Write(w,MapManifestHash,64);Write(w,OwnActor,64);w.Write((byte)RoomCapacity);return m.ToArray();}}
        bool ReadOffer(byte[] bytes){try{using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Utf8)){RoomName=Read(r,64);MapId=Read(r,128);MapManifestHash=Read(r,64);hostActor=Read(r,64);offeredCapacity=r.ReadByte();return Wire.IsHash(MapManifestHash)&&offeredCapacity>=2&&offeredCapacity<=Capacity&&m.Position==m.Length;}}catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}}
        static byte[] EncodeName(string name){using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){Write(w,name,64);return m.ToArray();}}
        static bool ReadName(byte[] bytes,out string name){name=null;try{using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Utf8)){name=Read(r,64);return m.Position==m.Length;}}catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}}
        static byte[] EncodeMember(int index,int count,R24LanMember row,string choice){using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){w.Write((byte)index);w.Write((byte)count);Write(w,row.Actor,64);Write(w,row.Name,96);w.Write(row.Team);w.Write(row.Host);w.Write(row.WorldLoaded);Write(w,choice,128);return m.ToArray();}}
        static bool DecodeMember(byte[] bytes,out int index,out int count,out R24LanMember row,out string choice){index=count=0;row=null;choice=null;try{using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Utf8)){index=r.ReadByte();count=r.ReadByte();string actor=Read(r,64),name=Read(r,96);int team=r.ReadInt32();bool host=r.ReadBoolean(),loaded=r.ReadBoolean();choice=Read(r,128);if(count<1||count>Capacity||index>=count||team< -1||team>7||m.Position!=m.Length)return false;row=new R24LanMember(actor,name,team,host,loaded);return true;}}catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}}
        static bool Finite(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value);}
        public void Dispose(){if(disposed)return;Check();disposed=true;try{ClearHealth();}catch(Ashfall.Modules.HealthStates.HealthStorageException){/* Closing never resumes authority; preserve the surfaced storage failure. */}finally{ClearSpawns();spawnWorld.Clear();ClearRawTracking();rawWorldClear=null;zoneDamage=null;podWorld=null;ballisticTrace=null;foodGeometry=null;supplyDropWorld=null;constructionWorld=null;projectileWorld=null;peers.Clear();principals.Clear();poses.Clear();localPresence=null;members=new R24LanMember[0];incoming=null;clientHello=null;mapFrame=pendingMapLoaded=null;SelectedMap=null;sessions=null;State=R24LanLobbyState.Offline;Status=HealthStorageFailure??"Not connected";}}
    }
}
