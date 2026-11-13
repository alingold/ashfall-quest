using System;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;using Ashfall.Integration.Playable.Networking;
partial class LobbyCases
{
 static int Loaded(R24LanLobby room){int n=0;foreach(var member in room.Members)if(member.WorldLoaded)n++;return n;}
 static void MapCases()
 {
  string first=new string('c',64),second=new string('d',64);
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();string host=pair.Host.OwnActor,client=pair.Client.OwnActor;
   Need(!pair.Client.BeginMapSelection("range",first),"Only actual host selects the shared map");Need(!pair.Host.BeginMapSelection("range","invalid"),"Invalid map identity never changes Session epoch");
   Need(pair.Host.BeginMapSelection("range",first)&&pair.Host.Epoch==1&&pair.Host.MapStage==SessionStage.Loading&&Loaded(pair.Host)==0,"Real Session BeginLoading invalidates readiness in new epoch");Need(!pair.Host.BeginMapSelection("valley",second),"No overlapping second map transition");
   Need(pair.Host.ReportMapLoaded(first),"Host real-world adapter reports matching map");for(int i=0;i<3;i++)pair.Tick();Need(pair.Client.Epoch==1&&pair.Client.SelectedMap!=null&&!pair.Client.SelectedMap.Active&&Loaded(pair.Host)==1,"Reliable host map plan reaches client; one load does not unlock barrier");
   Need(!pair.Client.ReportMapLoaded(second),"Wrong map bytes cannot acknowledge host map");pair.Tick();Need(pair.Host.MapStage==SessionStage.Loading&&!pair.Host.SelectedMap.Active,"Host still waits for actual matching participant readiness");
   Need(pair.Client.ReportMapLoaded(first),"Matching client load acknowledged");for(int i=0;i<4;i++)pair.Tick();Need(pair.Host.MapStage==SessionStage.Lobby&&pair.Host.SelectedMap.Active&&pair.Client.SelectedMap.Active&&Loaded(pair.Host)==2&&Loaded(pair.Client)==2,"Real Session Loaded/WorldReady/Activate publishes complete shared warmup barrier");Need(pair.Host.OwnActor==host&&pair.Client.OwnActor==client,"Map lifecycle preserves actual assigned member identities");
   Need(pair.Host.BeginMapSelection("valley",second)&&pair.Host.Epoch==2&&Loaded(pair.Host)==0,"Second map uses new real Session epoch and clears prior readiness");
   var old=new Packet{Kind=PacketKind.Command,FeatureId="playable.map.loaded/1",ActorId=client,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=1,Payload=System.Text.Encoding.UTF8.GetBytes(first)};pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(old)});pair.Tick();Need(Loaded(pair.Host)==0&&pair.Host.Members.Length==2,"Delayed prior-epoch ack cannot unlock new map or kick current peer");
   Need(pair.Host.ReportMapLoaded(second)&&pair.Client.ReportMapLoaded(second),"Surviving transport leases rebound to new Session epoch");for(int i=0;i<4;i++)pair.Tick();Need(pair.Host.SelectedMap.Active&&pair.Client.SelectedMap.Active&&Loaded(pair.Host)==2,"Second shared map barrier completes on real rebound bindings");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();Need(pair.Host.BeginMapSelection("range",first)&&pair.Host.ReportMapLoaded(first),"Loading timeout setup through production owner");for(int i=0;i<2;i++)pair.Tick();pair.Now=61;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.Epoch==2&&pair.Host.SelectedMap.Cancelled&&pair.Host.MapStage==SessionStage.Lobby&&Loaded(pair.Host)==0,"Source60s loading timeout returns real Session to lobby without fake readiness");Need(pair.Client.Epoch==2&&pair.Client.SelectedMap.Cancelled&&Loaded(pair.Client)==0&&pair.Client.State==R24LanLobbyState.Connected,"Timeout invalidation reaches client while connection membership survives");
  }
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();pair.Host.BeginMapSelection("range",first);pair.Host.ReportMapLoaded(first);for(int i=0;i<2;i++)pair.Tick();pair.ClientPipe.Close("server");pair.Tick();Need(pair.Host.SelectedMap.Active&&pair.Host.Members.Length==1&&Loaded(pair.Host)==1,"Departed loading member does not permanently block surviving host");
  }
 }
}
