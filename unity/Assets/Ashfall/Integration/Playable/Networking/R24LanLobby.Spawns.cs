using System;using System.Collections.Generic;using System.IO;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;using Ashfall.Integration.Sessions;
namespace Ashfall.Integration.Playable.Networking
{
    // net_bridge1000 source formulas in represented Unity coordinates. Callers
    // supply the real map frame; canonical Godot origins are never added twice.
    public static class R24LanSpawnCoordinates
    {
        static void Check(Float3 origin,int index){if(!origin.IsFinite||index<0||index>=R24LanLobby.Capacity)throw new ArgumentException("Finite represented source frame and member index required");}
        public static Float3 Warmup(Float3 origin,int index,bool archive)
        {Check(origin,index);return new Float3(origin.X+(index%4-1.5f)*(archive?.9f:1.5f),origin.Y+(archive?0:.1f),origin.Z+(archive?.65f-(index/4)*1.3f:-2-(index/4)*2));}
        public static Float3 BuiltinArena(Float3 origin,int index)
        {Check(origin,index);return new Float3(origin.X-15+(index%4)*10,origin.Y+2,origin.Z+6+((index/4)%2)*30+(index/8)*1.5f);}
        public static Float3 CyberArena(Float3 representedOrigin,int index)
        {Check(representedOrigin,index);return new Float3(representedOrigin.X-55+(index%4)*35,representedOrigin.Y+1,representedOrigin.Z-330+((index/4)%2)*660+(index/8)*1.5f);}
    }
    public interface IR24LanSpawnWorld:ISpawnWorld
    {bool ReadyForSpawn(string map,bool warmup);}
    public sealed class R24LanSpawn
    {
        public readonly string Actor,MapHash;public readonly long Epoch;public readonly int Revision,Team,Deaths;public readonly SessionLife Life;public readonly Float3 Feet;
        internal R24LanSpawn(string actor,string hash,long epoch,int revision,int team,int deaths,Float3 feet,SessionLife life=SessionLife.Alive){Life=life;Actor=actor;MapHash=hash;Epoch=epoch;Revision=revision;Team=team;Deaths=deaths;Feet=feet;}
    }
    public sealed partial class R24LanLobby
    {
        const string SpawnRequestFeature="playable.spawn.request/1",SpawnStateFeature="playable.spawn.state/1",SpawnRejectedFeature="playable.spawn.rejected/1";
        readonly SpawnWorld spawnWorld=new SpawnWorld();readonly Dictionary<string,R24LanSpawn> spawnStates=new Dictionary<string,R24LanSpawn>(StringComparer.Ordinal);
        byte[] pendingSpawnRequest;long spawnCommand,spawnStateSequence;double nextSpawnRequest;
        internal bool SpawnRequestOutstanding{get;private set;}
        public string SpawnStatus{get;private set;}public string SpawnFailure{get;private set;}internal string SpawnActorInFlight{get{return spawnWorld.Actor;}}
        // Session places revive-mode late joiners in WaitingRound/Spectator and
        // refuses Spawn with "spectator". That is "join next round", not a fault.
        public bool SpawnWaitingForNextRound{get;private set;}
        const string WaitingRoundRefusal="spectator";
        sealed class SpawnWorld:ISpawnWorld
        {
            internal ISpawnWorld Bound;readonly R24LanSpawnQueryCache query=new R24LanSpawnQueryCache();
            internal string Actor{get{return query.Actor;}}
            internal IDisposable Begin(string map,string actor,bool warmup){return query.Begin(Bound,map,actor,warmup);}
            internal void Clear(){query.Clear();Bound=null;}
            public SpawnSite[] Sites(string map,bool warmup){return query.Sites(map,warmup);}
            public bool IsSafe(string map,string actor,Float3 feet){return query.IsSafe(map,actor,feet);}
        }
        sealed class Placement:ISessionPlacement
        {
            readonly R24LanLobby owner;internal Placement(R24LanLobby owner){this.owner=owner;}
            public void Publish(string actor,long epoch,MemberSnapshot member){owner.PublishSpawn(actor,epoch,member);}
        }
        public void BindSpawnWorld(ISpawnWorld world)
        {Check();if(State!=R24LanLobbyState.Hosting||world==null||spawnWorld.Bound!=null)throw new InvalidOperationException("One host-owned spawn world required");spawnWorld.Bound=world;}
        bool SpawnReady(string actor)
        {return RawTrackingMapReady(actor,MapManifestHash)&&RoundCountdown<=0;}
        // Results retire tracking/commands, but the committed placement still witnesses Health.
        bool SpawnReceiptMapReady(string actor)
        {
            if(SelectedMap==null||!SelectedMap.Results)return RawTrackingMapReady(actor,MapManifestHash);
            if(!SelectedMap.Active||SelectedMap.Cancelled||SelectedMap.Epoch!=Epoch||SelectedMap.ManifestHash!=MapManifestHash)return false;
            foreach(var member in members)if(member.Actor==actor)return member.WorldLoaded;
            return false;
        }
        // Read-only composition signal; actual spawn still runs every existing Session/safety gate.
        public bool HostSpawnWorldReady{get{Check();return State==R24LanLobbyState.Hosting&&SelectedMap!=null&&SpawnWorldReady();}}
        bool SpawnWorldReady()
        {var ready=spawnWorld.Bound as IR24LanSpawnWorld;return spawnWorld.Bound!=null&&(ready==null||ready.ReadyForSpawn(MapId,SelectedMap.Rules==null));}
        internal bool WarmupPlayersPlaced
        {
            get{
                if(State!=R24LanLobbyState.Hosting||SelectedMap==null||!SelectedMap.Active||SelectedMap.Rules!=null)return false;
                foreach(var member in members){
                    if(spawnStates.ContainsKey(member.Actor))continue;
                    var actual=sessions.Get(member.Actor);
                    if(actual==null||actual.Life!=SessionLife.Spectator)return false;
                }
                return true;
            }
        }
        public bool RequestSpawn()
        {
            Check();if((State!=R24LanLobbyState.Hosting&&State!=R24LanLobbyState.Connected)||!SpawnReady(OwnActor)||pendingSpawnRequest!=null||SpawnRequestOutstanding)return false;
            if(clock.Now<nextSpawnRequest)return false;nextSpawnRequest=clock.Now+.2;SpawnFailure=null;SpawnWaitingForNextRound=false;
            long sequence=checked(++spawnCommand+1);if(State==R24LanLobbyState.Hosting)return ApplySpawn("local-host",OwnActor,sequence);
            SpawnRequestOutstanding=!spawnStates.ContainsKey(OwnActor);pendingSpawnRequest=Packet(PacketKind.Command,SpawnRequestFeature,OwnActor,new byte[0],sequence);SpawnStatus="Awaiting host-approved spawn";return true;
        }
        void ReadSpawnRequest(string connection,Packet packet)
        {
            if(packet.Payload.Length!=0||packet.BaseSequence!=-1||packet.Sequence<2){RejectedFrames++;return;}
            Peer peer;if(!peers.TryGetValue(connection,out peer)||!peer.Admitted)return;
            if(clock.Now-peer.SpawnBudgetAt>1){peer.SpawnBudgetAt=clock.Now;peer.SpawnBudget=0;}peer.SpawnBudget=Math.Min(121,peer.SpawnBudget+1);if(peer.SpawnBudget>120){RejectedFrames++;return;}
            if(peer.PendingSpawnSequence!=0)return;
            if(RawTrackingMapReady(peer.Actor,MapManifestHash)&&(RoundCountdown>0||spawnWorld.Bound!=null&&!SpawnWorldReady())){
                if(peer.PendingSpawnSequence==0){peer.PendingSpawnSequence=packet.Sequence;peer.PendingSpawnUntil=clock.Now+Session.LoadingTimeoutSeconds;}return;
            }
            ApplySpawn(connection,packet.ActorId,packet.Sequence);
        }
        bool ApplySpawn(string connection,string actor,long sequence)
        {
            if(!SpawnReady(actor))return RefuseSpawn(connection,actor,"Actual map readiness/countdown required");
            if(HealthOwnsSpawnRequest(actor))return RefuseSpawn(connection,actor,"Host Health lifecycle owns death/respawn");
            long at=checked(++tick);var frame=new Packet{Kind=PacketKind.Command,FeatureId="sessions.spawn/1",ActorId=actor,SessionId=SessionId,ContentHash=content,Epoch=Epoch,Sequence=sequence,BaseSequence=-1,Tick=at,CommandId="lan-spawn/"+Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)+"/"+sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),Payload=new byte[0]};
            var queued=sessions.Receive(connection,Wire.Encode(frame),at);if(!queued.Accepted)return RefuseSpawn(connection,actor,queued.Reason);
            RuleResult result;
            using(spawnWorld.Begin(MapId,actor,SelectedMap.Rules==null)){if(!sessions.DispatchOne(out result))return RefuseSpawn(connection,actor,"dispatch-unavailable");}
            if(!result.Accepted)return RefuseSpawn(connection,actor,result.Reason);RegisterPlacedHealth(actor);SpawnStatus="Actual Session spawn approved";return true;
        }
        bool RefuseSpawn(string connection,string actor,string reason)
        {
            SpawnStatus="Spawn refused: "+reason;if(actor==OwnActor){SpawnFailure=SpawnStatus;return false;}
            Peer peer;if(!peers.TryGetValue(connection,out peer)||!peer.Admitted||peer.Actor!=actor)return false;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){w.Write((byte)1);Write(w,actor,64);Write(w,reason,128);peer.Spawns[actor]=Packet(PacketKind.Snapshot,SpawnRejectedFeature,OwnActor,s.ToArray(),checked(++spawnStateSequence));}return false;
        }
        void ReadSpawnRejected(Packet packet)
        {
            if(packet.Sequence<1||packet.BaseSequence!=-1){Reject(serverPeer);return;}
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){if(r.ReadByte()!=1)throw new InvalidDataException();string actor=Read(r,64),reason=Read(r,128);if(s.Position!=s.Length)throw new InvalidDataException();if(actor==OwnActor){SpawnFailure=SpawnStatus="Spawn refused: "+reason;SpawnRequestOutstanding=false;pendingSpawnRequest=null;SpawnWaitingForNextRound=reason==WaitingRoundRefusal&&SelectedMap!=null&&SelectedMap.Rules!=null;if(SpawnWaitingForNextRound)Dirty("Round in progress: you join the next round");}}}
            catch(InvalidDataException){Reject(serverPeer);}catch(IOException){Reject(serverPeer);}catch(ArgumentException){Reject(serverPeer);}
        }
        void PublishSpawn(string actor,long epoch,MemberSnapshot member)
        {
            if(State!=R24LanLobbyState.Hosting||epoch!=Epoch||member==null||member.ActorId!=actor||!member.Loaded||!member.HasPosition||(member.Life!=SessionLife.Alive&&member.Life!=SessionLife.Eliminated)||member.SpawnRevision<1)throw new InvalidOperationException("Actual Session placement required");
            var state=new R24LanSpawn(actor,MapManifestHash,epoch,member.SpawnRevision,member.Team,member.Deaths,member.Position,member.Life);R24LanSpawn prior;
            if(!spawnStates.TryGetValue(actor,out prior)||prior.Revision!=state.Revision)ForgetRawTracking(actor);
            spawnStates[actor]=state;var frame=Packet(PacketKind.Snapshot,SpawnStateFeature,OwnActor,EncodeSpawn(state),checked(++spawnStateSequence));
            foreach(var p in peers.Values)if(p.Admitted)p.Spawns[actor]=frame;
        }
        static byte[] EncodeSpawn(R24LanSpawn state)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){w.Write((byte)2);Write(w,state.Actor,64);Write(w,state.MapHash,64);w.Write(state.Revision);w.Write(state.Team);w.Write(state.Deaths);w.Write((int)state.Life);w.Write(state.Feet.X);w.Write(state.Feet.Y);w.Write(state.Feet.Z);return s.ToArray();}
        }
        void ReadSpawnState(Packet packet)
        {
            if(packet.Sequence<1||packet.BaseSequence!=-1){Reject(serverPeer);return;}
            R24LanSpawn state;
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){if(r.ReadByte()!=2)throw new InvalidDataException();string actor=Read(r,64),hash=Read(r,64);int revision=r.ReadInt32(),team=r.ReadInt32(),deaths=r.ReadInt32();var life=(SessionLife)r.ReadInt32();var feet=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());if(s.Position!=s.Length||!Wire.IsHash(hash)||revision<1||team< -1||team>7||deaths<0||(life!=SessionLife.Alive&&life!=SessionLife.Eliminated)||!feet.IsFinite||Math.Abs(feet.X)>100000||Math.Abs(feet.Y)>100000||Math.Abs(feet.Z)>100000)throw new InvalidDataException();state=new R24LanSpawn(actor,hash,packet.Epoch,revision,team,deaths,feet,life);}}
            catch(InvalidDataException){Reject(serverPeer);return;}catch(IOException){Reject(serverPeer);return;}catch(ArgumentException){Reject(serverPeer);return;}
            // The host already applied its countdown gate; each placement is sent
            // once, so never drop it on this guest's own clock estimate (reads
            // still apply SpawnReady through TryReadSpawn).
            if(state.MapHash!=MapManifestHash||!SpawnReceiptMapReady(state.Actor))return;
            foreach(var member in members)if(member.Actor==state.Actor&&member.Team!=state.Team){Reject(serverPeer);return;}
            R24LanSpawn prior;if(spawnStates.TryGetValue(state.Actor,out prior)&&state.Revision<=prior.Revision)return;
            spawnStates[state.Actor]=state;ForgetRawTracking(state.Actor);if(state.Actor==OwnActor){pendingRawTracking=null;SpawnRequestOutstanding=false;pendingSpawnRequest=null;SpawnStatus="Actual host Session placement received";}
        }
        void FlushSpawns(string connection,Peer peer)
        {
            if(peer.Spawns.Count==0)return;string sent=null;foreach(var item in peer.Spawns){if(!Send(connection,item.Value))return;sent=item.Key;break;}if(sent!=null)peer.Spawns.Remove(sent);
        }
        void TickSpawns()
        {
            if(State==R24LanLobbyState.Connected){if(pendingSpawnRequest!=null&&Send(serverPeer,pendingSpawnRequest))pendingSpawnRequest=null;return;}
            if(State!=R24LanLobbyState.Hosting)return;
            foreach(var item in peers){var peer=item.Value;if(!peer.Admitted||peer.PendingSpawnSequence==0)continue;
                if(clock.Now>=peer.PendingSpawnUntil){peer.PendingSpawnSequence=0;RefuseSpawn(item.Key,peer.Actor,"host-world-not-ready");}
                else if(SpawnReady(peer.Actor)&&SpawnWorldReady()){long sequence=peer.PendingSpawnSequence;peer.PendingSpawnSequence=0;ApplySpawn(item.Key,peer.Actor,sequence);}
            }
        }
        public bool TryReadSpawn(string actor,out R24LanSpawn state)
        {Check();state=null;return actor!=null&&RoundCountdown<=0&&SpawnReceiptMapReady(actor)&&spawnStates.TryGetValue(actor,out state)&&state.Epoch==Epoch&&state.MapHash==MapManifestHash;}
        public bool OwnsSpawn(R24LanSpawn state)
        {Check();R24LanSpawn current;return state!=null&&TryReadSpawn(state.Actor,out current)&&Object.ReferenceEquals(state,current);}
        public MemberSnapshot ReadAuthorityMember(string actor)
        {Check();return State==R24LanLobbyState.Hosting&&actor!=null?sessions.Get(actor):null;}
        void ClearSpawns(){SpawnRequestOutstanding=false;spawnStates.Clear();pendingSpawnRequest=null;nextSpawnRequest=0;spawnCommand=0;foreach(var p in peers.Values){p.Spawns.Clear();p.PendingSpawnSequence=0;p.PendingSpawnUntil=0;p.SpawnBudget=0;p.SpawnBudgetAt=0;}SpawnStatus=SpawnFailure=null;SpawnWaitingForNextRound=false;}
        void RemoveSpawn(string actor){RemoveHealth(actor);spawnStates.Remove(actor);foreach(var p in peers.Values)p.Spawns.Remove(actor);}
    }
}
