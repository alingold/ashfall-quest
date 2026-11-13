using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using Ashfall.Modules.Networking;
using Ashfall.Integration.Playable.Match;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Integration.Playable.Networking;
partial class LobbyCases
{
    sealed class Build2Clearance:IConstructionWorld
    {
        internal int Calls;internal bool RejectSolid;
        public bool HasStaticContact(Panel p,float tolerance){return true;}
        public bool IsBuried(Panel p){return false;}
        public bool HasLineOfSight(string actor,Float3 a,Float3 b){return true;}
        public string Clearance(Panel p,string actor){Calls++;return Geometry.Contains(p,new Float3(1.41f,RejectSolid?.5f:1.5f,0),.2f)?"player_clearance":"";}
    }
    static void Build2Admission()
    {
        var world=new Build2Clearance();var service=new CandidateService("tiers",1,new TierGate(),world,false);
        var pose=new BuilderState(new Float3(1.41f,1.5f,-1),new Float3(0,0,1),new Float3(1.41f,1.6f,-1),true,true,0,true,2);
        CandidateService.PreparedChange change;
        Need(service.PreparePlace(new ConstructionCommand(TierContext(1),1),pose,out change).Accepted&&world.Calls==4,"Opening clearance queries four solid boxes");change.Abort();
        world.RejectSolid=true;
        Need(service.PreparePlace(new ConstructionCommand(TierContext(2),2),pose,out change).Accepted,"Blocked first face may select next free face");
        Need(change.Panels[0].Shape.Center.Z!=0,"Solid sill refuses occupied first face");change.Abort();
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindConstructionWorld(new BuildWorld());
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);
            Action raw=delegate{pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0,7,rx:1.5f,ry:1.2f,rz:1.5f));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();};
            for(int i=0;i<4;i++)raw();Need(pair.Host.RequestBuild(1,2),"Actual host pays Armoured placement");string id=pair.Host.SharedPanels[0].Id;
            Need(!pair.Host.RequestBuild(1,0,0,true),"Dismantle cannot reuse placement raw pose");
            for(int i=0;i<5;i++)raw();Need(pair.Host.RequestBuild(1,0,0,true),"Actual host dismantles own freshly aimed floor: "+pair.Host.LastBuildRejection);
            for(int i=0;i<5;i++)raw();Need(pair.Client.SharedPanels.Length==0&&pair.Client.WasPanelDismantled(id),"Guest receives dismantle removal cue");
            int rejected=pair.Host.RejectedFrames;long seq=5000;
            foreach(var bad in new[]{new byte[]{1,0,255,0},new byte[]{1,255,0,0},new byte[]{1,0,0,255},new byte[]{255,0,0,0}}){
                pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(new Packet{Protocol=Protocol.Name,Schema=Protocol.Schema,Kind=PacketKind.Command,FeatureId="playable.build.place/1",ActorId=pair.Client.OwnActor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=seq++,BaseSequence=-1,Payload=bad})});pair.Tick();
            }Need(pair.Host.RejectedFrames==rejected&&pair.Host.SharedPanels.Length==0&&pair.Client.State==R24LanLobbyState.Connected,"Invalid variant tier intent hand ignored without peer rejection");
        }
    }
    sealed class Build2Ground:IConstructionWorld
    {
        public bool HasStaticContact(Panel p,float tolerance){return p.Center.Y-Geometry.HalfSize(p).Y<=tolerance;}
        public bool IsBuried(Panel p){return false;}
        public string Clearance(Panel p,string actor){return "";}
        public bool HasLineOfSight(string actor,Float3 a,Float3 b){return true;}
    }
    static void BuildDismantlePolicy()
    {
        var local=new Ashfall.Integration.Playable.Activities.R23ConstructionSession(new BuildWorld());
        var localPose=new BuilderState(new Float3(1.41f,1.5f,-1),new Float3(0,0,1),new Float3(1.41f,1.6f,-1),true,true,2,true,0);
        PlacedPanel[] preview;int cost;
        Need(local.Evaluate(localPose,1,true,out preview,out cost).Accepted&&cost==3&&local.Balance==996,"Local placement actually pays three");
        Need(local.Dismantle(localPose,2).Accepted&&local.Balance==997&&local.Snapshot().Length==0,"Local dismantle transaction refunds exactly one");
        Need(!local.Dismantle(localPose,3).Accepted&&local.Balance==997,"Repeated local removal cannot mint charges");
        for(int tier=0;tier<3;tier++)foreach(bool infinite in new[]{false,true}){
            var service=new CandidateService("tiers",1,new TierGate(),new Build2Ground(),infinite);
            var wall=new Panel("wall",new Float3(0,Geometry.Side/2,0),2,false,1,tier,2);
            var floor=new Panel("floor",new Float3(0,Geometry.Side,0),1,false,1);
            service.ReconcileNetworkPreview(new[]{new PlacedPanel("root","builder",wall,wall.MaximumHealth),new PlacedPanel("upper","builder",floor,140)});
            var pose=new BuilderState(new Float3(1.2f,.5f,-2),new Float3(0,0,1),new Float3(1.2f,.7f,-2),true,true,0,true);
            CandidateService.PreparedChange change;
            Need(!service.PrepareDismantle(new ConstructionCommand(TierContext(1,"other"),1),pose,out change).Accepted,"Other owner cannot dismantle");
            var off=new BuilderState(pose.Origin,pose.Direction,pose.Head,true,true,0,false);
            Need(!service.PrepareDismantle(new ConstructionCommand(TierContext(2),2),off,out change).Accepted,"Rule OFF refuses dismantle");
            var far=new BuilderState(new Float3(1.2f,.5f,-7),pose.Direction,new Float3(1.2f,.7f,-7),true,true);
            Need(!service.PrepareDismantle(new ConstructionCommand(TierContext(3),3),far,out change).Accepted,"Six metre ray bound");
            Need(service.PrepareDismantle(new ConstructionCommand(TierContext(4),4),pose,out change).Accepted&&change.Cost==(infinite?0:-(tier+1)/2),"Half paid price rounded down, infinite zero");
            change.Abort();Need(service.Snapshot().Length==2,"Aborted dismantle preserves both pieces");
            Need(service.PrepareDismantle(new ConstructionCommand(TierContext(5),5),pose,out change).Accepted,"Retry valid dismantle");change.CommitValidated();
            Need(service.Snapshot().Length==1&&service.Snapshot()[0].Id=="upper","Only aimed piece removed initially");
            service.PrepareAdvance(.29f,false,out change);change.CommitValidated();Need(service.Snapshot().Length==1,"Collapse waits for support delay");
            service.PrepareAdvance(.02f,false,out change);change.CommitValidated();Need(service.Snapshot().Length==0,"Supported floor collapses after dismantle");
            Need(!service.PrepareDismantle(new ConstructionCommand(TierContext(6),6),pose,out change).Accepted,"Removed piece cannot refund twice");
        }
    }
    static void BuildSelectionControls()
    {
        var selection=new Ashfall.Integration.Playable.Activities.R25BuildSelection();
        selection.Step(false,false,.1f,true);
        Need(!selection.Step(true,true,.1f,true)&&selection.Piece==0,"Press does not cancel or cycle early");
        Need(!selection.Step(true,false,.1f,true)&&selection.Piece==1,"Tap release cycles piece");
        for(int i=0;i<3;i++)Need(!selection.Step(true,true,.1f,true),"Subthreshold hold remains active");
        Need(selection.Step(true,true,.1f,true),"0.4s cancels once");
        Need(!selection.Step(true,true,.1f,true),"Continued hold does not repeat cancel");
        selection.Step(true,false,.1f,true);Need(selection.Piece==1,"Hold release never cycles");
        selection.Step(false,false,.1f,true);Need(selection.Piece==1,"Selection survives preview lifecycle");
        for(int i=0;i<4;i++){selection.Step(true,true,.01f,true);selection.Step(true,false,.01f,true);}
        Need(selection.Piece==0,"Five pieces wrap including dismantle");
        selection.Step(true,true,.01f,false);selection.Step(true,false,.01f,false);Need(selection.Piece==0,"Rule off disables piece selection");
    }
    static void BuildVariantGeometry()
    {
        float side=Geometry.Side;
        foreach(int axis in new[]{0,2})for(int variant=0;variant<4;variant++)for(int tier=0;tier<3;tier++){
            var p=new Panel("wall",new Float3(0,side/2,0),axis,false,1,tier,variant);
            Need(p.Slot==new Panel("wall",p.Center,axis,false,1).Slot,"Variants share face slot");
            Need(p.MaximumHealth==new[]{180,110,160,150}[variant]*(tier==0?1:tier==1?1.75f:2.75f),"Variant tier health table");
            var boxes=Geometry.Boxes(p);Need(boxes.Length==new[]{1,1,4,3}[variant],"Variant box count");
            foreach(var box in boxes)Need(Geometry.Contains(p,box.Center,0)&&CandidateGeometry.Contains(p,box.Center,0),"Solid box center contained, geometry parity");
            var floor=new Panel("floor",new Float3(0,side,0),1,false,0);
            Need(Geometry.Connected(p,floor)==(variant!=1),"Half wall cannot support floor above; other variants can");
            var dir=axis==2?new Float3(0,0,1):new Float3(1,0,0);
            var start=axis==2?new Float3(0,1.5f,-2):new Float3(-2,1.5f,0);
            var host=typeof(R24LanLobby).GetMethod("BoxRay",BindingFlags.Static|BindingFlags.NonPublic);
            float hit=(float)host.Invoke(null,new object[]{start,dir,p,6f,0f});
            Need(Single.IsPositiveInfinity(hit)==(variant!=0),"Host bullets pass opening/above half wall");
            start=axis==2?new Float3(1.2f,.5f,-2):new Float3(-2,.5f,1.2f);
            Need(!Single.IsPositiveInfinity((float)host.Invoke(null,new object[]{start,dir,p,6f,0f})),"Host bullets stop at solid side");
        }
        var service=TierService();CandidateService.PreparedChange change;
        var pose=new BuilderState(TierPose(0).Origin,TierPose(0).Direction,TierPose(0).Head,true,true,0,false,2);
        Need(!service.PreparePlace(new ConstructionCommand(TierContext(1),1),pose,out change).Accepted,"Rule off refuses variant intent");
    }
    static void BuildVariantWire()
    {
        // Extend the actual production codec fixture to every valid axis/variant/tier.
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();
            var rows=new List<PlacedPanel>();
            for(int axis=0;axis<3;axis++)for(int variant=0;variant<(axis==1?1:4);variant++)for(int tier=0;tier<3;tier++){
                var shape=new Panel(axis==1?"floor":"wall",new Float3(axis*4,10,variant*4),axis,false,1,tier,variant);
                rows.Add(new PlacedPanel("variant/"+axis+"/"+variant+"/"+tier,pair.Host.OwnActor,shape,shape.MaximumHealth*.5f));
            }
            var frame=typeof(R24LanLobby).GetMethod("PanelFrame",BindingFlags.Instance|BindingFlags.NonPublic);
            var read=typeof(R24LanLobby).GetMethod("ReadPanel",BindingFlags.Instance|BindingFlags.NonPublic);long sequence=1;
            foreach(var row in rows){var packet=Decode((byte[])frame.Invoke(pair.Host,new object[]{row.Id,row,sequence++}));read.Invoke(pair.Client,new object[]{packet});}
            var sample=rows[0];var valid=Decode((byte[])frame.Invoke(pair.Host,new object[]{sample.Id,sample,sequence++}));
            int tierOffset;
            using(var stream=new MemoryStream(valid.Payload))using(var reader=new BinaryReader(stream)){
                reader.ReadBytes(reader.ReadUInt16());reader.ReadBoolean();reader.ReadBytes(reader.ReadUInt16());reader.ReadBoolean();reader.ReadByte();tierOffset=(int)stream.Position;
            }
            foreach(int offset in new[]{tierOffset,tierOffset+1}){
                var bad=Decode((byte[])frame.Invoke(pair.Host,new object[]{sample.Id,sample,sequence++}));bad.Payload[offset]=255;
                long before=pair.Client.ConstructionRevision;read.Invoke(pair.Client,new object[]{bad});
                Need(pair.Client.ConstructionRevision==before&&pair.Client.State==R24LanLobbyState.Connected,"Malformed tier/variant snapshot ignored without advancing sequence or disconnect");
            }
            Need(pair.Client.SharedPanels.Length==27,"All variant/tier/axis combinations decoded");
            foreach(var got in pair.Client.SharedPanels){var want=rows.Find(delegate(PlacedPanel p){return p.Id==got.Id;});Need(want.Shape.Variant==got.Shape.Variant&&want.Shape.Tier==got.Shape.Tier&&want.Health==got.Health,"Exact variant tier health roundtrip");}
        }
    }
}
