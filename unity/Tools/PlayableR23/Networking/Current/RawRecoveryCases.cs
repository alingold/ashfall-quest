using System;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    static void RecoveryFrame(Pair pair,double at,long sequence,R24LanRawTracking pose,byte[] payload=null)
    {pair.Now=at;var packet=RawPacket(pair,pair.Client.OwnActor,sequence,pose);if(payload!=null)packet.Payload=payload;pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(packet)});pair.Host.Tick(at);pair.Client.Tick(at);}
    static void RawRecoveryCases()
    {
        string hash=new string('c',64);
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindSpawnWorld(new RevisionWorld());bool clear=true;int queries=0;pair.Host.BindRawTrackingWorldQuery(delegate(Float3 a,Float3 b){queries++;return clear;});
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range");pair.Host.BeginRoundSelection(rules,hash,123);pair.Host.ReportMapLoaded(hash);for(int i=0;i<3;i++)pair.Tick();pair.Client.ReportMapLoaded(hash);for(int i=0;i<4;i++)pair.Tick();pair.Now=pair.Host.SelectedMap.StartsAt+.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);RealRawSpawns(pair);
            string actor=pair.Client.OwnActor;var before=pair.Host.ReadAuthorityMember(actor);double start=pair.Now+.05;
            RecoveryFrame(pair,start,1,RevRaw(hash,1,1));R24LanRawTrackingSample sample;Need(pair.Host.TryReadRawTracking(actor,pair.Now,out sample)&&sample.Pose.Root.Position.X==1,"Genuine current-revision raw pose is recovery anchor");
            double first=start+.05;RecoveryFrame(pair,first,2,RevRaw(hash,1,100));Need(pair.Host.LastRawTrackingRejection=="speed"&&pair.Host.CorrectedRawTracking==0,"First speed fault starts source timer without immediate relocation");
            RecoveryFrame(pair,first+.2499,3,RevRaw(hash,1,100));Need(pair.Host.CorrectedRawTracking==0&&pair.Host.ReadAuthorityMember(actor).SpawnRevision==1,"Quarter-second source timer does not fire early");
            RecoveryFrame(pair,first+.2501,4,RevRaw(hash,1,100));var after=pair.Host.ReadAuthorityMember(actor);
            Need(pair.Host.CorrectedRawTracking==1&&after.SpawnRevision==2&&after.Position.X==1,"Sustained invalid motion relocates real Session to last accepted root");
            Need(after.Life==before.Life&&after.Deaths==before.Deaths&&after.Team==before.Team&&after.Loaded==before.Loaded&&after.CanonicalActorId==before.CanonicalActorId,"Safety correction preserves actual Session life, counters, team and identity");
            Need(!pair.Host.TryReadRawTracking(actor,pair.Now,out sample),"Correction clears prior reference history before new revision evidence");
            R24LanSpawn placement;Need(pair.Client.TryReadSpawn(actor,out placement)&&placement.Revision==2&&placement.Feet.X==1&&placement.Life==after.Life,"Reliable publication carries actual corrected revision, root and preserved life");
            for(int i=0;i<45;i++){var packet=RawPacket(pair,actor,100+i,RevRaw(hash,1));pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(packet)});}pair.Now+=.001;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);
            Need(pair.Host.LastRawTrackingRejection=="rate"&&pair.Host.CorrectedRawTracking==1&&queries==0,"Correction cannot reset authenticated45-arrival budget or trigger a world query flood");
            RecoveryFrame(pair,start+1,200,RevRaw(hash,2,1));Need(pair.Host.LastRawTrackingRejection=="rate"&&!pair.Host.TryReadRawTracking(actor,pair.Now,out sample),"Arrival budget retains strict source greater-than-one-second boundary");
            RecoveryFrame(pair,start+1.00001,201,RevRaw(hash,2,1));Need(pair.Host.TryReadRawTracking(actor,pair.Now,out sample)&&sample.Pose.SpawnRevision==2,"New valid current stream resumes after actual rate window");
            clear=false;double fault=pair.Now+.05;RecoveryFrame(pair,fault,202,RevRaw(hash,2,1.2f));Need(pair.Host.LastRawTrackingRejection=="world-sweep"&&queries==1,"Actual composed clear oracle rejects changed head pose");
            RecoveryFrame(pair,fault+.1,203,RevRaw(hash,2,1));Need(pair.Host.TryReadRawTracking(actor,pair.Now,out sample),"Unchanged valid body accepts and clears pending fault timer");
            RecoveryFrame(pair,fault+.3,204,RevRaw(hash,2,1.2f));Need(pair.Host.CorrectedRawTracking==1,"Accepted intervening pose prevents an old timer from relocating the player");
            clear=true;RecoveryFrame(pair,fault+.31,205,RevRaw(hash,2,1));
            byte[] bad=RevRaw(hash,2,1).Encode();Array.Copy(BitConverter.GetBytes(Single.NaN),0,bad,98,4);R24LanRawTracking decoded;Need(!R24LanRawTracking.TryDecode(bad,out decoded),"Invalid body remains refused by public valid-body decoder");
            double body=fault+.4;RecoveryFrame(pair,body,206,RevRaw(hash,2),bad);Need(pair.Host.LastRawTrackingRejection=="body"&&pair.Host.CorrectedRawTracking==1,"Structurally valid authenticated bad body starts source recovery without becoming evidence");
            RecoveryFrame(pair,body+.2501,207,RevRaw(hash,2),bad);Need(pair.Host.CorrectedRawTracking==2&&pair.Host.ReadAuthorityMember(actor).SpawnRevision==3&&pair.Host.ReadAuthorityMember(actor).Position.X==1,"Sustained invalid body uses same trusted preserving correction");
        }
        RelocationOwnerCases();
    }
    sealed class RelocationPorts:IAuthorityGate,ISessionManagement,ISessionClock,IReconnectTokens,ISpawnWorld
    {
        internal double Clock=1;public double Now{get{return Clock;}}public string Issue(){return Guid.NewGuid().ToString("N");}public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("test authority");}public bool CanManage(CommandContext c){return c.ActorId=="host";}
        public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("source",new Float3(0,.12f,0),-1,false,false)};}public bool IsSafe(string map,string actor,Float3 feet){return true;}
    }
    static void RelocationOwnerCases()
    {
        var ports=new RelocationPorts();string hash=new string('c',64),content=new string('a',64);var rules=new SessionRules("range","respawn",hash,0,8,false,false,true,false,5);var session=new Session("s",content,rules,ports,ports,ports,ports,ports);long tick=0;
        Func<string,CommandContext> context=delegate(string actor){long id=++tick;return new CommandContext("s",actor,"control/"+id,id,session.Epoch,true);};string token;
        Need(session.Join(context("guest"),new JoinRequest(Protocol.Name,Protocol.Schema,content,"Guest",null,false),out token).Accepted,"Declared authority admits actual Session member");
        Need(session.BeginLoading(context("host"),rules,true).Accepted&&session.Loaded(context("guest"),hash).Accepted&&session.WorldReady(context("host"),hash).Accepted&&session.Activate(context("host")).Accepted&&session.Spawn(context("guest")).Accepted,"Actual Session loading/readiness/spawn lifecycle establishes position");
        Need(session.Eliminate(context("guest")).Accepted,"Actual authoritative Session death establishes eliminated state");var before=session.Get("guest");object identity=session.GetConnectionIdentity("guest");
        ports.Clock=3;Need(session.RelocateRoot(context("host"),"guest",before.SpawnRevision,new Float3(4,.12f,5)).Accepted,"Trusted host safety correction accepts eliminated player's real placed root");var after=session.Get("guest");
        Need(after.Life==SessionLife.Eliminated&&after.Deaths==1&&after.Deaths==before.Deaths&&after.SpawnRevision==before.SpawnRevision+1&&after.Position.X==4&&ReferenceEquals(identity,session.GetConnectionIdentity("guest")),"Relocation cannot resurrect eliminated member or replace connection");
        Need(!session.RelocateRoot(context("guest"),"guest",after.SpawnRevision,new Float3()).Accepted&&session.Get("guest").SpawnRevision==after.SpawnRevision,"Member context is not host management authority");
        Need(!session.RelocateRoot(context("host"),"guest",before.SpawnRevision,new Float3()).Accepted&&session.Get("guest").Position.X==4,"Stale expected revision cannot overwrite newer correction");
        Need(!session.RelocateRoot(context("host"),"guest",after.SpawnRevision,new Float3(Single.NaN,0,0)).Accepted&&session.Get("guest").Position.X==4,"Invalid target cannot mutate real member state");
        ports.Clock=5.99;Need(!session.Spawn(context("guest")).Accepted&&session.Get("guest").Life==SessionLife.Eliminated,"Correction cannot shorten the actual death respawn timer");
        ports.Clock=6;Need(session.Spawn(context("guest")).Accepted&&session.Get("guest").Life==SessionLife.Alive&&session.Get("guest").Deaths==1,"Correction cannot restart or extend original death timer; normal due spawn still works");
    }
}
