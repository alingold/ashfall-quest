using System;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
 sealed class SpawnWorld:IR24LanSpawnWorld
 {
  internal R24LanLobby Host;internal bool Clear=true,Ready=true;internal int Queries;internal string Map="range";
  public bool ReadyForSpawn(string map,bool warmup){return Ready&&map==Map;}
  public SpawnSite[] Sites(string map,bool warmup){return map==Map?new[]{new SpawnSite("source-a",new Float3(2,.12f,4),-1,false,false),new SpawnSite("source-b",new Float3(9,.12f,4),-1,false,false)}:new SpawnSite[0];}
  public bool IsSafe(string map,string actor,Float3 feet){Queries++;if(!Clear||map!=Map)return false;foreach(var member in Host.Members)if(member.Actor!=actor){R24LanSpawn other;if(Host.TryReadSpawn(member.Actor,out other)){double x=(double)other.Feet.X-feet.X,z=(double)other.Feet.Z-feet.Z;if(x*x+z*z<.54*.54)return false;}}return true;}
 }
 static void SharedWarmup(Pair pair,string hash)
 {Need(pair.Host.BeginMapSelection("range",hash)&&pair.Host.ReportMapLoaded(hash),"Spawn test real host map readiness");for(int i=0;i<3;i++)pair.Tick();Need(pair.Client.ReportMapLoaded(hash),"Spawn test real guest load acknowledgement");for(int i=0;i<4;i++)pair.Tick();}
 static void SpawnCases()
 {
  string hash=new string('c',64);R24LanSpawn placement;
  for(int i=0;i<8;i++){
   var range=R24LanSpawnCoordinates.BuiltinArena(new Float3(0,0,-600),i);float sourceX=-15+(i%4)*10,sourceZ=600-6-(i/4)*30;Need(range.X==sourceX&&range.Y==2&&range.Z==-sourceZ,"Range candidate preserves source600 origin and reflects offset once");
   var cyber=R24LanSpawnCoordinates.CyberArena(new Float3(0,0,-2400),i);sourceX=-55+(i%4)*35;sourceZ=2400+330-(i/4)*660;Need(cyber.X==sourceX&&cyber.Y==1&&cyber.Z==-sourceZ,"Cyber candidate retains represented-2400 origin instead of unshifted local city frame");
   var warm=R24LanSpawnCoordinates.Warmup(new Float3(10,2,20),i,true);Need(Math.Abs(warm.X-(10+(i%4-1.5f)*.9f))<.00001&&warm.Y==2&&Math.Abs(warm.Z-(-(-20-.65f+(i/4)*1.3f)))<.00001,"Archive warmup source two rows reflected once around approved hub");
   warm=R24LanSpawnCoordinates.Warmup(new Float3(0,0,-600),i,false);Need(warm.X==(i%4-1.5f)*1.5f&&warm.Y==.1f&&warm.Z==-602-(i/4)*2,"Builtin warmup preserves represented source origin");
  }
  bool invalid=false;try{R24LanSpawnCoordinates.CyberArena(new Float3(0,0,0),24);}catch(ArgumentException){invalid=true;}Need(invalid,"No candidate outside actual bounded roster/source 24-site contract");
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();Need(!pair.Host.RequestSpawn()&&!pair.Client.RequestSpawn(),"Membership alone never grants Session placement");
   var pending=pair.Host.ReadAuthorityMember(pair.Client.OwnActor);Need(pending.Life==SessionLife.Pending&&!pending.HasPosition&&!pending.Loaded&&pair.Client.ReadAuthorityMember(pair.Client.OwnActor)==null,"Actual Session remains pending without world/load/placement proof");
   SharedWarmup(pair,hash);Need(!pair.Host.RequestSpawn()&&pair.Host.SpawnStatus.Contains("no-safe-spawn")&&!pair.Host.TryReadSpawn(pair.Host.OwnActor,out placement),"Unbound real-world port fails closed without manufacturing Alive or position");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();var world=new SpawnWorld{Host=pair.Host};pair.Host.BindSpawnWorld(world);
   bool refused=false;try{pair.Host.BindSpawnWorld(world);}catch(InvalidOperationException){refused=true;}Need(refused,"Host world binding cannot be replaced mid-session");
   refused=false;try{pair.Client.BindSpawnWorld(world);}catch(InvalidOperationException){refused=true;}Need(refused,"Client cannot supply spawn safety oracle");
   SharedWarmup(pair,hash);Need(pair.Host.RequestSpawn()&&pair.Host.TryReadSpawn(pair.Host.OwnActor,out placement)&&placement.Revision==1&&placement.Epoch==pair.Host.Epoch&&placement.Feet.X==2,"Actual authenticated local-host command commits real Session safe placement");
   Need(pair.Client.RequestSpawn(),"Actual guest requests self placement without supplying position/team/life");for(int i=0;i<5;i++)pair.Tick();R24LanSpawn guest,host;
   Need(pair.Host.TryReadSpawn(pair.Client.OwnActor,out guest)&&guest.Feet.X==9&&guest.Revision==1&&guest.Actor==pair.Client.OwnActor&&guest.Team==1,"Actual Session chooses clear other site for authenticated guest with assigned team");
   var actual=pair.Host.ReadAuthorityMember(pair.Client.OwnActor);Need(actual.Life==SessionLife.Alive&&actual.Loaded&&actual.HasPosition&&actual.SpawnRevision==guest.Revision&&actual.Position.X==guest.Feet.X,"Published position/revision/life are actual Session outputs, not duplicate test flags");
   Need(pair.Client.TryReadSpawn(pair.Client.OwnActor,out placement)&&placement.Feet.X==guest.Feet.X&&placement.Revision==guest.Revision&&placement.MapHash==hash,"Reliable host snapshot publishes actual committed guest placement");
   Need(pair.Client.OwnsSpawn(placement)&&!pair.Client.OwnsSpawn(new R24LanSpawn(placement.Actor,placement.MapHash,placement.Epoch,placement.Revision,placement.Team,placement.Deaths,new Float3(100,0,100))),"Root placement capability is exact current host publication, not a caller-created object with matching flags");
   Need(pair.Client.TryReadSpawn(pair.Host.OwnActor,out host)&&host.Feet.X==2&&host.Team==0,"Guest receives genuine listen-host placement");
   int firstRevision=guest.Revision;pair.Client.RequestSpawn();for(int i=0;i<5;i++)pair.Tick();Need(pair.Host.TryReadSpawn(pair.Client.OwnActor,out guest)&&guest.Revision==firstRevision&&guest.Feet.X==9,"Same-epoch re-request preserves safe existing root and revision");
   long epoch=pair.Host.Epoch;Need(pair.Host.BeginMapSelection("range",hash)&&!pair.Host.TryReadSpawn(pair.Client.OwnActor,out placement),"Real BeginLoading invalidates all old-epoch placements");
   var old=new Packet{Kind=PacketKind.Command,FeatureId="playable.spawn.request/1",ActorId=pair.Client.OwnActor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=epoch,Sequence=77,BaseSequence=-1,Payload=new byte[0]};pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(old)});pair.Tick();Need(pair.Host.Members.Length==2&&!pair.Host.TryReadSpawn(pair.Client.OwnActor,out placement),"Old request cannot spawn into next map or kill current connection");
   pair.Host.ReportMapLoaded(hash);pair.Client.ReportMapLoaded(hash);for(int i=0;i<4;i++)pair.Tick();Need(pair.Host.RequestSpawn()&&pair.Client.RequestSpawn(),"New epoch permits new real safe placement requests");for(int i=0;i<5;i++)pair.Tick();Need(pair.Client.TryReadSpawn(pair.Client.OwnActor,out placement)&&placement.Epoch==epoch+1&&placement.Revision==1,"New placement belongs to new real Session epoch");
   string actor=pair.Client.OwnActor;pair.ClientPipe.Close("server");Need(!pair.Host.TryReadSpawn(actor,out placement)&&pair.Host.Members.Length==1,"Real disconnect retires placement and pending output for departed principal");Need(world.Queries>0,"Actual Session consulted supplied world safety port");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();var world=new SpawnWorld{Host=pair.Host};pair.Host.BindSpawnWorld(world);var rules=R24PlayableMatchRules.ForPreset("free_for_all","range");Need(pair.Host.BeginRoundSelection(rules,hash,123)&&pair.Host.ReportMapLoaded(hash),"Shared actual round starts real loading epoch");for(int i=0;i<3;i++)pair.Tick();pair.Client.ReportMapLoaded(hash);for(int i=0;i<4;i++)pair.Tick();Need(pair.Host.RoundCountdown>0&&!pair.Host.RequestSpawn()&&!pair.Client.RequestSpawn(),"Source placement waits for current round countdown, not only loaded flag");
   pair.Now=pair.Host.SelectedMap.StartsAt+.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Need(pair.Host.RequestSpawn()&&pair.Client.RequestSpawn(),"After actual host countdown authenticated Session accepts spawn commands");for(int i=0;i<5;i++)pair.Tick();Need(pair.Client.TryReadSpawn(pair.Client.OwnActor,out placement)&&placement.Revision==1,"Actual match stage guest placement replicated");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();var world=new SpawnWorld{Host=pair.Host};pair.Host.BindSpawnWorld(world);SharedWarmup(pair,hash);pair.ClientPipe.Pressure=2;Need(pair.Client.RequestSpawn(),"Bounded pending request accepts send pressure");for(int i=0;i<8;i++)pair.Tick();Need(pair.Client.TryReadSpawn(pair.Client.OwnActor,out placement),"Reliable spawn request and committed output survive transport pressure");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();pair.Host.BindSpawnWorld(new SpawnWorld{Host=pair.Host,Clear=false});SharedWarmup(pair,hash);pair.Client.RequestSpawn();for(int i=0;i<5;i++)pair.Tick();Need(pair.Client.SpawnFailure!=null&&pair.Client.SpawnFailure.Contains("no-safe-spawn")&&!pair.Client.TryReadSpawn(pair.Client.OwnActor,out placement),"Host safety refusal reaches guest without fake placement or silent60second wait");
   var member=pair.Host.ReadAuthorityMember(pair.Client.OwnActor);Need(member.Life==SessionLife.Pending&&!member.HasPosition&&member.SpawnRevision==0,"Rejected world query leaves genuine Session life/position untouched");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();var world=new SpawnWorld{Host=pair.Host,Ready=false};pair.Host.BindSpawnWorld(world);SharedWarmup(pair,hash);Need(pair.Client.RequestSpawn(),"Guest may reach countdown completion before host Root finishes round composition");for(int i=0;i<5;i++)pair.Tick();
   Need(world.Queries==0&&!pair.Host.TryReadSpawn(pair.Client.OwnActor,out placement)&&pair.Host.ReadAuthorityMember(pair.Client.OwnActor).Life==SessionLife.Pending,"One retained request waits for actual host world readiness without fake state or Physics query");
   pair.Client.RequestSpawn();world.Ready=true;for(int i=0;i<5;i++)pair.Tick();Need(pair.Client.TryReadSpawn(pair.Client.OwnActor,out placement)&&placement.Revision==1&&pair.Client.SpawnFailure==null,"Deferred request commits once after host readiness; repeated request cannot reorder it into a stale refusal");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();var world=new SpawnWorld{Host=pair.Host,Ready=false};pair.Host.BindSpawnWorld(world);SharedWarmup(pair,hash);pair.Client.RequestSpawn();for(int i=0;i<3;i++)pair.Tick();pair.Now+=60.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);for(int i=0;i<3;i++)pair.Tick();
   Need(pair.Client.SpawnFailure!=null&&pair.Client.SpawnFailure.Contains("host-world-not-ready")&&world.Queries==0&&pair.Host.ReadAuthorityMember(pair.Client.OwnActor).Life==SessionLife.Pending,"Source60second pending bound refuses unavailable host world without retrying Physics or granting life");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();var world=new SpawnWorld{Host=pair.Host};pair.Host.BindSpawnWorld(world);SharedWarmup(pair,hash);
   for(int i=0;i<121;i++){var request=new Packet{Kind=PacketKind.Command,FeatureId="playable.spawn.request/1",ActorId=pair.Client.OwnActor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=i+2,BaseSequence=-1,Payload=new byte[0]};pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(request)});}pair.Tick();
   Need(pair.Host.RejectedFrames==1&&world.Queries<=121&&pair.Host.TryReadSpawn(pair.Client.OwnActor,out placement)&&placement.Revision==1,"Source120command window bounds spawn query floods while preserving first real committed life");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();pair.Host.BindSpawnWorld(new SpawnWorld{Host=pair.Host});SharedWarmup(pair,hash);
   var spoof=new Packet{Kind=PacketKind.Command,FeatureId="playable.spawn.request/1",ActorId=pair.Host.OwnActor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=2,BaseSequence=-1,Payload=new byte[0]};pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(spoof)});pair.Tick();Need(pair.Host.RejectedFrames==1&&pair.Host.Members.Length==1&&!pair.Host.TryReadSpawn(pair.Host.OwnActor,out placement),"Client cannot spawn another principal by claiming actor identity");
  }
 }
}
