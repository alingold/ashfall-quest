using System;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Integration.Playable.Traversal;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    static void TraversalJet()
    {
        Need(R25TraversalGestures.ReservedZone(new Float3(.2f,-.7f,0))&&R25TraversalGestures.ReservedZone(new Float3(.2f,-.1f,-.3f))&&!R25TraversalGestures.ReservedZone(new Float3(.6f,-.2f,.5f)),"Hip/backpack zones reserved, forward aim free");
        var jet=new R25JetPack();Float3 v=new Float3();
        jet.Step(.1f,true,false,false,true,false,v,v,10);Need(jet.Power==0,"Jet waits 120 ms airborne");
        v=jet.Step(.02f,true,false,false,true,false,v,new Float3(1,0,0),10);Need(jet.Power==1&&Near(v.Y,.196f,.001f),"26 minus 16.2 acceleration");
        for(int i=0;i<20;i++)v=jet.Step(.1f,true,false,false,true,false,v,new Float3(1,0,0),10);
        Need(v.Y<=9&&v.X<=13&&v.X>12,"Jet vertical and horizontal clamps");
        float fuel=jet.Fuel;for(int i=0;i<8;i++)jet.Step(.1f,true,true,false,false,false,v,v,10);Need(Near(fuel,jet.Fuel,.001f),"Refill waits 0.8 supported seconds");
        for(int i=0;i<10;i++)jet.Step(.1f,true,false,true,false,false,v,v,10);Need(Near(jet.Fuel,Math.Min(1,fuel+.5f),.001f),"Climbing refills half tank per second");
        jet.Step(.1f,false,false,false,true,false,v,v,10);Need(jet.Power==0,"Interlock stops thrust");
        jet.Step(.1f,true,false,false,true,false,v,v,10);v=jet.Step(.1f,true,false,false,true,true,v,new Float3(0,0,1),10);Need(Near(v.Z,16,.001f)&&Near(v.Y,-10f/7,.001f),"Glide boost 1.6 with unchanged glide descent");
        jet.Fuel=.07f;bool sputter=false;for(int i=0;i<4;i++){jet.Step(.05f,true,false,false,true,false,new Float3(),new Float3(),10);sputter|=Near(jet.Power,.4f,.001f);}Need(sputter,"Low fuel sputters at 40 percent");
        jet.Fuel=1;for(int i=0;i<32;i++)jet.Step(.1f,true,false,false,true,false,new Float3(),new Float3(),10);Need(jet.Fuel<.00001f,"3.2 seconds drains tank");
        var hover=new R25JetPack();hover.Step(.1f,true,false,false,false,false,v,v,10);v=hover.Step(.1f,true,false,false,true,false,new Float3(4,0,0),new Float3(),10);Need(v.X<4&&v.Y>0,"Idle horizontal hover drag with net positive thrust");
    }
    static void TraversalRopes()
    {
        var rope=new R25WebRope();Need(!rope.Fire(new Float3(),new Float3(1,0,0))&&rope.Fire(new Float3(),new Float3(10,0,0)),"Anchor range gate");rope.Tick(.1f,true,true,false);
        Float3 v=new Float3(-2,3,0);Float3 p=rope.Constrain(new Float3(-1,0,0),ref v);Need(Near(p.X,0,.001f)&&Near(v.X,0,.001f)&&Near(v.Y,3,.001f),"Sphere projection removes outward velocity, retains tangent");
        float energy=R25TraversalVector.Dot(v,v);p=rope.Constrain(new Float3(-1,0,0),ref v);Need(R25TraversalVector.Dot(v,v)<=energy,"Repeated correction cannot add energy");
        rope.Forces(new Float3(),v,1,0,.1f);Need(Near(rope.Length,9.1f,.001f),"Reel cap 9 m/s");
        v=rope.Forces(new Float3(),new Float3(),0,3,.1f);Need(rope.Zipped&&Near(v.X,6,.001f),"Tug zip 6 m/s");rope.Tick(.1f,true,true,false);v=rope.Forces(new Float3(),new Float3(),0,3,.1f);Need(!rope.Zipped&&v.X==0,"Zip cooldown");
        for(int i=0;i<4;i++)rope.Tick(.1f,true,true,true);Need(!rope.Attached,"Blocked rope breaks after 0.3 seconds");Need(!rope.Fire(new Float3(),new Float3(10,0,0)),"Refire cooldown");for(int i=0;i<4;i++)rope.Tick(.1f,false,true,false);
        Need(rope.Fire(new Float3(),new Float3(10,0,0)),"Refire after cooldown");rope.Tick(.1f,true,false,false);Need(!rope.Attached,"Destroyed anchor breaks");
        var a=new R25WebRope();var b=new R25WebRope();a.Fire(new Float3(),new Float3(10,0,0));b.Fire(new Float3(),new Float3(-10,0,0));a.Tick(.1f,true,true,false);b.Tick(.1f,true,true,false);v=new Float3(0,12,0);p=new Float3(0,5,0);for(int i=0;i<8;i++){p=a.Constrain(p,ref v);p=b.Constrain(p,ref v);}Need(p.IsFinite&&v.IsFinite&&R25TraversalVector.Length(v)<=12,"Two rope solve stays finite and bounded");
        for(int i=0;i<121;i++)a.Tick(.1f,true,true,false);Need(!a.Attached,"12-second expiry");Need(Near(R25TraversalVector.Length(R25WebRope.Accept(new Float3(20,0,0),.1f)),32,.001f),"Release accepted velocity capped 32");
        Need(b.Forces(new Float3(),new Float3(),float.NaN,0,.1f).IsFinite,"Invalid force inputs cannot create nonfinite motion");
        b.Tick(.1f,false,true,false);Need(!b.Attached,"Release grip detaches");
    }
    static void TraversalWire()
    {
        var state=new R25WebState(true,new Float3(5,4,3),true,new Float3(-5,4,3));R25WebState copy;Need(R25WebState.Decode(state.Encode(),out copy)&&copy.Same(state),"25-byte state round trip");
        Need(!R25WebState.Decode(new byte[26],out copy)&&!R25WebState.Decode(new R25WebState(true,new Float3(float.NaN,0,0),false,new Float3()).Encode(),out copy),"Malformed finite codec gate");
        Need(!new R25WebState(true,new Float3(61,0,0),false,new Float3()).Near(new Float3()),"Host anchor within 60 m");
        var t=new R24LanTransform(new Float3(),new R24LanRotation(0,0,0,1));var pose=new R24LanPose("menu",0,32,t,t,t,t,new Float3(),0,0);R24LanPose read;Need(pose.Jetting&&R24LanPose.TryDecode(pose.Encode(),out read)&&read.Jetting,"Pose bit 32 accepted");Need(!new R24LanPose("menu",0,128,t,t,t,t,new Float3(),0,0).Valid,"Higher pose bits rejected");
        using(var pair=new Pair()){
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
            Need(pair.Client.SelectedMap.Rules.Jetpack&&pair.Client.SelectedMap.Rules.WebSlingers,"Default-on rules replicate");
            Need(pair.Client.PublishWeb(state),"Guest sends line state");for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadWeb(pair.Client.OwnActor,out copy)&&copy.Same(state),"Host accepts near finite anchors");Need(pair.Client.TryReadWeb(pair.Client.OwnActor,out copy)&&copy.Same(state),"Reliable host relay");
            int rejected=pair.Host.RejectedFrames;
            pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(new Packet{Protocol=Protocol.Name,Schema=Protocol.Schema,Kind=PacketKind.Command,FeatureId="playable.web.state/1",ActorId=pair.Client.OwnActor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=999,BaseSequence=-1,Payload=new byte[]{255}})});pair.Tick();Need(pair.Host.RejectedFrames==rejected&&pair.Client.State==R24LanLobbyState.Connected,"Bad payload ignored without rejecting peer");
            var pipe=new Pipe();var late=new R24LanLobby(pipe,pipe.Close,delegate{return "unused";},new string('a',64));pipe.Lost=late.TransportDisconnected;pair.HostPipe.Links.Add("late",new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=pair.HostPipe,Back="late"});late.StartClient("server","Late",pair.Now);pair.Host.TransportConnected("late");try{for(int i=0;i<10;i++){KeepRaw(pair,hash,0,8);late.Tick(pair.Now);}Need(late.TryReadWeb(pair.Client.OwnActor,out copy)&&copy.Same(state),"Late join replays active lines");}finally{late.Dispose();pipe.Dispose();}
            Need(pair.Client.PublishWeb(new R25WebState(false,new Float3(),false,new Float3())),"Release publishes empty state");for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);Need(!pair.Host.TryReadWeb(pair.Client.OwnActor,out copy)&&!pair.Client.TryReadWeb(pair.Client.OwnActor,out copy),"Release clears host and guest state");
            pair.Client.PublishWeb(state);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
            Need(pair.Host.BeginRoundSelection(R24PlayableMatchRules.ForPreset("free_for_all","range").With("web_slingers",false),hash,321),"Round reset");Need(!pair.Host.TryReadWeb(pair.Client.OwnActor,out copy),"Round reset clears lines");
        }
        using(var pair=new Pair()){
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("protection",0f),hash);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
            var far=new R25WebState(true,new Float3(1000,0,0),false,new Float3());pair.Client.PublishWeb(far);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);Need(!pair.Host.TryReadWeb(pair.Client.OwnActor,out copy),"Host refuses distant anchor");
            pair.Client.PublishWeb(state);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadWeb(pair.Client.OwnActor,out copy),"Line accepted before disconnect");pair.ClientPipe.Close("server");pair.Tick();Need(!pair.Host.TryReadWeb(pair.Client.OwnActor,out copy),"Disconnect clears line state");
        }
        using(var pair=new Pair()){
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("protection",0f),hash);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);pair.Client.PublishWeb(state);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
            var h=Health(pair.Host,pair.Client.OwnActor);Need(pair.Host.ApplyHostHealthLoss(pair.Client.OwnActor,h.Revision,h.Health,h.Shield,false).Accepted,"Actual producer death");for(int i=0;i<3;i++)pair.Tick();Need(!pair.Host.TryReadWeb(pair.Client.OwnActor,out copy)&&!pair.Client.TryReadWeb(pair.Client.OwnActor,out copy),"Death clears host and remote line state");
        }
        using(var pair=new Pair()){
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("web_slingers",false).With("jetpack",false),hash);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);pair.Client.PublishWeb(state);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);Need(!pair.Host.TryReadWeb(pair.Client.OwnActor,out copy)&&!pair.Client.SelectedMap.Rules.Jetpack,"Rule-off ignores state and replicates jet switch");
        }
    }
}
