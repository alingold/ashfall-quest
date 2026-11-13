using System;
using System.Collections.Generic;
using System.Reflection;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Modules.Networking;
using Ashfall.Integration.Playable.Activities;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    sealed class TierGate:IAuthorityGate {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
    static CommandContext TierContext(int n,string actor="builder") {return new CommandContext("tiers",actor,"tier/"+n,n,1,true);}
    static BuilderState TierPose(int tier,bool expanded=true,float x=1.5f)
    {return new BuilderState(new Float3(x,1.2f,1.5f),new Float3(0,-1,0),new Float3(x+5,1.6f,1.5f),true,true,tier,expanded);}
    static CandidateService TierService(bool infinite=false){return new CandidateService("tiers",1,new TierGate(),new BuildWorld(),infinite);}
    static void BuildTierPolicy()
    {
        foreach(string kind in new[]{"wall","floor"})for(int tier=0;tier<3;tier++){
            var p=new Panel(kind,new Float3(),kind=="floor"?1:2,false,0,tier);
            Need(p.Cost==tier+1&&p.MaximumHealth==(kind=="wall"?180:140)*(tier==0?1:tier==1?1.75f:2.75f),"Exact tier cost and health table");
            Need(p.Slot==new Panel(kind,p.Center,p.Axis,false,0).Slot,"Tiers share face slot");
            Need(Geometry.HalfSize(p).Equals(CandidateGeometry.HalfSize(p))&&Geometry.Contains(p,p.Center,0)==CandidateGeometry.Contains(p,p.Center,0),"Both geometry implementations agree");
        }
        var service=TierService();CandidateService.PreparedChange change;
        Need(service.PreparePlace(new ConstructionCommand(TierContext(1),1),TierPose(0),out change).Accepted&&change.Cost==1,"Light placement costs one");change.CommitValidated();var first=service.Snapshot()[0];
        Need(service.PrepareDamage(TierContext(2),first.Id,70,service.Revision,out change).Accepted,"Damage fixture");change.CommitValidated();
        var far=new BuilderState(TierPose(2).Origin,TierPose(2).Direction,new Float3(1000,1,0),true,true,2,true);
        Need(!service.PreparePlace(new ConstructionCommand(TierContext(3),3),far,out change).Accepted,"Upgrade retains head reach check");
        Need(service.PreparePlace(new ConstructionCommand(TierContext(3),3),TierPose(2),out change).Accepted&&change.Cost==2,"Upgrade charges difference");change.CommitValidated();var upgraded=service.Snapshot()[0];
        Need(service.Snapshot().Length==1&&upgraded.Id==first.Id&&upgraded.Shape.Tier==2&&upgraded.Health==192.5f,"Upgrade retains identity and 50 percent health");
        Need(service.PreparePlace(new ConstructionCommand(TierContext(4,"other"),4),TierPose(2),out change).Accepted&&change.Panels.Length==2,"Other owner cannot upgrade existing face; may select next free lattice face");change.Abort();
        Need(!service.PreparePlace(new ConstructionCommand(TierContext(5),5),TierPose(1,false),out change).Accepted,"Rule OFF refuses higher tier");
        Need(!service.PreparePlace(new ConstructionCommand(TierContext(6),6),TierPose(255),out change).Accepted,"Invalid tier refused");
        var local=new R23ConstructionSession(new BuildWorld());PlacedPanel[] plan;int cost;
        Need(local.Evaluate(TierPose(2),1,true,out plan,out cost).Accepted&&cost==3&&local.Balance==996,"Local ledger accepts tier three debit");
        var support=new R23ConstructionSession(new BuildWorld{FloorNeedsSupport=true});
        Need(support.Evaluate(TierPose(2),1,true,out plan,out cost).Accepted&&cost==4&&support.Balance==995,"Armoured floor plus Light automatic support costs four");
        var free=TierService(true);Need(free.PreparePlace(new ConstructionCommand(TierContext(1),1),TierPose(2),out change).Accepted&&change.Cost==0,"Infinite builds costs zero");change.Abort();
    }
    static void BuildOwnerCapacity()
    {
        var service=TierService();var panels=new PlacedPanel[96];
        for(int i=0;i<panels.Length;i++)panels[i]=new PlacedPanel("seed/"+i,"builder",new Panel("floor",new Float3(100+i*4,0,100),1,false,0),140);
        Need(service.ReconcileNetworkPreview(panels),"Seed owner capacity fixture");CandidateService.PreparedChange change;
        var result=service.PreparePlace(new ConstructionCommand(TierContext(1),1),TierPose(0),out change);
        Need(!result.Accepted&&result.Reason=="capacity_player"&&service.Snapshot().Length==96,"96 owner cap rejects without mutation");
        Need(service.PreparePlace(new ConstructionCommand(TierContext(2,"other"),2),TierPose(0),out change).Accepted,"Other owner retains session share");change.Abort();
        Need(service.PreparePlace(new ConstructionCommand(TierContext(3),3),TierPose(0,false),out change).Accepted,"Switch OFF restores legacy session-only capacity");change.Abort();
        // Use the exact host-selected lattice face for the at-cap upgrade.
        var face=CandidateGeometry.Intersections(TierPose(0).Origin,TierPose(0).Direction)[0];panels[0]=new PlacedPanel("upgrade","builder",face,140);service.ReconcileNetworkPreview(panels);
        Need(service.PreparePlace(new ConstructionCommand(TierContext(4),4),TierPose(2),out change).Accepted&&change.Cost==2&&change.Panels.Length==96,"Upgrade allowed at owner capacity");change.Abort();
        var full=new PlacedPanel[256];for(int i=0;i<full.Length;i++)full[i]=new PlacedPanel("full/"+i,"other",new Panel("floor",new Float3(100+i*4,0,100),1,false,0),140);service.ReconcileNetworkPreview(full);
        result=service.PreparePlace(new ConstructionCommand(TierContext(5),5),TierPose(0),out change);Need(!result.Accepted&&result.Reason=="capacity","256 session cap retained");
    }
    static void BuildLegacyRule()
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindConstructionWorld(new BuildWorld());pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){return range;});string hash=new string('b',64);
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("build_pieces",false);StartRound(pair,new HazardWorld(),rules,hash);
            Action raw=delegate{pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0,7,rx:1.5f,ry:1.2f,rz:1.5f));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();};for(int i=0;i<4;i++)raw();
            Need(!pair.Client.SelectedMap.Rules.BuildPieces&&!pair.Host.RequestBuild(1,1)&&pair.Host.SharedPanels.Length==0,"OFF rule replicates and refuses upgraded tier");
            Need(pair.Host.RequestBuild(1),"OFF rule permits legacy Light placement");for(int i=0;i<5;i++)raw();Need(pair.Client.SharedPanels.Length==1&&pair.Client.SharedPanels[0].Shape.Tier==0&&pair.Client.SharedPanels[0].Health==140,"OFF placement retains legacy maximum");
            Need(pair.Host.BeginRoundSelection(rules,hash,321),"Round reset requested");for(int i=0;i<5;i++)pair.Tick();Need(pair.Host.SharedPanels.Length==0&&pair.Client.SharedPanels.Length==0,"Round reset clears panel tiers with panels");
        }
    }
    static void BuildTierWire()
    {
        using(var pair=new Pair()){
            var world=new BuildWorld();for(int i=0;i<5;i++)pair.Tick();pair.Host.BindConstructionWorld(world);pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){return range;});string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);
            Action raw=delegate{pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0,7,rx:1.5f,ry:1.2f,rz:1.5f));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();};for(int i=0;i<4;i++)raw();
            Need(pair.Host.RequestBuild(1,1),"Host Reinforced build");for(int i=0;i<5;i++)raw();var first=pair.Host.SharedPanels[0];
            Need(first.Shape.Tier==1&&first.Health==245&&pair.Client.SharedPanels[0].Health==245&&pair.Client.SharedPanels[0].Shape.Tier==1,"Tier and exact maximum replicated");
            Need(pair.Host.RequestBuild(1,2),"Host upgrades exact face");for(int i=0;i<5;i++)raw();Need(pair.Host.SharedPanels[0].Id==first.Id&&pair.Client.SharedPanels[0].Shape.Tier==2&&pair.Client.SharedPanels[0].Health==385,"Upgrade snapshot reaches guest");
            var service=(CandidateService)typeof(R24LanLobby).GetField("sharedConstruction",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pair.Host);
            var rows=new List<PlacedPanel>();for(int axis=0;axis<3;axis++)for(int tier=0;tier<3;tier++){var shape=new Panel(axis==1?"floor":"wall",new Float3(20+axis*4,10,tier*4),axis,false,1,tier);rows.Add(new PlacedPanel("axis/"+axis+"/"+tier,pair.Host.OwnActor,shape,shape.MaximumHealth*.5f));}
            service.ReconcileNetworkPreview(rows.ToArray());typeof(R24LanLobby).GetMethod("PublishConstruction",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pair.Host,null);for(int i=0;i<6;i++)raw();
            Need(pair.Client.SharedPanels.Length==9,"All axes and tiers reach guest");for(int i=0;i<9;i++){var a=pair.Host.SharedPanels[i];var b=pair.Client.SharedPanels[i];Need(a.Id==b.Id&&a.Shape.Axis==b.Shape.Axis&&a.Shape.Tier==b.Shape.Tier&&a.Health==b.Health,"Exact axis tier and damaged health roundtrip");}
            Need(pair.Client.SelectedMap.Rules.BuildPieces,"Default ON rule replicated");
            var pipe=new Pipe();var late=new R24LanLobby(pipe,pipe.Close,delegate{return "unused";},new string('a',64));pipe.Lost=late.TransportDisconnected;pair.HostPipe.Links.Add("late",new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=pair.HostPipe,Back="late"});late.StartClient("server","Late",pair.Now);pair.Host.TransportConnected("late");
            try{for(int i=0;i<12;i++){raw();late.Tick(pair.Now);}Need(late.SharedPanels.Length==9,"Late join replays every tier");for(int i=0;i<9;i++)Need(late.SharedPanels[i].Shape.Tier==pair.Host.SharedPanels[i].Shape.Tier&&late.SharedPanels[i].Health==pair.Host.SharedPanels[i].Health,"Late replay retains exact tier health");}finally{late.Dispose();pipe.Dispose();}
            int rejected=pair.Host.RejectedFrames;long seq=1000;
            foreach(var bad in new[]{new byte[]{1,255},new byte[]{255,0},new byte[]{1},new byte[]{1,0,0,0,0}}){pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(new Packet{Protocol=Protocol.Name,Schema=Protocol.Schema,Kind=PacketKind.Command,FeatureId="playable.build.place/1",ActorId=pair.Client.OwnActor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=seq++,BaseSequence=-1,Payload=bad})});pair.Tick();}
            Need(pair.Host.RejectedFrames==rejected&&pair.Host.SharedPanels.Length==9&&pair.Client.State==R24LanLobbyState.Connected,"Malformed build commands do not mutate or disconnect");
            var apply=typeof(R24LanLobby).GetMethod("ApplyBuild",BindingFlags.Instance|BindingFlags.NonPublic);Need(!(bool)apply.Invoke(pair.Host,new object[]{pair.Client.OwnActor,1,255,0,false})&&pair.Client.State==R24LanLobbyState.Connected,"Malformed tier intent ignored, peer retained");
        }
    }
}
