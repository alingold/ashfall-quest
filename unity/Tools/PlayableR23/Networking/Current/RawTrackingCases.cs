using System;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
 static R24LanRawTracking Raw(string hash,float x=0,float y=0,float z=0)
 {return new R24LanRawTracking(hash,7,PoseAt(x,y,z),PoseAt(x,y+1.7f,z),PoseAt(x-.3f,y+1.1f,z+.2f),PoseAt(x+.3f,y+1.1f,z+.2f),.7f,.2f,1);}
 static bool Open(Float3 a,Float3 b){return true;}
 static Packet RawPacket(Pair pair,string actor,long sequence,R24LanRawTracking pose,long epoch=-1)
 {return new Packet{Kind=PacketKind.Delta,FeatureId="playable.tracking/2",ActorId=actor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=epoch<0?pair.Host.Epoch:epoch,Sequence=sequence,BaseSequence=-1,Payload=pose.Encode()};}
 static void RawFrame(Pair pair,Packet frame){pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(frame)});pair.Tick();}
 // 2026-11-08: guest placed first so its fixture-origin samples start at its actual
 // committed placement (site "a"), as the host spawn-anchor rule now requires.
 static void RealRawSpawns(Pair pair){bool client=pair.Client.RequestSpawn();for(int i=0;i<4;i++)pair.Tick();bool host=pair.Host.RequestSpawn();Console.WriteLine("RealRawSpawns epoch="+pair.Host.Epoch+" host="+host+" client="+client+" hostStatus="+pair.Host.SpawnStatus+" clientStatus="+pair.Client.SpawnStatus+" hostMap="+(pair.Host.SelectedMap==null?"null":pair.Host.SelectedMap.Active.ToString())+" countdown="+pair.Host.RoundCountdown);Need(host&&client,"Raw evidence prerequisites use actual Session placement");for(int i=0;i<5;i++)pair.Tick();}
 static void RawTrackingCases()
 {
  string hash=new string('c',64);R24LanRawTracking decoded;var pose=Raw(hash,3);
  byte[] bytes=pose.Encode();Need(bytes.Length==190&&R24LanRawTracking.TryDecode(bytes,out decoded)&&decoded.Left.Position.X==pose.Left.Position.X&&decoded.RightGrip==.2f,"Raw codec retains controller frame/grip independently of render pose");
  var bad=(byte[])bytes.Clone();bad[0]=3;Need(!R24LanRawTracking.TryDecode(bad,out decoded),"Unknown raw schema refused");Array.Resize(ref bad,191);Need(!R24LanRawTracking.TryDecode(bad,out decoded),"Trailing raw bytes refused");
  Need(!new R24LanRawTracking(hash,7,pose.Root,PoseAt(20,0,0),pose.Left,pose.Right,0,0).BodyValid,"Source five metre head/root bound");
  var history=new R24LanRawTrackingHistory();var broken=new R24LanTransform(new Float3(Single.NaN,0,0),new R24LanRotation(0,0,0,0));
  var oneHand=new R24LanRawTracking(hash,7,pose.Root,pose.Head,broken,pose.Right,Single.NaN,.2f);
  Need(R24LanRawTracking.TryDecode(oneHand.Encode(),out decoded)&&history.Accept(1,0,decoded,false,1,null),"Invalid left controller does not freeze valid body/right controller");
  var sample=history.Latest;Need(sample.Pose.Left.Valid&&sample.Pose.LeftGrip==0&&(sample.Pose.Flags&2)==0&&sample.Pose.Right.Position.X==pose.Right.Position.X&&sample.Pose.RightGrip==.2f&&history.SanitizedHands==1,"One invalid hand neutralized without borrowing rendered glove calibration");
  Need(Math.Abs(sample.Pose.Left.Position.X-2.7f)<.00001&&Math.Abs(sample.Pose.Left.Position.Y-1.35f)<.00001&&Math.Abs(sample.Pose.Left.Position.Z-.25f)<.00001,"Neutral hand reflects original source fallback once");
  Need(!history.Accept(1,.01,pose,false,1,null)&&history.Rejection=="sequence"&&history.Latest.Sequence==1,"Replay leaves accepted history unchanged");
  Need(!history.Accept(2,-1,pose,false,1,null)&&history.Rejection=="clock","Backward host clock refused");
  history=new R24LanRawTrackingHistory();Need(history.Accept(1,0,Raw(hash),true,1,Open)&&history.Accept(2,.05,Raw(hash,3),true,1,Open),"Source three metre horizontal allowance inclusive");
  Need(!history.Accept(3,.10,Raw(hash,6.001f),true,1,Open)&&history.Rejection=="speed"&&history.Latest.Sequence==2,"Excess horizontal travel cannot poison baseline");
  Need(history.Accept(4,.10,Raw(hash,3,6),true,1,Open),"Source independent120m/s ascent accepted");
  Need(history.Accept(5,.150001,Raw(hash,3,-12),true,1,Open),"Source independent360m/s descent accepted");
  Need(!history.Accept(6,.20,Raw(hash,3,-31),true,1,Open)&&history.Rejection=="speed","Descent exceeding source bound refused");
  int sweeps=0;Need(!history.Accept(7,.20,Raw(hash,3,-11),true,1,delegate(Float3 a,Float3 b){sweeps++;return false;})&&sweeps==1&&history.Rejection=="world-sweep","Host world oracle required for changed match head position");
  Need(!history.Accept(8,.20,Raw(hash,3,-11),true,1,null)&&history.Rejection=="world-sweep","Missing match oracle fails closed");
  Need(history.Accept(9,.20,Raw(hash,3,-12),true,1,null),"Unchanged head skips source movement ray");
  history=new R24LanRawTrackingHistory();for(int i=0;i<45;i++)Need(history.Accept(i+1,0,Raw(hash),false,1,null),"Source fixed-window45 accepted updates");
  Need(!history.Accept(46,1,Raw(hash),false,1,null)&&history.Rejection=="rate","Source strict greater-than-one-second rate reset");
  Need(history.Accept(47,1.00001,Raw(hash),false,1,null),"Rate window resumes after strict source boundary");
  history=new R24LanRawTrackingHistory();for(int i=0;i<30;i++)Need(history.Accept(i+1,i*.05,Raw(hash,i),false,1,null),"Warmup history uses genuine host time");
  Need(history.Count==20&&history.TryNearest(0,out sample)&&sample.Sequence==11,"Only newest source20 samples retained");
  Need(history.TryNearest(1.22,out sample)&&sample.Sequence==25,"Source nearest timestamp rather than fabricated interpolation");
  Need(!history.TryNearest(Double.NaN,out sample),"Nonfinite historical query refused");
  using(var pair=new Pair()){
   pair.Host.BindSpawnWorld(new SpawnWorld{Host=pair.Host});
   for(int i=0;i<5;i++)pair.Tick();string actor=pair.Client.OwnActor;R24LanRawTrackingSample stored;
   Need(!pair.Client.PublishRawTracking(Raw(hash))&&!pair.Host.TryReadRawTracking(actor,pair.Now,out stored),"Lobby membership alone grants no loaded tracking evidence");
   Need(pair.Host.BeginMapSelection("range",hash)&&pair.Host.ReportMapLoaded(hash),"Actual map owner enters real readiness lifecycle");for(int i=0;i<3;i++)pair.Tick();
   Need(!pair.Client.PublishRawTracking(Raw(hash)),"One loaded host cannot admit unloaded guest tracking");Need(pair.Client.ReportMapLoaded(hash),"Actual guest load acknowledgement");for(int i=0;i<4;i++)pair.Tick();RealRawSpawns(pair);
   Need(pair.Host.PublishRawTracking(Raw(hash,5))&&pair.Host.TryReadRawTracking(pair.Host.OwnActor,pair.Now,out stored)&&stored.Pose.Root.Position.X==5,"Listen host follows the same map-readiness and history admission path");
   for(int i=0;i<100;i++)Need(pair.Client.PublishRawTracking(Raw(hash,i)),"Client replaces pending raw tracking rather than enqueuing burst");for(int i=0;i<3;i++)pair.Tick();
   Need(pair.Host.TryReadRawTracking(actor,pair.Now,out stored)&&stored.Sequence==100&&stored.Pose.Root.Position.X==99&&stored.Time<=pair.Now,"Real lobby transport authenticates and timestamps coalesced raw pose");
   Need(!pair.Client.TryReadRawTracking(actor,pair.Now,out stored),"Clients do not own authority-side raw histories");
   RawFrame(pair,RawPacket(pair,actor,99,Raw(hash,-9)));Need(pair.Host.LastRawTrackingRejection=="sequence"&&pair.Host.TryReadRawTracking(actor,pair.Now,out stored)&&stored.Pose.Root.Position.X==99,"Out-of-order raw update cannot replace current evidence");
   RawFrame(pair,RawPacket(pair,actor,101,Raw(new string('d',64))));Need(pair.Host.LastRawTrackingRejection=="map-readiness"&&pair.Host.Members.Length==2,"Wrong map rejected without damaging membership or loaded state");
   pair.ClientPipe.Pressure=2;pair.Client.PublishRawTracking(Raw(hash,4));for(int i=0;i<6;i++)pair.Tick();Need(pair.Host.TryReadRawTracking(actor,pair.Now,out stored)&&stored.Pose.Root.Position.X==4,"Raw send pressure retains the newest bounded pending sample");
   long oldEpoch=pair.Host.Epoch;Need(pair.Host.BeginMapSelection("range",hash),"New actual map epoch");Need(!pair.Host.TryReadRawTracking(actor,pair.Now,out stored),"Epoch invalidation clears old raw history");
   RawFrame(pair,RawPacket(pair,actor,200,Raw(hash),oldEpoch));Need(pair.Host.Members.Length==2&&!pair.Host.TryReadRawTracking(actor,pair.Now,out stored),"Old epoch raw frame cannot recreate history or kill surviving connection");
   pair.Host.ReportMapLoaded(hash);pair.Client.ReportMapLoaded(hash);for(int i=0;i<4;i++)pair.Tick();RealRawSpawns(pair);pair.Client.PublishRawTracking(Raw(hash));for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.TryReadRawTracking(actor,pair.Now,out stored),"Current-epoch actual readiness enables new raw evidence");
   pair.Now+=1.01;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Need(!pair.Host.TryReadRawTracking(actor,pair.Now,out stored),"Stale raw data unavailable after one second");
   pair.Client.PublishRawTracking(Raw(hash));for(int i=0;i<3;i++)pair.Tick();pair.ClientPipe.Close("server");Need(!pair.Host.TryReadRawTracking(actor,pair.Now,out stored),"Actual departure retires raw controller history");
  }
  using(var pair=new Pair()){
   pair.Host.BindSpawnWorld(new OriginSpawnWorld()); // 2026-11-08: match samples start at the guest's actual placement (spawn anchor)
   for(int i=0;i<5;i++)pair.Tick();bool clear=true;int queries=0;pair.Host.BindRawTrackingWorldQuery(delegate(Float3 a,Float3 b){queries++;return clear;});
   var rules=R24PlayableMatchRules.ForPreset("free_for_all","range");pair.Host.BeginRoundSelection(rules,hash,123);pair.Host.ReportMapLoaded(hash);for(int i=0;i<3;i++)pair.Tick();pair.Client.ReportMapLoaded(hash);for(int i=0;i<4;i++)pair.Tick();pair.Now=pair.Host.SelectedMap.StartsAt+.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);RealRawSpawns(pair);
   pair.Client.PublishRawTracking(Raw(hash));for(int i=0;i<3;i++)pair.Tick();R24LanRawTrackingSample sample0;Need(pair.Host.TryReadRawTracking(pair.Client.OwnActor,pair.Now,out sample0)&&queries==0,"First actual raw sample stored without inventing Session spawn authority");
   clear=false;pair.Client.PublishRawTracking(Raw(hash,.2f));for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.LastRawTrackingRejection=="world-sweep"&&queries==1&&pair.Host.TryReadRawTracking(pair.Client.OwnActor,pair.Now,out sample0)&&sample0.Pose.Root.Position.X==0,"Production receive invokes bound host oracle and retains prior clear pose");
   clear=true;pair.Client.PublishRawTracking(Raw(hash,.2f));for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.TryReadRawTracking(pair.Client.OwnActor,pair.Now,out sample0)&&sample0.Pose.Root.Position.X==.2f,"New valid raw packet recovers without changing local gameplay policy");
  }
  using(var pair=new Pair()){
   pair.Host.BindSpawnWorld(new SpawnWorld{Host=pair.Host});
   for(int i=0;i<5;i++)pair.Tick();pair.Host.BeginMapSelection("range",hash);pair.Host.ReportMapLoaded(hash);for(int i=0;i<3;i++)pair.Tick();pair.Client.ReportMapLoaded(hash);for(int i=0;i<4;i++)pair.Tick();RealRawSpawns(pair);
   RawFrame(pair,RawPacket(pair,pair.Host.OwnActor,1,Raw(hash)));R24LanRawTrackingSample stored;Need(pair.Host.RejectedFrames==1&&pair.Host.Members.Length==1&&!pair.Host.TryReadRawTracking(pair.Host.OwnActor,pair.Now,out stored),"Raw actor spoof cannot override actual authenticated transport identity");
  }
 }
}
