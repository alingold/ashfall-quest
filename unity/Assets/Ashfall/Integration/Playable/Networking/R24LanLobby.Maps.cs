using System;using System.Collections.Generic;using System.IO;using System.Text;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed class R24LanMapSelection
    {
        public readonly string MapId,ManifestHash;public readonly long Epoch,Revision;public readonly bool Active,Cancelled;
        public readonly R24PlayableMatchRules Rules,WarmupRules;public R24PlayableMatchRules SourceRules{get{return Rules??WarmupRules;}}public readonly int Seed,Round;public readonly double StartsAt;public readonly bool Results;
        // The host returned the whole room to its lobby (menu city). Home is always Cancelled as well,
        // so every world owner already reads it as "no shared world"; members stay connected.
        public readonly bool Home;
        internal R24LanMapSelection(string map,string hash,long epoch,long revision,bool active,bool cancelled,R24PlayableMatchRules rules,int seed,int round,double startsAt,bool results,R24PlayableMatchRules warmupRules=null,bool home=false){Home=home;WarmupRules=warmupRules;MapId=map;ManifestHash=hash;Epoch=epoch;Revision=revision;Active=active;Cancelled=cancelled;Rules=rules;Seed=seed;Round=round;StartsAt=startsAt;Results=results;}
    }
    public sealed partial class R24LanLobby
    {
        const string MapStateFeature="playable.map/1",MapLoadedFeature="playable.map.loaded/1";
        byte[] mapFrame,pendingMapLoaded;long mapRevision;double nextLoadingHeartbeat,roundStartsAt;bool hostWorldReady,roundResults,roomLobby;R24PlayableMatchRules roundRules,warmupRules;int roundSeed,roundNumber;
        // Native geometry, collision and round assets can exceed the source-session minute.
        public const int LocalMapLoadSeconds=180,SharedMapReadySeconds=210;
        readonly bool nativeMapLoading;
        double sharedMapReadyAt;
        int unreadyMapDepartures;
        public string MapGuestNotice{get;private set;}
        public R24LanMapSelection SelectedMap{get;private set;}
        public SessionStage MapStage{get{return sessions!=null?sessions.Stage:SelectedMap==null||SelectedMap.Cancelled?SessionStage.Lobby:!SelectedMap.Active?SessionStage.Loading:SelectedMap.Rules==null?SessionStage.Lobby:SelectedMap.Results?SessionStage.Results:SessionStage.Match;}}
        CommandContext LifecycleContext(){long id=checked(++tick);return new CommandContext(SessionId,OwnActor,"sessions-lifecycle/"+id.ToString(System.Globalization.CultureInfo.InvariantCulture),id,Epoch,true);}
        void RebindSession()
        {
            string ignored;var r=sessions.Admit("local-host",Packet(PacketKind.Hello,Join,OwnActor,new byte[0],0),new JoinRequest(Protocol.Name,Protocol.Schema,content,displayName,null,false),++tick,out ignored);if(!r.Accepted)throw new InvalidOperationException("Host epoch rebind: "+r.Reason);
            foreach(var item in peers)if(item.Value.Admitted){var p=item.Value;r=sessions.Admit(item.Key,Packet(PacketKind.Hello,Join,p.Actor,new byte[0],0),new JoinRequest(Protocol.Name,Protocol.Schema,content,p.Name,null,false),++tick,out ignored);if(!r.Accepted)throw new InvalidOperationException("Member epoch rebind: "+r.Reason);p.LastPose=0;p.NextPose=0;p.SentPoses.Clear();}
            BeginHealthEpoch();ClearSpawns();ClearRawTracking();poses.Clear();localPresence=null;lastLocalSent=0;pendingMapLoaded=null;hostWorldReady=false;
        }
        public bool BeginMapSelection(string map,string manifestHash,R24PlayableMatchRules sourceRules=null)
        {return BeginMapCore(map,manifestHash,null,0,0,sourceRules);}
        public bool BeginRoundSelection(R24PlayableMatchRules rules,string manifestHash,int seed)
        {Check();if(rules==null||seed<=0||rules.MaxPlayers>Capacity||rules.MaxPlayers<members.Length)return false;return BeginMapCore(rules.MapId,manifestHash,rules,seed,1);}
        bool BeginMapCore(string map,string manifestHash,R24PlayableMatchRules matchRules,int seed,int round,R24PlayableMatchRules sourceRules=null)
        {
            Check();if(State!=R24LanLobbyState.Hosting||!Wire.IsHash(manifestHash)||!Wire.IsText(map,128,false)||MapStage==SessionStage.Loading)return false;
            var effective=matchRules??sourceRules??(roundRules??warmupRules??FallbackRoomRules(map,sessionTeams)).WithMap(map);if(effective.MapId!=map||effective.MaxPlayers>Capacity||effective.MaxPlayers<members.Length)return false;
            var retained=CaptureInventory(matchRules!=null&&round>1&&effective.RetainInventory&&MapId==map);
            ClosePendingPeers();
            var rules=new SessionRules(map,effective.Mode,manifestHash,effective.Teams,effective.MaxPlayers,false,effective.Locked,effective.LateJoin,effective.Spectators,effective.RespawnDelay);var result=sessions.Lifecycle(LifecycleContext(),LifecycleAction.BeginLoading,rules,null,matchRules!=null,nativeMapLoading);if(!result.Accepted)return false;
            sharedMapReadyAt=clock.Now+SharedMapReadySeconds;unreadyMapDepartures=0;MapGuestNotice=null;
            Epoch=sessions.Epoch;MapId=map;MapManifestHash=manifestHash;roundRules=matchRules;warmupRules=matchRules==null?effective:null;roundSeed=seed;roundNumber=round;roundStartsAt=0;roundResults=false;finalResults=null;roomLobby=false;RebindSession();RestoreInventory(retained);PublishMapState(false,false);PublishRoster();Dirty("Loading shared map: "+map);return true;
        }
        int sessionTeams=2;
        static R24PlayableMatchRules FallbackRoomRules(string map,int teams)
        {return teams==0?R24PlayableMatchRules.ForPreset("free_for_all",map):R24PlayableMatchRules.ForPreset("team_revive",map).With("teams",teams);}
        // HOME for a room: one new epoch that retires every world owner (Health, spawns, poses,
        // round clock) while all members stay admitted. Idempotent, and legal in any stage, so
        // it also aborts a shared map load. The room itself ends only through Dispose.
        public bool BeginRoomLobby()
        {
            Check();if(State!=R24LanLobbyState.Hosting)return false;if(SelectedMap==null||SelectedMap.Home)return true;
            var effective=(roundRules??warmupRules??FallbackRoomRules(MapId,sessionTeams)).WithMap(MapId);
            ClosePendingPeers();
            var rules=new SessionRules(MapId,effective.Mode,MapManifestHash,effective.Teams,effective.MaxPlayers,false,effective.Locked,effective.LateJoin,effective.Spectators,effective.RespawnDelay);var result=sessions.Lifecycle(LifecycleContext(),LifecycleAction.BeginLoading,rules,null,false);if(!result.Accepted)return false;
            unreadyMapDepartures=0;MapGuestNotice=null;Epoch=sessions.Epoch;roundRules=null;warmupRules=effective;roundSeed=0;roundNumber=0;roundStartsAt=0;roundResults=false;finalResults=null;roomLobby=true;RebindSession();PublishMapState(false,true);SettleRoomLobby();PublishRoster();return true;
        }
        // Nobody loads a world for the room lobby, so the host acknowledges every member itself:
        // the Session leaves Loading at once and returns to its Lobby stage, where it admits joiners.
        void SettleRoomLobby()
        {
            if(sessions.Stage!=SessionStage.Loading||!ApplyMapLoaded("local-host",OwnActor,MapManifestHash))return;
            foreach(var item in new List<KeyValuePair<string,Peer>>(peers))if(item.Value.Admitted&&!ApplyMapLoaded(item.Key,item.Value.Actor,MapManifestHash))return;
            if(sessions.Stage!=SessionStage.Loading||!sessions.Lifecycle(LifecycleContext(),LifecycleAction.WorldReady,null,MapManifestHash,false).Accepted)return;
            sessions.Lifecycle(LifecycleContext(),LifecycleAction.Activate,null,null,false);
        }
        // An unadmitted hello carries its offer's epoch; a new epoch strands it.
        void ClosePendingPeers()
        {var pending=new List<string>();foreach(var pair in peers)if(!pair.Value.Admitted)pending.Add(pair.Key);foreach(string id in pending){closePeer(id);TransportDisconnectedIfRetained(id);}}
        // Session.RecoverLoading advances the real epoch inside a late Loaded,
        // WorldReady or Activate as well as on the heartbeat. Adopt it at once:
        // otherwise every later lifecycle/departure uses retired epoch bindings.
        bool SyncRecoveredEpoch()
        {
            if(State!=R24LanLobbyState.Hosting||sessions==null||sessions.Epoch==Epoch)return false;
            Epoch=sessions.Epoch;ClosePendingPeers();RebindSession();PublishMapState(false,true);PublishRoster();return true;
        }
        string MapRosterStatus(string fallback){if(SelectedMap==null||SelectedMap.Home)return fallback;if(SelectedMap.Cancelled)return "Shared map loading timed out";if(SelectedMap.Results)return "LAN MATCH RESULTS";if(SelectedMap.Active){if(SelectedMap.Rules!=null)return RoundCountdown>0?"LAN ROUND STARTS IN "+Math.Ceiling(RoundCountdown):"LAN ROUND "+SelectedMap.Round+" / "+SelectedMap.Rules.Rounds;return "Shared map open: "+MapId;}int loaded=0;foreach(var m in members)if(m.WorldLoaded)loaded++;return "Loading shared map: "+loaded+" / "+members.Length+" ready";}
        public bool ReportMapLoaded(string manifestHash)
        {
            Check();if(SelectedMap==null||SelectedMap.Cancelled||manifestHash!=SelectedMap.ManifestHash)return false;
            if(State==R24LanLobbyState.Hosting){if(!ApplyMapLoaded("local-host",OwnActor,manifestHash))return false;if(!hostWorldReady){var r=sessions.Lifecycle(LifecycleContext(),LifecycleAction.WorldReady,null,manifestHash,false);if(!r.Accepted){SyncRecoveredEpoch();return false;}hostWorldReady=true;}return true;}
            if(State!=R24LanLobbyState.Connected)return false;pendingMapLoaded=Packet(PacketKind.Command,MapLoadedFeature,OwnActor,Utf8.GetBytes(manifestHash),1);return true;
        }
        bool ApplyMapLoaded(string connection,string actor,string hash)
        {
            var member=sessions.Get(actor);if(member==null)return false;if(member.Loaded)return true;
            long hostTick=checked(++tick);var frame=new Packet{Kind=PacketKind.Command,FeatureId="sessions.loaded/1",ActorId=actor,SessionId=SessionId,ContentHash=content,Epoch=Epoch,Sequence=1,BaseSequence=-1,Tick=hostTick,CommandId="lan-map-loaded/"+Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture),Payload=Utf8.GetBytes(hash)};
            var result=sessions.Receive(connection,Wire.Encode(frame),hostTick);if(!result.Accepted){SyncRecoveredEpoch();return false;}RuleResult applied;bool dispatched=sessions.DispatchOne(out applied);if(SyncRecoveredEpoch()||!dispatched||!applied.Accepted)return false;PublishRoster();return true;
        }
        void ReadMapLoaded(string connection,Packet packet)
        {if(SelectedMap==null||SelectedMap.Cancelled||packet.Payload.Length!=64)return;string hash;try{hash=Utf8.GetString(packet.Payload);}catch(DecoderFallbackException){RejectedFrames++;return;}if(hash!=MapManifestHash||!ApplyMapLoaded(connection,packet.ActorId,hash))RejectedFrames++;}
        void PublishMapState(bool active,bool cancelled)
        {
            if(active&&roundRules!=null&&roundStartsAt==0)roundStartsAt=clock.Now+3;
            bool home=roomLobby&&cancelled&&roundRules==null;
            if(active&&roundRules!=null&&!roundResults)ClearRetainedScoreboard();
            else if(retainedScoreboard!=null)scoreDirty=true;
            SelectedMap=new R24LanMapSelection(MapId,MapManifestHash,Epoch,++mapRevision,active,cancelled,roundRules,roundSeed,roundNumber,roundStartsAt,roundResults,warmupRules,home);
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){w.Write((byte)3);Write(w,MapId,128);Write(w,MapManifestHash,64);w.Write(active);w.Write(cancelled);w.Write(clock.Now);w.Write((byte)(home?2:roundRules!=null?1:0));R24LanRoundCodec.Write(w,roundRules??warmupRules);if(roundRules!=null){w.Write(roundSeed);w.Write(roundNumber);w.Write(roundStartsAt);w.Write(roundResults);}mapFrame=Packet(PacketKind.Snapshot,MapStateFeature,OwnActor,s.ToArray(),mapRevision);}
            foreach(var p in peers.Values)if(p.Admitted)p.MapState=mapFrame;Dirty(home?"Room lobby":cancelled?"Shared map loading timed out":active?"Shared map open: "+MapId:"Loading shared map: "+MapId);
        }
        void ReadMapState(Packet packet)
        {
            if(packet.Epoch<Epoch||packet.Epoch==Epoch&&packet.Sequence<=mapRevision)return;
            string map,hash;bool active,cancelled,results=false,home=false;double hostTime,starts=0;R24PlayableMatchRules rules=null,warmup=null;int seed=0,round=0;
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){if(r.ReadByte()!=3)throw new InvalidDataException();map=Read(r,128);hash=Read(r,64);byte a=r.ReadByte(),c=r.ReadByte();active=a==1;cancelled=c==1;hostTime=r.ReadDouble();byte match=r.ReadByte();if(match>2)throw new InvalidDataException();var source=R24LanRoundCodec.Read(r,map);if(source.MaxPlayers>Capacity)throw new InvalidDataException();if(match!=1)warmup=source;home=match==2;if(match==1){rules=source;seed=r.ReadInt32();round=r.ReadInt32();starts=r.ReadDouble();byte done=r.ReadByte();results=done==1;if(seed<=0||round<1||round>rules.Rounds||!Finite(starts)||starts<0||active&&starts<=0||done>1)throw new InvalidDataException();}if(a>1||c>1||!Wire.IsHash(hash)||!Finite(hostTime)||hostTime<0||s.Position!=s.Length||active&&cancelled||home&&!cancelled||packet.Sequence<1)throw new InvalidDataException();}}
            catch(InvalidDataException){Reject(serverPeer);return;}catch(IOException){Reject(serverPeer);return;}catch(ArgumentException){Reject(serverPeer);return;}
            if(packet.Epoch>Epoch){finalResults=null;Epoch=packet.Epoch;ClearHealth();ClearSpawns();ClearRawTracking();poses.Clear();localPresence=null;lastLocalSent=0;pendingMapLoaded=null;incoming=null;incomingRevision=committedRevision=-1;}
            if(active&&rules!=null&&!results)ClearRetainedScoreboard();
            hostClock.Observe(hostTime,clock.Now);mapRevision=packet.Sequence;MapId=map;MapManifestHash=hash;SelectedMap=new R24LanMapSelection(map,hash,Epoch,mapRevision,active,cancelled,rules,seed,round,starts,results,warmup,home);Dirty(home?"Room lobby":cancelled?"Shared map loading timed out":active?"Shared map open: "+map:"Loading shared map: "+map);
        }
        void NoteUnreadyMapDeparture(string name)
        {
            if(!nativeMapLoading||SelectedMap==null||SelectedMap.Cancelled||sessions.Stage!=SessionStage.Loading)return;
            unreadyMapDepartures++;
            MapGuestNotice=unreadyMapDepartures==1?name+" left before loading the shared map":unreadyMapDepartures+" guests left before loading the shared map (last: "+name+")";
        }
        void DropUnreadyMapGuests()
        {
            int count=0;
            foreach(var entry in peers)
            {
                if(!entry.Value.Admitted)continue;
                var member=sessions.Get(entry.Value.Actor);
                if(member!=null&&!member.Loaded)expired[count++]=entry.Key;
            }
            for(int i=0;i<count;i++){string id=expired[i];expired[i]=null;closePeer(id);TransportDisconnectedIfRetained(id);}
        }
        void TickMaps()
        {
            if(State==R24LanLobbyState.Connected){if(pendingMapLoaded!=null&&Send(serverPeer,pendingMapLoaded))pendingMapLoaded=null;return;}
            if(State==R24LanLobbyState.Hosting&&SelectedMap!=null&&SelectedMap.Home&&sessions.Stage==SessionStage.Loading){SettleRoomLobby();return;}
            if(State!=R24LanLobbyState.Hosting||SelectedMap==null||SelectedMap.Cancelled||sessions.Stage!=SessionStage.Loading)return;
            if(clock.Now>=nextLoadingHeartbeat){nextLoadingHeartbeat=clock.Now+.25;var result=sessions.Lifecycle(LifecycleContext(),LifecycleAction.AdvanceTime,null,null,false);if(!result.Accepted)throw new InvalidOperationException("Shared map heartbeat: "+result.Reason);if(SyncRecoveredEpoch())return;}
            if(!hostWorldReady)return;
            if(nativeMapLoading&&clock.Now>=sharedMapReadyAt)DropUnreadyMapGuests();
            foreach(var member in sessions.CaptureMembers())if(!member.Loaded)return;
            var activated=sessions.Lifecycle(LifecycleContext(),LifecycleAction.Activate,null,null,false);if(!activated.Accepted){if(SyncRecoveredEpoch())return;throw new InvalidOperationException("Shared map activation: "+activated.Reason);}PublishMapState(true,false);PublishRoster();
        }
    }
}
