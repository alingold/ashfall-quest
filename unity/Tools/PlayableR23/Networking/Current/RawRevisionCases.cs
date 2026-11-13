using System;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;using Ashfall.Integration.Playable.Networking;
partial class LobbyCases
{
    sealed class RevisionWorld:IR24LanSpawnWorld
    {
        internal bool Shift;public bool ReadyForSpawn(string map,bool warmup){return map=="range";}
        public SpawnSite[] Sites(string map,bool warmup){float x=Shift?100:0;return new[]{new SpawnSite("a",new Float3(x,.12f,0),-1,false,false),new SpawnSite("b",new Float3(x+8,.12f,0),-1,false,false)};}
        public bool IsSafe(string map,string actor,Float3 position){return map=="range"&&(!Shift||position.X>=100);}
    }
    static R24LanRawTracking RevRaw(string hash,int revision,float x=0)
    {var p=Raw(hash,x);return new R24LanRawTracking(hash,p.Flags,p.Root,p.Head,p.Left,p.Right,p.LeftGrip,p.RightGrip,revision);}
    static void RawRevisionCases()
    {
        string hash=new string('c',64);R24LanRawTracking decoded;
        var packet=RevRaw(hash,7,4).Encode();Need(packet.Length==190&&R24LanRawTracking.TryDecode(packet,out decoded)&&decoded.SpawnRevision==7,"Revision is explicit in schema2 controller codec");
        packet[0]=1;Need(!R24LanRawTracking.TryDecode(packet,out decoded),"Prior tracking schema cannot enter revised stream");
        var history=new R24LanRawTrackingHistory();Need(history.Accept(1,0,RevRaw(hash,1),false,1,null)&&!history.Accept(2,.05,RevRaw(hash,2),false,1,null)&&history.Rejection=="spawn-revision"&&history.Latest.Pose.SpawnRevision==1,"History cannot silently cross a revision boundary");
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();var world=new RevisionWorld();pair.Host.BindSpawnWorld(world);SharedWarmup(pair,hash);
            Need(!pair.Client.PublishRawTracking(RevRaw(hash,1)),"Loaded map without actual Session placement grants no raw stream");
            Need(pair.Host.RequestSpawn()&&pair.Client.RequestSpawn(),"Actual Session placement requested for both principals");for(int i=0;i<5;i++)pair.Tick();
            string actor=pair.Client.OwnActor;R24LanSpawn spawn;Need(pair.Client.TryReadSpawn(actor,out spawn)&&spawn.Revision==1&&pair.Host.ReadAuthorityMember(actor).Life==SessionLife.Alive,"Revision originates from genuine host Session output");
            Need(!pair.Client.PublishRawTracking(RevRaw(hash,0))&&!pair.Client.PublishRawTracking(RevRaw(hash,2)),"Local old and future revisions refused before queue");
            Need(pair.Client.PublishRawTracking(RevRaw(hash,1)),"Matching actual placement permits body evidence");for(int i=0;i<3;i++)pair.Tick();R24LanRawTrackingSample sample;
            Need(pair.Host.TryReadRawTracking(actor,pair.Now,out sample)&&sample.Pose.SpawnRevision==1,"Host stores current revision only");
            RawFrame(pair,RawPacket(pair,actor,1000,RevRaw(hash,2)));Need(pair.Host.LastRawTrackingRejection=="spawn-revision"&&pair.Host.TryReadRawTracking(actor,pair.Now,out sample)&&sample.Pose.SpawnRevision==1,"Authenticated future revision cannot replace current history");
            world.Shift=true;Need(pair.Client.RequestSpawn(),"Actual safety relocation uses existing Session command");for(int i=0;i<5;i++)pair.Tick();
            Need(pair.Client.TryReadSpawn(actor,out spawn)&&spawn.Revision==2&&spawn.Feet.X>=100&&pair.Host.ReadAuthorityMember(actor).SpawnRevision==2,"Real Session safety relocation increments authoritative revision");
            Need(!pair.Host.TryReadRawTracking(actor,pair.Now,out sample),"New committed placement retires all previous pose evidence");
            RawFrame(pair,RawPacket(pair,actor,1001,RevRaw(hash,1)));Need(pair.Host.LastRawTrackingRejection=="spawn-revision"&&!pair.Host.TryReadRawTracking(actor,pair.Now,out sample),"Delayed old-revision packet cannot recreate cleared history");
            Need(pair.Client.PublishRawTracking(RevRaw(hash,2,spawn.Feet.X)),"Current relocated stream can resume");for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.TryReadRawTracking(actor,pair.Now,out sample)&&sample.Pose.SpawnRevision==2&&sample.Pose.Root.Position.X>=100,"New history starts only after genuine committed revision");
        }
    }
}
