using System;using System.Collections.Generic;using System.IO;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.Playable.Networking
{
    // A host-produced, immutable presentation receipt. This is never a Health
    // mutation port, a client life claim, or a replacement HealthService.
    public sealed class R24LanHealthState
    {
        public readonly string Actor,CanonicalActor,MapHash;public readonly long Epoch,Revision,LifeGeneration,ConnectionGeneration;
        public readonly int SpawnRevision,Team,Deaths;public readonly LifeState Life;public readonly float Health,Shield,Charge;
        public readonly double DownedUntil,RespawnAt,ProtectionUntil,ReadyUntil;public readonly bool Connected;
        internal R24LanHealthState(string actor,string canonical,string map,long epoch,int spawn,int team,int deaths,long revision,long lifeGeneration,long connectionGeneration,LifeState life,float health,float shield,float charge,double downed,double respawn,double protection,double ready,bool connected)
        {Actor=actor;CanonicalActor=canonical;MapHash=map;Epoch=epoch;SpawnRevision=spawn;Team=team;Deaths=deaths;Revision=revision;LifeGeneration=lifeGeneration;ConnectionGeneration=connectionGeneration;Life=life;Health=health;Shield=shield;Charge=charge;DownedUntil=downed;RespawnAt=respawn;ProtectionUntil=protection;ReadyUntil=ready;Connected=connected;}
        internal R24LanHealthState(string actor,string map,long epoch,int spawn,HealthSnapshot health):this(actor,health.Id,map,epoch,spawn,health.Team,health.Deaths,health.Revision,health.LifeGeneration,health.ConnectionGeneration,health.Life,health.Health,health.Shield,health.Charge,health.DownedUntil,health.RespawnAt,health.ProtectionUntil,health.ReadyUntil,health.Connected){}
    }
    public sealed partial class R24LanLobby
    {
        const string HealthStateFeature="playable.health.state/1";
        HealthService hostHealth;readonly Dictionary<string,R24LanHealthState> healthStates=new Dictionary<string,R24LanHealthState>(StringComparer.Ordinal);
        long healthStateSequence;bool syncingHealth;
        sealed class HostHealthWorld:IHealthWorld,IInteractionQuery
        {
            readonly R24LanLobby owner;internal HostHealthWorld(R24LanLobby owner){this.owner=owner;}
            public double Now{get{return owner.clock.Now;}}
            public bool InMatch{get{return owner.State==R24LanLobbyState.Hosting&&owner.sessions!=null&&owner.sessions.Stage==SessionStage.Match&&owner.SelectedMap!=null&&owner.SelectedMap.Epoch==owner.Epoch&&owner.SelectedMap.Active&&!owner.SelectedMap.Cancelled&&!owner.SelectedMap.Results&&owner.RoundCountdown<=0;}}
            public bool TeamChangesAllowed{get{return false;}}
            // Authenticated current-revision raw tracking (never render presence)
            // supplies hands/chest/facing; the client contributes only pose.paddles.
            public bool TryGetPose(string actor,out RescuePose pose){return owner.TryRescuePose(actor,out pose);}
            public bool HasLineOfSight(string actor,string target,Float3 from,Float3 to,float distance){return owner.rawWorldClear!=null&&from.IsFinite&&to.IsFinite&&owner.rawWorldClear(from,to);}
        }
        // Host-only Health gate. Member-scoped contexts (paddle sampling, revive)
        // are minted inside one host operation; a client never supplies them.
        sealed class HealthGate:IAuthorityGate
        {
            readonly string host;internal string Minted;internal HealthGate(string host){this.host=host;}
            public RuleResult Validate(CommandContext c){return c.IsAuthority&&(c.ActorId==host||Minted!=null&&c.ActorId==Minted)?RuleResult.Accept():RuleResult.Reject("host-only");}
        }
        Func<string,long,IHealthImageStore> healthStoreFactory;int healthStoreKeys=4096;long healthResidentBytes=67108864;
        public void BindHealthStorage(Func<string,long,IHealthImageStore> factory,int maximumReceiptKeys=4096,long residentStateBytes=67108864){Check();if(State!=R24LanLobbyState.Hosting||factory==null||healthStoreFactory!=null||hostHealth!=null||maximumReceiptKeys<1||maximumReceiptKeys>16384)throw new InvalidOperationException("Bind host Health storage before map entry");healthStoreFactory=factory;healthStoreKeys=maximumReceiptKeys;healthResidentBytes=residentStateBytes;}
        string retiredHealthStorageFailure;
        public string HealthStorageFailure{get{return hostHealth==null?retiredHealthStorageFailure:hostHealth.StorageFailure??retiredHealthStorageFailure;}}
        HealthGate healthGate;
        RuleResult AsMember(string canonical,Func<CommandContext,RuleResult> operation)
        {
            if(healthGate==null||String.IsNullOrEmpty(canonical))return RuleResult.Reject("host-health-owner-required");long id=checked(++tick);
            healthGate.Minted=canonical;try{return operation(new CommandContext(SessionId,canonical,"health-member/"+id.ToString(System.Globalization.CultureInfo.InvariantCulture),id,Epoch,true));}finally{healthGate.Minted=null;}
        }
        void BeginHealthEpoch()
        {
            ClearHealth();if(State!=R24LanLobbyState.Hosting)return;
            var world=new HostHealthWorld(this);var rules=roundRules??warmupRules;if(rules==null)throw new InvalidOperationException("Actual selected source health rules required");
            healthGate=new HealthGate(OwnActor);hostHealth=new HealthService(SessionId,Epoch,rules.Teams,rules.Mode=="respawn",healthGate,world,world,rules.DownedSeconds,rules.RespawnDelay,rules.Protection);if(healthStoreFactory!=null){var store=healthStoreFactory(SessionId,Epoch);try{if(store.Read()!=null)throw new HealthStorageException("existing epoch requires explicit recovery; a live LAN Session cannot be reconstructed from Health alone");hostHealth.EnableDurableImages(store,content,healthStoreKeys,healthResidentBytes);}catch{store.Dispose();throw;}}
        }
        void ClearHealth()
        {try{if(hostHealth!=null){retiredHealthStorageFailure=hostHealth.StorageFailure??retiredHealthStorageFailure;hostHealth.ReleaseDurableImages();}}catch(HealthStorageException e){retiredHealthStorageFailure=e.Message;throw;}finally{hostHealth=null;healthGate=null;healthStates.Clear();healthStateSequence=0;syncingHealth=false;foreach(var peer in peers.Values)peer.Health.Clear();ClearCombat();ClearPods();}}
        void PruneHealth()
        {var retired=new List<string>();foreach(var actor in healthStates.Keys)if(!HasMember(actor))retired.Add(actor);foreach(var actor in retired)healthStates.Remove(actor);}
        void RemoveHealth(string actor)
        {
            // Session disconnect is authoritative; no disconnected actor retains
            // a presentation capability or a queued frame in this lobby.
            if(hostHealth!=null){var member=sessions.Get(actor);R24LanHealthState prior;if(healthStates.TryGetValue(actor,out prior)){var h=hostHealth.Read(prior.CanonicalActor);if(h!=null&&h.Connected){var result=hostHealth.SetConnected(LifecycleContext(),h.Id,h.Revision,false);if(!result.Accepted)throw new InvalidOperationException("Host Health departure: "+result.Reason);hostHealth.DrainTransitions();}}if(member!=null)throw new InvalidOperationException("Session departure must precede Health retirement");}
            healthStates.Remove(actor);foreach(var peer in peers.Values)peer.Health.Remove(actor);
            // Departed custody/score/pod state is retired with Session membership.
            RetireCombatCustody(actor);if(combat.Remove(actor))scoreDirty=true;PodRide ride;if(pods.TryGetValue(actor,out ride)&&ride.Phase==R24LanPodPhase.Flying)FinishPod(actor,ride,R24LanPodPhase.Released,ride.Plan.Position(Math.Max(0,RoundElapsed-ride.Started)));
        }
        bool HealthOwnsSpawnRequest(string actor)
        {if(hostHealth==null)return false;var member=sessions.Get(actor);var health=member==null?null:hostHealth.Read(member.CanonicalActorId);return health!=null&&health.Life!=LifeState.Alive;}
        void RegisterPlacedHealth(string actor)
        {
            if(hostHealth==null)return;var member=sessions.Get(actor);
            if(member==null||!member.Loaded||!member.HasPosition||member.Life!=SessionLife.Alive)throw new InvalidOperationException("Actual placed Session member required for Health registration");
            if(hostHealth.Read(member.CanonicalActorId)==null){var rules=roundRules??warmupRules;var result=hostHealth.RegisterConfiguredSpawn(LifecycleContext(),member.CanonicalActorId,member.Team,false,rules.StartingHealth,rules.StartingShield);if(!result.Accepted)throw new InvalidOperationException("Host Health registration: "+result.Reason);}
            EnsureCombatActor(actor);
            SyncHostHealth();
        }
        void SyncHostHealth()
        {
            if(hostHealth==null||syncingHealth)return;syncingHealth=true;
            try{
                foreach(var member in sessions.CaptureMembers()){
                    if(!member.Loaded||!member.HasPosition)continue;var health=hostHealth.Read(member.CanonicalActorId);if(health==null)continue;
                    R24LanHealthState prior;if(healthStates.TryGetValue(member.ActorId,out prior)&&prior.Revision==health.Revision&&prior.SpawnRevision==member.SpawnRevision)continue;
                    if(sessions.Stage==SessionStage.Match||sessions.Stage==SessionStage.Results){var projected=sessions.ProjectCommittedHealth(LifecycleContext(),member.ActorId,hostHealth,clock.Now+1);if(!projected.Accepted)throw new InvalidOperationException("Host Health projection: "+projected.Reason);}
                    var receipt=new R24LanHealthState(member.ActorId,MapManifestHash,Epoch,member.SpawnRevision,health);healthStates[member.ActorId]=receipt;
                    var frame=Packet(Ashfall.Modules.Networking.PacketKind.Snapshot,HealthStateFeature,OwnActor,EncodeHealth(receipt),checked(++healthStateSequence));foreach(var peer in peers.Values)if(peer.Admitted)peer.Health[member.ActorId]=frame;
                }
                // The committed snapshots above were projected and queued before
                // transferring their actual bounded producer transition batch.
                hostHealth.DrainTransitions();
            }finally{syncingHealth=false;}
        }
        void TickHealth()
        {
            if(State!=R24LanLobbyState.Hosting||hostHealth==null)return;
            var advanced=hostHealth.Advance(LifecycleContext());if(!advanced.Accepted)throw new InvalidOperationException("Host Health clock: "+advanced.Reason);SyncHostHealth();TickWarmupRecovery();TickRemoteHazards();
            if(roundRules==null||roundRules.Mode!="respawn"||SelectedMap==null||!SelectedMap.Active||SelectedMap.Results||sessions.Stage!=SessionStage.Match||RoundCountdown>0||!SpawnWorldReady())return;
            foreach(var member in sessions.CaptureMembers()){
                var health=hostHealth.Read(member.CanonicalActorId);if(health==null||!health.Connected||health.Life!=LifeState.Dead||clock.Now<health.RespawnAt||!member.Loaded||!member.HasPosition)continue;
                using(spawnWorld.Begin(MapId,member.ActorId,false)){
                    var sites=spawnWorld.Sites(MapId,false);if(sites==null)continue;
                    foreach(var site in sites){
                        if(site==null||site.CapsulePlatform||(site.Team!=-1&&site.Team!=member.Team)||!spawnWorld.IsSafe(MapId,member.ActorId,site.Feet))continue;
                        var result=sessions.RespawnConfiguredHealth(LifecycleContext(),member.ActorId,hostHealth,site,roundRules.StartingHealth,roundRules.StartingShield,clock.Now+1);
                        if(result.Accepted)break;
                    }
                }
                SyncHostHealth();
            }
        }
        // Trusted local composition only, deliberately no client packet feature.
        // Physical hit, armor, items and weapon evidence must be supplied by the
        // eventual existing combat owner before this adapter is exposed there.
        internal RuleResult ApplyHostHealthLoss(string actor,long revision,float healthLoss,float shieldLoss,bool revivable)
        {
            Check();if(pumping||State!=R24LanLobbyState.Hosting||hostHealth==null)return RuleResult.Reject("host-health-owner-required");
            var member=sessions.Get(actor);if(member==null||!member.Loaded||!member.HasPosition)return RuleResult.Reject("actual-placement-required");
            var result=hostHealth.ApplyLoss(LifecycleContext(),member.CanonicalActorId,revision,healthLoss,shieldLoss,revivable);SyncHostHealth();return result;
        }
        internal RuleResult ApplyHostHealthFall(string actor,long revision)
        {
            Check();if(pumping||State!=R24LanLobbyState.Hosting||hostHealth==null)return RuleResult.Reject("host-health-owner-required");
            var member=sessions.Get(actor);if(member==null||!member.Loaded||!member.HasPosition)return RuleResult.Reject("actual-placement-required");
            var result=hostHealth.ApplyMatchFallDeath(LifecycleContext(),member.CanonicalActorId,revision);SyncHostHealth();return result;
        }
        public bool TryReadHealth(string actor,out R24LanHealthState health)
        {
            Check();health=null;R24LanHealthState value;R24LanSpawn spawn;
            if(actor==null||!healthStates.TryGetValue(actor,out value)||value.Epoch!=Epoch||value.MapHash!=MapManifestHash||!TryReadSpawn(actor,out spawn)||spawn.Revision!=value.SpawnRevision)return false;
            health=value;return true;
        }
        public bool OwnsHealth(R24LanHealthState health)
        {Check();R24LanHealthState actual;return health!=null&&TryReadHealth(health.Actor,out actual)&&Object.ReferenceEquals(health,actual);}
        void FlushHealth(string connection,Peer peer)
        {if(peer.Health.Count==0)return;string sent=null;foreach(var item in peer.Health){if(!Send(connection,item.Value))return;sent=item.Key;break;}if(sent!=null)peer.Health.Remove(sent);}
        static byte[] EncodeHealth(R24LanHealthState h)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream,Utf8)){
                w.Write((byte)1);Write(w,h.Actor,64);Write(w,h.CanonicalActor,128);Write(w,h.MapHash,64);w.Write(h.SpawnRevision);w.Write(h.Team);w.Write(h.Deaths);w.Write(h.Revision);w.Write(h.LifeGeneration);w.Write(h.ConnectionGeneration);w.Write((byte)h.Life);w.Write(h.Health);w.Write(h.Shield);w.Write(h.Charge);w.Write(h.DownedUntil);w.Write(h.RespawnAt);w.Write(h.ProtectionUntil);w.Write(h.ReadyUntil);w.Write(h.Connected);return stream.ToArray();
            }
        }
        void ReadHealthState(Ashfall.Modules.Networking.Packet packet)
        {
            if(packet.Sequence<1||packet.BaseSequence!=-1){Reject(serverPeer);return;}
            R24LanHealthState state;
            try{using(var stream=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(stream,Utf8)){
                if(r.ReadByte()!=1)throw new InvalidDataException();string actor=Read(r,64),canonical=Read(r,128),map=Read(r,64);int spawn=r.ReadInt32(),team=r.ReadInt32(),deaths=r.ReadInt32();long revision=r.ReadInt64(),life=r.ReadInt64(),connection=r.ReadInt64();byte kind=r.ReadByte();float hp=r.ReadSingle(),shield=r.ReadSingle(),charge=r.ReadSingle();double downed=r.ReadDouble(),respawn=r.ReadDouble(),protection=r.ReadDouble(),ready=r.ReadDouble();byte connected=r.ReadByte();
                if(stream.Position!=stream.Length||!Ashfall.Modules.Networking.Wire.IsHash(map)||spawn<1||team< -1||team>7||deaths<0||revision<1||life<1||connection<1||kind>(byte)LifeState.Spectator||!Finite(hp)||hp<0||hp>100||!Finite(shield)||shield<0||shield>100||!Finite(charge)||charge<0||charge>1||!Finite(downed)||downed<0||!Finite(respawn)||respawn<0||!Finite(protection)||protection<0||!Finite(ready)||ready<0||connected>1||kind==(byte)LifeState.Alive&&hp<=0||kind!=(byte)LifeState.Alive&&(hp!=0||shield!=0))throw new InvalidDataException();
                state=new R24LanHealthState(actor,canonical,map,packet.Epoch,spawn,team,deaths,revision,life,connection,(LifeState)kind,hp,shield,charge,downed,respawn,protection,ready,connected==1);
            }}catch(InvalidDataException){Reject(serverPeer);return;}catch(IOException){Reject(serverPeer);return;}catch(ArgumentException){Reject(serverPeer);return;}
            // Host-gated receipt; never discard it on this guest's countdown estimate.
            if(state.MapHash!=MapManifestHash||!RawTrackingMapReady(state.Actor,MapManifestHash))return;bool known=false;foreach(var member in members)if(member.Actor==state.Actor){if(member.Team!=state.Team){Reject(serverPeer);return;}known=true;break;}if(!known)return;
            R24LanHealthState prior;if(healthStates.TryGetValue(state.Actor,out prior)){
                if(state.CanonicalActor!=prior.CanonicalActor||state.Revision<prior.Revision||state.LifeGeneration<prior.LifeGeneration||state.ConnectionGeneration<prior.ConnectionGeneration||state.SpawnRevision<prior.SpawnRevision)return;
                if(state.Revision==prior.Revision&&state.SpawnRevision==prior.SpawnRevision)return;
            }
            healthStates[state.Actor]=state;
        }
    }
}
