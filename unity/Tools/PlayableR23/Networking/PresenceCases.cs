using System;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Integration.Playable.Networking;
partial class LobbyCases
{
 static R24LanTransform PoseAt(float x,float y,float z){return new R24LanTransform(new Float3(x,y,z),new R24LanRotation(0,0,0,1));}
 static R24LanPose Pose(float x=0,int style=0){return new R24LanPose("menu",style,15,PoseAt(x,0,0),PoseAt(x,1.7088f,0),PoseAt(x-.3f,1.1f,.2f),PoseAt(x+.3f,1.1f,.2f),new Float3(0,0,0),.7f,.2f);}
 static Packet PosePacket(Pair pair,string actor,long sequence,byte[] bytes){return new Packet{Kind=PacketKind.Delta,FeatureId="playable.presence/1",ActorId=actor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=0,Sequence=sequence,BaseSequence=-1,Payload=bytes};}
 static void PresenceCases()
 {
  R24LanPose decoded;byte[] original=Pose(3,114).Encode();Need(original.Length<240&&R24LanPose.TryDecode(original,out decoded)&&decoded.Style==114&&decoded.Root.Position.X==3,"Finite compact source-index/pose roundtrip");
  var broken=(byte[])original.Clone();broken[6]=255;Need(!R24LanPose.TryDecode(broken,out decoded),"Unknown style refused before loading assets");
  var trailing=new byte[original.Length+1];Array.Copy(original,trailing,original.Length);Need(!R24LanPose.TryDecode(trailing,out decoded),"Trailing data refused");Need(!R24LanPose.TryDecode(new byte[]{1,255},out decoded),"Bounded scene length refused");
  var p=Pose();Need(!new R24LanPose("map:bad",0,15,p.Root,p.Head,p.Left,p.Right,p.Velocity,0,0).Valid,"Scene must be menu or actual map-manifest hash");Need(!new R24LanPose("menu",0,15,PoseAt(float.NaN,0,0),p.Head,p.Left,p.Right,p.Velocity,0,0).Valid,"Nonfinite root refused");Need(!new R24LanPose("menu",0,15,p.Root,p.Head,p.Left,PoseAt(100,1,0),p.Velocity,0,0).Valid,"Unbounded hand offset refused");Need(!new R24LanTransform(new Float3(0,0,0),new R24LanRotation(0,0,0,0)).Valid,"Collapsed quaternion refused");
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();Need(pair.Host.PublishPresence(Pose(2))&&pair.Client.PublishPresence(Pose(-2)),"Real admitted members publish presence");for(int i=0;i<3;i++)pair.Tick();R24LanPoseSnapshot hp,cp;
   Need(pair.Host.TryReadPresence(pair.Client.OwnActor,out cp)&&cp.Pose.Root.Position.X==-2,"Client pose bound to host-assigned actor");Need(pair.Client.TryReadPresence(pair.Host.OwnActor,out hp)&&hp.Pose.Root.Position.X==2,"Actual host pose relayed to client");foreach(var member in pair.Host.Members)Need(!member.WorldLoaded,"Presence does not fabricate map-ready or placement");
   for(int i=0;i<100;i++)pair.Client.PublishPresence(Pose(i));for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.TryReadPresence(pair.Client.OwnActor,out cp)&&cp.Pose.Root.Position.X==99,"Unsent pose burst coalesces to latest; no hundred-packet queue");long sequence=cp.Sequence;
   pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(PosePacket(pair,pair.Client.OwnActor,1,Pose(-9).Encode()))});pair.Tick();Need(pair.Host.TryReadPresence(pair.Client.OwnActor,out cp)&&cp.Sequence==sequence&&cp.Pose.Root.Position.X==99,"Reordered stale pose cannot roll back rendered state");
   pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(PosePacket(pair,pair.Client.OwnActor,1000,broken))});pair.Tick();Need(pair.Host.RejectedPoses==1&&pair.Host.Members.Length==2&&pair.Host.TryReadPresence(pair.Client.OwnActor,out cp)&&cp.Sequence==sequence,"Malformed visual pose cannot replace valid pose or mutate membership");
   pair.Client.PublishPresence(Pose(4));pair.ClientPipe.Pressure=2;for(int i=0;i<5;i++)pair.Tick();Need(pair.Host.TryReadPresence(pair.Client.OwnActor,out cp)&&cp.Pose.Root.Position.X==4,"Unreliable send pressure retains latest bounded pose");
   string actor=pair.Client.OwnActor;pair.ClientPipe.Close("server");Need(!pair.Host.TryReadPresence(actor,out cp)&&pair.Host.Members.Length==1,"Departure drops actor pose with actual Session membership");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();pair.Host.PublishPresence(Pose(5));pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(PosePacket(pair,pair.Host.OwnActor,100,Pose(55).Encode()))});pair.Tick();R24LanPoseSnapshot host;
   Need(pair.Host.RejectedFrames==1&&pair.Host.Members.Length==1&&pair.Host.TryReadPresence(pair.Host.OwnActor,out host)&&host.Pose.Root.Position.X==5,"Client cannot overwrite host/other actor's pose identity");
  }
 }
}
