using System;using System.IO;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Sessions;using Ashfall.Modules.Networking;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    sealed class HealthSpawnWorld:IR24LanSpawnWorld
    {
        internal int Picks,Queries;internal bool Clear=true;
        public bool ReadyForSpawn(string map,bool warmup){return map=="range";}
        public SpawnSite[] Sites(string map,bool warmup){Picks++;return new[]{new SpawnSite("source-rng-selected",new Float3(2+Picks*5,.12f,4),-1,false,false)};}
        public bool IsSafe(string map,string actor,Float3 feet){Queries++;return Clear&&map=="range";}
    }
    static HealthSpawnWorld StartHealthRound(Pair pair,bool revive=false)
    {
        for(int i=0;i<5;i++)pair.Tick();var world=new HealthSpawnWorld();pair.Host.BindSpawnWorld(world);
        R24LanHealthState missing;Need(!pair.Host.TryReadHealth(pair.Host.OwnActor,out missing)&&!pair.Client.TryReadHealth(pair.Client.OwnActor,out missing),"No Health presentation grant from bare lobby membership");
        var rules=R24PlayableMatchRules.ForPreset(revive?"team_revive":"free_for_all","range").With("starting_health",80).With("starting_shield",25).With("protection",2f).With("respawn_delay",1f).With("downed_seconds",10f);
        string hash=new string('b',64);Need(pair.Host.BeginRoundSelection(rules,hash,123)&&pair.Host.ReportMapLoaded(hash),"Health integration uses actual shared round selection and host load");for(int i=0;i<3;i++)pair.Tick();Need(pair.Client.ReportMapLoaded(hash),"Health integration uses actual guest load acknowledgment");for(int i=0;i<4;i++)pair.Tick();
        pair.Now=pair.Host.SelectedMap.StartsAt+.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Need(pair.Host.RequestSpawn()&&pair.Client.RequestSpawn(),"Health registration follows real host and guest Session spawn requests");for(int i=0;i<7;i++)pair.Tick();return world;
    }
    static R24LanHealthState Health(R24LanLobby lobby,string actor)
    {R24LanHealthState value;Need(lobby.TryReadHealth(actor,out value),"Actual current epoch/revision-bound Health receipt available");return value;}
    static void HostHealthCases()
    {
        using(var pair=new Pair()){
            var world=StartHealthRound(pair);string host=pair.Host.OwnActor,guest=pair.Client.OwnActor;var h=Health(pair.Host,host);var g=Health(pair.Client,guest);var m=pair.Host.ReadAuthorityMember(guest);
            Need(h.Health==80&&h.Shield==25&&h.Life==LifeState.Alive&&g.Health==80&&g.Shield==25&&g.SpawnRevision==1&&g.CanonicalActor==m.CanonicalActorId&&m.Life==SessionLife.Alive,"Configured host producer and authenticated guest snapshot match actual canonical placement");
            Need(pair.Host.OwnsHealth(h)&&pair.Client.OwnsHealth(g)&&!pair.Host.OwnsHealth(g),"Health receipts are exact current owner capabilities, not cross-lobby values");
            Need(!pair.Client.ApplyHostHealthLoss(guest,g.Revision,1,0,false).Accepted,"Client lobby cannot invoke trusted host Health mutations");
            Need(!pair.Host.ApplyHostHealthLoss(host,h.Revision,1,0,false).Accepted&&Health(pair.Host,host).Revision==h.Revision,"Actual source protection blocks early damage without a revision change");
            Need(!pair.Host.ApplyHostHealthLoss("unplaced",1,1,0,false).Accepted,"Unplaced identity cannot receive a Health mutation");
            double protection=h.ProtectionUntil;pair.Now=protection+.01;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);
            Need(pair.Host.ApplyHostHealthLoss(host,h.Revision,10,5,false).Accepted,"Trusted host adapter reaches actual configured producer after protection");for(int i=0;i<3;i++)pair.Tick();var hurt=Health(pair.Host,host);var remote=Health(pair.Client,host);
            Need(hurt.Health==70&&hurt.Shield==20&&remote.Health==70&&remote.Shield==20&&hurt.Revision==remote.Revision&&!pair.Host.OwnsHealth(h),"Committed damage invalidates old receipt and reliably publishes canonical revision");
            Need(!pair.Host.ApplyHostHealthLoss(host,h.Revision,1,0,false).Accepted&&Health(pair.Host,host).Health==70,"Stale evaluated damage cannot mutate a newer canonical revision");
            Need(!pair.Host.ApplyHostHealthLoss(host,hurt.Revision,float.NaN,0,false).Accepted&&Health(pair.Host,host).Revision==hurt.Revision,"Invalid loss retains actual producer state");
            int root=hurt.SpawnRevision;Need(pair.Host.RequestSpawn(),"Alive actor can re-request existing actual placement");for(int i=0;i<3;i++)pair.Tick();hurt=Health(pair.Host,host);
            Need(hurt.Health==70&&hurt.Shield==20&&hurt.ProtectionUntil==protection&&hurt.SpawnRevision==root,"Repeated placement cannot heal, refresh protection, or increment root revision");
            Need(pair.Host.ApplyHostHealthLoss(host,hurt.Revision,70,20,true).Accepted,"Real committed lethal Health transition accepted");var dead=Health(pair.Host,host);var eliminated=pair.Host.ReadAuthorityMember(host);
            Need(dead.Life==LifeState.Dead&&dead.Deaths==1&&eliminated.Life==SessionLife.Eliminated&&eliminated.Deaths==1&&eliminated.SpawnRevision==root,"Host Session projects actual producer death and preserves current source root");
            Need(!pair.Host.RequestSpawn()&&pair.Host.ReadAuthorityMember(host).Life==SessionLife.Eliminated,"Direct spawn request cannot bypass actual Health-owned death/respawn");
            pair.Now=dead.RespawnAt-.001;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Need(Health(pair.Host,host).Life==LifeState.Dead&&pair.Host.ReadAuthorityMember(host).SpawnRevision==root,"Both actual owners remain dead before the configured deadline");
            world.Clear=false;pair.Now=dead.RespawnAt;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Need(Health(pair.Host,host).Life==LifeState.Dead&&pair.Host.ReadAuthorityMember(host).Life==SessionLife.Eliminated,"Due respawn cannot grant life when actual source safety refuses");
            world.Clear=true;int picks=world.Picks;pair.Tick();var alive=Health(pair.Host,host);m=pair.Host.ReadAuthorityMember(host);
            Need(world.Picks==picks+1&&alive.Life==LifeState.Alive&&alive.Health==80&&alive.Shield==25&&alive.Deaths==1&&alive.LifeGeneration==2&&alive.SpawnRevision==root+1&&m.Life==SessionLife.Alive&&m.Deaths==1&&m.SpawnRevision==alive.SpawnRevision,"One scoped source pick jointly commits actual configured Health and Session respawn");
            Need(Math.Abs(alive.ProtectionUntil-(pair.Now+2))<.000001&&Health(pair.Host,guest).LifeGeneration==1,"Respawn protection uses actual host time without granting a new life to another actor");
            for(int i=0;i<5;i++)pair.Tick();remote=Health(pair.Client,host);Need(remote.Revision==alive.Revision&&remote.SpawnRevision==alive.SpawnRevision&&remote.Health==80,"New root and Health receipt converge over reliable simulated transport");
            long epoch=pair.Host.Epoch;Need(pair.Host.BeginMapSelection("range",new string('b',64)),"Actual map transition starts new host epoch");R24LanHealthState absent;
            Need(pair.Host.Epoch==epoch+1&&!pair.Host.TryReadHealth(host,out absent)&&!pair.Host.OwnsHealth(alive),"Map transition retires old producer presentation and old receipt capability");for(int i=0;i<3;i++)pair.Tick();Need(!pair.Client.TryReadHealth(host,out absent)&&!pair.Client.OwnsHealth(remote),"Guest invalidates old map Health receipts before new placement");
        }
        using(var pair=new Pair()){
            StartHealthRound(pair,true);string actor=pair.Client.OwnActor;var initial=Health(pair.Host,actor);pair.Now=initial.ProtectionUntil+.01;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);
            Need(pair.Host.ApplyHostHealthLoss(actor,initial.Revision,80,25,true).Accepted,"Team-revive host producer accepts declared lethal revivable loss");var downed=Health(pair.Host,actor);
            Need(downed.Life==LifeState.Downed&&pair.Host.ReadAuthorityMember(actor).Life==SessionLife.Eliminated&&downed.DownedUntil>pair.Now,"Actual Downed state projects without fabricating a healthy Session actor");
            for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.SelectedMap.Results&&pair.Client.SelectedMap.Results,"A 1v1 Team Revive down ends the round on both peers");
            Need(Health(pair.Host,actor).Revision==downed.Revision&&Health(pair.Client,actor).Life==LifeState.Downed&&!pair.Host.ApplyHostHealthLoss(actor,downed.Revision,1,0,false).Accepted,"Celebration preserves final Health receipts and refuses further live damage");
            pair.ClientPipe.Close("server");R24LanHealthState absent;Need(pair.Host.ReadAuthorityMember(actor)==null&&!pair.Host.TryReadHealth(actor,out absent),"Actual Session departure retires host Health receipt and queued output");
        }
        using(var pair=new Pair()){
            StartHealthRound(pair);string actor=pair.Client.OwnActor;var packet=new Packet{Kind=PacketKind.Snapshot,FeatureId="playable.health.state/1",ActorId=actor,SessionId=pair.Client.SessionId,ContentHash=new string('a',64),Epoch=pair.Client.Epoch,Sequence=999,BaseSequence=-1,Payload=new byte[0]};
            pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(packet)});pair.Tick();Need(pair.Host.Members.Length==1&&pair.Host.ReadAuthorityMember(actor)==null,"A client Health-state packet is rejected before any host life mutation");
        }
        using(var pair=new Pair()){
            StartHealthRound(pair);string actor=pair.Host.OwnActor;var h=Health(pair.Host,actor);pair.Now=h.ProtectionUntil+.01;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);
            Need(pair.Host.ApplyHostHealthLoss(actor,h.Revision,1,0,false).Accepted,"Wire-order case uses committed producer damage");pair.Now+=.01;pair.Host.Tick(pair.Now);byte[] delayed=null;
            foreach(var frame in pair.ClientPipe.Inbox){Packet packet;if(Wire.TryDecode(frame.Bytes,out packet)&&packet.FeatureId=="playable.health.state/1")delayed=(byte[])frame.Bytes.Clone();}
            Need(delayed!=null,"Captured real encoded host Health snapshot, not a fixture-created healthy state");pair.Client.Tick(pair.Now);h=Health(pair.Host,actor);
            pair.HostPipe.Pressure=1;
            for(int i=0;i<3;i++){Need(pair.Host.ApplyHostHealthLoss(actor,h.Revision,1,0,false).Accepted,"Producer advances through ordinary host losses under send pressure");h=Health(pair.Host,actor);}
            pair.Tick();for(int i=0;i<3;i++)pair.Tick();var latest=Health(pair.Client,actor);Need(latest.Health==h.Health&&latest.Revision==h.Revision,"Bounded pending reliable state coalesces to the actual latest producer snapshot after send pressure");
            pair.ClientPipe.Inbox.Enqueue(new Frame{Peer="server",Bytes=delayed});pair.Now+=.01;pair.Client.Tick(pair.Now);Need(Health(pair.Client,actor).Revision==latest.Revision&&Health(pair.Client,actor).Health==latest.Health,"Delayed older Health revision cannot roll back displayed canonical state");
            Packet invalid;Need(Wire.TryDecode(delayed,out invalid),"Actual captured snapshot decodes for malformed-input control");invalid.Sequence+=100;
            using(var stream=new MemoryStream(invalid.Payload,true))using(var reader=new BinaryReader(stream)){
                reader.ReadByte();for(int i=0;i<3;i++)reader.ReadBytes(reader.ReadUInt16());reader.ReadInt32();reader.ReadInt32();reader.ReadInt32();reader.ReadInt64();reader.ReadInt64();reader.ReadInt64();reader.ReadByte();long hp=stream.Position;using(var writer=new BinaryWriter(stream)){stream.Position=hp;writer.Write(float.NaN);}}
            pair.ClientPipe.Inbox.Enqueue(new Frame{Peer="server",Bytes=Wire.Encode(invalid)});pair.Now+=.01;pair.Client.Tick(pair.Now);R24LanHealthState absent;Need(pair.Client.State==R24LanLobbyState.Offline&&!pair.Client.TryReadHealth(actor,out absent)&&!pair.Client.OwnsHealth(latest),"Malformed host snapshot fails closed and clears obsolete Health presentation capabilities");
        }
    }
}
